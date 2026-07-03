using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace KLibU.Logging
{
    public class HighPrecisionClock
    {
        [DllImport("Kernel32.dll", CallingConvention = CallingConvention.Winapi)]
        private static extern void GetSystemTimePreciseAsFileTime(out long filetime);

        public static DateTime UtcNow
        {
            get
            {
                long preciseTime;
                GetSystemTimePreciseAsFileTime(out preciseTime);
                return DateTime.FromFileTimeUtc(preciseTime);
            }
        }

        public static long UtcNowIn100nsTicks
        {
            get
            {
                long preciseTime;
                GetSystemTimePreciseAsFileTime(out preciseTime);
                return preciseTime;
            }
        }

        public static double MeasurePrecision()
        {
            var duration = TimeSpan.FromSeconds(5);
            var distinctValues = new HashSet<DateTime>();
            var stopWatch = Stopwatch.StartNew();

            while (stopWatch.Elapsed < duration)
            {
                //distinctValues.Add(DateTime.UtcNow);
                distinctValues.Add(HighPrecisionClock.UtcNow);
            }

            double precision = stopWatch.Elapsed.TotalMilliseconds / distinctValues.Count;
            return precision;
        }

        public static double MeasureDefaultPrecision()
        {
            var duration = TimeSpan.FromSeconds(5);
            var distinctValues = new HashSet<DateTime>();
            var stopWatch = Stopwatch.StartNew();

            while (stopWatch.Elapsed < duration)
            {
                distinctValues.Add(DateTime.UtcNow);
            }

            double precision = stopWatch.Elapsed.TotalMilliseconds / distinctValues.Count;
            return precision;
        }

    }
}