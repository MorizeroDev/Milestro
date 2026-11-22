using System;
using Milestro.Components.Internal;
using Milestro.Skia;
using NUnit.Framework;

namespace Milestro.Tests
{
    public sealed class TextPipelineDiagnosticsTests
    {
        private bool previouslyEnabled;
        private bool previouslyFreshnessEnabled;

        [SetUp]
        public void SetUp()
        {
            previouslyEnabled = TextPipelineDiagnostics.Enabled;
            previouslyFreshnessEnabled = TextPipelineDiagnostics.FreshnessEnabled;
            TextPipelineDiagnostics.Enabled = false;
            TextPipelineDiagnostics.FreshnessEnabled = false;
        }

        [TearDown]
        public void TearDown()
        {
            TextPipelineDiagnostics.Enabled = previouslyEnabled;
            TextPipelineDiagnostics.FreshnessEnabled = previouslyFreshnessEnabled;
        }

        [Test]
        public void DisabledFreshnessDoesNotRecordRequestsOrWork()
        {
            var state = new TextPipelineFreshness();
            state.Request(10);
            state.Prepare();
            state.Accept();
            state.Complete(12);
            state.Defer();

            Assert.That(state.RequestedVersion, Is.Zero);
            Assert.That(state.PreparationAttempts, Is.Zero);
            Assert.That(state.AcceptedSubmissions, Is.Zero);
            Assert.That(state.Completions, Is.Zero);
            Assert.That(state.DeferredAttempts, Is.Zero);
        }

        [Test]
        public void UnsupportedSubmissionCannotPublishAnAttributedCompletion()
        {
            TextPipelineDiagnostics.FreshnessEnabled = true;
            var state = new TextPipelineFreshness();
            state.Request(10);
            state.Prepare();
            state.Accept(false);
            state.Complete(11);

            Assert.That(state.UnsupportedSubmission, Is.True);
            Assert.That(state.CompletedVersion, Is.Zero);
            Assert.That(state.Completions, Is.Zero);
        }

        [Test]
        public void PendingCompletionKeepsItsAcceptedRequestWhenNewerTextIsPrepared()
        {
            TextPipelineDiagnostics.FreshnessEnabled = true;
            var state = new TextPipelineFreshness();
            state.Request(10);
            state.Prepare();
            state.Accept();
            state.Request(11);
            state.Prepare();
            state.Defer();
            state.Complete(12);

            Assert.That(state.RequestedVersion, Is.EqualTo(2));
            Assert.That(state.PreparedVersion, Is.EqualTo(2));
            Assert.That(state.CompletedVersion, Is.EqualTo(1));
            Assert.That(state.CompletedRequestFrame, Is.EqualTo(10));
            Assert.That(state.CompletedFrame, Is.EqualTo(12));
            Assert.That(state.AcceptedSubmissions, Is.EqualTo(1));
            Assert.That(state.DeferredAttempts, Is.EqualTo(1));
            state.Accept();
            state.Complete(13);
            Assert.That(state.CompletedVersion, Is.EqualTo(2));
            Assert.That(state.CompletedRequestFrame, Is.EqualTo(11));
        }

        [Test]
        public void CoalescedRequestsPublishOnlyTheFinalVersionAndItsRequestFrame()
        {
            TextPipelineDiagnostics.FreshnessEnabled = true;
            var state = new TextPipelineFreshness();
            state.Request(20);
            state.Request(21);
            state.Request(21);
            state.Prepare();
            state.Accept();
            state.Complete(21);

            Assert.That(state.CompletedVersion, Is.EqualTo(3));
            Assert.That(state.CompletedRequestFrame, Is.EqualTo(21));
            Assert.That(state.PreparationAttempts, Is.EqualTo(1));
            Assert.That(state.AcceptedSubmissions, Is.EqualTo(1));
            Assert.That(state.Completions, Is.EqualTo(1));
            Assert.That(TextPipelineDiagnostics.Enabled, Is.False);
        }

        [Test]
        public void WindowCountsCarryoverEvenWhenANewerRequestCompletesBeforeObservation()
        {
            TextPipelineDiagnostics.FreshnessEnabled = true;
            var state = new TextPipelineFreshness();
            state.Request(1);
            state.Prepare();
            state.Accept();
            state.BeginWindow();
            state.Request(2);
            state.Prepare();
            state.Complete(2);
            state.Accept();
            state.Complete(2);

            Assert.That(state.CompletedVersion, Is.EqualTo(2));
            Assert.That(state.Completions, Is.EqualTo(2));
            Assert.That(state.WindowCarryoverCompletions, Is.EqualTo(1));
            Assert.That(state.WindowCompletedVersions, Is.EqualTo(1));
        }

        [Test]
        public void DisabledObservationPreservesCountersAndCannotProduceAnAvailableDelta()
        {
            var before = TextPipelineDiagnostics.Capture();

            TextPipelineDiagnostics.Record(TextPipelineCounter.GlyphAtlasUploads, 3);
            using (TextPipelineDiagnostics.Measure(TextPipelineStage.GlyphAtlasUpload)) { }
            var after = TextPipelineDiagnostics.Capture();

            Assert.That(after.Enabled, Is.False);
            Assert.That(after.GlyphAtlasUploads, Is.EqualTo(before.GlyphAtlasUploads));
            Assert.That(after.GlyphAtlasUploadTicks, Is.EqualTo(before.GlyphAtlasUploadTicks));
            Assert.Throws<InvalidOperationException>(() => after.DeltaFrom(before));
        }

        [Test]
        public void ObservationDoesNotResetExistingTotalsAndSnapshotsOwnTheirValues()
        {
            TextPipelineDiagnostics.Enabled = true;
            TextPipelineDiagnostics.Record(TextPipelineCounter.GlyphAtlasUploadBytes, 1024);
            var before = TextPipelineDiagnostics.Capture();
            TextPipelineDiagnostics.Record(TextPipelineCounter.GlyphAtlasUploadBytes, 4096);
            var after = TextPipelineDiagnostics.Capture();

            Assert.That(after.DeltaFrom(before).GlyphAtlasUploadBytes, Is.EqualTo(4096));
            Assert.That(TextPipelineDiagnostics.Capture().GlyphAtlasUploadBytes,
                Is.EqualTo(before.GlyphAtlasUploadBytes + 4096));
            TextPipelineDiagnostics.Enabled = false;
            TextPipelineDiagnostics.Enabled = true;
            Assert.That(TextPipelineDiagnostics.Capture().GlyphAtlasUploadBytes,
                Is.EqualTo(after.GlyphAtlasUploadBytes));
        }

        [Test]
        public void InterruptedSessionRejectsPartialObservation()
        {
            TextPipelineDiagnostics.Enabled = true;
            var before = TextPipelineDiagnostics.Capture();
            TextPipelineDiagnostics.Enabled = false;
            TextPipelineDiagnostics.Enabled = true;

            Assert.Throws<InvalidOperationException>(() => TextPipelineDiagnostics.Capture().DeltaFrom(before));
        }

        [Test]
        public void CoalescingReportsRequestsEnqueuesAndActualParticipantInvocationsSeparately()
        {
            var queue = new TextPreparationQueue();
            var participant = new Participant();
            TextPipelineDiagnostics.Enabled = true;
            var before = TextPipelineDiagnostics.Capture();

            queue.Enqueue(participant);
            queue.Enqueue(participant);
            queue.Enqueue(participant);
            queue.Process();
            queue.Process();
            var delta = TextPipelineDiagnostics.Capture().DeltaFrom(before);

            Assert.That(delta.SchedulerRequests, Is.EqualTo(3));
            Assert.That(delta.SchedulerUniqueEnqueues, Is.EqualTo(1));
            Assert.That(delta.SchedulerPreparations, Is.EqualTo(1));
            Assert.That(participant.Calls, Is.EqualTo(1));
            Assert.That(delta.StopwatchFrequency, Is.GreaterThan(0));
            Assert.That(delta.PreparationTicks, Is.GreaterThanOrEqualTo(0));
            Assert.That(delta.TimestampTicks, Is.GreaterThanOrEqualTo(delta.PreparationTicks));
        }

        [Test]
        public void CancelledAndInactiveParticipantsDoNotCountAsPreparations()
        {
            var queue = new TextPreparationQueue();
            var cancelled = new Participant();
            var inactive = new Participant { IsPreparationActive = false };
            TextPipelineDiagnostics.Enabled = true;
            var before = TextPipelineDiagnostics.Capture();

            queue.Enqueue(cancelled);
            queue.Enqueue(inactive);
            queue.Remove(cancelled);
            queue.Process();
            var delta = TextPipelineDiagnostics.Capture().DeltaFrom(before);

            Assert.That(delta.SchedulerRequests, Is.EqualTo(2));
            Assert.That(delta.SchedulerUniqueEnqueues, Is.EqualTo(2));
            Assert.That(delta.SchedulerPreparations, Is.Zero);
            Assert.That(delta.PreparationTicks, Is.Zero);
        }

        [Test]
        public void UploadCounterTracksOneFullAlphaPageAndIgnoresCleanFlushes()
        {
            using var page = new TextGlyphAtlas.Page(32);
            TextPipelineDiagnostics.Enabled = true;
            var before = TextPipelineDiagnostics.Capture();

            page.Flush();
            page.Flush();
            var delta = TextPipelineDiagnostics.Capture().DeltaFrom(before);

            Assert.That(delta.GlyphAtlasUploads, Is.EqualTo(1));
            Assert.That(delta.GlyphAtlasUploadBytes, Is.EqualTo(32 * 32));
            Assert.That(delta.GlyphRasterizations, Is.Zero);
        }

        private sealed class Participant : ITextPreparationParticipant
        {
            public bool IsPreparationActive { get; set; } = true;
            internal int Calls { get; private set; }
            public void PrepareText() => ++Calls;
        }
    }
}
