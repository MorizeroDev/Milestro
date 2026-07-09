using Milestro.Util;
using UnityEngine;

namespace Milestro.Components.Internal
{
    internal static class TextBoxFlowVisibleRange
    {
        private const float EdgeEpsilon = 0.001f;
        private const float MinimumVisibleHeight = 1f;

        public static bool TryNormalize(float localStartY, float localEndY, out float normalizedStartY, out float normalizedEndY)
        {
            return TryNormalize(localStartY, localEndY, 0f, out normalizedStartY, out normalizedEndY);
        }

        public static bool TryNormalize(float localStartY,
            float localEndY,
            float maxEndY,
            out float normalizedStartY,
            out float normalizedEndY)
        {
            normalizedStartY = Sanitize(localStartY);
            normalizedEndY = Sanitize(localEndY);
            if (normalizedEndY < normalizedStartY)
            {
                normalizedEndY = normalizedStartY;
            }

            var hasMaxEnd = FloatUtil.IsFinite(maxEndY) && maxEndY > 0f;
            if (hasMaxEnd)
            {
                normalizedStartY = Mathf.Min(normalizedStartY, maxEndY);
                normalizedEndY = Mathf.Min(normalizedEndY, maxEndY);
            }

            if (normalizedEndY - normalizedStartY < MinimumVisibleHeight - EdgeEpsilon)
            {
                normalizedStartY = 0f;
                normalizedEndY = 0f;
                return false;
            }

            normalizedStartY = Mathf.Floor(normalizedStartY + EdgeEpsilon);
            normalizedEndY = Mathf.Ceil(normalizedEndY - EdgeEpsilon);
            if (hasMaxEnd)
            {
                normalizedStartY = Mathf.Min(normalizedStartY, maxEndY);
                normalizedEndY = Mathf.Min(normalizedEndY, maxEndY);
            }

            if (normalizedEndY - normalizedStartY < MinimumVisibleHeight)
            {
                normalizedStartY = 0f;
                normalizedEndY = 0f;
                return false;
            }

            return true;
        }

        public static int ResolveCapacityHeight(float requestedCapacityHeight,
            float visibleHeight,
            float maxHeight)
        {
            var minimumHeight = FloatUtil.IsFinite(visibleHeight)
                ? Mathf.Max(MinimumVisibleHeight, visibleHeight)
                : MinimumVisibleHeight;
            var capacityHeight = FloatUtil.IsFinite(requestedCapacityHeight) && requestedCapacityHeight > 0f
                ? requestedCapacityHeight
                : minimumHeight;
            if (FloatUtil.IsFinite(maxHeight) && maxHeight > 0f)
            {
                capacityHeight = Mathf.Min(capacityHeight, maxHeight);
            }

            capacityHeight = Mathf.Max(minimumHeight, capacityHeight);
            if (capacityHeight >= int.MaxValue)
            {
                return int.MaxValue;
            }

            return Mathf.Max(1, Mathf.CeilToInt(capacityHeight - EdgeEpsilon));
        }

        private static float Sanitize(float value)
        {
            if (!FloatUtil.IsFinite(value) || value < 0f)
            {
                return 0f;
            }

            return value;
        }
    }
}
