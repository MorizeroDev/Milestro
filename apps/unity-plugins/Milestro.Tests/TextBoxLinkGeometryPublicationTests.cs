using System;
using Milestro.Components.Internal;
using Milestro.RichTextParser;
using Milestro.Skia;
using NUnit.Framework;
using UnityEngine;

namespace Milestro.Tests
{
    public class TextBoxLinkGeometryPublicationTests
    {
        [Test]
        public void PublicationStateIsValueTypeAndDefaultStateHasNoLinkWork()
        {
            Assert.That(typeof(TextBoxLinkGeometryPublication).IsValueType, Is.True);
            var publication = new TextBoxLinkGeometryPublication();
            Assert.That(publication.HasPending, Is.False);
            Assert.That(publication.TryGetPublished(out _, out _), Is.False);
        }

        [Test]
        public void PendingSubmissionFailsClosedUntilMatchingDrawnPublishesWholeSnapshot()
        {
            var publication = new TextBoxLinkGeometryPublication();
            var snapshot = Snapshot(new Rect(3f, 4f, 50f, 60f),
                new Vector2(7f, 8f),
                new Vector2Int(90, 100));

            Assert.That(publication.TryBegin(hasGeometry: true, snapshot, out var token), Is.True);
            Assert.That(publication.HasPending, Is.True);
            Assert.That(publication.TryGetPublished(out _, out _), Is.False);
            Assert.That(publication.TryBegin(hasGeometry: true, snapshot, out _), Is.False,
                "A target must never enqueue a second render while its first submission is pending.");
            Assert.That(publication.Complete(token + 1,
                    UnitySkiaRenderTextureSurface.RenderSubmissionStatus.Drawn,
                    out _),
                Is.False);
            Assert.That(publication.TryGetPublished(out _, out _), Is.False);

            Assert.That(publication.Complete(token,
                    UnitySkiaRenderTextureSurface.RenderSubmissionStatus.Drawn,
                    out var needsRebuild),
                Is.True);
            Assert.That(needsRebuild, Is.False);
            Assert.That(publication.TryGetPublished(out var published, out var generation), Is.True);
            Assert.That(generation, Is.Not.Zero);
            Assert.That(published.Paragraph, Is.SameAs(snapshot.Paragraph));
            Assert.That(published.Links, Is.SameAs(snapshot.Links));
            Assert.That(published.PaintPosition, Is.EqualTo(snapshot.PaintPosition));
            Assert.That(published.ClipRect, Is.EqualTo(snapshot.ClipRect));
            Assert.That(published.VisibleOutputSize, Is.EqualTo(snapshot.VisibleOutputSize));
        }

        [Test]
        public void LateOldTokenCannotPublishOverNewPendingSubmission()
        {
            var publication = new TextBoxLinkGeometryPublication();
            var oldSnapshot = Snapshot(new Rect(1f, 2f, 3f, 4f), Vector2.one, new Vector2Int(10, 20));
            var newSnapshot = Snapshot(new Rect(5f, 6f, 7f, 8f), Vector2.zero, new Vector2Int(30, 40));

            Assert.That(publication.TryBegin(true, oldSnapshot, out var oldToken), Is.True);
            Assert.That(publication.Complete(oldToken,
                    UnitySkiaRenderTextureSurface.RenderSubmissionStatus.Skipped,
                    out _),
                Is.True);
            Assert.That(publication.TryBegin(true, newSnapshot, out var newToken), Is.True);

            Assert.That(publication.Complete(oldToken,
                    UnitySkiaRenderTextureSurface.RenderSubmissionStatus.Drawn,
                    out _),
                Is.False);
            Assert.That(publication.HasPending, Is.True);
            Assert.That(publication.TryGetPublished(out _, out _), Is.False);
            Assert.That(publication.Complete(newToken,
                    UnitySkiaRenderTextureSurface.RenderSubmissionStatus.Drawn,
                    out _),
                Is.True);
            Assert.That(publication.TryGetPublished(out var published, out _), Is.True);
            Assert.That(published.ClipRect, Is.EqualTo(newSnapshot.ClipRect));
        }

        [Test]
        public void PendingRebuildRequestsCoalesceAndDoNotForceASecondDuplicate()
        {
            var publication = new TextBoxLinkGeometryPublication();
            publication.RequireControlledSubmission();

            Assert.That(publication.TryBegin(false, default, out var token), Is.True);
            publication.RequestRebuild();
            publication.RequestRebuild();
            publication.RequestRebuild();
            Assert.That(publication.Complete(token,
                    UnitySkiaRenderTextureSurface.RenderSubmissionStatus.Drawn,
                    out var needsRebuild),
                Is.True);
            Assert.That(needsRebuild, Is.True);
            Assert.That(publication.TryGetPublished(out _, out _), Is.False,
                "No-link submissions must not publish or allocate link geometry.");

            Assert.That(publication.TryBegin(false, default, out var followUpToken), Is.True);
            Assert.That(publication.Complete(followUpToken,
                    UnitySkiaRenderTextureSurface.RenderSubmissionStatus.Drawn,
                    out needsRebuild),
                Is.True);
            Assert.That(needsRebuild, Is.False);
        }

        [Test]
        public void OrdinarySubmissionsRemainUnrestrictedUntilALinkTransitionThenDrainBeforePublication()
        {
            var publication = new TextBoxLinkGeometryPublication();

            Assert.That(publication.RequiresControlledSubmission, Is.False);
            publication.TrackOrdinarySubmission();
            publication.TrackOrdinarySubmission();
            Assert.That(publication.TryBegin(true,
                    Snapshot(new Rect(1f, 1f, 10f, 10f), Vector2.zero, new Vector2Int(20, 20)),
                    out _),
                Is.False);
            Assert.That(publication.RequiresControlledSubmission, Is.True);
            Assert.That(publication.CanBeginControlledSubmission, Is.False);
            Assert.That(publication.CompleteOrdinarySubmission(), Is.True);
            Assert.That(publication.CanBeginControlledSubmission, Is.False);
            Assert.That(publication.CompleteOrdinarySubmission(), Is.True);
            Assert.That(publication.CanBeginControlledSubmission, Is.True);
            Assert.That(publication.CompleteOrdinarySubmission(), Is.False,
                "Every ordinary render callback must be consumed exactly once.");

            Assert.That(publication.TryBegin(true,
                    Snapshot(new Rect(1f, 1f, 10f, 10f), Vector2.zero, new Vector2Int(20, 20)),
                    out var token),
                Is.True);
            Assert.That(publication.Complete(token,
                    UnitySkiaRenderTextureSurface.RenderSubmissionStatus.Drawn,
                    out _),
                Is.True);
            Assert.That(publication.TryGetPublished(out _, out _), Is.True);
        }

        [Test]
        public void LinkRemovalReturnsToOrdinaryModeOnlyAfterSuccessfulStableNoGeometryDraw()
        {
            var publication = new TextBoxLinkGeometryPublication();
            publication.RequireControlledSubmission();
            Assert.That(publication.TryBegin(true,
                    Snapshot(new Rect(1f, 1f, 10f, 10f), Vector2.zero, new Vector2Int(20, 20)),
                    out var linkedToken),
                Is.True);
            Assert.That(publication.Complete(linkedToken,
                    UnitySkiaRenderTextureSurface.RenderSubmissionStatus.Drawn,
                    out _),
                Is.True);

            Assert.That(publication.TryBegin(false, default, out var skippedToken), Is.True);
            Assert.That(publication.Complete(skippedToken,
                    UnitySkiaRenderTextureSurface.RenderSubmissionStatus.Skipped,
                    out _),
                Is.True);
            Assert.That(publication.RequiresControlledSubmission, Is.True);

            Assert.That(publication.TryBegin(false, default, out var dirtyToken), Is.True);
            publication.RequestRebuild();
            Assert.That(publication.Complete(dirtyToken,
                    UnitySkiaRenderTextureSurface.RenderSubmissionStatus.Drawn,
                    out var needsRebuild),
                Is.True);
            Assert.That(needsRebuild, Is.True);
            Assert.That(publication.RequiresControlledSubmission, Is.True);

            Assert.That(publication.TryBegin(false, default, out var stableToken), Is.True);
            Assert.That(publication.Complete(stableToken,
                    UnitySkiaRenderTextureSurface.RenderSubmissionStatus.Drawn,
                    out needsRebuild),
                Is.True);
            Assert.That(needsRebuild, Is.False);
            Assert.That(publication.RequiresControlledSubmission, Is.False);
            publication.TrackOrdinarySubmission();
            Assert.That(publication.CompleteOrdinarySubmission(), Is.True);
        }

        [Test]
        public void SkippedCompletionClearsInFlightWithoutPublishing()
        {
            AssertNonDrawnCompletionClearsInFlight(UnitySkiaRenderTextureSurface.RenderSubmissionStatus.Skipped);
        }

        [Test]
        public void FailedCompletionClearsInFlightWithoutPublishing()
        {
            AssertNonDrawnCompletionClearsInFlight(UnitySkiaRenderTextureSurface.RenderSubmissionStatus.Failed);
        }

        private static void AssertNonDrawnCompletionClearsInFlight(
            UnitySkiaRenderTextureSurface.RenderSubmissionStatus status)
        {
            var publication = new TextBoxLinkGeometryPublication();
            Assert.That(publication.TryBegin(true,
                    Snapshot(new Rect(1f, 1f, 1f, 1f), Vector2.zero, Vector2Int.one),
                    out var token),
                Is.True);

            Assert.That(publication.Complete(token, status, out _), Is.True);
            Assert.That(publication.HasPending, Is.False);
            Assert.That(publication.TryGetPublished(out _, out _), Is.False);
            Assert.That(publication.TryBegin(false, default, out _), Is.True,
                "Failed or skipped render completion must not starve the target.");
        }

        [Test]
        public void CancelAndResetRejectLateCompletionAndReleaseInFlight()
        {
            var publication = new TextBoxLinkGeometryPublication();
            Assert.That(publication.TryBegin(true,
                    Snapshot(new Rect(1f, 1f, 1f, 1f), Vector2.zero, Vector2Int.one),
                    out var cancelledToken),
                Is.True);
            Assert.That(publication.Cancel(cancelledToken, out _), Is.True);
            Assert.That(publication.HasPending, Is.False);

            Assert.That(publication.TryBegin(false, default, out var teardownToken), Is.True);
            publication.Reset();
            Assert.That(publication.HasPending, Is.False);
            Assert.That(publication.Complete(teardownToken,
                    UnitySkiaRenderTextureSurface.RenderSubmissionStatus.Drawn,
                    out _),
                Is.False);
            Assert.That(publication.TryGetPublished(out _, out _), Is.False);
            Assert.That(publication.RequiresControlledSubmission, Is.False);
            Assert.That(publication.CanBeginControlledSubmission, Is.True);
        }

        [Test]
        public void ViewportSnapshotComparisonDetectsEveryGeometryChangingInput()
        {
            var baseline = TextBoxRenderViewport.Fixed(new Vector2Int(100, 50),
                default,
                Vector2.zero,
                Vector2.zero);
            var same = TextBoxRenderViewport.Fixed(new Vector2Int(100, 50),
                default,
                Vector2.zero,
                Vector2.zero);

            Assert.That(baseline.Matches(same), Is.True);
            Assert.That(baseline.Matches(TextBoxRenderViewport.Fixed(new Vector2Int(101, 50),
                default,
                Vector2.zero,
                Vector2.zero)), Is.False);
            Assert.That(baseline.Matches(TextBoxRenderViewport.Fixed(new Vector2Int(100, 50),
                default,
                new Vector2(1f, 0f),
                Vector2.zero)), Is.False);
            Assert.That(baseline.Matches(TextBoxRenderViewport.Fixed(new Vector2Int(100, 50),
                default,
                Vector2.zero,
                new Vector2(0f, 1f))), Is.False);
            Assert.That(baseline.Matches(TextBoxRenderViewport.Invisible(new Vector2Int(100, 50), default)), Is.False);
        }

        private static TextBoxLinkGeometrySnapshot Snapshot(Rect clip,
            Vector2 paint,
            Vector2Int visibleOutput)
        {
            return new TextBoxLinkGeometrySnapshot(null!,
                new[] { new LinkAnnotation("href", "id", 0, 1, 0) },
                paint,
                clip,
                visibleOutput);
        }
    }
}
