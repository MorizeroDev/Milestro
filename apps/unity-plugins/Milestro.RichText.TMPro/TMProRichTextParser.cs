#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using Milestro.Model;
using Milestro.RichTextParser;
using Milestro.Skia;
using Milestro.Skia.TextLayout;
using Paraparty.Colors;
using UnityEngine;
using CoreRichTextParser = Milestro.RichTextParser.RichTextParser;

namespace Milestro.RichText.TMPro
{
    /// <summary>
    /// Parses the TextMesh Pro rich-text subset supported by Milestro's paragraph model.
    /// Unsupported or unmatched tags are preserved literally and reported as warnings.
    /// </summary>
    public sealed class TMProRichTextParser : IRichTextParser
    {
        public const string Id = "tmpro";
        public const string DisplayName = "TMPro";

        public ParagraphPayload Parse(string source, RichTextParseContext context)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (context == null) throw new ArgumentNullException(nameof(context));
            return new DirectParser(source, context).Parse();
        }

        private readonly struct Tag
        {
            internal Tag(string name, string value, string raw, bool hasExplicitValue,
                bool isClosing, bool isSelfClosing)
            {
                Name = name;
                Value = value;
                Raw = raw;
                HasExplicitValue = hasExplicitValue;
                IsClosing = isClosing;
                IsSelfClosing = isSelfClosing;
            }

            internal string Name { get; }
            internal string Value { get; }
            internal string Raw { get; }
            internal bool HasExplicitValue { get; }
            internal bool IsClosing { get; }
            internal bool IsSelfClosing { get; }
        }

        private enum OpenTagKind
        {
            Inline,
            Literal,
            Link,
            Block,
        }

        private readonly struct OpenTag
        {
            internal OpenTag(string name, TextStyleState previousStyle, OpenTagKind kind)
            {
                Name = name;
                PreviousStyle = previousStyle;
                Kind = kind;
            }

            internal string Name { get; }
            internal TextStyleState PreviousStyle { get; }
            internal OpenTagKind Kind { get; }
        }

        private enum ParagraphMeasurementKind
        {
            Indent,
            LineIndent,
            Margin,
            MarginLeft,
            MarginRight,
            Width,
        }

        private sealed class DirectParser
        {
            private readonly string source;
            private readonly ParagraphPayloadBuilder builder;
            private readonly Stack<OpenTag> openTags = new Stack<OpenTag>();
            private TextStyleState style = new TextStyleState();
            private bool noParse;

            internal DirectParser(string source, RichTextParseContext context)
            {
                this.source = source;
                builder = new ParagraphPayloadBuilder(context.Locale);
            }

            internal ParagraphPayload Parse()
            {
                var position = 0;
                while (position < source.Length)
                {
                    var tagStart = source.IndexOf('<', position);
                    if (tagStart < 0)
                    {
                        AppendText(source.Substring(position));
                        break;
                    }

                    AppendText(source.Substring(position, tagStart - position));
                    if (!TryReadTag(tagStart, out var tag, out var nextPosition))
                    {
                        AppendText("<");
                        position = tagStart + 1;
                        continue;
                    }

                    if (noParse)
                    {
                        if (tag.IsClosing && NameEquals(tag.Name, "noparse")) noParse = false;
                        else AppendText(tag.Raw);
                        position = nextPosition;
                        continue;
                    }

                    if (tag.IsClosing)
                    {
                        CloseTag(tag);
                    }
                    else if (NameEquals(tag.Name, "br"))
                    {
                        if (tag.Value.Length == 0) AppendText("\n");
                        else WarnAndAppendLiteral(tag);
                    }
                    else if (NameEquals(tag.Name, "noparse"))
                    {
                        if (tag.Value.Length != 0) WarnAndAppendLiteral(tag);
                        else if (!tag.IsSelfClosing) noParse = true;
                    }
                    else if (NameEquals(tag.Name, "link") && HasOpenLink())
                    {
                        WarnAndAppendLiteral(tag);
                        if (!tag.IsSelfClosing)
                            openTags.Push(new OpenTag(tag.Name, style, OpenTagKind.Literal));
                    }
                    else if (TryOpenTag(tag, out var openTag))
                    {
                        if (tag.IsSelfClosing) CloseFrame(openTag);
                        else openTags.Push(openTag);
                    }
                    else
                    {
                        WarnAndAppendLiteral(tag);
                        if (!tag.IsSelfClosing)
                            openTags.Push(new OpenTag(tag.Name, style, OpenTagKind.Literal));
                    }

                    position = nextPosition;
                }

                while (openTags.Count > 0) CloseFrame(openTags.Pop());
                return builder.Build();
            }

            private bool TryOpenTag(Tag tag, out OpenTag openTag)
            {
                openTag = default;
                if (tag.Value.Length > 0 && !tag.HasExplicitValue) return false;
                var name = tag.Name.ToLowerInvariant();
                switch (name)
                {
                    case "b":
                        return TryOpenSimple(tag, name, target => target.FontWeight = 700, out openTag);
                    case "i":
                        return TryOpenSimple(tag, name, target => target.Italic = true, out openTag);
                    case "u":
                        return TryOpenSimple(tag, name, target => target.Underline = true, out openTag);
                    case "s":
                    case "strikethrough":
                        return TryOpenSimple(tag, name, target => target.Strikethrough = true, out openTag);
                    case "sup": return TryOpenScript(tag, name, true, out openTag);
                    case "sub": return TryOpenScript(tag, name, false, out openTag);
                    case "color": return TryOpenColor(tag, tag.Value, out openTag);
                    case "alpha": return TryOpenAlpha(tag, out openTag);
                    case "size": return TryOpenSize(tag, out openTag);
                    case "font": return TryOpenFont(tag, out openTag);
                    case "font-weight": return TryOpenFontWeight(tag, out openTag);
                    case "cspace": return TryOpenCharacterSpacing(tag, out openTag);
                    case "voffset": return TryOpenVerticalOffset(tag, out openTag);
                    case "line-height": return TryOpenLineHeight(tag, out openTag);
                    case "mark": return TryOpenMark(tag, out openTag);
                    case "uppercase":
                    case "allcaps":
                        return TryOpenSimple(tag, name,
                            target => target.TextTransform = TextTransform.Uppercase, out openTag);
                    case "lowercase":
                        return TryOpenSimple(tag, name,
                            target => target.TextTransform = TextTransform.Lowercase, out openTag);
                    case "smallcaps":
                        return TryOpenSimple(tag, name,
                            target => target.TextTransform = TextTransform.SmallCaps, out openTag);
                    case "nobr":
                        return TryOpenSimple(tag, name, target => target.NoWrap = true, out openTag);
                    case "link":
                        if (tag.Value.Length == 0) return false;
                        builder.BeginLink(tag.Value);
                        openTag = new OpenTag(name, style, OpenTagKind.Link);
                        return true;
                    case "align": return TryOpenAlignment(tag, out openTag);
                    case "indent":
                        return TryOpenParagraphMeasurement(tag, ParagraphMeasurementKind.Indent,
                            out openTag);
                    case "line-indent":
                        return TryOpenParagraphMeasurement(tag, ParagraphMeasurementKind.LineIndent,
                            out openTag);
                    case "margin":
                        return TryOpenParagraphMeasurement(tag, ParagraphMeasurementKind.Margin,
                            out openTag);
                    case "margin-left":
                        return TryOpenParagraphMeasurement(tag, ParagraphMeasurementKind.MarginLeft,
                            out openTag);
                    case "margin-right":
                        return TryOpenParagraphMeasurement(tag, ParagraphMeasurementKind.MarginRight,
                            out openTag);
                    case "width":
                        return TryOpenParagraphMeasurement(tag, ParagraphMeasurementKind.Width,
                            out openTag);
                    default:
                        return name[0] == '#' && TryOpenColor(tag, name, out openTag);
                }
            }

            private bool TryOpenSimple(Tag tag, string name, Action<TextStyleState> apply,
                out OpenTag openTag)
            {
                if (tag.Value.Length != 0)
                {
                    openTag = default;
                    return false;
                }

                openTag = OpenInline(name, apply);
                return true;
            }

            private OpenTag OpenInline(string name, Action<TextStyleState> apply)
            {
                var previousStyle = style;
                style = style.Clone();
                apply(style);
                return new OpenTag(name, previousStyle, OpenTagKind.Inline);
            }

            private OpenTag OpenBlock(string name, Action<ParagraphStyleState> apply)
            {
                var blockStyle = new ParagraphStyleState();
                apply(blockStyle);
                builder.BeginBlock(blockStyle, style);
                return new OpenTag(name, style, OpenTagKind.Block);
            }

            private bool TryOpenScript(Tag tag, string name, bool superscript, out OpenTag openTag)
            {
                return TryOpenSimple(tag, name, target =>
                {
                    target.FontSizeScale = (target.FontSizeScale ?? 1f) *
                        CoreRichTextParser.ScriptFontSizeScale;
                    target.BaselineShiftEm = (target.BaselineShiftEm ?? 0f) +
                        (superscript
                            ? CoreRichTextParser.SuperscriptBaselineShiftEm
                            : CoreRichTextParser.SubscriptBaselineShiftEm);
                }, out openTag);
            }

            private bool TryOpenColor(Tag tag, string value, out OpenTag openTag)
            {
                if (!ColorUtils.TryParseColor(value, out var color))
                {
                    openTag = default;
                    return false;
                }

                openTag = OpenInline("color", target => target.Color = color);
                return true;
            }

            private bool TryOpenAlpha(Tag tag, out OpenTag openTag)
            {
                if (tag.Value.Length != 3 || tag.Value[0] != '#' ||
                    !int.TryParse(tag.Value.Substring(1), NumberStyles.AllowHexSpecifier,
                        CultureInfo.InvariantCulture, out var alpha))
                {
                    openTag = default;
                    return false;
                }

                var opacity = alpha / 255f;
                openTag = OpenInline("alpha", target => target.Alpha = opacity);
                return true;
            }

            private bool TryOpenSize(Tag tag, out OpenTag openTag)
            {
                if (tag.Value.StartsWith("+", StringComparison.Ordinal) ||
                    !TryParseMeasurement(tag.Value, out var number, out var unit) || number <= 0f)
                {
                    openTag = default;
                    return false;
                }

                if (unit.Length == 0 || unit == "px")
                {
                    openTag = OpenInline("size", target => target.FontSize = number);
                    return true;
                }

                if (unit == "%" || unit == "em")
                {
                    var scale = unit == "%" ? number / 100f : number;
                    openTag = OpenInline("size", target => target.FontSizeScale = scale);
                    return true;
                }

                openTag = default;
                return false;
            }

            private bool TryOpenFont(Tag tag, out OpenTag openTag)
            {
                if (string.IsNullOrWhiteSpace(tag.Value))
                {
                    openTag = default;
                    return false;
                }

                var family = tag.Value;
                openTag = OpenInline("font", target =>
                {
                    target.FontFamilyTokens = new List<FontFamilyToken> { FontFamilyToken.Exact(family) };
                    target.FontFamilies = new List<string> { family };
                });
                return true;
            }

            private bool TryOpenFontWeight(Tag tag, out OpenTag openTag)
            {
                if (!int.TryParse(tag.Value, NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out var weight) || weight <= 0 || weight > 1000)
                {
                    openTag = default;
                    return false;
                }

                openTag = OpenInline("font-weight", target => target.FontWeight = weight);
                return true;
            }

            private bool TryOpenCharacterSpacing(Tag tag, out OpenTag openTag)
            {
                if (!TryParseMeasurement(tag.Value, out var number, out var unit) ||
                    (unit.Length != 0 && unit != "px"))
                {
                    openTag = default;
                    return false;
                }

                openTag = OpenInline("cspace", target => target.LetterSpacing = number);
                return true;
            }

            private bool TryOpenVerticalOffset(Tag tag, out OpenTag openTag)
            {
                if (!TryParseMeasurement(tag.Value, out var number, out var unit))
                {
                    openTag = default;
                    return false;
                }

                if (unit.Length == 0 || unit == "px")
                {
                    openTag = OpenInline("voffset", target => target.BaselineShift = number);
                    return true;
                }

                if (unit == "em" || unit == "%")
                {
                    var em = unit == "%" ? number / 100f : number;
                    openTag = OpenInline("voffset", target => target.BaselineShiftEm = em);
                    return true;
                }

                openTag = default;
                return false;
            }

            private bool TryOpenLineHeight(Tag tag, out OpenTag openTag)
            {
                if (!TryParseMeasurement(tag.Value, out var number, out var unit) || number <= 0f ||
                    (unit != "%" && unit != "em"))
                {
                    openTag = default;
                    return false;
                }

                var multiplier = unit == "%" ? number / 100f : number;
                openTag = OpenInline("line-height", target => target.LineHeight = multiplier);
                return true;
            }

            private bool TryOpenMark(Tag tag, out OpenTag openTag)
            {
                if (tag.Value.Length == 0)
                {
                    openTag = OpenInline("mark", target =>
                        target.BackgroundColor = new Color32(255, 255, 0, 255));
                    return true;
                }

                if (!ColorUtils.TryParseColor(tag.Value, out var color))
                {
                    openTag = default;
                    return false;
                }

                openTag = OpenInline("mark", target => target.BackgroundColor = color);
                return true;
            }

            private bool TryOpenAlignment(Tag tag, out OpenTag openTag)
            {
                TextAlign alignment;
                switch (tag.Value.ToLowerInvariant())
                {
                    case "left": alignment = TextAlign.Left; break;
                    case "center": alignment = TextAlign.Center; break;
                    case "right": alignment = TextAlign.Right; break;
                    case "justified": alignment = TextAlign.Justify; break;
                    default:
                        openTag = default;
                        return false;
                }

                openTag = OpenBlock("align", target => target.TextAlign = alignment);
                return true;
            }

            private bool TryOpenParagraphMeasurement(Tag tag, ParagraphMeasurementKind kind,
                out OpenTag openTag)
            {
                var requirePositive = kind == ParagraphMeasurementKind.Width;
                if (!TryParseMeasurement(tag.Value, out var number, out var unit) ||
                    (requirePositive ? number <= 0f : number < 0f) ||
                    (unit.Length != 0 && unit != "px" && unit != "em" && unit != "%"))
                {
                    openTag = default;
                    return false;
                }

                openTag = OpenBlock(tag.Name.ToLowerInvariant(), target =>
                {
                    switch (kind)
                    {
                        case ParagraphMeasurementKind.Indent:
                            target.MarginLeft = MakeMargin(number, unit);
                            break;
                        case ParagraphMeasurementKind.LineIndent:
                            if (unit == "em") target.TextIndentEm = number;
                            else
                            {
                                target.TextIndent = unit == "%"
                                    ? TextIndent.Percentage(number)
                                    : TextIndent.Pixels(number);
                            }
                            break;
                        case ParagraphMeasurementKind.Margin:
                            target.MarginLeft = MakeMargin(number, unit);
                            target.MarginRight = MakeMargin(number, unit);
                            break;
                        case ParagraphMeasurementKind.MarginLeft:
                            target.MarginLeft = MakeMargin(number, unit);
                            break;
                        case ParagraphMeasurementKind.MarginRight:
                            target.MarginRight = MakeMargin(number, unit);
                            break;
                        case ParagraphMeasurementKind.Width:
                            target.Width = MakeMargin(number, unit);
                            break;
                        default: throw new ArgumentOutOfRangeException(nameof(kind));
                    }
                });
                return true;
            }

            private void CloseTag(Tag tag)
            {
                if (openTags.Count > 0 && NameEquals(openTags.Peek().Name, tag.Name))
                {
                    var openTag = openTags.Pop();
                    if (openTag.Kind == OpenTagKind.Literal)
                    {
                        style = openTag.PreviousStyle;
                        WarnAndAppendLiteral(tag);
                    }
                    else CloseFrame(openTag);
                    return;
                }

                WarnAndAppendLiteral(tag);
            }

            private void CloseFrame(OpenTag openTag)
            {
                style = openTag.PreviousStyle;
                if (openTag.Kind == OpenTagKind.Link) builder.EndLink();
                else if (openTag.Kind == OpenTagKind.Block) builder.EndBlock();
            }

            private void WarnAndAppendLiteral(Tag tag)
            {
                Debug.LogWarning(
                    $"Milestro TMPro parser: unsupported or unmatched tag '{tag.Name}'; " +
                    "treating it as plain text.");
                AppendText(tag.Raw);
            }

            private void AppendText(string text)
            {
                builder.AppendText(text, style);
            }

            private bool HasOpenLink()
            {
                foreach (var openTag in openTags)
                    if (openTag.Kind == OpenTagKind.Link) return true;
                return false;
            }

            private bool TryReadTag(int start, out Tag tag, out int nextPosition)
            {
                tag = default;
                nextPosition = start + 1;
                var end = FindTagEnd(start);
                if (end < 0) return false;
                var raw = source.Substring(start, end - start + 1);
                var content = source.Substring(start + 1, end - start - 1);
                var cursor = 0;
                SkipWhiteSpace(content, ref cursor, content.Length);
                var isClosing = cursor < content.Length && content[cursor] == '/';
                if (isClosing) ++cursor;
                var contentEnd = content.Length;
                var isSelfClosing = false;
                if (!isClosing)
                {
                    var trailing = contentEnd - 1;
                    while (trailing >= 0 && char.IsWhiteSpace(content[trailing])) --trailing;
                    if (trailing >= 0 && content[trailing] == '/')
                    {
                        isSelfClosing = true;
                        contentEnd = trailing;
                    }
                }

                SkipWhiteSpace(content, ref cursor, contentEnd);
                var nameStart = cursor;
                while (cursor < contentEnd && !char.IsWhiteSpace(content[cursor]) &&
                       content[cursor] != '=' && content[cursor] != '/') ++cursor;
                var name = content.Substring(nameStart, cursor - nameStart);
                if (name.Length == 0 || (!char.IsLetter(name[0]) && name[0] != '#')) return false;

                string value;
                var hasExplicitValue = false;
                if (isClosing)
                {
                    SkipWhiteSpace(content, ref cursor, contentEnd);
                    if (cursor != contentEnd) return false;
                    value = "";
                }
                else
                {
                    SkipWhiteSpace(content, ref cursor, contentEnd);
                    if (cursor < contentEnd)
                    {
                        hasExplicitValue = content[cursor] == '=';
                        if (hasExplicitValue) ++cursor;
                    }

                    value = content.Substring(cursor, contentEnd - cursor).Trim();
                    if (hasExplicitValue && value.Length > 0 && (value[0] == '"' || value[0] == '\''))
                    {
                        var quote = value[0];
                        if (value.Length < 2 || value[value.Length - 1] != quote) return false;
                        value = value.Substring(1, value.Length - 2);
                    }
                }

                tag = new Tag(name, value, raw, hasExplicitValue, isClosing, isSelfClosing);
                nextPosition = end + 1;
                return true;
            }

            private int FindTagEnd(int start)
            {
                var quote = '\0';
                for (var index = start + 1; index < source.Length; ++index)
                {
                    var current = source[index];
                    if (quote != '\0')
                    {
                        if (current == quote) quote = '\0';
                        continue;
                    }

                    if (current == '>') return index;
                    if (current != '"' && current != '\'') continue;
                    var previous = index - 1;
                    while (previous > start && char.IsWhiteSpace(source[previous])) --previous;
                    if (previous > start && source[previous] == '=') quote = current;
                }

                return -1;
            }

            private static void SkipWhiteSpace(string text, ref int cursor, int end)
            {
                while (cursor < end && char.IsWhiteSpace(text[cursor])) ++cursor;
            }

            private static bool TryParseMeasurement(string value, out float number, out string unit)
            {
                unit = "";
                var numberText = value;
                if (value.EndsWith("%", StringComparison.Ordinal))
                {
                    unit = "%";
                    numberText = value.Substring(0, value.Length - 1);
                }
                else if (value.EndsWith("px", StringComparison.OrdinalIgnoreCase) ||
                         value.EndsWith("em", StringComparison.OrdinalIgnoreCase))
                {
                    unit = value.Substring(value.Length - 2).ToLowerInvariant();
                    numberText = value.Substring(0, value.Length - 2);
                }

                return float.TryParse(numberText,
                           NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                           CultureInfo.InvariantCulture, out number) && float.IsFinite(number);
            }

            private static MarginValue MakeMargin(float number, string unit)
            {
                var marginUnit = unit == "%"
                    ? MarginUnit.ContainerPercent
                    : unit == "em" ? MarginUnit.Em : MarginUnit.Px;
                return new MarginValue { Value = number, Unit = marginUnit };
            }

            private static bool NameEquals(string left, string right)
            {
                return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
