using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace KLibU.Audio
{
    /// <summary>
    /// Polls every registered AudioChainMonitor at 1 Hz.
    ///
    /// Telemetry goes to AudioAuditLog (own file, buffered). Events — stall, NaN,
    /// exception, config change — additionally reach the app log and raise Alarm.
    ///
    /// Default tick line is a SUMMARY. Per-chain detail is appended only for
    /// chains that deviate from the healthy value or have latched a fault, so a
    /// 24-chain foraging scene costs ~120 bytes/sec instead of ~1.7 kB/sec.
    /// Nothing diagnostic is lost: "counters freeze staggered by audibility"
    /// still shows up, and names exactly the chains that stalled.
    ///
    ///   AUDIT utc=…T18:23:01.1234567Z dsp=1234.5678 proc=Game chains=24 dn=47 peak=0.0000..0.9210 nan=0 exc=0 odd=0 new=0
    ///   AUDIT utc=…T18:25:14.9871234Z dsp=1367.4021 proc=Game chains=24 dn=47 peak=0.0000..0.9210 nan=0 exc=0 odd=2 new=0 | chain=Target[07].pan n=6231 dn=0 peak=0.0000 nan=- exc=- ch=8 | chain=Fiducial n=6231 dn=0 peak=0.0000 nan=- exc=- ch=8
    ///
    ///   dn   is the MODE across chains, not any one chain's value. Healthy is
    ///        sampleRate/dspBufferSize — 46.875 at 48kHz/1024, so it alternates
    ///        47/46 on its own. ENV carries rate= and dspBuffer= for reference.
    ///   odd  chains deviating from the mode by more than DeltaTolerance, or
    ///        carrying a latched NaN/exception. Only these are enumerated.
    ///   new  chains registered since the last tick. Baselined, not evaluated.
    ///
    /// Decision table:
    ///   dn -> 0 on every chain at once           graph-level (config change, DSP thread death)
    ///   dn -> 0 staggered, ascending audibility  voice virtualisation
    ///   dn normal, peak=NaN                      poisoning; lowest nan= names the source
    ///   dn normal, peak=0                        generator zeroed
    ///   dn normal, peak clean, silence in room   below Unity (device or audio engine)
    ///   ...and the same in BOTH processes        below both. Card or driver.
    /// </summary>
    public class AudioWatchdog : MonoBehaviour
    {
        /// <summary>Raised on the main thread. Wire this to something the operator can see.</summary>
        public static event Action<string> Alarm;

        /// <summary>"Game" or "HTS". Set at startup so the two logs are distinguishable.</summary>
        public static string ProcessTag = "Unknown";

        private const float TickSeconds = 1f;
        private const int StallTicksBeforeAlarm = 2;

        /// <summary>
        /// A chain is only "odd" if its delta differs from the mode by MORE than
        /// this. Pass 1 reads the monitors sequentially while the audio thread
        /// keeps counting, so a buffer landing mid-loop splits the chains between
        /// two adjacent values with nothing actually wrong. The healthy rate is
        /// also non-integral (48000/1024 = 46.875), so dn alternates by one on
        /// its own. Stalls are dn=0 against a mode of ~47 and clear this easily.
        /// </summary>
        private const long DeltaTolerance = 1;

        private static AudioWatchdog _instance;
        private static readonly object _lock = new object();
        private static readonly List<AudioChainMonitor> _monitors = new List<AudioChainMonitor>();

        private readonly Dictionary<string, long> _lastCount = new Dictionary<string, long>();
        private readonly Dictionary<string, int> _stallTicks = new Dictionary<string, int>();
        private readonly Dictionary<long, int> _deltaHistogram = new Dictionary<long, int>();
        private readonly HashSet<string> _reported = new HashSet<string>();

        /// <summary>
        /// Chains whose baseline has been established. A chain registered since
        /// the last tick has no _lastCount entry, so its delta would come out as
        /// its entire BufferCount — flagging every target in foraging on the tick
        /// after scene setup. First tick records the count and evaluates nothing.
        /// </summary>
        private readonly HashSet<string> _baselined = new HashSet<string>();

        /// <summary>Ids seen this tick. Used to expire bookkeeping for dead chains.</summary>
        private readonly HashSet<string> _present = new HashSet<string>();

        private readonly StringBuilder _sb = new StringBuilder(1024);
        private readonly StringBuilder _odd = new StringBuilder(512);

        private float _nextTick;

        // ------------------------------------------------------------------
        // Registration
        // ------------------------------------------------------------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("AudioWatchdog");
            _instance = go.AddComponent<AudioWatchdog>();
            DontDestroyOnLoad(go);
        }

        public static AudioChainMonitor Register(string id)
        {
            var m = new AudioChainMonitor(id);
            lock (_lock) { _monitors.Add(m); }
            return m;
        }

        public static void Unregister(AudioChainMonitor m)
        {
            if (m == null) return;
            lock (_lock) { _monitors.Remove(m); }
        }

        // ------------------------------------------------------------------
        // Lifecycle
        // ------------------------------------------------------------------

        private void Start()
        {
            // Start() runs on the first frame — after every
            // RuntimeInitializeOnLoadMethod, so ProcessTag is set by now.
            // Idempotent, so a second caller is harmless.
            AudioAuditLog.HeaderProvider = EnvironmentLine;
            AudioAuditLog.OpenForRun(ProcessTag);

            // Also to the app log, once, so the ENV is visible to someone who
            // never opens the audit file.
            Debug.Log(EnvironmentLine());

            AudioSettings.OnAudioConfigurationChanged += OnConfigChanged;
        }

        private void OnDestroy()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnConfigChanged;
        }

        /// <summary>Layer 4: session identity. A hardware or format substitution can never again go unnoticed.</summary>
        private static string EnvironmentLine()
        {
            var c = AudioSettings.GetConfiguration();
            return $"AUDIT ENV utc={Utc()} proc={ProcessTag} " +
                   $"app={Application.version} machine={SystemInfo.deviceName} " +
                   $"os={SystemInfo.operatingSystem} " +
                   $"rate={c.sampleRate} dspBuffer={c.dspBufferSize} speakerMode={c.speakerMode} " +
                   $"realVoices={c.numRealVoices} virtualVoices={c.numVirtualVoices} " +
                   $"outputRate={AudioSettings.outputSampleRate} " +
                   $"driverCaps={AudioSettings.driverCapabilities}";
        }

        private void OnConfigChanged(bool deviceWasChanged)
        {
            var c = AudioSettings.GetConfiguration();
            AudioAuditLog.WriteEvent(
                $"AUDIT CONFIGCHANGE utc={Utc()} dsp={AudioSettings.dspTime:F4} proc={ProcessTag} " +
                $"deviceWasChanged={deviceWasChanged} rate={c.sampleRate} " +
                $"dspBuffer={c.dspBufferSize} speakerMode={c.speakerMode}",
                LogType.Warning);

            Alarm?.Invoke("Audio configuration changed — AudioSources do not auto-resume");
        }

        // ------------------------------------------------------------------
        // The tick
        // ------------------------------------------------------------------

        private void Update()
        {
            if (Time.unscaledTime < _nextTick) return;

            // Accumulate rather than reset from the frame time, or every interval
            // is >= 1s and drifts later — at 60fps, ~1.016s ticks, biasing dn high.
            _nextTick += TickSeconds;

            // If we fell far behind (hitch, editor pause, breakpoint), don't try
            // to catch up with a burst of ticks — resync.
            if (_nextTick < Time.unscaledTime - TickSeconds)
                _nextTick = Time.unscaledTime + TickSeconds;

            AudioChainMonitor[] list;
            lock (_lock) { list = _monitors.ToArray(); }

            if (list.Length == 0) { AudioAuditLog.Tick(); return; }

            // ---- pass 1: read every monitor once, find the healthy delta ----
            var snaps = new AudioChainMonitor.Snapshot[list.Length];
            var deltas = new long[list.Length];
            var fresh = new bool[list.Length];

            _deltaHistogram.Clear();
            _present.Clear();

            for (int i = 0; i < list.Length; i++)
            {
                snaps[i] = list[i].Read();
                var id = snaps[i].Id;
                _present.Add(id);

                // A chain registered since the last tick has no baseline. Its
                // delta would be its whole BufferCount. Record and evaluate
                // nothing this tick.
                fresh[i] = !_baselined.Contains(id);

                long last;
                _lastCount.TryGetValue(id, out last);
                deltas[i] = snaps[i].BufferCount - last;
                _lastCount[id] = snaps[i].BufferCount;

                if (fresh[i])
                {
                    _baselined.Add(id);
                    continue;   // and keep it out of the histogram
                }

                int n;
                _deltaHistogram.TryGetValue(deltas[i], out n);
                _deltaHistogram[deltas[i]] = n + 1;
            }

            // Mode rather than an expected value derived from the sample rate:
            // robust to tick jitter, and if every chain agrees, that IS healthy.
            long modeDelta = 0;
            int best = -1;
            foreach (var kv in _deltaHistogram)
            {
                if (kv.Value > best) { best = kv.Value; modeDelta = kv.Key; }
            }

            bool haveMode = best > 0;   // false on the very first tick of a run

            // ---- pass 2: summarise, enumerate only what deviates ------------
            float peakMin = float.MaxValue, peakMax = float.MinValue;
            int nanChains = 0, excChains = 0, oddChains = 0, freshChains = 0;

            _odd.Length = 0;

            for (int i = 0; i < list.Length; i++)
            {
                var s = snaps[i];
                long delta = deltas[i];

                if (s.SawNaN) nanChains++;
                if (s.SawException) excChains++;

                if (!float.IsNaN(s.Peak))
                {
                    if (s.Peak < peakMin) peakMin = s.Peak;
                    if (s.Peak > peakMax) peakMax = s.Peak;
                }

                if (fresh[i])
                {
                    freshChains++;
                    if (AudioAuditLog.Verbose) AppendChain(s, delta, "new");
                    continue;   // no odd flag, no stall evaluation, no alarm
                }

                // Tolerance absorbs both the non-integral buffer rate and the
                // tearing across pass 1. A stall is dn=0 against a mode of ~47.
                bool deltaOdd = haveMode && Math.Abs(delta - modeDelta) > DeltaTolerance;
                bool odd = deltaOdd || s.SawNaN || s.SawException;
                if (odd) oddChains++;

                if (AudioAuditLog.Verbose || odd)
                    AppendChain(s, delta, null);

                RaiseEventsFor(s, delta);
            }

            // Chains come and go all session — targets spawn and are destroyed.
            // Without this the bookkeeping dictionaries grow for the life of the
            // run, which for a take-home unit is a long time.
            PruneBookkeeping();

            if (peakMin > peakMax) { peakMin = 0f; peakMax = 0f; }

            _sb.Length = 0;
            _sb.Append("AUDIT utc=").Append(Utc())
               .Append(" dsp=").Append(AudioSettings.dspTime.ToString("F4"))
               .Append(" proc=").Append(ProcessTag)
               .Append(" chains=").Append(list.Length)
               .Append(" dn=").Append(modeDelta)
               .Append(" peak=").Append(peakMin.ToString("F4")).Append("..").Append(peakMax.ToString("F4"))
               .Append(" nan=").Append(nanChains)
               .Append(" exc=").Append(excChains)
               .Append(" odd=").Append(oddChains)
               .Append(" new=").Append(freshChains);

            if (_odd.Length > 0) _sb.Append(_odd);

            AudioAuditLog.Write(_sb.ToString());
            AudioAuditLog.Tick();
        }

        private void AppendChain(AudioChainMonitor.Snapshot s, long delta, string note)
        {
            _odd.Append(" | chain=").Append(s.Id)
                .Append(" n=").Append(s.BufferCount)
                .Append(" dn=").Append(delta)
                .Append(" peak=").Append(float.IsNaN(s.Peak) ? "NaN" : s.Peak.ToString("F4"))
                .Append(" nan=").Append(s.SawNaN ? s.FirstNaNBuffer.ToString() : "-")
                .Append(" exc=").Append(s.SawException ? s.FirstExceptionBuffer.ToString() : "-")
                .Append(" ch=").Append(s.Channels);

            if (!string.IsNullOrEmpty(note)) _odd.Append(" note=").Append(note);
        }

        /// <summary>
        /// Expire per-chain bookkeeping for chains that no longer exist. Cheap at
        /// 1 Hz, and it's the difference between bounded and unbounded memory over
        /// a multi-day run with respawning targets.
        /// </summary>
        private void PruneBookkeeping()
        {
            if (_lastCount.Count <= _present.Count) return;

            _expired.Clear();
            foreach (var id in _lastCount.Keys)
                if (!_present.Contains(id)) _expired.Add(id);

            foreach (var id in _expired)
            {
                _lastCount.Remove(id);
                _stallTicks.Remove(id);
                _baselined.Remove(id);
                _reported.Remove(id + ":nan");
                _reported.Remove(id + ":exc");
            }
        }

        private readonly List<string> _expired = new List<string>();

        // ------------------------------------------------------------------
        // Events — each fires once per chain per session
        // ------------------------------------------------------------------

        private void RaiseEventsFor(AudioChainMonitor.Snapshot s, long delta)
        {
            if (s.SawException && _reported.Add(s.Id + ":exc"))
            {
                AudioAuditLog.WriteEvent(
                    $"AUDIT EXCEPTION utc={Utc()} proc={ProcessTag} chain={s.Id} " +
                    $"atBuffer={s.FirstExceptionBuffer} :: {Flatten(s.FirstExceptionMessage)}",
                    LogType.Error);
                Alarm?.Invoke($"Audio chain '{s.Id}' threw");
            }

            if (s.SawNaN && _reported.Add(s.Id + ":nan"))
            {
                AudioAuditLog.WriteEvent(
                    $"AUDIT NAN utc={Utc()} proc={ProcessTag} chain={s.Id} atBuffer={s.FirstNaNBuffer}",
                    LogType.Error);
                Alarm?.Invoke($"Audio chain '{s.Id}' produced NaN");
            }

            // A chain that was counting and stopped is the signature.
            int stalls;
            _stallTicks.TryGetValue(s.Id, out stalls);

            if (s.BufferCount > 0 && delta == 0)
            {
                stalls++;
                if (stalls == StallTicksBeforeAlarm)
                {
                    AudioAuditLog.WriteEvent(
                        $"AUDIT STALL utc={Utc()} proc={ProcessTag} chain={s.Id} frozenAt={s.BufferCount}",
                        LogType.Error);
                    Alarm?.Invoke($"Audio chain '{s.Id}' stopped rendering");
                }
            }
            else
            {
                if (stalls >= StallTicksBeforeAlarm)
                {
                    AudioAuditLog.WriteEvent(
                        $"AUDIT RESUME utc={Utc()} proc={ProcessTag} chain={s.Id} at={s.BufferCount}",
                        LogType.Warning);
                }
                stalls = 0;
            }

            _stallTicks[s.Id] = stalls;
        }

        private static string Flatten(string s) =>
            string.IsNullOrEmpty(s) ? "" : s.Replace("\r", " ").Replace("\n", " ");

        private static string Utc() => DateTime.UtcNow.ToString("O");
    }
}
