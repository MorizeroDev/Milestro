#nullable enable
using System.Collections.Generic;
using Milestro.RichTextParser;
using Milestro.Skia.TextLayout;

namespace Milestro.Extensions
{
    public static class ParagraphPayloadExtension
    {
        public static ParagraphStyle ToParagraphStyle(this ParagraphStyleState t, ParagraphStyle baseParaStyle)
        {
            var ret = new ParagraphStyle();

            if (t.TextAlign.HasValue)
            {
                ret.TextAlign = (int)t.TextAlign.Value;
            }
            else
            {
                ret.TextAlign = baseParaStyle.TextAlign;
            }

            return ret;
        }

        public static TextStyle ToTextStyle(this TextStyleState t, TextStyle baseTextStyle)
        {
            var ret = new TextStyle();
            ret.SetFontFamilies(baseTextStyle.GetFontFamilies());

            ret.Decoration = (t.Underline ? 0x1 : 0x0) + (t.Strikethrough ? 0x2 : 0x0);

            ret.GetFontStyle(out int weight, out int width, out int slant);
            if (t.Bold)
            {
                weight = 700;
            }

            if (t.Italic)
            {
                slant = 1;
            }

            ret.SetFontStyle(weight, width, slant);

            if (t.Color.HasValue)
            {
                ret.Color = t.Color.Value;
            }
            else
            {
                ret.Color = baseTextStyle.Color;
            }

            if (t.FontSize > 0)
            {
                ret.FontSize = t.FontSize;
            }
            else
            {
                ret.FontSize = baseTextStyle.FontSize;
            }

            ret.Locale = baseTextStyle.Locale;


            var shadowList = baseTextStyle.GetShadows();
            if (shadowList.Count > 0)
            {
                ret.AddShadows(shadowList);
            }

            return ret;
        }

        public static Paragraph ToParagraph(this ParagraphPayload payload, ParagraphStyle baseParaStyle,
            TextStyle baseTextStyle)
        {
            var paragraphStyle = payload.ParagraphStyle.ToParagraphStyle(baseParaStyle);
            paragraphStyle.SetTextStyle(baseTextStyle);

            var builder = new ParagraphBuilder(paragraphStyle);

            foreach (var item in payload.Body)
            {
                builder.PushStyle(item.TextStyle.ToTextStyle(baseTextStyle));
                builder.AddText(item.Content);
                builder.Pop();
            }

            return builder.Build();
        }
    }
}
