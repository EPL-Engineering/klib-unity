using System;
using System.Threading;

namespace KLibU.Audio
{
    /// <summary>
    /// Instrumentation for a single OnAudioFilterRead chain.
    ///
    /// Written from the audio thread, read from the main thread.
    /// No allocation, no locks, no logging on the audio path — the audio thread
    /// only ever increments a counter and stores two scalars.
    ///
    /// Usage:
    ///     private AudioChainMonitor _monitor;
    ///     void Awake()  { _monitor = AudioWatchdog.Register($"Target[{name}]"); }
    ///     void OnDestroy() { AudioWatchdog.Unregister(_monitor); }
    ///
    ///     void OnAudioFilterRead(float[] data, int channels)
    ///     {
    ///         try
    ///         {
    ///             ... your chain writes data[] ...
    ///         }
    ///         catch (Exception ex) { _monitor.NoteException(ex); }
    ///         finally { _monitor.NoteBuffer(data, channels); }
    ///     }
    /// </summary>
    public sealed class AudioChainMonitor
    {
        public readonly string Id;

        private long _bufferCount;
        private float _peak;
        private long _firstNaNBuffer;         // 0 = never seen
        private long _firstExceptionBuffer;   // 0 = never seen
        private string _firstExceptionMessage;
        private int _channels;
        private int _bufferLength;

        /// <summary>
        /// Peak is computed over every Nth sample. 1 = every sample.
        /// Raise this if DSP load becomes a concern with many concurrent chains.
        /// NaN detection always scans every sample regardless.
        /// </summary>
        public int PeakStride = 1;

        public AudioChainMonitor(string id)
        {
            Id = id ?? "unnamed";
        }

        /// <summary>
        /// Call at the END of OnAudioFilterRead, after the chain has written data[].
        /// Safe to call from the audio thread.
        /// </summary>
        public void NoteBuffer(float[] data, int channels)
        {
            long n = Interlocked.Increment(ref _bufferCount);
            _channels = channels;
            _bufferLength = data.Length;

            float peak = 0f;
            int stride = PeakStride < 1 ? 1 : PeakStride;

            for (int i = 0; i < data.Length; i++)
            {
                float v = data[i];

                // NaN and Inf both fail every ordered comparison, so they must be
                // tested explicitly — a NaN peak would otherwise read as "healthy".
                if (float.IsNaN(v) || float.IsInfinity(v))
                {
                    Interlocked.CompareExchange(ref _firstNaNBuffer, n, 0);
                    Interlocked.Exchange(ref _peak, float.NaN);
                    return;
                }

                if ((i % stride) == 0)
                {
                    float a = v < 0f ? -v : v;
                    if (a > peak) peak = a;
                }
            }

            Interlocked.Exchange(ref _peak, peak);
        }

        /// <summary>
        /// Latches the FIRST exception only. Prevents a throwing chain from
        /// emitting 47 log lines per second and burying everything else.
        /// </summary>
        public void NoteException(Exception ex)
        {
            long n = Interlocked.Read(ref _bufferCount);
            if (Interlocked.CompareExchange(ref _firstExceptionBuffer, n, 0) == 0)
            {
                _firstExceptionMessage = ex.ToString();
            }
        }

        public Snapshot Read()
        {
            return new Snapshot
            {
                Id = Id,
                BufferCount = Interlocked.Read(ref _bufferCount),
                // atomic read idiom: compare against 0, write 0 back if it is 0 (no-op)
                Peak = Interlocked.CompareExchange(ref _peak, 0f, 0f),
                FirstNaNBuffer = Interlocked.Read(ref _firstNaNBuffer),
                FirstExceptionBuffer = Interlocked.Read(ref _firstExceptionBuffer),
                FirstExceptionMessage = _firstExceptionMessage,
                Channels = _channels,
                BufferLength = _bufferLength
            };
        }

        public struct Snapshot
        {
            public string Id;
            public long BufferCount;
            public float Peak;
            public long FirstNaNBuffer;
            public long FirstExceptionBuffer;
            public string FirstExceptionMessage;
            public int Channels;
            public int BufferLength;

            public bool SawNaN => FirstNaNBuffer > 0;
            public bool SawException => FirstExceptionBuffer > 0;
        }
    }
}
