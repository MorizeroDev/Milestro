using System;
using System.Collections.Generic;
using Milestro.Extensions;
using Milestro.Skia.TextLayout;
using UnityEngine;
using UnityEngine.Serialization;

namespace Milestro.Experimental.Components
{
    [AddComponentMenu("Milestro/Experimental/Simple Bitmap Text Box")]
    public class SimpleBitmapTextBox : BitmapTextBox
    {
        [TextArea(3, 10)]
        [SerializeField]
        [FormerlySerializedAs("content")]
        private string m_content = "";

        [SerializeField]
        [FormerlySerializedAs("layoutWidth")]
        private int m_layoutWidth = 640;

        [SerializeField]
        [FormerlySerializedAs("autoLayoutWidth")]
        private bool m_autoLayoutWidth = false;

        [SerializeField]
        [FormerlySerializedAs("fontFamilies")]
        private List<string> m_fontFamilies = new List<string>() { "system-ui" };

        [SerializeField]
        [FormerlySerializedAs("size")]
        private float m_size = 36;

        [SerializeField]
        [FormerlySerializedAs("color")]
        private Color m_color = Color.white;

        [SerializeField]
        [FormerlySerializedAs("locale")]
        private string m_locale = "zh-Hans";

        [NonSerialized] private bool m_havePropertiesChanged = true;

        public string content
        {
            get => m_content;
            set
            {
                m_content = value;
                m_havePropertiesChanged = true;
            }
        }

        public int layoutWidth
        {
            get => m_layoutWidth;
            set
            {
                m_layoutWidth = value;
                m_havePropertiesChanged = true;
            }
        }

        public bool autoLayoutWidth
        {
            get => m_autoLayoutWidth;
            set
            {
                m_autoLayoutWidth = value;
                m_havePropertiesChanged = true;
            }
        }

        public List<string> fontFamilies
        {
            get => m_fontFamilies;
            set
            {
                m_fontFamilies = value;
                m_havePropertiesChanged = true;
            }
        }

        public float size
        {
            get => m_size;
            set
            {
                m_size = value;
                m_havePropertiesChanged = true;
            }
        }

        public Color color
        {
            get => m_color;
            set
            {
                m_color = value;
                m_havePropertiesChanged = true;
            }
        }

        public string locale
        {
            get => m_locale;
            set
            {
                m_locale = value;
                m_havePropertiesChanged = true;
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            m_havePropertiesChanged = true;
        }
#endif

        private void UpdateParagraph()
        {
            if (!m_havePropertiesChanged && Paragraph != null)
            {
                return;
            }

            ParagraphStyle paragraphStyle = new ParagraphStyle();

            TextStyle textStyle = new TextStyle();
            textStyle.SetFontFamilies(fontFamilies);
            textStyle.FontSize = size;
            textStyle.Locale = locale;
            textStyle.Color = color;

            var parser = new RichTextParser.RichTextParser();
            parser.ParseText(content ?? "");
            var segments = parser.ConvertToSegments();

            Paragraph = segments.ToParagraph(paragraphStyle, textStyle);
            Paragraph.Layout(autoLayoutWidth ? rect.rect.width : layoutWidth);

            RenderParagraph();
            m_havePropertiesChanged = false;
        }

        private void Update()
        {
            UpdateParagraph();
        }

        protected override void OnRectTransformDimensionsChangeInternal()
        {
            if (!Inited) return;

            m_havePropertiesChanged = true;
            UpdateParagraph();
        }
    }
}
