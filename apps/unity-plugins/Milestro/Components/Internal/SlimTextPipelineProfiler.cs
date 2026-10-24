using System.Diagnostics;
using Unity.Profiling;

namespace Milestro.Components.Internal
{
    internal static class SlimTextPipelineProfiler
    {
        private static readonly double TicksToMilliseconds = 1000.0 / Stopwatch.Frequency;

        public static readonly ProfilerMarker ProducerUpdateMarker =
            new ProfilerMarker(ProfilerCategory.Scripts, "Milestro.SlimText.ProducerUpdate");
        public static readonly ProfilerMarker RebuildFastManagedMarker =
            new ProfilerMarker(ProfilerCategory.Scripts, "Milestro.SlimText.RebuildFastManaged");
        public static readonly ProfilerMarker EnsureFontMarker =
            new ProfilerMarker(ProfilerCategory.Scripts, "Milestro.SlimText.EnsureFont");
        public static readonly ProfilerMarker EnsureSnapshotMarker =
            new ProfilerMarker(ProfilerCategory.Scripts, "Milestro.SlimText.EnsureSnapshot");
        public static readonly ProfilerMarker EncodeUpdateTextMarker =
            new ProfilerMarker(ProfilerCategory.Scripts, "Milestro.SlimText.EncodeUpdateText");
        public static readonly ProfilerMarker MeasureTextMarker =
            new ProfilerMarker(ProfilerCategory.Scripts, "Milestro.SlimText.MeasureText");
        public static readonly ProfilerMarker SubmitFastMarker =
            new ProfilerMarker(ProfilerCategory.Scripts, "Milestro.SlimText.SubmitFast");
        public static readonly ProfilerMarker WorldTextBatchUpdateMarker =
            new ProfilerMarker(ProfilerCategory.Scripts, "Milestro.WorldText.BatchUpdate");
        public static readonly ProfilerMarker WorldTextBatchMeshMarker =
            new ProfilerMarker(ProfilerCategory.Scripts, "Milestro.WorldText.BatchMesh");
        public static readonly ProfilerMarker WorldTextBatchUvMarker =
            new ProfilerMarker(ProfilerCategory.Scripts, "Milestro.WorldText.BatchUvs");
        public static readonly ProfilerMarker WorldTextBatchObserveMarker =
            new ProfilerMarker(ProfilerCategory.Scripts, "Milestro.WorldText.BatchObserve");
        public static readonly ProfilerMarker WorldTextBatchOriginalStateMarker =
            new ProfilerMarker(ProfilerCategory.Scripts, "Milestro.WorldText.BatchOriginalState");

        public static bool Enabled;

        public static long RebuildCalls;
        public static long RebuildSkippedNoDraw;
        public static double EnsureSurfaceMs;
        public static double EnsureFontMs;
        public static double EnsureSnapshotMs;
        public static double EncodeUpdateTextMs;
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
        public static long WorldTextBatchUpdateCalls;
        public static double WorldTextBatchUpdateMs;
        public static long WorldTextBatchMeshCalls;
        public static double WorldTextBatchMeshMs;
        public static long WorldTextBatchUvCalls;
        public static double WorldTextBatchUvMs;
        public static double WorldTextBatchObserveMs;
        public static double WorldTextBatchOriginalStateMs;

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
            EnsureSnapshotMs = 0;
            EncodeUpdateTextMs = 0;
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
            WorldTextBatchUpdateCalls = 0;
            WorldTextBatchUpdateMs = 0;
            WorldTextBatchMeshCalls = 0;
            WorldTextBatchMeshMs = 0;
            WorldTextBatchUvCalls = 0;
            WorldTextBatchUvMs = 0;
            WorldTextBatchObserveMs = 0;
            WorldTextBatchOriginalStateMs = 0;
        }
    }
}
