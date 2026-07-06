using UnityEngine;

namespace Milestro.Util
{
    internal sealed class ScrollTween
    {
        private const float MinimumDurationSeconds = 0.1f;
        private const float MaximumDurationSeconds = 1.0f;
        private const float DurationScaleSeconds = 0.150f;
        private const float DurationReferenceDelta = 100f;
        private static readonly CubicBezierCurve EaseInOutCurve = new CubicBezierCurve(0.42f, 0f, 0.58f, 1f);
        private static readonly CubicBezierCurve EaseOutCurve = new CubicBezierCurve(0f, 0f, 0.58f, 1f);

        private float start;
        private float target;
        private float elapsed;
        private float duration;
        private float lastApplied;
        private CubicBezierCurve curve = EaseOutCurve;

        private bool isActive;

        internal bool IsActive()
        {
            return isActive;
        }

        public bool ScrollTo(float currentValue,
            float targetValue,
            float maxValue,
            out float nextValue,
            bool animated = false)
        {
            nextValue = ClampScroll(targetValue, maxValue);
            if (!animated || NearlyEqual(currentValue, nextValue))
            {
                target = nextValue;
                lastApplied = nextValue;
                Cancel();
                return !NearlyEqual(currentValue, nextValue);
            }

            Start(currentValue, nextValue, EaseInOutCurve, ResolveDurationSeconds(Mathf.Abs(nextValue - currentValue)));
            nextValue = currentValue;
            return false;
        }

        public bool ScrollBy(float currentValue, float delta, float maxValue)
        {
            if (!FloatUtil.IsFinite(delta) || NearlyEqual(delta, 0f))
            {
                return false;
            }

            var previousTarget = isActive ? target : currentValue;
            var nextTarget = ClampScroll(previousTarget + delta, maxValue);
            if (NearlyEqual(previousTarget, nextTarget))
            {
                return false;
            }

            Start(currentValue, nextTarget, EaseOutCurve, ResolveDurationSeconds(Mathf.Abs(nextTarget - previousTarget)));
            return true;
        }

        public void Cancel()
        {
            isActive = false;
            elapsed = 0f;
            duration = 0f;
        }

        public bool CancelIfExternallyMoved(float currentValue)
        {
            if (!isActive || NearlyEqual(currentValue, lastApplied))
            {
                return false;
            }

            Cancel();
            return true;
        }

        public bool Tick(float currentValue, float maxValue, float deltaTime, out float nextValue)
        {
            nextValue = currentValue;
            if (!isActive)
            {
                return false;
            }

            if (CancelIfExternallyMoved(currentValue))
            {
                return false;
            }

            var nextTarget = ClampScroll(target, maxValue);
            if (!NearlyEqual(target, nextTarget))
            {
                Start(currentValue, nextTarget, curve, ResolveDurationSeconds(Mathf.Abs(nextTarget - currentValue)));
            }

            elapsed += Mathf.Max(0f, deltaTime);
            var progress = Mathf.Clamp01(duration > 0f ? elapsed / duration : 1f);
            var easedProgress = curve.Evaluate(progress);
            nextValue = Mathf.LerpUnclamped(start, target, easedProgress);

            if (progress >= 1f || NearlyEqual(nextValue, target))
            {
                nextValue = target;
                lastApplied = nextValue;
                Cancel();
                return !NearlyEqual(currentValue, nextValue);
            }

            lastApplied = nextValue;
            return !NearlyEqual(currentValue, nextValue);
        }

        private void Start(float currentValue, float targetValue, CubicBezierCurve easingCurve, float durationSeconds)
        {
            start = currentValue;
            target = targetValue;
            elapsed = 0f;
            duration = durationSeconds;
            lastApplied = currentValue;
            curve = easingCurve;
            isActive = true;
        }

        private static float ResolveDurationSeconds(float distance)
        {
            if (!FloatUtil.IsFinite(distance))
            {
                return 0f;
            }

            var normalizedDistance = Mathf.Max(0f, distance) / DurationReferenceDelta;
            return Mathf.Clamp(DurationScaleSeconds * Mathf.Sqrt(normalizedDistance),
                MinimumDurationSeconds,
                MaximumDurationSeconds);
        }

        private static float ClampScroll(float value, float maxValue)
        {
            if (!FloatUtil.IsFinite(value))
            {
                return 0f;
            }

            return Mathf.Clamp(value, 0f, FloatUtil.IsFinite(maxValue) ? Mathf.Max(0f, maxValue) : 0f);
        }

        private static bool NearlyEqual(float a, float b)
        {
            return Mathf.Abs(a - b) <= 0.01f;
        }

    }
}
