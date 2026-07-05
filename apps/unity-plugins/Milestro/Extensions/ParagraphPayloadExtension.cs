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
            ret.TextDirection = t.TextDirection ?? baseParaStyle.TextDirection;
            ret.TextAlign = t.TextAlign ?? baseParaStyle.TextAlign;

            if (baseParaStyle.IsUnlimitedLines)
            {
                ret.ClearMaxLines();
            }
            else
            {
                ret.MaxLines = baseParaStyle.MaxLines;
            }

            if (baseParaStyle.IsEllipsized)
            {
                ret.SetEllipsis(baseParaStyle.Ellipsis);
            }

            ret.Height = baseParaStyle.Height;
            ret.TextHeightBehavior = baseParaStyle.TextHeightBehavior;
            ret.ReplaceTabCharacters = baseParaStyle.ReplaceTabCharacters;
            ret.ApplyRoundingHack = baseParaStyle.ApplyRoundingHack;
            if (!baseParaStyle.IsHintingOn)
            {
                ret.TurnHintingOff();
            }

            return ret;
        }

        public static TextStyle ToTextStyle(this TextStyleState t, TextStyle baseTextStyle)
        {
            var ret = new TextStyle();
            ret.DecorationMode = baseTextStyle.DecorationMode;
            ret.DecorationColor = baseTextStyle.DecorationColor;
            ret.DecorationStyle = baseTextStyle.DecorationStyle;
            ret.DecorationThicknessMultiplier = baseTextStyle.DecorationThicknessMultiplier;

            ret.Decoration =
                baseTextStyle.Decoration |
                (t.Underline ? TextDecoration.Underline : TextDecoration.NoDecoration) |
                (t.Strikethrough ? TextDecoration.LineThrough : TextDecoration.NoDecoration);

            baseTextStyle.GetFontStyle(out int weight, out var width, out var slant);
            if (t.FontWeight.HasValue)
            {
                weight = t.FontWeight.Value;
            }

            if (t.FontWidth.HasValue)
            {
                width = t.FontWidth.Value;
            }

            if (t.FontSlant.HasValue)
            {
                slant = t.FontSlant.Value;
            }

            ret.SetFontStyle(weight, width, slant);
            ret.SetFontFamilies(new List<string>(t.FontFamilies ?? baseTextStyle.GetFontFamilies()));

            if (t.Color.HasValue)
            {
                ret.Color = t.Color.Value;
            }
            else
            {
                ret.Color = baseTextStyle.Color;
            }

            if (t.FontSize.HasValue)
            {
                ret.FontSize = t.FontSize.Value;
            }
            else
            {
                ret.FontSize = baseTextStyle.FontSize;
            }

            ret.Locale = t.Locale ?? baseTextStyle.Locale;
            ret.BaselineShift = t.BaselineShift ?? baseTextStyle.BaselineShift;
            ret.Height = baseTextStyle.Height;
            ret.HeightOverride = baseTextStyle.HeightOverride;
            ret.HalfLeading = baseTextStyle.HalfLeading;
            ret.LetterSpacing = t.LetterSpacing ?? baseTextStyle.LetterSpacing;
            ret.WordSpacing = baseTextStyle.WordSpacing;
            ret.TextBaseline = baseTextStyle.TextBaseline;


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
