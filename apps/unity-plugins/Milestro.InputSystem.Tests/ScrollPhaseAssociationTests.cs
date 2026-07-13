using Milestro.InputSystem.Service;
using NUnit.Framework;
using UnityEngine;

namespace Milestro.InputSystemTests
{
    public class ScrollPhasePollBudgetTests
    {
        [Test]
        public void BudgetIsSharedAcrossCallsWithinAFrameAndResetsOnTheNextFrame()
        {
            var budget = new ScrollPhasePollBudget(3);

            Assert.That(budget.TryConsume(10), Is.True);
            Assert.That(budget.TryConsume(10), Is.True);
            Assert.That(budget.TryConsume(10), Is.True);
            Assert.That(budget.TryConsume(10), Is.False);
            Assert.That(budget.PollsThisFrame, Is.EqualTo(3));
            Assert.That(budget.TryConsume(11), Is.True);
            Assert.That(budget.PollsThisFrame, Is.EqualTo(1));
        }

        [Test]
        public void RejectsANonPositiveBudget()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new ScrollPhasePollBudget(0));
        }
    }

    public class ScrollPhaseAssociationTests
    {
        [Test]
        public void AssociatesWhenNativeArrivesBeforeActionAndUgui()
        {
            var tracker = new ScrollPhaseAssociationTracker();

            tracker.ObserveNative(10, Evidence(1, gestureId: 7, eventNumber: 20));
            tracker.ObserveAction(10, 12.5, new Vector2(0f, -3f));
            tracker.ObserveUgui(10, 101, new Vector2(0f, -1f));
            tracker.AdvanceFrame(10);

            AssertAssociation(tracker, 7, 101);
        }

        [Test]
        public void AssociatesWhenActionAndUguiArriveBeforeNative()
        {
            var tracker = new ScrollPhaseAssociationTracker();

            tracker.ObserveAction(20, 50.25, new Vector2(2f, 0f));
            tracker.ObserveUgui(20, 202, new Vector2(1f, 0f));
            tracker.ObserveNative(21,
                Evidence(1, gestureId: 8, eventNumber: 30, delta: new Vector2(4f, 0f), timestamp: 10.0));
            tracker.AdvanceFrame(21);

            AssertAssociation(tracker, 8, 202);
            Assert.That(tracker.Association!.Value.ObservedTimestampOffset, Is.EqualTo(40.25));
        }

        [Test]
        public void SequenceGapRemainsInvalidUntilACleanBegan()
        {
            var tracker = new ScrollPhaseAssociationTracker();
            tracker.ObserveNative(1, Evidence(1, gestureId: 4, eventNumber: 10, gesturePhase: 3));

            tracker.ObserveNative(1, Evidence(3, gestureId: 4, eventNumber: 11, gesturePhase: 3));
            Assert.That(tracker.IsInvalid, Is.True);
            Assert.That(tracker.FailureReason, Is.EqualTo("native-sequence-gap"));

            tracker.ObserveNative(2, Evidence(4, gestureId: 4, eventNumber: 12, gesturePhase: 3));
            Assert.That(tracker.IsInvalid, Is.True);

            tracker.ObserveNative(3, Evidence(5, gestureId: 5, eventNumber: 13, gesturePhase: 2));
            Assert.That(tracker.IsInvalid, Is.False);
            tracker.ObserveAction(3, 2d, new Vector2(0f, -1f));
            tracker.ObserveUgui(3, 303, new Vector2(0f, -1f));
            tracker.AdvanceFrame(3);
            AssertAssociation(tracker, 5, 303);
        }

        [Test]
        public void OverflowedBeganCannotRecoverUntilANewerCleanBegan()
        {
            var tracker = new ScrollPhaseAssociationTracker();

            tracker.ObserveNative(1,
                Evidence(1, gestureId: 4, eventNumber: 10, gesturePhase: 2, queueOverflowed: true));
            Assert.That(tracker.IsInvalid, Is.True);

            tracker.ObserveNative(2, Evidence(2, gestureId: 4, eventNumber: 11, gesturePhase: 3));
            Assert.That(tracker.IsInvalid, Is.True);

            tracker.ObserveNative(3, Evidence(3, gestureId: 5, eventNumber: 12, gesturePhase: 2));
            Assert.That(tracker.IsInvalid, Is.False);
        }

        [Test]
        public void EventNumberMayJumpButMayNotRepeatWithinAWindow()
        {
            var tracker = new ScrollPhaseAssociationTracker();
            tracker.ObserveNative(5, Evidence(1, gestureId: 9, eventNumber: 10));
            tracker.ObserveNative(5, Evidence(2, gestureId: 9, eventNumber: 50));
            tracker.ObserveAction(5, 1d, new Vector2(0f, -2f));
            tracker.ObserveUgui(5, 505, new Vector2(0f, -1f));
            tracker.AdvanceFrame(5);
            AssertAssociation(tracker, 9, 505);

            tracker.Reset();
            tracker.ObserveNative(5, Evidence(1, gestureId: 9, eventNumber: 50));
            tracker.ObserveNative(5, Evidence(2, gestureId: 9, eventNumber: 50));
            Assert.That(tracker.IsInvalid, Is.True);
            Assert.That(tracker.FailureReason, Is.EqualTo("native-event-number-not-monotonic"));
        }

        [Test]
        public void EventNumberLedgerTracksInterleavedWindows()
        {
            var tracker = new ScrollPhaseAssociationTracker();
            tracker.ObserveNative(1, Evidence(1, windowNumber: 1, keyWindowNumber: 1, eventNumber: 10,
                delta: Vector2.zero));
            tracker.ObserveNative(1, Evidence(2, windowNumber: 2, keyWindowNumber: 2, eventNumber: 20,
                delta: Vector2.zero));
            tracker.ObserveNative(1, Evidence(3, windowNumber: 1, keyWindowNumber: 1, eventNumber: 10,
                delta: Vector2.zero));

            Assert.That(tracker.IsInvalid, Is.True);
            Assert.That(tracker.FailureReason, Is.EqualTo("native-event-number-not-monotonic"));
        }

        [Test]
        public void InitialAssociationRequiresTheNativeEventWindowToBeKey()
        {
            var tracker = new ScrollPhaseAssociationTracker();

            tracker.ObserveNative(1, Evidence(1, windowNumber: 4, keyWindowNumber: 5));

            Assert.That(tracker.IsInvalid, Is.True);
            Assert.That(tracker.FailureReason, Is.EqualTo("native-window-not-key"));
        }

        [Test]
        public void MultipleNativeGesturesFailClosed()
        {
            var tracker = new ScrollPhaseAssociationTracker();
            tracker.ObserveNative(1, Evidence(1, gestureId: 1, eventNumber: 10));
            tracker.ObserveNative(1, Evidence(2, gestureId: 2, eventNumber: 11));
            tracker.ObserveAction(1, 1d, new Vector2(0f, -1f));
            tracker.ObserveUgui(1, 1, new Vector2(0f, -1f));

            tracker.AdvanceFrame(1);

            Assert.That(tracker.IsInvalid, Is.True);
            Assert.That(tracker.FailureReason, Is.EqualTo("multiple-native-candidates"));
        }

        [Test]
        public void MultipleNativeWindowsFailClosed()
        {
            var tracker = new ScrollPhaseAssociationTracker();
            tracker.ObserveNative(1,
                Evidence(1, gestureId: 1, windowNumber: 4, keyWindowNumber: 4, eventNumber: 10));
            tracker.ObserveNative(1,
                Evidence(2, gestureId: 1, windowNumber: 5, keyWindowNumber: 5, eventNumber: 11));
            tracker.ObserveAction(1, 1d, new Vector2(0f, -1f));
            tracker.ObserveUgui(1, 1, new Vector2(0f, -1f));

            tracker.AdvanceFrame(1);

            Assert.That(tracker.IsInvalid, Is.True);
            Assert.That(tracker.FailureReason, Is.EqualTo("multiple-native-candidates"));
        }

        [Test]
        public void BoundGestureCannotMoveToAnotherWindow()
        {
            var tracker = new ScrollPhaseAssociationTracker();
            tracker.ObserveNative(1, Evidence(1, gestureId: 1, eventNumber: 10));
            tracker.ObserveAction(1, 1d, new Vector2(0f, -1f));
            tracker.ObserveUgui(1, 1, new Vector2(0f, -1f));
            tracker.AdvanceFrame(1);
            AssertAssociation(tracker, 1, 1);

            tracker.ObserveNative(2,
                Evidence(2, gestureId: 1, windowNumber: 5, keyWindowNumber: 5, eventNumber: 11));

            Assert.That(tracker.IsInvalid, Is.True);
            Assert.That(tracker.FailureReason, Is.EqualTo("bound-gesture-window-changed"));
        }

        [Test]
        public void ZeroDeltaEndFromAnotherWindowCannotEndTheBoundGesture()
        {
            var tracker = new ScrollPhaseAssociationTracker();
            tracker.ObserveNative(1, Evidence(1, gestureId: 1, eventNumber: 10));
            tracker.ObserveAction(1, 1d, new Vector2(0f, -1f));
            tracker.ObserveUgui(1, 1, new Vector2(0f, -1f));
            tracker.AdvanceFrame(1);
            AssertAssociation(tracker, 1, 1);
            var crossWindowEnd = Evidence(2,
                gestureId: 1,
                windowNumber: 5,
                keyWindowNumber: 5,
                eventNumber: 11,
                delta: Vector2.zero,
                gesturePhase: 5,
                momentumPhase: 5);

            tracker.ObserveNative(2, crossWindowEnd);

            Assert.That(tracker.IsInvalid, Is.True);
            Assert.That(tracker.FailureReason, Is.EqualTo("bound-gesture-window-changed"));
            Assert.That(tracker.AcceptsLifecycle(crossWindowEnd), Is.False);
        }

        [Test]
        public void ZeroDeltaEndFromBoundKeyWindowMayReachLifecycle()
        {
            var tracker = new ScrollPhaseAssociationTracker();
            tracker.ObserveNative(1, Evidence(1, gestureId: 1, eventNumber: 10));
            tracker.ObserveAction(1, 1d, new Vector2(0f, -1f));
            tracker.ObserveUgui(1, 1, new Vector2(0f, -1f));
            tracker.AdvanceFrame(1);
            var boundEnd = Evidence(2,
                gestureId: 1,
                eventNumber: 11,
                delta: Vector2.zero,
                gesturePhase: 5,
                momentumPhase: 5);

            tracker.ObserveNative(2, boundEnd);

            Assert.That(tracker.IsInvalid, Is.False);
            Assert.That(tracker.AcceptsLifecycle(boundEnd), Is.True);
        }

        [Test]
        public void ZeroDeltaEndIsRejectedWhenBoundWindowIsNotKey()
        {
            var tracker = new ScrollPhaseAssociationTracker();
            tracker.ObserveNative(1, Evidence(1, gestureId: 1, eventNumber: 10));
            tracker.ObserveAction(1, 1d, new Vector2(0f, -1f));
            tracker.ObserveUgui(1, 1, new Vector2(0f, -1f));
            tracker.AdvanceFrame(1);
            var nonKeyEnd = Evidence(2,
                gestureId: 1,
                windowNumber: 4,
                keyWindowNumber: 5,
                eventNumber: 11,
                delta: Vector2.zero,
                gesturePhase: 5,
                momentumPhase: 5);

            tracker.ObserveNative(2, nonKeyEnd);

            Assert.That(tracker.IsInvalid, Is.True);
            Assert.That(tracker.FailureReason, Is.EqualTo("bound-gesture-window-not-key"));
            Assert.That(tracker.AcceptsLifecycle(nonKeyEnd), Is.False);
        }

        [Test]
        public void MultipleActionsFailClosed()
        {
            var tracker = new ScrollPhaseAssociationTracker();
            tracker.ObserveAction(1, 1d, new Vector2(0f, -1f));

            tracker.ObserveAction(1, 1.1d, new Vector2(0f, -1f));

            Assert.That(tracker.IsInvalid, Is.True);
            Assert.That(tracker.FailureReason, Is.EqualTo("multiple-input-actions"));
        }

        [Test]
        public void MultipleUguiEventsFailClosed()
        {
            var tracker = new ScrollPhaseAssociationTracker();
            tracker.ObserveUgui(1, 10, new Vector2(0f, -1f));

            tracker.ObserveUgui(1, 11, new Vector2(0f, -1f));

            Assert.That(tracker.IsInvalid, Is.True);
            Assert.That(tracker.FailureReason, Is.EqualTo("multiple-ugui-events"));
        }

        [Test]
        public void MissingEvidenceFailsWhenItsFrameWindowCloses()
        {
            var tracker = new ScrollPhaseAssociationTracker();
            tracker.ObserveAction(5, 1d, new Vector2(0f, -1f));

            tracker.AdvanceFrame(7);

            Assert.That(tracker.IsInvalid, Is.True);
            Assert.That(tracker.FailureReason, Is.EqualTo("incomplete-association-evidence"));
        }

        [Test]
        public void EvidenceDirectionMismatchFailsClosed()
        {
            var tracker = new ScrollPhaseAssociationTracker();
            tracker.ObserveNative(1, Evidence(1, delta: new Vector2(0f, -1f)));
            tracker.ObserveAction(1, 1d, new Vector2(0f, 1f));
            tracker.ObserveUgui(1, 1, new Vector2(0f, 1f));

            tracker.AdvanceFrame(1);

            Assert.That(tracker.IsInvalid, Is.True);
            Assert.That(tracker.FailureReason, Is.EqualTo("evidence-direction-mismatch"));
        }

        [Test]
        public void NativeEvidenceCapacityIsBounded()
        {
            var tracker = new ScrollPhaseAssociationTracker();
            for (var index = 0; index < 9; ++index)
            {
                tracker.ObserveNative(1, Evidence(index + 1, eventNumber: index + 1));
            }

            Assert.That(tracker.IsInvalid, Is.True);
            Assert.That(tracker.FailureReason, Is.EqualTo("native-evidence-capacity-exhausted"));
        }

        private static NativeScrollPhaseEvidence Evidence(long sequence,
            long gestureId = 1,
            long windowNumber = 4,
            long keyWindowNumber = 4,
            long eventNumber = 1,
            Vector2? delta = null,
            double timestamp = 1d,
            int gesturePhase = 2,
            int momentumPhase = 1,
            bool queueOverflowed = false)
        {
            return new NativeScrollPhaseEvidence(sequence,
                gestureId,
                timestamp,
                windowNumber,
                keyWindowNumber,
                eventNumber,
                delta ?? new Vector2(0f, -1f),
                gesturePhase,
                momentumPhase,
                queueOverflowed);
        }

        private static void AssertAssociation(ScrollPhaseAssociationTracker tracker,
            long gestureId,
            int pointerEventIdentity)
        {
            Assert.That(tracker.IsInvalid, Is.False);
            Assert.That(tracker.Association.HasValue, Is.True);
            Assert.That(tracker.Association!.Value.GestureId, Is.EqualTo(gestureId));
            Assert.That(tracker.Association!.Value.PointerEventIdentity, Is.EqualTo(pointerEventIdentity));
        }
    }

    public class ScrollPhaseLifecycleTrackerTests
    {
        [Test]
        public void EndWithoutMomentumRemainsPendingAndDelayedMomentumReturnsOnce()
        {
            var tracker = new ScrollPhaseLifecycleTracker();
            tracker.Bind(11);

            Assert.That(tracker.Observe(11, gesturePhase: 5, momentumPhase: 1),
                Is.EqualTo(ScrollPhaseLifecycleDecision.PendingEnd));
            Assert.That(tracker.IsPendingEnd, Is.True);
            Assert.That(tracker.HasReturned, Is.False);
            Assert.That(tracker.Observe(11, gesturePhase: 1, momentumPhase: 1),
                Is.EqualTo(ScrollPhaseLifecycleDecision.None));
            Assert.That(tracker.IsPendingEnd, Is.True);
            Assert.That(tracker.Observe(11, gesturePhase: 1, momentumPhase: 2),
                Is.EqualTo(ScrollPhaseLifecycleDecision.WaitForMomentum));
            Assert.That(tracker.IsPendingEnd, Is.False);
            Assert.That(tracker.Observe(11, gesturePhase: 1, momentumPhase: 5),
                Is.EqualTo(ScrollPhaseLifecycleDecision.ReturnAfterMomentum));
            Assert.That(tracker.HasReturned, Is.True);
            Assert.That(tracker.Observe(11, gesturePhase: 1, momentumPhase: 5),
                Is.EqualTo(ScrollPhaseLifecycleDecision.None));
        }

        [Test]
        public void SameEventEndAndMomentumBeganWaits()
        {
            var tracker = new ScrollPhaseLifecycleTracker();
            tracker.Bind(12);

            Assert.That(tracker.Observe(12, gesturePhase: 5, momentumPhase: 2),
                Is.EqualTo(ScrollPhaseLifecycleDecision.WaitForMomentum));
            Assert.That(tracker.IsPendingEnd, Is.False);
        }

        [Test]
        public void GestureCancelReturnsOnlyOnce()
        {
            var tracker = new ScrollPhaseLifecycleTracker();
            tracker.Bind(13);

            Assert.That(tracker.Observe(13, gesturePhase: 6, momentumPhase: 1),
                Is.EqualTo(ScrollPhaseLifecycleDecision.ReturnAfterCancel));
            Assert.That(tracker.HasReturned, Is.True);
            Assert.That(tracker.Observe(13, gesturePhase: 6, momentumPhase: 1),
                Is.EqualTo(ScrollPhaseLifecycleDecision.None));
        }

        [Test]
        public void OtherGestureCannotReturnBoundOwner()
        {
            var tracker = new ScrollPhaseLifecycleTracker();
            tracker.Bind(14);

            Assert.That(tracker.Observe(15, gesturePhase: 1, momentumPhase: 5),
                Is.EqualTo(ScrollPhaseLifecycleDecision.None));
        }
    }
}
