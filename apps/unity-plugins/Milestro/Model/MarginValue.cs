using System;
using UnityEngine;

namespace Milestro.Model
{
    [Serializable]
    public struct MarginValue
    {
        [SerializeField]
        private float m_value;

        [SerializeField]
        private MarginUnit m_unit;

        [SerializeField]
        private bool m_auto;

        public float Value
        {
            get => m_value;
            set => m_value = value;
        }

        public MarginUnit Unit
        {
            get => m_unit;
            set => m_unit = value;
        }

        public bool Auto
        {
            get => m_auto;
            set => m_auto = value;
        }

        public float Resolve(MarginResolveContext context, MarginAxis axis)
        {
            if (m_auto || !IsFinite(m_value))
            {
                return 0f;
            }

            switch (m_unit)
            {
                case MarginUnit.ContainerPercent:
                    return m_value * 0.01f * (axis == MarginAxis.Horizontal
                        ? context.ContainerWidth
                        : context.ContainerHeight);
                case MarginUnit.Vw:
                    return m_value * 0.01f * context.ContainerWidth;
                case MarginUnit.Vh:
                    return m_value * 0.01f * context.ContainerHeight;
                case MarginUnit.Em:
                    return m_value * context.FontSize;
                default:
                    return m_value;
            }
        }

        public void Normalize()
        {
            if (!IsFinite(m_value))
            {
                m_value = 0f;
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
