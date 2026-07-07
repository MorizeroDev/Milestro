using System;
using UnityEngine;

namespace Milestro.Model
{
    [Serializable]
    public sealed class Margin
    {
        [SerializeField]
        private MarginValue m_top;

        [SerializeField]
        private MarginValue m_right;

        [SerializeField]
        private MarginValue m_bottom;

        [SerializeField]
        private MarginValue m_left;

        public ref MarginValue Top => ref m_top;
        public ref MarginValue Right => ref m_right;
        public ref MarginValue Bottom => ref m_bottom;
        public ref MarginValue Left => ref m_left;

        public ResolvedMargin Resolve(MarginResolveContext context)
        {
            return new ResolvedMargin(m_left.Resolve(context, MarginAxis.Horizontal),
                m_top.Resolve(context, MarginAxis.Vertical),
                m_right.Resolve(context, MarginAxis.Horizontal),
                m_bottom.Resolve(context, MarginAxis.Vertical));
        }

        public void Normalize()
        {
            m_top.Normalize();
            m_right.Normalize();
            m_bottom.Normalize();
            m_left.Normalize();
        }

        public static Margin FixedZero()
        {
            return new Margin();
        }
    }
}
