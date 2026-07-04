namespace Milestro.Util
{
    internal static class FloatUtil
    {
        internal static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        internal static float ScrollOffsetToPercent(float offset, float maxOffset)
        {
            if (!IsFinite(offset) || !IsFinite(maxOffset) || maxOffset <= 0f)
            {
                return 0f;
            }

            return UnityEngine.Mathf.Clamp01(offset / maxOffset);
        }

        internal static float PercentToScrollOffset(float percent, float maxOffset)
        {
            if (!IsFinite(percent) || !IsFinite(maxOffset) || maxOffset <= 0f)
            {
                return 0f;
            }

            return UnityEngine.Mathf.Clamp01(percent) * maxOffset;
        }
    }
}
