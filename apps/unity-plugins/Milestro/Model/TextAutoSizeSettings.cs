using System;
using UnityEngine;

namespace Milestro.Model
{
    /// <summary>
    /// Serialized auto-size configuration shared by text producers. Rendered as a single
    /// foldout in the inspector instead of loose flat fields. <see cref="wrapMode"/> only
    /// applies to TextBox layout; SlimText producers ignore it.
    /// </summary>
    [Serializable]
    public struct TextAutoSizeSettings
    {
        public const float DefaultFontSizeMin = 12f;
        public const float DefaultFontSizeMax = 72f;

        private const float MinFontSizeBound = 0.1f;

        [SerializeField]
        private bool m_enabled;

        [SerializeField]
        private float m_fontSizeMin;

        [SerializeField]
        private float m_fontSizeMax;

        [SerializeField]
        private TextAutoSizeWrapMode m_wrapMode;

        public static TextAutoSizeSettings Default => new TextAutoSizeSettings
        {
            m_fontSizeMin = DefaultFontSizeMin,
            m_fontSizeMax = DefaultFontSizeMax,
        };

        public bool enabled
        {
            get => m_enabled;
            set => m_enabled = value;
        }

        public float fontSizeMin
        {
            get => m_fontSizeMin;
            set => m_fontSizeMin = value;
        }

        public float fontSizeMax
        {
            get => m_fontSizeMax;
            set => m_fontSizeMax = value;
        }

        public TextAutoSizeWrapMode wrapMode
        {
            get => m_wrapMode;
            set => m_wrapMode = value;
        }

        /// <summary>
        /// Clamps both bounds to a sane finite range and orders them so
        /// <see cref="fontSizeMax"/> is never smaller than <see cref="fontSizeMin"/>.
        /// </summary>
        public void Normalize()
        {
            m_fontSizeMin = NormalizeBound(m_fontSizeMin);
            m_fontSizeMax = NormalizeBound(m_fontSizeMax);
            if (m_fontSizeMax < m_fontSizeMin)
            {
                m_fontSizeMax = m_fontSizeMin;
            }
        }

        public static float NormalizeBound(float value)
        {
            return IsFinite(value) ? Mathf.Max(MinFontSizeBound, value) : MinFontSizeBound;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
