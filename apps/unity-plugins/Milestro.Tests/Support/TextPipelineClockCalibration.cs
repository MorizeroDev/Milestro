using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Milestro.Tests
{
    internal sealed class TextPipelineClockCalibration
    {
        public long stopwatchBefore;
        public long stopwatchAfter;
        public long qpcTimestamp;
        public long qpcFrequency;
        public bool available;
        public long stopwatchTimestamp => stopwatchBefore + (stopwatchAfter - stopwatchBefore) / 2;
        public double uncertaintyQpcTicks => (stopwatchAfter - stopwatchBefore) *
            (double)qpcFrequency / Stopwatch.Frequency / 2;

        [DllImport("kernel32.dll")]
        private static extern bool QueryPerformanceCounter(out long value);

        [DllImport("kernel32.dll")]
        private static extern bool QueryPerformanceFrequency(out long value);

        internal static TextPipelineClockCalibration Capture()
        {
            var result = new TextPipelineClockCalibration();
            if (UnityEngine.Application.platform != UnityEngine.RuntimePlatform.WindowsPlayer &&
                UnityEngine.Application.platform != UnityEngine.RuntimePlatform.WindowsEditor) return result;
            var frequencyAvailable = QueryPerformanceFrequency(out result.qpcFrequency);
            result.stopwatchBefore = Stopwatch.GetTimestamp();
            var timestampAvailable = QueryPerformanceCounter(out result.qpcTimestamp);
            result.stopwatchAfter = Stopwatch.GetTimestamp();
            result.available = frequencyAvailable && timestampAvailable && result.qpcFrequency > 0 &&
                result.stopwatchAfter >= result.stopwatchBefore;
            return result;
        }
    }
}
