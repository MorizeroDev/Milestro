using UnityEngine;

namespace Milestro.Util
{
    internal static class ScrollDeltaUtil
    {
        private const float Epsilon = 0.01f;

        public static ScrollDeltaConsumption ConsumeOffsetDelta(float currentOffset,
            float delta,
            float maxOffset)
        {
            var safeMaxOffset = FloatUtil.IsFinite(maxOffset) ? Mathf.Max(0f, maxOffset) : 0f;
            var safeCurrentOffset = FloatUtil.IsFinite(currentOffset)
                ? Mathf.Clamp(currentOffset, 0f, safeMaxOffset)
                : 0f;
            if (!FloatUtil.IsFinite(delta) || Mathf.Abs(delta) <= Epsilon)
            {
                return new ScrollDeltaConsumption(safeCurrentOffset, 0f, 0f);
            }

            var nextOffset = Mathf.Clamp(safeCurrentOffset + delta, 0f, safeMaxOffset);
            var consumedDelta = nextOffset - safeCurrentOffset;
            if (Mathf.Abs(consumedDelta) <= Epsilon)
            {
                return new ScrollDeltaConsumption(safeCurrentOffset, 0f, delta);
            }

            var unusedDelta = delta - consumedDelta;
            if (Mathf.Abs(unusedDelta) <= Epsilon)
            {
                unusedDelta = 0f;
            }

            return new ScrollDeltaConsumption(nextOffset, consumedDelta, unusedDelta);
        }
    }
}
