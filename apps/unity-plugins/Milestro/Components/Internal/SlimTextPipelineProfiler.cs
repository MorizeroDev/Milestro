using System.Diagnostics;

namespace Milestro.Components.Internal
{
    internal static class SlimTextPipelineProfiler
    {
        private static readonly double TicksToMilliseconds = 1000.0 / Stopwatch.Frequency;

        public static bool Enabled;

        public static long RebuildCalls;
        public static long RebuildSkippedNoDraw;
        public static double EnsureSurfaceMs;
        public static double EnsureFontMs;
        public static double BuildCommandsMs;
        public static double MeasureTextMs;
        public static double TrySubmitMs;

        public static long SubmitCalls;
        public static double SubmitPrepareMs;
        public static double SubmitMarshalMs;
        public static double SubmitEnqueueMs;

        public static long FastPathCalls;
        public static long ProducerRebuildCalls;
        public static double ProducerRebuildMs;
        public static long PresenterUpdateCalls;
        public static double PresenterUpdateMs;
        public static long CompletionCalls;
        public static double CompletionMs;

        public static long Timestamp => Stopwatch.GetTimestamp();

        public static double ElapsedMs(long startTimestamp)
        {
            return (Stopwatch.GetTimestamp() - startTimestamp) * TicksToMilliseconds;
        }

        public static void Reset()
        {
            RebuildCalls = 0;
            RebuildSkippedNoDraw = 0;
            EnsureSurfaceMs = 0;
            EnsureFontMs = 0;
            BuildCommandsMs = 0;
            MeasureTextMs = 0;
            TrySubmitMs = 0;
            SubmitCalls = 0;
            SubmitPrepareMs = 0;
            SubmitMarshalMs = 0;
            SubmitEnqueueMs = 0;
            FastPathCalls = 0;
            ProducerRebuildCalls = 0;
            ProducerRebuildMs = 0;
            PresenterUpdateCalls = 0;
            PresenterUpdateMs = 0;
            CompletionCalls = 0;
            CompletionMs = 0;
        }
    }
}
