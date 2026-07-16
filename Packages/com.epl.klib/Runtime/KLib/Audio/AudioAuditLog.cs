using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace KLibU.Audio
{
    /// <summary>
    /// Run-scoped writer for the audio audit stream. One file (rolled) per
    /// process lifetime, opened at startup and closed at quit.
    ///
    /// Deliberately NOT routed through KLogger: the app log is human-read, is
    /// shipped whole over TCP by GetLog, and is rendered in a WinForms textbox.
    /// This stream is machine-read and must not go near that path.
    ///
    /// Two tiers:
    ///   Write()      — telemetry. Buffered, flushed on a timer. File only.
    ///   WriteEvent() — faults. Flushed immediately AND mirrored to the app log.
    ///   Mark()       — boundaries. Flushed immediately. File only by default.
    ///
    /// WHY PER-RUN: the audio graph and the intercom singleton outlive every
    /// scene and every measurement. A per-session file structurally cannot show
    /// a chain that stopped three measurements ago, and cannot cover the scene
    /// transitions and hardware yield/resume intervals where damage is most
    /// likely. Continuity is the signal.
    ///
    /// Association with a data file is by MARK rather than by filename:
    ///
    ///   AUDIT MARK utc=… kind=SESSION.start dataFile=D:\…\foraging_20260716_142301.txt
    ///   AUDIT MARK utc=… kind=SESSION.end   dataFile=… reason=Finished
    ///
    /// A per-session provenance record is then an offline slice between marks,
    /// which is strictly more useful than a per-session file: the analyst can
    /// see either side of the boundary.
    ///
    /// THREADING: main thread only. Never call from OnAudioFilterRead — this
    /// touches the disk and takes a lock.
    /// </summary>
    public static class AudioAuditLog
    {
        // ------------------------------------------------------------------
        // Configuration
        // ------------------------------------------------------------------

        /// <summary>Enumerate every chain every tick. For the bench soak rig.</summary>
        public static bool Verbose = false;

        /// <summary>Retention: newest N audit files kept in the folder.</summary>
        public static int MaxFiles = 200;

        /// <summary>Retention: total bytes across audit files.</summary>
        public static long MaxFolderBytes = 250L * 1024 * 1024;

        /// <summary>Roll to a new part at this size. ~120 B/s means a day is ~3.5 MB.</summary>
        public static long MaxFileBytes = 50L * 1024 * 1024;

        /// <summary>Roll at local midnight regardless of size.</summary>
        public static bool RollDaily = true;

        /// <summary>How often buffered telemetry reaches the disk.</summary>
        public static double FlushIntervalSeconds = 10.0;

        /// <summary>Consecutive IO failures tolerated before giving up.</summary>
        public static int MaxConsecutiveFailures = 5;

        /// <summary>
        /// Emitted as a normal line at the top of EVERY part, including rolled
        /// ones — otherwise part 003 of a take-home run has no session identity
        /// and can't be interpreted on its own. AudioWatchdog sets this to its
        /// ENV line.
        /// </summary>
        public static Func<string> HeaderProvider;

        // ------------------------------------------------------------------
        // State
        // ------------------------------------------------------------------

        private static readonly object _lock = new object();

        private static StreamWriter _writer;
        private static string _path;
        private static string _folder;
        private static string _stem;
        private static int _part;
        private static DateTime _runStart;
        private static DateTime _rollDate;
        private static DateTime _lastFlush;
        private static long _linesWritten;
        private static long _linesDropped;
        private static int _consecutiveFailures;
        private static bool _disabled;
        private static bool _quitHooked;

        public static bool IsOpen { get { lock (_lock) { return _writer != null; } } }
        public static string CurrentPath { get { lock (_lock) { return _path; } } }
        public static bool IsDisabled { get { lock (_lock) { return _disabled; } } }

        // ------------------------------------------------------------------
        // Opening — once per process, at startup
        // ------------------------------------------------------------------

        /// <summary>
        /// Opens the run's audit file. Idempotent; safe to call more than once.
        /// Called by AudioWatchdog.Start(), which runs on the first frame — well
        /// after any RuntimeInitializeOnLoadMethod that sets ProcessTag.
        ///
        ///   &lt;persistentDataPath&gt;/AuditLogs/Game_20260716_142301_audit.log
        /// </summary>
        public static void OpenForRun(string processTag, string header = null)
        {
            lock (_lock)
            {
                if (_disabled || _writer != null) return;

                _runStart = DateTime.Now;
                _rollDate = _runStart.Date;
                _stem = $"{Sanitize(processTag)}_{_runStart:yyyyMMdd_HHmmss}";
                _folder = Path.Combine(Application.persistentDataPath, "AuditLogs");
                _part = 0;

                OpenPart(header);
            }
        }

        /// <summary>
        /// Explicit folder override — for the bench soak rig. Named distinctly
        /// so OpenForRun(tag, "D:\\path") can't silently bind the folder to the
        /// header parameter.
        /// </summary>
        public static void OpenForRunInFolder(string processTag, string folder, string header = null)
        {
            lock (_lock)
            {
                if (_disabled || _writer != null) return;

                _runStart = DateTime.Now;
                _rollDate = _runStart.Date;
                _stem = $"{Sanitize(processTag)}_{_runStart:yyyyMMdd_HHmmss}";
                _folder = folder;
                _part = 0;

                OpenPart(header);
            }
        }

        private static void OpenPart(string header)
        {
            try
            {
                if (!string.IsNullOrEmpty(_folder) && !Directory.Exists(_folder))
                    Directory.CreateDirectory(_folder);

                Prune(_folder);

                var path = PartPath(_part);

                // FileShare.ReadWrite so the file can be tailed live and read by
                // a GetAuditLog request without interrupting the run.
                var stream = new FileStream(path, FileMode.Create, FileAccess.Write,
                                            FileShare.ReadWrite, 8192);
                _writer = new StreamWriter(stream) { AutoFlush = false };

                _path = path;
                _lastFlush = DateTime.UtcNow;
                _linesWritten = 0;
                _consecutiveFailures = 0;

                HookQuit();

                _writer.WriteLine($"# audio audit  run={_stem}  part={_part}  " +
                                  $"opened={DateTime.UtcNow:O}  verbose={Verbose}");

                if (_part > 0)
                    _writer.WriteLine($"# continues from {Path.GetFileName(PartPath(_part - 1))}");

                if (!string.IsNullOrEmpty(header))
                    _writer.WriteLine("# " + Flatten(header));

                // Session identity, repeated per part so each file stands alone.
                if (HeaderProvider != null)
                {
                    try
                    {
                        var line = HeaderProvider();
                        if (!string.IsNullOrEmpty(line)) _writer.WriteLine(Flatten(line));
                    }
                    catch (Exception hx)
                    {
                        _writer.WriteLine($"# header provider threw: {hx.Message}");
                    }
                }

                if (_linesDropped > 0)
                {
                    _writer.WriteLine($"# {_linesDropped} line(s) written before the log opened were dropped");
                    _linesDropped = 0;
                }

                _writer.Flush();

                // The pointer in the app log. The only line a normal reader needs.
                Debug.Log($"AUDIT audit log -> {path}");
            }
            catch (Exception ex)
            {
                Fail(ex, "open");
            }
        }

        private static string PartPath(int part)
        {
            var name = part == 0 ? $"{_stem}_audit.log" : $"{_stem}_audit_{part:D3}.log";
            return Path.Combine(_folder ?? "", name);
        }

        // ------------------------------------------------------------------
        // Writing
        // ------------------------------------------------------------------

        /// <summary>Telemetry. Buffered. Main thread only.</summary>
        public static void Write(string line)
        {
            if (line == null) return;

            lock (_lock)
            {
                if (_disabled) return;

                if (_writer == null)
                {
                    _linesDropped++;
                    return;
                }

                try
                {
                    _writer.WriteLine(line);
                    _linesWritten++;
                    _consecutiveFailures = 0;
                }
                catch (Exception ex)
                {
                    Fail(ex, "write");
                }
            }
        }

        /// <summary>
        /// A fault worth seeing: stall, NaN latch, exception latch, config change.
        /// Written, flushed immediately, and mirrored to the app log.
        /// </summary>
        public static void WriteEvent(string line, LogType mirror = LogType.Log)
        {
            Write(line);
            Flush();

            switch (mirror)
            {
                case LogType.Error:
                case LogType.Exception:
                case LogType.Assert:
                    Debug.LogError(line);
                    break;
                case LogType.Warning:
                    Debug.LogWarning(line);
                    break;
                default:
                    Debug.Log(line);
                    break;
            }
        }

        /// <summary>
        /// A boundary in the run: session start/end, scene change, hardware
        /// yield/resume. Flushed immediately so it survives a hard stop.
        /// File only unless mirror is set — the app log already records most of
        /// these in its own vocabulary.
        ///
        ///   Mark("SESSION.start", $"dataFile={path}");
        ///   Mark("SESSION.end",   $"dataFile={path} reason=Finished");
        ///   Mark("SCENE",         $"name={sceneName}");
        ///   Mark("HW.yield");  Mark("HW.resume");
        /// </summary>
        public static void Mark(string kind, string detail = null, bool mirror = false)
        {
            var line = $"AUDIT MARK utc={DateTime.UtcNow:O} kind={kind}" +
                       (string.IsNullOrEmpty(detail) ? "" : " " + Flatten(detail));

            if (mirror) WriteEvent(line);
            else { Write(line); Flush(); }
        }

        /// <summary>Call once per watchdog tick. Handles the timed flush and rotation.</summary>
        public static void Tick()
        {
            lock (_lock)
            {
                if (_writer == null || _disabled) return;

                if ((DateTime.UtcNow - _lastFlush).TotalSeconds >= FlushIntervalSeconds)
                {
                    FlushInternal();
                    RollIfNeeded();
                }
            }
        }

        public static void Flush()
        {
            lock (_lock) { FlushInternal(); }
        }

        private static void FlushInternal()
        {
            if (_writer == null || _disabled) return;
            try
            {
                _writer.Flush();
                _lastFlush = DateTime.UtcNow;
                _consecutiveFailures = 0;
            }
            catch (Exception ex)
            {
                Fail(ex, "flush");
            }
        }

        // ------------------------------------------------------------------
        // Rotation — a take-home unit may be powered for weeks
        // ------------------------------------------------------------------

        private static void RollIfNeeded()
        {
            if (_writer == null || _disabled) return;

            bool bySize = false;
            try { bySize = _writer.BaseStream.Length >= MaxFileBytes; }
            catch { /* stream length unavailable; size rolling just won't fire */ }

            bool byDate = RollDaily && DateTime.Now.Date != _rollDate;

            if (!bySize && !byDate) return;

            int next = _part + 1;
            string reason = bySize ? "size" : "date";

            try
            {
                _writer.WriteLine($"# rolling to {Path.GetFileName(PartPath(next))} " +
                                  $"reason={reason} closed={DateTime.UtcNow:O} lines={_linesWritten}");
                _writer.Flush();
                _writer.Dispose();
            }
            catch { /* rolling anyway */ }

            _writer = null;
            _part = next;
            _rollDate = DateTime.Now.Date;

            OpenPart(null);
        }

        // ------------------------------------------------------------------
        // Closing
        // ------------------------------------------------------------------

        /// <summary>Wired to Application.quitting. The only place this is called.</summary>
        public static void Close()
        {
            lock (_lock) { CloseInternal(); }
        }

        private static void CloseInternal()
        {
            if (_writer == null) return;

            try
            {
                _writer.WriteLine($"# audio audit closed={DateTime.UtcNow:O} " +
                                  $"runDurationMin={(DateTime.Now - _runStart).TotalMinutes:F1} " +
                                  $"lines={_linesWritten}");
                _writer.Flush();
                _writer.Dispose();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"AUDIT error closing audit log: {ex.Message}");
            }

            _writer = null;
            _path = null;
        }

        // ------------------------------------------------------------------
        // Housekeeping
        // ------------------------------------------------------------------

        /// <summary>
        /// A take-home unit has nobody to clean up after it. Newest-first,
        /// delete until under both caps. Runs before each part is created, so
        /// the live file is never a candidate.
        /// </summary>
        private static void Prune(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return;

            try
            {
                var dir = new DirectoryInfo(folder);
                if (!dir.Exists) return;

                var files = dir.GetFiles("*_audit*.log")
                               .OrderByDescending(f => f.LastWriteTimeUtc)
                               .ToList();

                long running = 0;
                int deleted = 0;

                for (int i = 0; i < files.Count; i++)
                {
                    running += files[i].Length;

                    if (i >= MaxFiles || running > MaxFolderBytes)
                    {
                        try { files[i].Delete(); deleted++; }
                        catch { /* locked or gone; try again next time */ }
                    }
                }

                if (deleted > 0)
                    Debug.Log($"AUDIT pruned {deleted} old audit file(s) from {folder}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"AUDIT prune failed in {folder}: {ex.Message}");
            }
        }

        private static void Fail(Exception ex, string what)
        {
            _consecutiveFailures++;

            if (_consecutiveFailures < MaxConsecutiveFailures)
            {
                Debug.LogWarning($"AUDIT audit log {what} failed " +
                                 $"({_consecutiveFailures}/{MaxConsecutiveFailures}): {ex.Message}");
                return;
            }

            _disabled = true;
            try { _writer?.Dispose(); } catch { }
            _writer = null;
            _path = null;

            // Loud: a disabled audit log means this run's data has no provenance.
            Debug.LogError($"AUDIT AUDIT LOG DISABLED after {_consecutiveFailures} " +
                           $"consecutive {what} failures: {ex}");
        }

        /// <summary>Re-enable after the operator has fixed whatever broke (disk full, etc).</summary>
        public static void Reset()
        {
            lock (_lock)
            {
                _disabled = false;
                _consecutiveFailures = 0;
            }
        }

        private static void HookQuit()
        {
            if (_quitHooked) return;
            _quitHooked = true;
            Application.quitting += Close;
        }

        private static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "Unknown";
            var bad = Path.GetInvalidFileNameChars();
            var chars = s.Select(c => bad.Contains(c) || c == ' ' ? '_' : c).ToArray();
            return new string(chars);
        }

        private static string Flatten(string s) =>
            string.IsNullOrEmpty(s) ? "" : s.Replace("\r", " ").Replace("\n", " ");
    }
}
