using System;
using UnityEngine;

namespace Milestro.Model
{
    [Serializable]
    public class TextShadowDescription
    {
        [SerializeField]
        private Color m_color = new Color(0, 0, 0, 1);

        [SerializeField]
        private Vector2 m_offset;

        [SerializeField, Min(0)]
        private double m_blurSigma;

        public Color32 Color => m_color;
        public Vector2 Offset => m_offset;
        public double BlurSigma => m_blurSigma;

        public TextShadowDescription(Color32 color, Vector2 offset, double blurSigma)
        {
            m_color = color;
            m_offset = offset;
            m_blurSigma = blurSigma;
        }
    }
}
