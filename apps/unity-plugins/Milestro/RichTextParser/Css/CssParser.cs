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
                                   ParagraphStyleState? paragraphStyle = null)
        {
            FontFeatureOperation? kerningOperation = null;
            List<FontFeatureOperation>? featureOperations = null;
            TextIndent? textIndent = null;
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
                else if (paragraphStyle != null &&
                         declaration.Property.Equals("text-indent", StringComparison.OrdinalIgnoreCase) &&
                         TryParseTextIndent(declaration.Value, out var indent))
                {
                    textIndent = indent;
                }
                else if (paragraphStyle != null &&
                         declaration.Property.Equals("margin-top", StringComparison.OrdinalIgnoreCase) &&
                         TryParseBlockMargin(declaration.Value, out var marginTop))
                {
                    paragraphStyle.MarginTop = marginTop;
                }
                else if (paragraphStyle != null &&
                         declaration.Property.Equals("margin-bottom", StringComparison.OrdinalIgnoreCase) &&
                         TryParseBlockMargin(declaration.Value, out var marginBottom))
                {
                    paragraphStyle.MarginBottom = marginBottom;
                }
            }

            if (kerningOperation.HasValue) textStyle.FontFeatureOperations.Add(kerningOperation.Value);
            if (featureOperations != null) textStyle.FontFeatureOperations.AddRange(featureOperations);
            if (paragraphStyle != null && textIndent.HasValue) paragraphStyle.TextIndent = textIndent;
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

        private static bool TryParseTextIndent(CssSlice value, out TextIndent indent)
        {
            indent = default;
            var isPercentage = value.EndsWith("%", StringComparison.Ordinal);
            var isPixels = value.EndsWith("px", StringComparison.OrdinalIgnoreCase);
            var suffixLength = isPercentage ? 1 : isPixels ? 2 : 0;
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

        private static bool TryParseBlockMargin(CssSlice value, out MarginValue margin)
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
    }
}
