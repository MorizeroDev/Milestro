using Milestro.Model;
using UnityEngine;

namespace Milestro.Util
{
    internal sealed class ScrollElasticAxis
    {
        internal const float VisualEpsilon = 0.1f;

        private float returnStart;
        private float returnElapsed;
        private float returnRate;
        private float returnMaxElapsed;
        private bool returnActive;

        internal float Offset { get; private set; }
        internal bool IsActive => Mathf.Abs(Offset) > VisualEpsilon;
        internal bool IsReturning => returnActive;

        internal bool Apply(float currentLogicalOffset,
            float maxLogicalOffset,
            float delta,
            ScrollElasticSettings settings,
            out float nextLogicalOffset)
        {
            var max = NormalizeNonNegative(maxLogicalOffset);
            nextLogicalOffset = Mathf.Clamp(Normalize(currentLogicalOffset), 0f, max);
            if (!settings.Enabled || max <= VisualEpsilon || settings.MaxOverscroll <= VisualEpsilon ||
                Mathf.Abs(delta) <= VisualEpsilon || !FloatUtil.IsFinite(delta))
            {
                return false;
            }

            returnActive = false;
            returnElapsed = 0f;
            var remaining = delta;

            if (IsActive && Mathf.Sign(remaining) != Mathf.Sign(Offset))
            {
                var consumed = Mathf.Min(Mathf.Abs(remaining), Mathf.Abs(Offset));
                Offset += Mathf.Sign(remaining) * consumed;
                remaining -= Mathf.Sign(remaining) * consumed;
                if (Mathf.Abs(Offset) <= VisualEpsilon)
                {
                    Offset = 0f;
                }
            }

            if (Mathf.Abs(remaining) <= VisualEpsilon)
            {
                return true;
            }

            var previousLogicalOffset = nextLogicalOffset;
            nextLogicalOffset = Mathf.Clamp(nextLogicalOffset + remaining, 0f, max);
            remaining -= nextLogicalOffset - previousLogicalOffset;
            if (Mathf.Abs(remaining) <= VisualEpsilon)
            {
                return true;
            }

            var capacity = Mathf.Max(0f, settings.MaxOverscroll - Mathf.Abs(Offset));
            if (capacity > VisualEpsilon && settings.Resistance > 0f)
            {
                var capacityRatio = Mathf.Clamp01(capacity / settings.MaxOverscroll);
                var resistedDelta = remaining * settings.Resistance * capacityRatio;
                Offset = Mathf.Clamp(Offset + resistedDelta, -settings.MaxOverscroll, settings.MaxOverscroll);
                if (Mathf.Abs(Offset) <= VisualEpsilon)
                {
                    Offset = 0f;
                }
            }

            return IsActive || !Mathf.Approximately(previousLogicalOffset, nextLogicalOffset);
        }

        internal void BeginReturn(ScrollElasticSettings settings)
        {
            if (!IsActive || settings.ReturnDurationSeconds <= 0f ||
                settings.MaxOverscroll <= VisualEpsilon)
            {
                Settle();
                return;
            }

            returnStart = Offset;
            returnElapsed = 0f;
            returnRate = Mathf.Log(settings.MaxOverscroll / VisualEpsilon) / settings.ReturnDurationSeconds;
            returnMaxElapsed = settings.ReturnDurationSeconds * 4f;
            if (!FloatUtil.IsFinite(returnRate) || returnRate <= 0f ||
                !FloatUtil.IsFinite(returnMaxElapsed) || returnMaxElapsed <= 0f)
            {
                Settle();
                return;
            }
            returnActive = true;
        }

        internal bool TickReturn(float deltaTime, ScrollElasticSettings settings)
        {
            if (!returnActive)
            {
                return false;
            }

            if (!settings.Enabled || !FloatUtil.IsFinite(deltaTime) ||
                settings.ReturnDurationSeconds <= 0f || settings.MaxOverscroll <= VisualEpsilon)
            {
                return Settle();
            }

            var previous = Offset;
            returnElapsed += Mathf.Max(0f, deltaTime);
            if (!FloatUtil.IsFinite(returnElapsed) || returnElapsed >= returnMaxElapsed)
            {
                return Settle();
            }

            Offset = EvaluateReturnOffset(returnStart, returnRate, returnElapsed);
            if (!FloatUtil.IsFinite(Offset))
            {
                return Settle();
            }
            if (Mathf.Abs(Offset) <= VisualEpsilon)
            {
                Offset = 0f;
                returnActive = false;
            }

            return !Mathf.Approximately(previous, Offset);
        }

        internal static float EvaluateReturnOffset(float start, float rate, float elapsed)
        {
            return start * Mathf.Exp(-rate * elapsed);
        }

        internal bool Settle()
        {
            var changed = !Mathf.Approximately(Offset, 0f);
            Offset = 0f;
            returnStart = 0f;
            returnElapsed = 0f;
            returnRate = 0f;
            returnMaxElapsed = 0f;
            returnActive = false;
            return changed;
        }

        internal bool SettleIfUnavailable(float maxLogicalOffset, ScrollElasticReleasePolicy releasePolicy)
        {
            if (NormalizeNonNegative(maxLogicalOffset) > VisualEpsilon)
            {
                return false;
            }

            releasePolicy.Cancel();
            return Settle();
        }

        private static float Normalize(float value)
        {
            return FloatUtil.IsFinite(value) ? value : 0f;
        }

        private static float NormalizeNonNegative(float value)
        {
            return Mathf.Max(0f, Normalize(value));
        }
    }
}
