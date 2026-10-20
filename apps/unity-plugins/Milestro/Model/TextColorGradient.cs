using System;
using UnityEngine;

namespace Milestro.Model
{
    [Serializable]
    public struct TextColorGradient : IEquatable<TextColorGradient>
    {
        [SerializeField]
        private Color m_topLeft;

        [SerializeField]
        private Color m_topRight;

        [SerializeField]
        private Color m_bottomLeft;

        [SerializeField]
        private Color m_bottomRight;

        public TextColorGradient(Color topLeft, Color topRight, Color bottomLeft, Color bottomRight)
        {
            m_topLeft = topLeft;
            m_topRight = topRight;
            m_bottomLeft = bottomLeft;
            m_bottomRight = bottomRight;
        }

        public Color TopLeft
        {
            get => m_topLeft;
            set => m_topLeft = value;
        }

        public Color TopRight
        {
            get => m_topRight;
            set => m_topRight = value;
        }

        public Color BottomLeft
        {
            get => m_bottomLeft;
            set => m_bottomLeft = value;
        }

        public Color BottomRight
        {
            get => m_bottomRight;
            set => m_bottomRight = value;
        }

        public static TextColorGradient White =>
            new TextColorGradient(Color.white, Color.white, Color.white, Color.white);

        public bool Equals(TextColorGradient other)
        {
            return m_topLeft.Equals(other.m_topLeft)
                   && m_topRight.Equals(other.m_topRight)
                   && m_bottomLeft.Equals(other.m_bottomLeft)
                   && m_bottomRight.Equals(other.m_bottomRight);
        }

        public override bool Equals(object? obj)
        {
            return obj is TextColorGradient other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(m_topLeft, m_topRight, m_bottomLeft, m_bottomRight);
        }

        public static bool operator ==(TextColorGradient left, TextColorGradient right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TextColorGradient left, TextColorGradient right)
        {
            return !left.Equals(right);
        }
    }
}
