#nullable enable
using System.Collections.Generic;
using Milestro.Model;
using Milestro.RichTextParser;
using Milestro.Skia.TextLayout;

namespace Milestro.Extensions
{
    public static class ParagraphPayloadExtension
    {
        public static ParagraphStyle ToParagraphStyle(this ParagraphStyleState t, ParagraphStyle baseParaStyle)
        {
            var ret = new ParagraphStyle();
            ret.TextDirection = baseParaStyle.TextDirection;
            ret.MaxLines = baseParaStyle.MaxLines;
            if (baseParaStyle.IsEllipsized)
            {
                ret.SetEllipsis(baseParaStyle.Ellipsis);
            }

            if (t.TextAlign.HasValue)
            {
                ret.TextAlign = t.TextAlign.Value;
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

            ret.Decoration =
                (t.Underline ? TextDecoration.Underline : TextDecoration.NoDecoration) |
                (t.Strikethrough ? TextDecoration.LineThrough : TextDecoration.NoDecoration);

            baseTextStyle.GetFontStyle(out int weight, out var width, out var slant);
            if (t.FontWeight.HasValue)
            {
                weight = t.FontWeight.Value;
            }

            if (t.Italic)
            {
                slant = FontSlant.Italic;
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
