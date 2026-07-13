using System;
using UnityEngine;

namespace Milestro.InputSystem.Service
{
    internal sealed class ScrollPhasePollBudget
    {
        private readonly int maximumPollsPerFrame;
        private int frame = -1;
        private int polls;

        internal ScrollPhasePollBudget(int maximumPollsPerFrame)
        {
            if (maximumPollsPerFrame <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumPollsPerFrame));
            }
            this.maximumPollsPerFrame = maximumPollsPerFrame;
        }

        internal int PollsThisFrame => polls;

        internal void Reset()
        {
            frame = -1;
            polls = 0;
        }

        internal bool TryConsume(int currentFrame)
        {
            if (frame != currentFrame)
            {
                frame = currentFrame;
                polls = 0;
            }
            if (polls >= maximumPollsPerFrame)
            {
                return false;
            }

            ++polls;
            return true;
        }
    }

    internal readonly struct NativeScrollPhaseEvidence
    {
        internal NativeScrollPhaseEvidence(long sequence,
            long gestureId,
            double timestamp,
            long windowNumber,
            long keyWindowNumber,
            long eventNumber,
            Vector2 scrollingDelta,
            int gesturePhase,
            int momentumPhase,
            bool queueOverflowed)
        {
            Sequence = sequence;
            GestureId = gestureId;
            Timestamp = timestamp;
            WindowNumber = windowNumber;
            KeyWindowNumber = keyWindowNumber;
            EventNumber = eventNumber;
            ScrollingDelta = scrollingDelta;
            GesturePhase = gesturePhase;
            MomentumPhase = momentumPhase;
            QueueOverflowed = queueOverflowed;
        }

        internal long Sequence { get; }
        internal long GestureId { get; }
        internal double Timestamp { get; }
        internal long WindowNumber { get; }
        internal long KeyWindowNumber { get; }
        internal long EventNumber { get; }
        internal Vector2 ScrollingDelta { get; }
        internal int GesturePhase { get; }
        internal int MomentumPhase { get; }
        internal bool QueueOverflowed { get; }
        internal bool HasDelta => !Mathf.Approximately(ScrollingDelta.x, 0f) ||
                                  !Mathf.Approximately(ScrollingDelta.y, 0f);
    }

    internal readonly struct ScrollPhaseAssociation
    {
        internal ScrollPhaseAssociation(long gestureId,
            long windowNumber,
            int pointerEventIdentity,
            double observedTimestampOffset)
        {
            GestureId = gestureId;
            WindowNumber = windowNumber;
            PointerEventIdentity = pointerEventIdentity;
            ObservedTimestampOffset = observedTimestampOffset;
        }

        internal long GestureId { get; }
        internal long WindowNumber { get; }
        internal int PointerEventIdentity { get; }

        // Evidence only. The PoC must prove both clocks before using this value for matching.
        internal double ObservedTimestampOffset { get; }
    }

    internal sealed class ScrollPhaseAssociationTracker
    {
        private const int MaximumEvidenceCount = 8;
        private const int MaximumFrameSeparation = 1;

        private readonly NativeCandidate[] nativeCandidates = new NativeCandidate[MaximumEvidenceCount];
        private readonly ActionCandidate[] actionCandidates = new ActionCandidate[MaximumEvidenceCount];
        private readonly UguiCandidate[] uguiCandidates = new UguiCandidate[MaximumEvidenceCount];
        private readonly long[] eventWindows = new long[MaximumEvidenceCount];
        private readonly long[] eventNumbers = new long[MaximumEvidenceCount];

        private int nativeCount;
        private int actionCount;
        private int uguiCount;
        private int eventWindowCount;
        private long lastSequence;
        private bool awaitingCleanBegan;

        internal bool IsInvalid { get; private set; }
        internal string FailureReason { get; private set; } = string.Empty;
        internal ScrollPhaseAssociation? Association { get; private set; }

        internal void Reset()
        {
            nativeCount = 0;
            actionCount = 0;
            uguiCount = 0;
            eventWindowCount = 0;
            lastSequence = 0;
            awaitingCleanBegan = false;
            IsInvalid = false;
            FailureReason = string.Empty;
            Association = null;
        }

        internal void ObserveNative(int frame, NativeScrollPhaseEvidence evidence)
        {
            if (lastSequence != 0 && evidence.Sequence != lastSequence + 1)
            {
                Invalidate("native-sequence-gap", awaitBegan: true);
            }
            lastSequence = evidence.Sequence;

            if (evidence.QueueOverflowed)
            {
                Invalidate("native-queue-overflow", awaitBegan: true);
                return;
            }

            var cleanBegan = evidence.GesturePhase == 2 && evidence.GestureId != 0;
            if (awaitingCleanBegan)
            {
                if (!cleanBegan)
                {
                    return;
                }
                RecoverAtBegan();
            }

            if (!ObserveEventNumber(evidence.WindowNumber, evidence.EventNumber))
            {
                Invalidate("native-event-number-not-monotonic", awaitBegan: true);
                return;
            }

            if (Association.HasValue)
            {
                var association = Association.Value;
                if (evidence.GestureId != association.GestureId)
                {
                    return;
                }
                if (evidence.WindowNumber == 0 || evidence.WindowNumber != evidence.KeyWindowNumber)
                {
                    Invalidate("bound-gesture-window-not-key", awaitBegan: true);
                    return;
                }
                if (evidence.WindowNumber != association.WindowNumber)
                {
                    Invalidate("bound-gesture-window-changed", awaitBegan: true);
                }
                return;
            }
            if (!evidence.HasDelta || evidence.GestureId == 0)
            {
                return;
            }
            if (evidence.WindowNumber == 0 || evidence.WindowNumber != evidence.KeyWindowNumber)
            {
                Invalidate("native-window-not-key", awaitBegan: true);
                return;
            }
            if (nativeCount == nativeCandidates.Length)
            {
                Invalidate("native-evidence-capacity-exhausted", awaitBegan: true);
                return;
            }

            nativeCandidates[nativeCount++] = new NativeCandidate(frame, evidence);
        }

        internal bool AcceptsLifecycle(NativeScrollPhaseEvidence evidence)
        {
            if (IsInvalid || !Association.HasValue)
            {
                return false;
            }

            var association = Association.Value;
            return evidence.GestureId == association.GestureId &&
                   evidence.WindowNumber == association.WindowNumber &&
                   evidence.WindowNumber == evidence.KeyWindowNumber;
        }

        internal void ObserveAction(int frame, double timestamp, Vector2 delta)
        {
            if (IsInvalid || Association.HasValue)
            {
                return;
            }
            if (actionCount != 0)
            {
                Invalidate("multiple-input-actions", awaitBegan: true);
                return;
            }
            actionCandidates[actionCount++] = new ActionCandidate(frame, timestamp, delta);
        }

        internal void ObserveUgui(int frame, int pointerEventIdentity, Vector2 delta)
        {
            if (IsInvalid || Association.HasValue)
            {
                return;
            }
            if (uguiCount != 0)
            {
                Invalidate("multiple-ugui-events", awaitBegan: true);
                return;
            }
            uguiCandidates[uguiCount++] = new UguiCandidate(frame, pointerEventIdentity, delta);
        }

        internal void AdvanceFrame(int frame)
        {
            if (IsInvalid || Association.HasValue || !HasEvidence())
            {
                return;
            }

            if (nativeCount != 0 && actionCount == 1 && uguiCount == 1)
            {
                SealEvidence();
                return;
            }

            var latestEvidenceFrame = LatestEvidenceFrame();
            if (frame > latestEvidenceFrame + MaximumFrameSeparation)
            {
                SealEvidence();
            }
        }

        internal void Complete()
        {
            if (!IsInvalid && !Association.HasValue && HasEvidence())
            {
                SealEvidence();
            }
        }

        internal void Fail(string reason)
        {
            Invalidate(reason, awaitBegan: true);
        }

        private void SealEvidence()
        {
            if (actionCount != 1 || uguiCount != 1 || nativeCount == 0)
            {
                Invalidate("incomplete-association-evidence", awaitBegan: true);
                return;
            }

            var action = actionCandidates[0];
            var ugui = uguiCandidates[0];
            if (action.Frame != ugui.Frame)
            {
                Invalidate("action-ugui-frame-mismatch", awaitBegan: true);
                return;
            }

            var gestureId = nativeCandidates[0].Evidence.GestureId;
            var windowNumber = nativeCandidates[0].Evidence.WindowNumber;
            var nativeTimestamp = nativeCandidates[0].Evidence.Timestamp;
            var nativeDelta = Vector2.zero;
            for (var index = 0; index < nativeCount; ++index)
            {
                var candidate = nativeCandidates[index];
                if (Math.Abs(candidate.Frame - action.Frame) > MaximumFrameSeparation)
                {
                    Invalidate("native-action-frame-mismatch", awaitBegan: true);
                    return;
                }
                if (candidate.Evidence.GestureId != gestureId || candidate.Evidence.WindowNumber != windowNumber)
                {
                    Invalidate("multiple-native-candidates", awaitBegan: true);
                    return;
                }

                nativeTimestamp = candidate.Evidence.Timestamp;
                nativeDelta += candidate.Evidence.ScrollingDelta;
            }

            if (!SameDirection(nativeDelta, action.Delta) || !SameDirection(action.Delta, ugui.Delta))
            {
                Invalidate("evidence-direction-mismatch", awaitBegan: true);
                return;
            }

            Association = new ScrollPhaseAssociation(gestureId,
                windowNumber,
                ugui.PointerEventIdentity,
                action.Timestamp - nativeTimestamp);
            ClearPendingEvidence();
        }

        private bool ObserveEventNumber(long windowNumber, long eventNumber)
        {
            if (windowNumber == 0 || eventNumber <= 0)
            {
                return true;
            }

            for (var index = 0; index < eventWindowCount; ++index)
            {
                if (eventWindows[index] != windowNumber)
                {
                    continue;
                }
                if (eventNumber <= eventNumbers[index])
                {
                    return false;
                }
                eventNumbers[index] = eventNumber;
                return true;
            }

            if (eventWindowCount == eventWindows.Length)
            {
                return false;
            }
            eventWindows[eventWindowCount] = windowNumber;
            eventNumbers[eventWindowCount] = eventNumber;
            ++eventWindowCount;
            return true;
        }

        private void Invalidate(string reason, bool awaitBegan)
        {
            IsInvalid = true;
            FailureReason = reason;
            Association = null;
            awaitingCleanBegan = awaitBegan;
            ClearPendingEvidence();
        }

        private void RecoverAtBegan()
        {
            IsInvalid = false;
            FailureReason = string.Empty;
            awaitingCleanBegan = false;
            ClearPendingEvidence();
        }

        private bool HasEvidence()
        {
            return nativeCount != 0 || actionCount != 0 || uguiCount != 0;
        }

        private int LatestEvidenceFrame()
        {
            var latest = -1;
            for (var index = 0; index < nativeCount; ++index)
            {
                latest = Math.Max(latest, nativeCandidates[index].Frame);
            }
            for (var index = 0; index < actionCount; ++index)
            {
                latest = Math.Max(latest, actionCandidates[index].Frame);
            }
            for (var index = 0; index < uguiCount; ++index)
            {
                latest = Math.Max(latest, uguiCandidates[index].Frame);
            }
            return latest;
        }

        private void ClearPendingEvidence()
        {
            nativeCount = 0;
            actionCount = 0;
            uguiCount = 0;
        }

        private static bool SameDirection(Vector2 first, Vector2 second)
        {
            var firstHasX = !Mathf.Approximately(first.x, 0f);
            var firstHasY = !Mathf.Approximately(first.y, 0f);
            var secondHasX = !Mathf.Approximately(second.x, 0f);
            var secondHasY = !Mathf.Approximately(second.y, 0f);
            return firstHasX == secondHasX && firstHasY == secondHasY &&
                   (!firstHasX || Mathf.Sign(first.x) == Mathf.Sign(second.x)) &&
                   (!firstHasY || Mathf.Sign(first.y) == Mathf.Sign(second.y));
        }

        private readonly struct NativeCandidate
        {
            internal NativeCandidate(int frame, NativeScrollPhaseEvidence evidence)
            {
                Frame = frame;
                Evidence = evidence;
            }

            internal int Frame { get; }
            internal NativeScrollPhaseEvidence Evidence { get; }
        }

        private readonly struct ActionCandidate
        {
            internal ActionCandidate(int frame, double timestamp, Vector2 delta)
            {
                Frame = frame;
                Timestamp = timestamp;
                Delta = delta;
            }

            internal int Frame { get; }
            internal double Timestamp { get; }
            internal Vector2 Delta { get; }
        }

        private readonly struct UguiCandidate
        {
            internal UguiCandidate(int frame, int pointerEventIdentity, Vector2 delta)
            {
                Frame = frame;
                PointerEventIdentity = pointerEventIdentity;
                Delta = delta;
            }

            internal int Frame { get; }
            internal int PointerEventIdentity { get; }
            internal Vector2 Delta { get; }
        }
    }

    internal enum ScrollPhaseLifecycleDecision
    {
        None,
        PendingEnd,
        WaitForMomentum,
        ReturnAfterMomentum,
        ReturnAfterCancel
    }

    internal sealed class ScrollPhaseLifecycleTracker
    {
        private long gestureId;
        private bool pendingEnd;
        private bool returned;

        internal bool IsPendingEnd => pendingEnd;
        internal bool HasReturned => returned;

        internal void Bind(long value)
        {
            gestureId = value;
            pendingEnd = false;
            returned = false;
        }

        internal void Reset()
        {
            gestureId = 0;
            pendingEnd = false;
            returned = false;
        }

        internal ScrollPhaseLifecycleDecision Observe(long sampleGestureId, int gesturePhase, int momentumPhase)
        {
            if (gestureId == 0 || sampleGestureId != gestureId || returned)
            {
                return ScrollPhaseLifecycleDecision.None;
            }
            if (momentumPhase == 5 || momentumPhase == 6)
            {
                pendingEnd = false;
                returned = true;
                return ScrollPhaseLifecycleDecision.ReturnAfterMomentum;
            }
            if (gesturePhase == 6)
            {
                pendingEnd = false;
                returned = true;
                return ScrollPhaseLifecycleDecision.ReturnAfterCancel;
            }
            if (gesturePhase == 5 && momentumPhase == 1)
            {
                pendingEnd = true;
                return ScrollPhaseLifecycleDecision.PendingEnd;
            }
            if (gesturePhase == 5 && momentumPhase == 2)
            {
                pendingEnd = false;
                return ScrollPhaseLifecycleDecision.WaitForMomentum;
            }
            if (momentumPhase == 2 || momentumPhase == 3 || momentumPhase == 4)
            {
                pendingEnd = false;
                return ScrollPhaseLifecycleDecision.WaitForMomentum;
            }
            return ScrollPhaseLifecycleDecision.None;
        }
    }
}
