using Milease.Enums;
using Milease.Utils;
using UnityEngine;

namespace Milestro.Util
{
    internal sealed class ScrollTween
    {
        private float start;
        private float target;
        private float elapsed;
        private float lastApplied;

        public bool IsActive { get; private set; }
        public float Target => target;

        public void Start(float currentValue, float targetValue)
        {
            start = currentValue;
            target = targetValue;
            elapsed = 0f;
            lastApplied = currentValue;
            IsActive = true;
        }

        public void Cancel()
        {
            IsActive = false;
            elapsed = 0f;
        }

        public bool CancelIfExternallyMoved(float currentValue)
        {
            if (!IsActive || NearlyEqual(currentValue, lastApplied))
            {
                return false;
            }

            Cancel();
            return true;
        }

        public bool Tick(float currentValue, float maxValue, float deltaTime, float durationSeconds, out float nextValue)
        {
            nextValue = currentValue;
            if (!IsActive)
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
                target = nextTarget;
                start = currentValue;
                elapsed = 0f;
            }

            if (durationSeconds <= 0f)
            {
                nextValue = target;
                lastApplied = nextValue;
                Cancel();
                return !NearlyEqual(currentValue, nextValue);
            }

            elapsed += Mathf.Max(0f, deltaTime);
            var progress = Mathf.Clamp01(elapsed / durationSeconds);
            var easedProgress = EaseUtility.GetEasedProgress(progress, EaseType.Out, EaseFunction.Cubic);
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

        private static float ClampScroll(float value, float maxValue)
        {
            if (!IsFinite(value))
            {
                return 0f;
            }

            return Mathf.Clamp(value, 0f, IsFinite(maxValue) ? Mathf.Max(0f, maxValue) : 0f);
        }

        private static bool NearlyEqual(float a, float b)
        {
            return Mathf.Abs(a - b) <= 0.01f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
