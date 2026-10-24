using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Milestro.Model
{
    [Serializable]
    public sealed class TextStyleSettings
    {
        public event Action? Changed;

        [SerializeField]
        private List<string> m_fontFamilies = new List<string>() { "system-ui" };

        [NonSerialized]
        private FontFamilyList? m_fontFamiliesView;

        [SerializeField]
        private float m_size = 36;

        [SerializeField]
        [Range(0, 1000)]
        private int m_weight = FontWeight.Normal;

        [SerializeField]
        private FontWidth m_width = FontWidth.Normal;

        [SerializeField]
        private FontSlant m_slant = FontSlant.Upright;

        [SerializeField]
        private Color m_textColor = Color.white;

        [SerializeField]
        private float m_letterSpacing;

        [SerializeField]
        private float m_wordSpacing;

        [SerializeField]
        private float m_lineHeight;

        [SerializeField]
        private string m_locale = "zh-Hans";

        public IList<string> fontFamilies
        {
            get => m_fontFamiliesView ??= new FontFamilyList(this, FontFamilies);
            set
            {
                m_fontFamilies = value == null ? new List<string>() : new List<string>(value);
                m_fontFamiliesView = null;
                NotifyChanged();
            }
        }

        public float size
        {
            get => m_size;
            set => SetField(ref m_size, value);
        }

        public int weight
        {
            get => m_weight;
            set => SetField(ref m_weight, value);
        }

        public FontWidth width
        {
            get => m_width;
            set => SetField(ref m_width, value);
        }

        public FontSlant slant
        {
            get => m_slant;
            set => SetField(ref m_slant, value);
        }

        public Color textColor
        {
            get => m_textColor;
            set => SetField(ref m_textColor, value);
        }

        public float letterSpacing
        {
            get => m_letterSpacing;
            set => SetField(ref m_letterSpacing, value);
        }

        public float wordSpacing
        {
            get => m_wordSpacing;
            set => SetField(ref m_wordSpacing, value);
        }

        public float lineHeight
        {
            get => m_lineHeight;
            set => SetField(ref m_lineHeight, value);
        }

        public string locale
        {
            get => m_locale;
            set
            {
                var nextValue = value ?? "";
                if (m_locale == nextValue)
                {
                    return;
                }

                m_locale = nextValue;
                NotifyChanged();
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

        private List<string> FontFamilies => m_fontFamilies ??= new List<string>();

        private sealed class FontFamilyList : Collection<string>
        {
            private readonly TextStyleSettings m_owner;

            internal FontFamilyList(TextStyleSettings owner, IList<string> families)
                : base(families)
            {
                m_owner = owner;
            }

            protected override void InsertItem(int index, string item)
            {
                base.InsertItem(index, item);
                NotifyOwner();
            }

            protected override void SetItem(int index, string item)
            {
                if (this[index] == item)
                {
                    return;
                }

                base.SetItem(index, item);
                NotifyOwner();
            }

            protected override void RemoveItem(int index)
            {
                base.RemoveItem(index);
                NotifyOwner();
            }

            protected override void ClearItems()
            {
                if (Count == 0)
                {
                    return;
                }

                base.ClearItems();
                NotifyOwner();
            }

            private void NotifyOwner()
            {
                if (ReferenceEquals(m_owner.FontFamilies, Items))
                {
                    m_owner.NotifyChanged();
                }
            }
        }
    }
}
