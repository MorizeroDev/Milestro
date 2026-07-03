namespace Milestro.Util
{
    internal static class FloatUtil
    {
        internal static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
