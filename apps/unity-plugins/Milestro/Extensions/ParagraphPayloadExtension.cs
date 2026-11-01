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
        public static ParagraphStyle ToParagraphStyle(this ParagraphStyleState t,
            ParagraphStyle baseParaStyle,
            float? fontSize = null)
        {
            var ret = new ParagraphStyle();
            ret.TextDirection = t.TextDirection ?? baseParaStyle.TextDirection;
            ret.TextAlign = t.TextAlign ?? baseParaStyle.TextAlign;
            ret.WritingMode = baseParaStyle.WritingMode;
            ret.TextIndent = ResolveTextIndent(t, baseParaStyle, fontSize);

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
            ParagraphStyle baseParaStyle,
            float? fontSize = null)
        {
            var ret = new ParagraphStyle();
            ret.TextDirection = blockStyle.TextDirection ?? payloadDefault.TextDirection ?? baseParaStyle.TextDirection;
            ret.TextAlign = blockStyle.TextAlign ?? payloadDefault.TextAlign ?? baseParaStyle.TextAlign;
            ret.WritingMode = baseParaStyle.WritingMode;
            var indentStyle = blockStyle.TextIndent.HasValue || blockStyle.TextIndentEm.HasValue
                ? blockStyle
                : payloadDefault;
            ret.TextIndent = ResolveTextIndent(indentStyle, baseParaStyle, fontSize);

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

        private static TextIndent ResolveTextIndent(ParagraphStyleState style,
            ParagraphStyle baseParaStyle,
            float? fontSize)
        {
            if (style.TextIndent.HasValue) return style.TextIndent.Value;
            if (!style.TextIndentEm.HasValue) return baseParaStyle.TextIndent;

            var effectiveFontSize = style.TextIndentFontSize ?? fontSize;
            if (!effectiveFontSize.HasValue)
            {
                using var textStyle = baseParaStyle.GetTextStyle();
                effectiveFontSize = textStyle.FontSize;
            }

            return TextIndent.Pixels(style.TextIndentEm.Value * effectiveFontSize.Value * style.TextIndentFontSizeScale);
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
                // Rich text state follows the TMPro convention (positive raises the
                // baseline); Skia canvas Y grows downward, so positive BaselineShift
                // moves glyphs down. Negate at the boundary.
                ret.BaselineShift = -t.BaselineShift.Value;
            }
            else if (t.BaselineShiftEm.HasValue)
            {
                ret.BaselineShift = baseTextStyle.BaselineShift - t.BaselineShiftEm.Value * unscaledFontSize;
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
            using var paragraphStyle = payload.ParagraphStyle.ToParagraphStyle(baseParaStyle, baseTextStyle.FontSize);
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

                AddBlockContents(payload, block, builder, baseTextStyle);

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

            foreach (var annotation in payload.TateChuYokoAnnotations)
            {
                var separatorShift = 0;
                for (var index = 1; index < blocks.Count; ++index)
                {
                    if (blocks[index].StartUtf16 <= annotation.StartUtf16)
                    {
                        separatorShift = index;
                    }
                }

                builder.AddTateChuYokoUtf16(annotation.StartUtf16 + separatorShift,
                    annotation.EndUtf16 + separatorShift, annotation.Sizing);
            }

            foreach (var annotation in payload.OrientationAnnotations)
            {
                var separatorShift = 0;
                for (var index = 1; index < blocks.Count; ++index)
                {
                    if (blocks[index].StartUtf16 <= annotation.StartUtf16)
                    {
                        separatorShift = index;
                    }
                }

                builder.AddOrientationOverrideUtf16(annotation.StartUtf16 + separatorShift,
                    annotation.EndUtf16 + separatorShift, annotation.Orientation);
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
                using var paragraphStyle = block.Style.ToParagraphStyle(payload.ParagraphStyle,
                    baseParaStyle,
                    baseTextStyle.FontSize);
                paragraphStyle.SetTextStyle(baseTextStyle);

                using var builder = new ParagraphBuilder(paragraphStyle);
                AddBlockContents(payload, block, builder, baseTextStyle);

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

                foreach (var annotation in payload.TateChuYokoAnnotations)
                {
                    if (!block.ContainsRange(annotation.StartUtf16, annotation.EndUtf16))
                    {
                        continue;
                    }

                    builder.AddTateChuYokoUtf16(annotation.StartUtf16 - block.StartUtf16,
                        annotation.EndUtf16 - block.StartUtf16, annotation.Sizing);
                }

                foreach (var annotation in payload.OrientationAnnotations)
                {
                    if (!block.ContainsRange(annotation.StartUtf16, annotation.EndUtf16))
                    {
                        continue;
                    }

                    builder.AddOrientationOverrideUtf16(annotation.StartUtf16 - block.StartUtf16,
                        annotation.EndUtf16 - block.StartUtf16, annotation.Orientation);
                }

                ret.Add(builder.Build());
            }

            return ret;
        }

        private static void AddBlockContents(ParagraphPayload payload, ParagraphBlock block,
            ParagraphBuilder builder, TextStyle baseTextStyle)
        {
            var placeholders = new List<PlaceholderAnnotation>();
            foreach (var placeholder in payload.Placeholders)
            {
                if (block.ContainsRange(placeholder.StartUtf16, placeholder.EndUtf16))
                    placeholders.Add(placeholder);
            }
            var next = 0;
            var cursor = block.StartUtf16;
            void AppendPlaceholder()
            {
                var placeholder = placeholders[next++];
                using var style = placeholder.TextStyle.ToTextStyle(baseTextStyle);
                builder.PushStyle(style);
                builder.AddPlaceholder(placeholder.Width, placeholder.Height);
                builder.Pop();
                ++cursor;
            }
            foreach (var segment in block.Segments)
            {
                using var style = segment.TextStyle.ToTextStyle(baseTextStyle);
                builder.PushStyle(style);
                var offset = 0;
                while (offset < segment.Content.Length)
                {
                    if (next < placeholders.Count && placeholders[next].StartUtf16 == cursor)
                    {
                        AppendPlaceholder();
                        continue;
                    }
                    var count = segment.Content.Length - offset;
                    if (next < placeholders.Count)
                        count = Math.Min(count, placeholders[next].StartUtf16 - cursor);
                    if (count <= 0) throw new InvalidOperationException("Invalid placeholder source position");
                    builder.AddText(segment.Content.Substring(offset, count));
                    cursor += count;
                    offset += count;
                }
                builder.Pop();
            }
            while (next < placeholders.Count)
            {
                if (placeholders[next].StartUtf16 != cursor)
                    throw new InvalidOperationException("Invalid placeholder source position");
                AppendPlaceholder();
            }
        }
    }
}
