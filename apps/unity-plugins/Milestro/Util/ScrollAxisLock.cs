using Milestro.Configuration;
using UnityEngine;

namespace Milestro.Util
{
    internal sealed class ScrollAxisLock
    {
        private readonly float gestureTimeoutSeconds;
        private readonly float deadzone;
        private readonly float dominanceRatio;
        private readonly float decisionDistance;
        private readonly float initialCorrectionSeconds;

        private ScrollAxis resolvedAxis = ScrollAxis.None;
        private Vector2 candidateDelta;
        private Vector2 correctionDelta;
        private bool axisCommitted;
        private float gestureStartTime = -1f;
        private float lastScrollTime = -1f;

        public ScrollAxisLock() : this(MilestroConfiguration.Configuration.ScrollAxisLock.DefaultGestureTimeoutSeconds,
            MilestroConfiguration.Configuration.ScrollAxisLock.DefaultDeadzone,
            MilestroConfiguration.Configuration.ScrollAxisLock.DefaultDominanceRatio,
            MilestroConfiguration.Configuration.ScrollAxisLock.DefaultDecisionDistance,
            MilestroConfiguration.Configuration.ScrollAxisLock.DefaultInitialCorrectionSeconds)
        {
        }

        public ScrollAxisLock(float gestureTimeoutSeconds, float deadzone, float dominanceRatio)
            : this(gestureTimeoutSeconds,
                deadzone,
                dominanceRatio,
                Mathf.Max(MilestroConfiguration.Configuration.ScrollAxisLock.DefaultDecisionDistance, deadzone),
                MilestroConfiguration.Configuration.ScrollAxisLock.DefaultInitialCorrectionSeconds)
        {
        }

        public ScrollAxisLock(float gestureTimeoutSeconds, float deadzone, float dominanceRatio, float decisionDistance)
            : this(gestureTimeoutSeconds,
                deadzone,
                dominanceRatio,
                decisionDistance,
                MilestroConfiguration.Configuration.ScrollAxisLock.DefaultInitialCorrectionSeconds)
        {
        }

        public ScrollAxisLock(float gestureTimeoutSeconds,
            float deadzone,
            float dominanceRatio,
            float decisionDistance,
            float initialCorrectionSeconds)
        {
            this.gestureTimeoutSeconds = Mathf.Max(0.01f, gestureTimeoutSeconds);
            this.deadzone = Mathf.Max(0f, deadzone);
            this.dominanceRatio = Mathf.Max(1f, dominanceRatio);
            this.decisionDistance = Mathf.Max(this.deadzone, decisionDistance);
            this.initialCorrectionSeconds = Mathf.Max(0f, initialCorrectionSeconds);
        }

        public ScrollAxis Resolve(Vector2 scrollDelta,
            bool forceHorizontal,
            out Vector2 contentOffsetDelta,
            out Vector2 lockedScrollDelta)
        {
            contentOffsetDelta = Vector2.zero;
            lockedScrollDelta = Vector2.zero;
            ResetIfGestureExpired();

            var deltaX = FloatUtil.IsFinite(scrollDelta.x) ? scrollDelta.x : 0f;
            var deltaY = FloatUtil.IsFinite(scrollDelta.y) ? scrollDelta.y : 0f;
            var sanitizedDelta = new Vector2(deltaX, deltaY);
            var hasHorizontalDelta = !Mathf.Approximately(deltaX, 0f);
            var hasVerticalDelta = !Mathf.Approximately(deltaY, 0f);
            if (!hasHorizontalDelta && !hasVerticalDelta)
            {
                return ScrollAxis.None;
            }

            var now = Time.unscaledTime;
            if (lastScrollTime < 0f)
            {
                gestureStartTime = now;
            }

            lastScrollTime = now;
            if (forceHorizontal && hasVerticalDelta)
            {
                ResetGestureState();
                contentOffsetDelta = new Vector2(-deltaY, 0f);
                lockedScrollDelta = new Vector2(0f, deltaY);
                return ScrollAxis.Horizontal;
            }

            if (!axisCommitted)
            {
                UpdatePendingAxis(sanitizedDelta);
            }
            else if (CanCorrectCommittedAxis(now))
            {
                correctionDelta += sanitizedDelta;
                if (IsDecisionReady(correctionDelta))
                {
                    var correctedAxis = ResolveGestureAxis(correctionDelta);
                    if (correctedAxis != ScrollAxis.None)
                    {
                        resolvedAxis = correctedAxis;
                    }
                }
            }

            if (resolvedAxis == ScrollAxis.None)
            {
                lockedScrollDelta = sanitizedDelta;
                return ScrollAxis.None;
            }

            // Direct X already follows Milestro's logical horizontal direction. Vertical wheel
            // input, including Shift-emulated horizontal input above, uses the opposite sign.
            if (resolvedAxis == ScrollAxis.Horizontal)
            {
                contentOffsetDelta = new Vector2(deltaX, 0f);
                lockedScrollDelta = new Vector2(deltaX, 0f);
            }
            else if (resolvedAxis == ScrollAxis.Vertical)
            {
                contentOffsetDelta = new Vector2(0f, -deltaY);
                lockedScrollDelta = new Vector2(0f, deltaY);
            }
            else
            {
                contentOffsetDelta = new Vector2(deltaX, -deltaY);
                lockedScrollDelta = sanitizedDelta;
            }

            return resolvedAxis;
        }

        public void Reset()
        {
            ResetGestureState();
            lastScrollTime = -1f;
        }

        private void ResetIfGestureExpired()
        {
            if (lastScrollTime >= 0f && Time.unscaledTime - lastScrollTime > gestureTimeoutSeconds)
            {
                Reset();
            }
        }

        private ScrollAxis ResolveGestureAxis(Vector2 delta)
        {
            var absX = Mathf.Abs(delta.x);
            var absY = Mathf.Abs(delta.y);
            if (Mathf.Max(absX, absY) < deadzone)
            {
                return ScrollAxis.None;
            }

            if (absX >= absY * dominanceRatio)
            {
                return ScrollAxis.Horizontal;
            }

            if (absY >= absX * dominanceRatio)
            {
                return ScrollAxis.Vertical;
            }

            return ScrollAxis.Free;
        }

        private bool IsDecisionReady(Vector2 delta)
        {
            return Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y)) >= decisionDistance;
        }

        private void UpdatePendingAxis(Vector2 scrollDelta)
        {
            var observedAxis = ResolveGestureAxis(scrollDelta);
            if (observedAxis == ScrollAxis.None)
            {
                return;
            }

            if (resolvedAxis == ScrollAxis.None || observedAxis != resolvedAxis)
            {
                resolvedAxis = observedAxis;
                candidateDelta = scrollDelta;
            }
            else
            {
                candidateDelta += scrollDelta;
            }

            axisCommitted = IsDecisionReady(candidateDelta);
            if (axisCommitted)
            {
                correctionDelta = Vector2.zero;
            }
        }

        private bool CanCorrectCommittedAxis(float now)
        {
            return axisCommitted &&
                   gestureStartTime >= 0f &&
                   initialCorrectionSeconds > 0f &&
                   now - gestureStartTime <= initialCorrectionSeconds;
        }

        private void ResetGestureState()
        {
            resolvedAxis = ScrollAxis.None;
            candidateDelta = Vector2.zero;
            correctionDelta = Vector2.zero;
            axisCommitted = false;
            gestureStartTime = -1f;
        }

    }
}
