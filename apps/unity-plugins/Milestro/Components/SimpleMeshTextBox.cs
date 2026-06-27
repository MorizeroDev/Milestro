using System.Collections.Generic;
using Milestro.Skia.TextLayout;
using UnityEngine;

namespace Milestro.Components
{
    public class SimpleMeshTextBox : SkParagraphMeshTextBox
    {
        [TextArea(3, 10)] [SerializeField] string content = "";
        
        [SerializeField] List<string> fontFamilies = new List<string>() { "Source Han Sans VF" };
        [SerializeField] float size = 36;
        [SerializeField] string locale = "zh-Hans";

        private void Update()
        {
            TextStyle textStyle = new TextStyle();
            textStyle.SetFontFamilies(fontFamilies);
            textStyle.FontSize = size;
            textStyle.Locale = locale;

            var paragraphStyle = new ParagraphStyle();
            paragraphStyle.SetTextStyle(textStyle);

            var paragraphBuilder = new ParagraphBuilder(paragraphStyle);
            paragraphBuilder.AddText(content);

            var paragraph = paragraphBuilder.Build();
            paragraph.Layout(1080);

            Paragraph = paragraph;
        }
    }
}
