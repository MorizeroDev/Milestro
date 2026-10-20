using System;
using UnityEngine;

namespace Milestro.Model
{
    [Serializable]
    public sealed class ParagraphStyleSettings
    {
        public event Action? Changed;

        [SerializeField]
        private TextAlign m_textAlign = TextAlign.Start;

        [SerializeField]
        private TextDirection m_textDirection = TextDirection.Ltr;

        [SerializeField]
        private bool m_singleLine;

        [SerializeField]
        private string m_ellipsisString = "…";

        [SerializeField]
        [Min(0f)]
        private float m_paragraphSpacing;

        public TextAlign textAlign
        {
            get => m_textAlign;
            set => SetField(ref m_textAlign, value);
        }

        public TextDirection textDirection
        {
            get => m_textDirection;
            set => SetField(ref m_textDirection, value);
        }

        public bool singleLine
        {
            get => m_singleLine;
            set => SetField(ref m_singleLine, value);
        }

        public string ellipsisString
        {
            get => m_ellipsisString;
            set
            {
                var nextValue = value ?? "";
                if (m_ellipsisString == nextValue)
                {
                    return;
                }

                m_ellipsisString = nextValue;
                NotifyChanged();
            }
        }

        /// <summary>
        /// Extra gap in points inserted between adjacent rich text paragraph blocks,
        /// equivalent to a default stylesheet rule of "p { margin-bottom: value }".
        /// </summary>
        public float paragraphSpacing
        {
            get => m_paragraphSpacing;
            set
            {
                var nextValue = float.IsFinite(value) && value > 0f ? value : 0f;
                SetField(ref m_paragraphSpacing, nextValue);
            }
        }

        private void SetField<T>(ref T field, T value)
        {
            if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            NotifyChanged();
        }

        private void NotifyChanged()
        {
            Changed?.Invoke();
        }
    }
}
