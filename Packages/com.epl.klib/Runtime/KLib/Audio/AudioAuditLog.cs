using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace KLibU.Audio
{
    /// <summary>
    /// Session-scoped writer for the audio audit stream.
    ///
    /// Deliberately NOT routed through KLogger: the app log is human-read, is
    /// shipped whole over TCP by GetLog, and is rendered in a WinForms textbox.
    /// This stream is machine-read and must not go anywhere near that path.
    ///
    /// Two tiers:
    ///   Write()      — telemetry. Buffered, flushed on a timer. File only.
    ///   WriteEvent() — events. Flushed immediately AND mirrored to the app log,
    ///                  so someone tracing an unrelated bug still sees that
    ///                  something happened, with a pointer to this file.
    ///
    /// THREADING: main thread only. Never call from OnAudioFilterRead — this
    /// touches the disk and takes a lock. The audio thread's only job is to
    /// increment a counter in AudioChainMonitor; AudioWatchdog does the writing.
    ///
    /// LIFECYCLE: AudioWatchdog bootstraps before scene load, but the session's
    /// data file path isn't known until the measurement initialises. Lines
    /// written before Open() land in a bounded ring buffer and are flushed into
    /// the file when it opens, so startup ENV and early ticks aren't lost.
    ///
    ///     // at measurement init, once the data file path is known:
    ///     AudioAuditLog.OpenAlongside(dataFilePath);
    ///
    ///     // at measurement end:
    ///     AudioAuditLog.Close();
    /// </summary>
    public static class AudioAuditLog
    {
        // ------------------------------------------------------------------
        // Configuration
        // ------------------------------------------------------------------

        /// <summary>Enumerate every chain every tick. For the bench soak rig.</summary>
        public static bool Verbose = false;

        /// <summary>Retention: newest N session files kept.</summary>
        public static int MaxSessionFiles = 200;

        /// <summary>Retention: total bytes across session files.</summary>
        public static long MaxFolderBytes = 250L * 1024 * 1024;

        /// <summary>How often buffered telemetry reaches the disk.</summary>
        public static double FlushIntervalSeconds = 10.0;

        /// <summary>Lines held in memory before a session file is opened.</summary>
        public static int PreSessionBufferLines = 300;

        /// <summary>Consecutive IO failures tolerated before giving up.</summary>
        public static int MaxConsecutiveFailures = 5;

        private const string Suffix = "_audit.log";

        // ------------------------------------------------------------------
        // State
        // ------------------------------------------------------------------

        private static readonly object _lock = new object();
        private static readonly Queue<string> _preSession = new Queue<string>();

        private static StreamWriter _writer;
        private static string _path;
        private static DateTime _lastFlush;
        private static long _linesWritten;
        private static int _consecutiveFailures;
        private static bool _disabled;
        private static bool _quitHooked;

        public static bool IsOpen { get { lock (_lock) { return _writer != null; } } }
        public static string CurrentPath { get { lock (_lock) { return _path; } } }
        public static bool IsDisabled { get { lock (_lock) { return _disabled; } } }

        // ------------------------------------------------------------------
        // Opening
        // ------------------------------------------------------------------

        /// <summary>
        /// Opens an audit file next to the session's data file, sharing its stem:
        ///     ...\Subject\foraging_20260716_142301.txt
        ///  -> ...\Subject\foraging_20260716_142301_audit.log
        /// so audit and data are trivially associated at analysis time.
        /// </summary>
        public static void OpenAlongside(string dataFilePath, string header = null)
        {
            if (string.IsNullOrEmpty(dataFilePath))
            {
                OpenDefault(header);
                return;
            }

            try
            {
                var folder = Path.GetDirectoryName(dataFilePath);
                var stem = Path.GetFileNameWithoutExtension(dataFilePath);
                Open(Path.Combine(folder ?? "", stem + Suffix), header);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"AUDIT could not derive audit path from '{dataFilePath}': {ex.Message}");
                OpenDefault(header);
            }
        }

        /// <summary>Fallback for sessions with no data file (lobby, free play, bench soak).</summary>
        public static void OpenDefault(string header = null)
        {
            var folder = Path.Combine(Application.persistentDataPath, "AuditLogs");
            var name = $"session_{DateTime.Now:yyyyMMdd_HHmmss}{Suffix}";
            Open(Path.Combine(folder, name), header);
        }

        public static void Open(string path, string header = null)
        {
            lock (_lock)
            {
                if (_disabled) return;

                CloseInternal();

                try
                {
                    var folder = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                        Directory.CreateDirectory(folder);

                    Prune(folder);

                    // FileShare.ReadWrite so the file can be tailed while live and
                    // read by a GetAuditLog request without closing the session.
                    var stream = new FileStream(path, FileMode.Create, FileAccess.Write,
                                                FileShare.ReadWrite, 8192);
                    _writer = new StreamWriter(stream) { AutoFlush = false };

                    _path = path;
                    _lastFlush = DateTime.UtcNow;
                    _linesWritten = 0;
                    _consecutiveFailures = 0;

                    HookQuit();

                    _writer.WriteLine($"# audio audit opened={DateTime.UtcNow:O} verbose={Verbose}");
                    if (!string.IsNullOrEmpty(header))
                        _writer.WriteLine("# " + header.Replace("\n", " "));

                    if (_preSession.Count > 0)
                    {
                        _writer.WriteLine($"# --- {_preSession.Count} pre-session line(s) ---");
                        while (_preSession.Count > 0)
                            _writer.WriteLine(_preSession.Dequeue());
                        _writer.WriteLine("# --- end pre-session ---");
                    }

                    _writer.Flush();

                    // The pointer in the app log. This is the only line the
                    // normal reader ever needs to see.
                    Debug.Log($"AUDIT audit log -> {path}");
                }
                catch (Exception ex)
                {
                    Fail(ex, "open");
                }
            }
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
                    _preSession.Enqueue(line);
                    while (_preSession.Count > PreSessionBufferLines)
                        _preSession.Dequeue();
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
        /// An event worth seeing: stall, NaN latch, exception latch, config change.
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

        /// <summary>Call once per watchdog tick. Handles the timed flush.</summary>
        public static void Tick()
        {
            lock (_lock)
            {
                if (_writer == null || _disabled) return;
                if ((DateTime.UtcNow - _lastFlush).TotalSeconds >= FlushIntervalSeconds)
                    FlushInternal();
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
        // Closing
        // ------------------------------------------------------------------

        public static void Close()
        {
            lock (_lock) { CloseInternal(); }
        }

        private static void CloseInternal()
        {
            if (_writer == null) return;

            try
            {
                _writer.WriteLine($"# audio audit closed={DateTime.UtcNow:O} lines={_linesWritten}");
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
        /// A take-home unit has nobody to clean up after it. Newest-first, delete
        /// until under both caps.
        /// </summary>
        private static void Prune(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return;

            try
            {
                var files = new DirectoryInfo(folder)
                    .GetFiles("*" + Suffix)
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .ToList();

                long running = 0;
                int deleted = 0;

                for (int i = 0; i < files.Count; i++)
                {
                    running += files[i].Length;

                    if (i >= MaxSessionFiles || running > MaxFolderBytes)
                    {
                        try { files[i].Delete(); deleted++; }
                        catch { /* locked or gone; try again next session */ }
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
                Debug.LogWarning($"AUDIT audit log {what} failed ({_consecutiveFailures}/{MaxConsecutiveFailures}): {ex.Message}");
                return;
            }

            _disabled = true;
            try { _writer?.Dispose(); } catch { }
            _writer = null;
            _path = null;

            // Loud, because a disabled audit log means the next session's data
            // has no provenance record.
            Debug.LogError($"AUDIT AUDIT LOG DISABLED after {_consecutiveFailures} consecutive {what} failures: {ex}");
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
    }
}
