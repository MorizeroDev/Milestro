using System;
using System.Diagnostics;
using Unity.Profiling;

namespace Milestro.Components.Internal
{
    internal struct TextPipelineFreshness
    {
        internal long RequestedVersion;
        internal long PreparedVersion;
        internal long SubmittedVersion;
        internal long CompletedVersion;
        internal long PreparationAttempts;
        internal long AcceptedSubmissions;
        internal long Completions;
        internal long DeferredAttempts;
        internal int RequestedFrame;
        internal int PreparedRequestFrame;
        internal int SubmittedRequestFrame;
        internal int CompletedRequestFrame;
        internal int CompletedFrame;
        internal long WindowCarryoverCompletions;
        internal long WindowCompletedVersions;
        internal bool UnsupportedSubmission;
        private long windowRequestBoundary;
        private long windowCompletedVersion;
        private bool submissionPending;

        internal void BeginWindow()
        {
            windowRequestBoundary = RequestedVersion;
            windowCompletedVersion = CompletedVersion;
            WindowCarryoverCompletions = 0;
            WindowCompletedVersions = 0;
        }

        internal void Request(int frame)
        {
            if (!TextPipelineDiagnostics.FreshnessEnabled) return;
            ++RequestedVersion;
            RequestedFrame = frame;
        }

        internal void Prepare()
        {
            if (!TextPipelineDiagnostics.FreshnessEnabled) return;
            PreparedVersion = RequestedVersion;
            PreparedRequestFrame = RequestedFrame;
            ++PreparationAttempts;
        }

        internal void Accept(bool supportsVersionTracking = true)
        {
            if (!TextPipelineDiagnostics.FreshnessEnabled) return;
            UnsupportedSubmission |= !supportsVersionTracking;
            if (UnsupportedSubmission) return;
            SubmittedVersion = PreparedVersion;
            SubmittedRequestFrame = PreparedRequestFrame;
            submissionPending = true;
            ++AcceptedSubmissions;
        }

        internal void Complete(int frame)
        {
            if (!TextPipelineDiagnostics.FreshnessEnabled || !submissionPending || UnsupportedSubmission) return;
            submissionPending = false;
            CompletedVersion = SubmittedVersion;
            CompletedRequestFrame = SubmittedRequestFrame;
            CompletedFrame = frame;
            ++Completions;
            if (CompletedVersion <= windowRequestBoundary)
            {
                ++WindowCarryoverCompletions;
            }
            else if (CompletedVersion != windowCompletedVersion)
            {
                ++WindowCompletedVersions;
            }
            windowCompletedVersion = CompletedVersion;
        }

        internal void DiscardPending() => submissionPending = false;

        internal void Defer()
        {
            if (TextPipelineDiagnostics.FreshnessEnabled) ++DeferredAttempts;
        }
    }

    internal enum TextPipelineCounter
    {
        SchedulerRequests,
        SchedulerUniqueEnqueues,
        SchedulerPreparations,
        ParagraphPreparations,
        ParagraphBuilds,
        SlimTextPreparations,
        ParagraphLayouts,
        GlyphSnapshotExports,
        GlyphMeshBuilds,
        GlyphMeshPopulates,
        GlyphCacheHits,
        GlyphCacheMisses,
        GlyphRasterizations,
        GlyphAtlasUploads,
        GlyphAtlasUploadBytes,
        TextureRegionsSubmitted,
        TexturePageSubmissions,
        Count
    }

    internal enum TextPipelineStage
    {
        Preparation,
        ParagraphPreparation,
        ParagraphBuild,
        SlimTextPreparation,
        ParagraphLayout,
        GlyphSnapshotExport,
        GlyphMeshBuild,
        GlyphMeshPopulate,
        GlyphRasterization,
        GlyphAtlasUpload,
        TexturePageSubmission,
        Count
    }

    internal static class TextPipelineDiagnostics
    {
        private static readonly long[] Counters = new long[(int)TextPipelineCounter.Count];
        private static readonly long[] Timings = new long[(int)TextPipelineStage.Count];
        private static readonly ProfilerMarker[] Markers = CreateMarkers();
        private static bool enabled;
        private static long sessionId;

        internal static bool FreshnessEnabled { get; set; }

        internal static bool Enabled
        {
            get => enabled;
            set
            {
                if (enabled == value)
                {
                    return;
                }
                enabled = value;
                ++sessionId;
            }
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void DisableOnSubsystemRegistration()
        {
            Enabled = false;
            FreshnessEnabled = false;
        }

        internal static TextPipelineDiagnosticSnapshot Capture() =>
            new TextPipelineDiagnosticSnapshot(enabled, sessionId, Stopwatch.GetTimestamp(),
                (long[])Counters.Clone(), (long[])Timings.Clone());

        internal static void Record(TextPipelineCounter counter, long amount = 1)
        {
            if (enabled)
            {
                Counters[(int)counter] += amount;
            }
        }

        internal static Scope Measure(TextPipelineStage stage) =>
            enabled ? new Scope(stage, sessionId) : default;

        private static ProfilerMarker[] CreateMarkers()
        {
            var markers = new ProfilerMarker[(int)TextPipelineStage.Count];
            for (var index = 0; index < markers.Length; ++index)
            {
                markers[index] = new ProfilerMarker("Milestro.TextPipeline." + (TextPipelineStage)index);
            }
            return markers;
        }

        internal readonly struct Scope : IDisposable
        {
            private readonly TextPipelineStage stage;
            private readonly long session;
            private readonly long start;
            private readonly bool active;

            internal Scope(TextPipelineStage stage, long session)
            {
                this.stage = stage;
                this.session = session;
                active = true;
                Markers[(int)stage].Begin();
                start = Stopwatch.GetTimestamp();
            }

            public void Dispose()
            {
                if (!active)
                {
                    return;
                }
                var elapsed = Stopwatch.GetTimestamp() - start;
                Markers[(int)stage].End();
                if (enabled && session == sessionId)
                {
                    Timings[(int)stage] += elapsed;
                }
            }
        }
    }

    internal sealed class TextPipelineDiagnosticSnapshot
    {
        private readonly long[] counters;
        private readonly long[] timings;

        internal TextPipelineDiagnosticSnapshot(bool enabled, long sessionId, long timestampTicks,
            long[] counters, long[] timings)
        {
            Enabled = enabled;
            SessionId = sessionId;
            TimestampTicks = timestampTicks;
            this.counters = counters;
            this.timings = timings;
        }

        public bool Enabled { get; }
        public long SessionId { get; }
        public long TimestampTicks { get; }
        public long StopwatchFrequency => Stopwatch.Frequency;
        public long SchedulerRequests => counters[(int)TextPipelineCounter.SchedulerRequests];
        public long SchedulerUniqueEnqueues => counters[(int)TextPipelineCounter.SchedulerUniqueEnqueues];
        public long SchedulerPreparations => counters[(int)TextPipelineCounter.SchedulerPreparations];
        public long ParagraphPreparations => counters[(int)TextPipelineCounter.ParagraphPreparations];
        public long ParagraphBuilds => counters[(int)TextPipelineCounter.ParagraphBuilds];
        public long SlimTextPreparations => counters[(int)TextPipelineCounter.SlimTextPreparations];
        public long ParagraphLayouts => counters[(int)TextPipelineCounter.ParagraphLayouts];
        public long GlyphSnapshotExports => counters[(int)TextPipelineCounter.GlyphSnapshotExports];
        public long GlyphMeshBuilds => counters[(int)TextPipelineCounter.GlyphMeshBuilds];
        public long GlyphMeshPopulates => counters[(int)TextPipelineCounter.GlyphMeshPopulates];
        public long GlyphCacheHits => counters[(int)TextPipelineCounter.GlyphCacheHits];
        public long GlyphCacheMisses => counters[(int)TextPipelineCounter.GlyphCacheMisses];
        public long GlyphRasterizations => counters[(int)TextPipelineCounter.GlyphRasterizations];
        public long GlyphAtlasUploads => counters[(int)TextPipelineCounter.GlyphAtlasUploads];
        public long GlyphAtlasUploadBytes => counters[(int)TextPipelineCounter.GlyphAtlasUploadBytes];
        public long TextureRegionsSubmitted => counters[(int)TextPipelineCounter.TextureRegionsSubmitted];
        public long TexturePageSubmissions => counters[(int)TextPipelineCounter.TexturePageSubmissions];
        public long PreparationTicks => timings[(int)TextPipelineStage.Preparation];
        public long ParagraphPreparationTicks => timings[(int)TextPipelineStage.ParagraphPreparation];
        public long ParagraphBuildTicks => timings[(int)TextPipelineStage.ParagraphBuild];
        public long SlimTextPreparationTicks => timings[(int)TextPipelineStage.SlimTextPreparation];
        public long ParagraphLayoutTicks => timings[(int)TextPipelineStage.ParagraphLayout];
        public long GlyphSnapshotExportTicks => timings[(int)TextPipelineStage.GlyphSnapshotExport];
        public long GlyphMeshBuildTicks => timings[(int)TextPipelineStage.GlyphMeshBuild];
        public long GlyphMeshPopulateTicks => timings[(int)TextPipelineStage.GlyphMeshPopulate];
        public long GlyphRasterizationTicks => timings[(int)TextPipelineStage.GlyphRasterization];
        public long GlyphAtlasUploadTicks => timings[(int)TextPipelineStage.GlyphAtlasUpload];
        public long TexturePageSubmissionTicks => timings[(int)TextPipelineStage.TexturePageSubmission];

        public TextPipelineDiagnosticSnapshot DeltaFrom(TextPipelineDiagnosticSnapshot previous)
        {
            if (previous == null || !Enabled || !previous.Enabled || SessionId != previous.SessionId ||
                TimestampTicks < previous.TimestampTicks)
            {
                throw new InvalidOperationException("Diagnostics require an uninterrupted enabled interval.");
            }
            return new TextPipelineDiagnosticSnapshot(true, SessionId, TimestampTicks - previous.TimestampTicks,
                Subtract(counters, previous.counters), Subtract(timings, previous.timings));
        }

        private static long[] Subtract(long[] current, long[] previous)
        {
            var delta = new long[current.Length];
            for (var index = 0; index < delta.Length; ++index)
            {
                delta[index] = current[index] - previous[index];
            }
            return delta;
        }
    }
}
