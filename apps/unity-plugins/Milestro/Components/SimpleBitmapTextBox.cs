using System;
using System.Collections.Generic;
using Milestro.Extensions;
using Milestro.Skia.TextLayout;
using UnityEngine;

namespace Milestro.Components
{
    public class SimpleBitmapTextBox : SkParagraphBitmapTextBox
    {
        [TextArea(3, 10)] [SerializeField] public string content = "";

        [SerializeField] public int layoutWidth = 640;

        [SerializeField] public List<string> fontFamilies = new List<string>() { "Source Han Sans VF" };

        [SerializeField] public float size = 36;

        [SerializeField] public Color color = Color.white;

        [SerializeField] public string locale = "zh-Hans";

        [NonSerialized] private string cachedContent = "";

        private void Update()
        {
            if (cachedContent == content)
            {
                return;
            }

            cachedContent = content;

            ParagraphStyle paragraphStyle = new ParagraphStyle();

            TextStyle textStyle = new TextStyle();
            textStyle.SetFontFamilies(fontFamilies);
            textStyle.FontSize = size;
            textStyle.Locale = locale;
            textStyle.Color = color;

            var parser = new RichTextParser.RichTextParser();
            parser.ParseText(cachedContent);
            var segments = parser.ConvertToSegments();

            Paragraph = segments.ToParagraph(paragraphStyle, textStyle);
            Paragraph.Layout(layoutWidth);


            RenderParagraph();
        }
    }
}
