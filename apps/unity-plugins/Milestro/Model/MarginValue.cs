using System;

namespace Milestro.Model
{
    [Serializable]
    public struct MarginValue
    {
        public float Value;
        public MarginUnit Unit;
        public bool Auto;

        public float Resolve(MarginResolveContext context, MarginAxis axis)
        {
            if (Auto || !IsFinite(Value))
            {
                return 0f;
            }

            switch (Unit)
            {
                case MarginUnit.ContainerPercent:
                    return Value * 0.01f * (axis == MarginAxis.Horizontal
                        ? context.ContainerWidth
                        : context.ContainerHeight);
                case MarginUnit.Vw:
                    return Value * 0.01f * context.ContainerWidth;
                case MarginUnit.Vh:
                    return Value * 0.01f * context.ContainerHeight;
                case MarginUnit.Em:
                    return Value * context.FontSize;
                default:
                    return Value;
            }
        }

        public void Normalize()
        {
            if (!IsFinite(Value))
            {
                Value = 0f;
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
