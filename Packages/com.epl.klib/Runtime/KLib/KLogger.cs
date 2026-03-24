using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace KLibU
{
    /// <summary>
    /// Simple logging utility
    /// </summary>
    /// <remarks>Creates a persistent singleton MonoBehavior that receives a copy of the UnityEngine.Debug messages and stores them to file.</remarks>
    /// <example>
    /// The following example shows typical usage.
    /// <code language="c#">
    /// KLogger.Create(path).StartLogging();
    /// </code>
    ///</example>
    public class KLogger : MonoBehaviour
    {
        /// <summary>
        /// Logging level
        /// </summary>
        public enum Level {
            /// <summary>
            /// Does not include Debug
            /// </summary>
            Default,
            /// <summary>
            /// All messages
            /// </summary>
            Verbose };

        private string _logPath;
        private StringBuilder _log;
        private const string _dateFormat = "yyyy-MM-dd HH:mm:ss.fff";

        /// <summary>
        /// Minimum level of message to log
        /// </summary>
        public Level MinimumLevel { set; get; } = Level.Default;
        /// <summary>
        /// Number of days log files are retained.
        /// </summary>
        public float RetainDays { set; get; } = 14;

        // Make singleton
        private static KLogger _instance;
        /// <summary>
        /// Returns instance of KLogger. Creates one if it doesn't exist.
        /// </summary>
        public static KLogger Log
        {
            get
            {
                if (_instance == null)
                {
                    GameObject gobj = GameObject.Find("Logger");
                    if (gobj != null)
                    {
                        _instance = gobj.GetComponent<KLogger>();
                    }
                    else
                    {
                        _instance = new GameObject("Logger").AddComponent<KLogger>();
                    }
                    _instance.Init();
                }
                return _instance;
            }
        }

        public static string LogPath { get { return Log._logPath; } }

        /// <summary>
        /// Create and initialize logger.
        /// </summary>
        /// <param name="logPath">path to log</param>
        /// <param name="minimumLevel">sets <see cref="MinimumLevel"/></param>
        /// <param name="retainDays">set <see cref="RetainDays"/></param>
        /// <returns>Instance of KLogger</returns>
        public static KLogger Create(string logPath, Level minimumLevel = Level.Default, float retainDays = 14)
        {
            string dateTag = $"-{System.DateTime.Now.ToString("yyyyMMdd")}";
            var extension = Path.GetExtension(logPath);
            Log._logPath = logPath.Replace(extension, $"{dateTag}{extension}");

            Log.MinimumLevel = minimumLevel;
            Log.RetainDays = retainDays;

            var folder = Path.GetDirectoryName(logPath);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            Log.PurgeLogs(logPath);

            Log._log = new StringBuilder(1000);

            return Log;
        }

        /// <summary>
        /// Convenience method that prepends "[Debug]" to beginning of message to create a Debug level
        /// </summary>
        /// <remarks>Unity's LogType does not contain a "Debug" level. This is a simple workaround to provide that functionality.</remarks>
        /// <param name="message">Debug-level message to log</param>
        public static void Debug(string message)
        {
            UnityEngine.Debug.Log("[Debug] " + message);
        }

        private void Init()
        {
            DontDestroyOnLoad(this);
        }

        /// <summary>
        /// Attaches KLogger to UnityEngine.Debug messages
        /// </summary>
        public void StartLogging()
        {
            Application.logMessageReceivedThreaded += HandleLog;
        }

        /// <summary>
        /// Detaches KLogger from UnityEngine.Debug messages
        /// </summary>
        public void StopLogging()
        {
            Application.logMessageReceivedThreaded -= HandleLog;
            FlushLog();
        }

        void OnDisable()
        {
            UnityEngine.Debug.Log("hmmmm");
            Application.logMessageReceivedThreaded -= HandleLog;
            FlushLog();
        }

        public void FlushLog()
        {
            Files.AppendTextFile(_logPath, _log.ToString());
            _log.Clear();
        }

        void HandleLog(string logString, string stackTrace, LogType type)
        {
            if (logString.StartsWith("[Debug]"))
            {
                if (MinimumLevel == Level.Verbose)
                {
                    _log.AppendLine($"{System.DateTime.Now.ToString(_dateFormat)} {logString}");
                }
            }
            else
            {
                _log.AppendLine($"{System.DateTime.Now.ToString(_dateFormat)} [{type}] {logString}");
                if (type == LogType.Error || type == LogType.Exception)
                {
                    _log.AppendLine(stackTrace);
                }
            }

            if (_log.Length > 1000)
            {
                FlushLog();
            }
        }

        void PurgeLogs(string logPath)
        {
            var folder = Path.GetDirectoryName(logPath);
            var pattern = Path.GetFileNameWithoutExtension(logPath) + "-*.txt";

            foreach (var f in Directory.EnumerateFiles(folder, pattern))
            {
                var path = Path.Combine(folder, f);
                var ct = File.GetCreationTime(path);
                if ((System.DateTime.Now - ct).TotalDays > RetainDays)
                {
                    File.Delete(path);
                }
            }
        }
    }
}