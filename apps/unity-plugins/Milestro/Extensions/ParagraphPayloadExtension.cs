#nullable enable
using System;
using System.Collections.Generic;
using Milestro.Model;
using Milestro.RichTextParser;
using Milestro.Skia;
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
            ret.TextIndent = t.TextIndent ?? baseParaStyle.TextIndent;

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

        /// <summary>
        /// Resolves a block paragraph style, falling back to the payload default style and
        /// finally to the component-provided base style.
        /// </summary>
        public static ParagraphStyle ToParagraphStyle(this ParagraphStyleState blockStyle,
            ParagraphStyleState payloadDefault,
            ParagraphStyle baseParaStyle)
        {
            var ret = new ParagraphStyle();
            ret.TextDirection = blockStyle.TextDirection ?? payloadDefault.TextDirection ?? baseParaStyle.TextDirection;
            ret.TextAlign = blockStyle.TextAlign ?? payloadDefault.TextAlign ?? baseParaStyle.TextAlign;
            ret.TextIndent = blockStyle.TextIndent ?? payloadDefault.TextIndent ?? baseParaStyle.TextIndent;

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
            if (t.FontFamilyTokens != null)
            {
                ret.SetFontFamilyTokens(t.FontFamilyTokens);
            }
            else if (t.FontFamilies != null)
            {
                ret.SetFontFamilies(new List<string>(t.FontFamilies));
            }
            else
            {
                ret.SetFontFamilyTokens(baseTextStyle.GetFontFamilyTokens());
            }

            if (t.Color.HasValue)
            {
                ret.Color = t.Color.Value;
            }
            else
            {
                ret.Color = baseTextStyle.Color;
            }

            var unscaledFontSize = t.FontSize ?? baseTextStyle.FontSize;
            ret.FontSize = unscaledFontSize * (t.FontSizeScale ?? 1f);

            ret.Locale = t.Locale ?? baseTextStyle.Locale;
            if (t.BaselineShift.HasValue)
            {
                ret.BaselineShift = t.BaselineShift.Value;
            }
            else if (t.BaselineShiftEm.HasValue)
            {
                ret.BaselineShift = baseTextStyle.BaselineShift + t.BaselineShiftEm.Value * unscaledFontSize;
            }
            else
            {
                ret.BaselineShift = baseTextStyle.BaselineShift;
            }

            ret.Height = baseTextStyle.Height;
            ret.HeightOverride = baseTextStyle.HeightOverride;
            ret.HalfLeading = baseTextStyle.HalfLeading;
            ret.LetterSpacing = t.LetterSpacing ?? baseTextStyle.LetterSpacing;
            ret.WordSpacing = baseTextStyle.WordSpacing;
            ret.TextBaseline = baseTextStyle.TextBaseline;

            ret.CopyFontFeaturesFrom(baseTextStyle);
            foreach (var operation in t.FontFeatureOperations)
            {
                switch (operation.Type)
                {
                    case FontFeatureOperationType.ResetAll:
                        ret.ResetFontFeature();
                        break;
                    case FontFeatureOperationType.Set:
                        ret.SetFontFeature(operation.Feature!, operation.Value);
                        break;
                    case FontFeatureOperationType.Remove:
                        ret.RemoveFontFeature(operation.Feature!);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }


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
            using var paragraphStyle = payload.ParagraphStyle.ToParagraphStyle(baseParaStyle);
            paragraphStyle.SetTextStyle(baseTextStyle);

            using var builder = new ParagraphBuilder(paragraphStyle);

            var blocks = payload.Blocks;
            var blockIndex = 0;
            foreach (var block in blocks)
            {
                if (blockIndex > 0)
                {
                    // Legacy single-paragraph consumers get a plain line break between blocks.
                    using var breakStyle = baseTextStyle;
                    builder.PushStyle(breakStyle);
                    builder.AddText("\n");
                    builder.Pop();
                }

                foreach (var item in block.Segments)
                {
                    using var textStyle = item.TextStyle.ToTextStyle(baseTextStyle);
                    builder.PushStyle(textStyle);
                    builder.AddText(item.Content);
                    builder.Pop();
                }

                ++blockIndex;
            }

            foreach (var annotation in payload.RubyAnnotations)
            {
                // Each block separator shifts later ranges by one UTF-16 unit.
                var separatorShift = 0;
                for (var index = 1; index < blocks.Count; ++index)
                {
                    if (blocks[index].StartUtf16 <= annotation.StartUtf16)
                    {
                        separatorShift = index;
                    }
                }

                builder.AddRubyUtf16(annotation.StartUtf16 + separatorShift,
                    annotation.EndUtf16 + separatorShift,
                    annotation.Text,
                    annotation.Style);
            }

            return builder.Build();
        }

        /// <summary>
        /// Builds one native paragraph per payload block. Ruby annotations are attached to
        /// the block that fully contains their range; annotations that straddle a block
        /// boundary cannot be built and are skipped with an error.
        /// </summary>
        public static List<Paragraph> ToParagraphs(this ParagraphPayload payload,
            ParagraphStyle baseParaStyle,
            TextStyle baseTextStyle)
        {
            var blocks = payload.Blocks;
            var ret = new List<Paragraph>(blocks.Count);
            foreach (var block in blocks)
            {
                using var paragraphStyle = block.Style.ToParagraphStyle(payload.ParagraphStyle, baseParaStyle);
                paragraphStyle.SetTextStyle(baseTextStyle);

                using var builder = new ParagraphBuilder(paragraphStyle);
                foreach (var segment in block.Segments)
                {
                    using var textStyle = segment.TextStyle.ToTextStyle(baseTextStyle);
                    builder.PushStyle(textStyle);
                    builder.AddText(segment.Content);
                    builder.Pop();
                }

                foreach (var annotation in payload.RubyAnnotations)
                {
                    if (!block.ContainsRange(annotation.StartUtf16, annotation.EndUtf16))
                    {
                        continue;
                    }

                    builder.AddRubyUtf16(annotation.StartUtf16 - block.StartUtf16,
                        annotation.EndUtf16 - block.StartUtf16,
                        annotation.Text,
                        annotation.Style);
                }

                ret.Add(builder.Build());
            }

            return ret;
        }
    }
}
