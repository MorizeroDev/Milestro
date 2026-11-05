#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using Milestro.Model;
using Milestro.RichTextParser;
using Milestro.Skia.TextLayout;

namespace Milestro.RichTextParser.Css
{
    internal static class CssParser
    {
        internal static void Apply(TextStyleState textStyle,
                                   string declarations,
                                   ParagraphStyleState? paragraphStyle = null,
                                   bool allowViewportBlockMargins = false,
                                   ICollection<string>? issues = null)
        {
            FontFeatureOperation? kerningOperation = null;
            List<FontFeatureOperation>? featureOperations = null;
            bool? textCombineUpright = null;
            TateChuYokoSizing? tateChuYokoSizing = null;
            VerticalTextOrientation? textOrientation = null;
            bool? noWrap = null;
            TextTransform? textTransform = null;
            TextIndent? textIndent = null;
            float? textIndentEm = null;
            var parser = new CssDeclarationParser(declarations);
            while (parser.TryRead(out var declaration))
            {
                if (!declaration.IsValid) continue;
                if (declaration.Property.Equals("font-kerning", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseKerning(declaration.Value, out var operation)) kerningOperation = operation;
                }
                else if (declaration.Property.Equals("font-feature-settings", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseFeatureSettings(declaration.Value, out var operations)) featureOperations = operations;
                }
                else if (declaration.Property.Equals("text-combine-upright", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseTextCombineUpright(declaration.Value, out var all))
                    {
                        textCombineUpright = all;
                    }
                    else
                    {
                        issues?.Add($"text-combine-upright={declaration.Value} defaulted to none");
                        textCombineUpright = false;
                    }
                }
                else if (declaration.Property.Equals(
                             "--milestro-text-combine-upright-sizing",
                             StringComparison.OrdinalIgnoreCase))
                {
                    if (declaration.Value.Equals("squeeze", StringComparison.OrdinalIgnoreCase))
                    {
                        tateChuYokoSizing = TateChuYokoSizing.Squeeze;
                    }
                    else if (declaration.Value.Equals("grow", StringComparison.OrdinalIgnoreCase))
                    {
                        tateChuYokoSizing = TateChuYokoSizing.Grow;
                    }
                    else
                    {
                        issues?.Add(
                            $"--milestro-text-combine-upright-sizing={declaration.Value} defaulted to squeeze");
                        tateChuYokoSizing = TateChuYokoSizing.Squeeze;
                    }
                }
                else if (declaration.Property.Equals("text-orientation", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseTextOrientation(declaration.Value, out var orientation))
                    {
                        textOrientation = orientation;
                    }
                    else
                    {
                        issues?.Add($"text-orientation={declaration.Value} defaulted to mixed");
                        textOrientation = VerticalTextOrientation.Mixed;
                    }
                }
                else if (declaration.Property.Equals("white-space", StringComparison.OrdinalIgnoreCase))
                {
                    if (declaration.Value.Equals("normal", StringComparison.OrdinalIgnoreCase))
                    {
                        noWrap = false;
                    }
                    else if (declaration.Value.Equals("nowrap", StringComparison.OrdinalIgnoreCase))
                    {
                        noWrap = true;
                    }
                }
                else if (declaration.Property.Equals("text-transform", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseTextTransform(declaration.Value, out var transform))
                    {
                        textTransform = transform;
                    }
                }
                else if (paragraphStyle != null &&
                         declaration.Property.Equals("text-indent", StringComparison.OrdinalIgnoreCase) &&
                         TryParseTextIndent(declaration.Value, out var indent, out var isEm))
                {
                    textIndent = isEm ? (TextIndent?)null : indent;
                    textIndentEm = isEm ? indent.Value : (float?)null;
                }
                else if (paragraphStyle != null &&
                         TryApplyBlockMargin(paragraphStyle, declaration.Property, declaration.Value,
                             allowViewportBlockMargins))
                {
                }
            }

            if (kerningOperation.HasValue) textStyle.FontFeatureOperations.Add(kerningOperation.Value);
            if (featureOperations != null) textStyle.FontFeatureOperations.AddRange(featureOperations);
            if (textCombineUpright.HasValue) textStyle.TextCombineUprightAll = textCombineUpright.Value;
            if (tateChuYokoSizing.HasValue)
                textStyle.TextCombineUprightSizing = tateChuYokoSizing.Value;
            if (textOrientation.HasValue)
                textStyle.TextOrientation = textOrientation.Value;
            if (noWrap.HasValue) textStyle.NoWrap = noWrap.Value;
            if (textTransform.HasValue) textStyle.TextTransform = textTransform.Value;
            if (paragraphStyle != null && (textIndent.HasValue || textIndentEm.HasValue))
            {
                paragraphStyle.TextIndent = textIndent;
                paragraphStyle.TextIndentEm = textIndentEm;
                paragraphStyle.TextIndentFontSize = textStyle.FontSize;
                paragraphStyle.TextIndentFontSizeScale = textStyle.FontSizeScale ?? 1f;
            }
        }

        private static bool TryParseKerning(CssSlice value, out FontFeatureOperation operation)
        {
            if (value.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                operation = new FontFeatureOperation(FontFeatureOperationType.Remove, "kern");
                return true;
            }

            if (value.Equals("normal", StringComparison.OrdinalIgnoreCase))
            {
                operation = new FontFeatureOperation(FontFeatureOperationType.Set, "kern", 1);
                return true;
            }

            if (value.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                operation = new FontFeatureOperation(FontFeatureOperationType.Set, "kern", 0);
                return true;
            }

            operation = default;
            return false;
        }

        private static bool TryParseTextCombineUpright(CssSlice value, out bool all)
        {
            if (value.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                all = true;
                return true;
            }

            all = false;
            return value.Equals("none", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryParseTextOrientation(CssSlice value,
            out VerticalTextOrientation orientation)
        {
            if (value.Equals("mixed", StringComparison.OrdinalIgnoreCase))
            {
                orientation = VerticalTextOrientation.Mixed;
                return true;
            }

            if (value.Equals("upright", StringComparison.OrdinalIgnoreCase))
            {
                orientation = VerticalTextOrientation.Upright;
                return true;
            }

            if (value.Equals("sideways", StringComparison.OrdinalIgnoreCase))
            {
                orientation = VerticalTextOrientation.Sideways;
                return true;
            }

            orientation = VerticalTextOrientation.Mixed;
            return false;
        }

        private static bool TryParseTextTransform(CssSlice value, out TextTransform textTransform)
        {
            if (value.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                textTransform = TextTransform.None;
                return true;
            }

            if (value.Equals("lowercase", StringComparison.OrdinalIgnoreCase))
            {
                textTransform = TextTransform.Lowercase;
                return true;
            }

            if (value.Equals("uppercase", StringComparison.OrdinalIgnoreCase))
            {
                textTransform = TextTransform.Uppercase;
                return true;
            }

            if (value.Equals("smallcaps", StringComparison.OrdinalIgnoreCase))
            {
                textTransform = TextTransform.SmallCaps;
                return true;
            }

            textTransform = TextTransform.None;
            return false;
        }

        private static bool TryParseFeatureSettings(CssSlice value, out List<FontFeatureOperation> operations)
        {
            operations = new List<FontFeatureOperation>();
            if (value.Equals("normal", StringComparison.OrdinalIgnoreCase))
            {
                operations.Add(new FontFeatureOperation(FontFeatureOperationType.ResetAll));
                return true;
            }

            if (!TrySplitFeatures(value, out var items) || items.Count == 0) return false;
            foreach (var item in items)
            {
                if (!TryParseFeature(item, out var operation))
                {
                    operations.Clear();
                    return false;
                }

                operations.Add(operation);
            }

            return true;
        }

        private static bool TrySplitFeatures(CssSlice value, out List<CssSlice> items)
        {
            items = new List<CssSlice>();
            var lexer = new CssLexer(value);
            var itemStart = value.Start;
            var parenthesisDepth = 0;
            while (true)
            {
                var token = lexer.Next();
                if (token.Type == CssTokenType.End)
                {
                    return parenthesisDepth == 0 && TryAddFeatureItem(value, itemStart, value.End, items);
                }

                if (token.Type == CssTokenType.Invalid) return false;
                if (token.Type == CssTokenType.LeftParenthesis)
                {
                    parenthesisDepth++;
                }
                else if (token.Type == CssTokenType.RightParenthesis)
                {
                    if (parenthesisDepth == 0) return false;
                    parenthesisDepth--;
                }
                else if (token.Type == CssTokenType.Comma && parenthesisDepth == 0)
                {
                    if (!TryAddFeatureItem(value, itemStart, token.Start, items)) return false;
                    itemStart = token.End;
                }
            }
        }

        private static bool TryAddFeatureItem(CssSlice value, int start, int end, List<CssSlice> items)
        {
            var item = new CssSlice(value.Source, start, end - start).Trim();
            if (item.Length == 0) return false;
            items.Add(item);
            return true;
        }

        private static bool TryParseFeature(CssSlice item, out FontFeatureOperation operation)
        {
            operation = default;
            if (item.Length < 2 || (item[0] != '\'' && item[0] != '"')) return false;

            var quote = item[0];
            var closingQuote = item.IndexOf(quote, 1);
            if (closingQuote != 5) return false;

            var feature = item.Slice(1, closingQuote - 1);
            for (var index = 0; index < feature.Length; index++)
            {
                var character = feature[index];
                if (character < ' ' || character > '~') return false;
            }

            var value = 1;
            var rawValue = item.Slice(closingQuote + 1, item.Length - closingQuote - 1).Trim();
            if (rawValue.Length > 0)
            {
                if (rawValue.Equals("on", StringComparison.OrdinalIgnoreCase))
                {
                    value = 1;
                }
                else if (rawValue.Equals("off", StringComparison.OrdinalIgnoreCase))
                {
                    value = 0;
                }
                else if (!int.TryParse(rawValue.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture,
                             out value) || value < 0)
                {
                    return false;
                }
            }

            operation = new FontFeatureOperation(FontFeatureOperationType.Set, feature.ToString(), value);
            return true;
        }

        private static bool TryParseTextIndent(CssSlice value, out TextIndent indent, out bool isEm)
        {
            indent = default;
            isEm = value.EndsWith("em", StringComparison.OrdinalIgnoreCase);
            var isPercentage = value.EndsWith("%", StringComparison.Ordinal);
            var isPixels = value.EndsWith("px", StringComparison.OrdinalIgnoreCase);
            var suffixLength = isPercentage ? 1 : isPixels || isEm ? 2 : 0;
            var number = value.Slice(0, value.Length - suffixLength);
            var trimmedNumber = number.Trim();
            if (number.Length == 0 || trimmedNumber.Length != number.Length ||
                !float.TryParse(number.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ||
                float.IsNaN(result) || float.IsInfinity(result))
            {
                return false;
            }

            if (suffixLength == 0 && result != 0) return false;
            indent = new TextIndent(result, isPercentage);
            return true;
        }

        private static bool TryParseBlockMargin(CssSlice value, bool allowViewportBlockMargins,
            out MarginValue margin)
        {
            margin = default;
            MarginUnit unit;
            var suffixLength = 0;
            if (value.EndsWith("%", StringComparison.Ordinal))
            {
                unit = MarginUnit.ContainerPercent;
                suffixLength = 1;
            }
            else if (value.EndsWith("px", StringComparison.OrdinalIgnoreCase))
            {
                unit = MarginUnit.Px;
                suffixLength = 2;
            }
            else if (value.EndsWith("em", StringComparison.OrdinalIgnoreCase))
            {
                unit = MarginUnit.Em;
                suffixLength = 2;
            }
            else if (allowViewportBlockMargins && value.EndsWith("vw", StringComparison.OrdinalIgnoreCase))
            {
                unit = MarginUnit.Vw;
                suffixLength = 2;
            }
            else if (allowViewportBlockMargins && value.EndsWith("vh", StringComparison.OrdinalIgnoreCase))
            {
                unit = MarginUnit.Vh;
                suffixLength = 2;
            }
            else
            {
                unit = MarginUnit.Px;
            }

            var number = value.Slice(0, value.Length - suffixLength);
            var trimmedNumber = number.Trim();
            if (number.Length == 0 || trimmedNumber.Length != number.Length ||
                !float.TryParse(number.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ||
                float.IsNaN(result) || float.IsInfinity(result) || result < 0f)
            {
                return false;
            }

            if (suffixLength == 0 && result != 0f) return false;
            margin = new MarginValue { Value = result, Unit = unit };
            return true;
        }

        private static bool TryApplyBlockMargin(ParagraphStyleState style, CssSlice property,
            CssSlice value, bool allowViewportBlockMargins)
        {
            if (property.Equals("margin", StringComparison.OrdinalIgnoreCase))
            {
                return TryApplyBlockMarginShorthand(style, value, allowViewportBlockMargins);
            }

            if (!TryParseBlockMargin(value, allowViewportBlockMargins, out var margin))
            {
                return false;
            }

            if (property.Equals("margin-top", StringComparison.OrdinalIgnoreCase))
            {
                style.MarginTop = margin;
            }
            else if (property.Equals("margin-right", StringComparison.OrdinalIgnoreCase))
            {
                style.MarginRight = margin;
            }
            else if (property.Equals("margin-bottom", StringComparison.OrdinalIgnoreCase))
            {
                style.MarginBottom = margin;
            }
            else if (property.Equals("margin-left", StringComparison.OrdinalIgnoreCase))
            {
                style.MarginLeft = margin;
            }
            else
            {
                return false;
            }

            return true;
        }

        private static bool TryApplyBlockMarginShorthand(ParagraphStyleState style, CssSlice value,
            bool allowViewportBlockMargins)
        {
            var slices = new CssSlice[4];
            var count = 0;
            var position = 0;
            while (position < value.Length)
            {
                while (position < value.Length && char.IsWhiteSpace(value[position])) position++;
                if (position == value.Length) break;
                if (count == slices.Length) return false;
                var start = position;
                while (position < value.Length && !char.IsWhiteSpace(value[position])) position++;
                slices[count++] = value.Slice(start, position - start);
            }

            if (count == 0) return false;
            var margins = new MarginValue[count];
            for (var index = 0; index < count; ++index)
            {
                if (!TryParseBlockMargin(slices[index], allowViewportBlockMargins, out margins[index]))
                {
                    return false;
                }
            }

            style.MarginTop = margins[0];
            style.MarginRight = margins[count == 1 ? 0 : 1];
            style.MarginBottom = margins[count < 3 ? 0 : 2];
            style.MarginLeft = margins[count == 1 ? 0 : count == 2 ? 1 : count == 3 ? 1 : 3];
            return true;
        }
    }
}
