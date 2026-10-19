using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using Milestro.Model;
using Milestro.RichTextParser.AST;
using Milestro.RichTextParser.Css;
using Milestro.Skia;
using Milestro.Skia.TextLayout;
using Paraparty.Colors;
using UnityEngine;
using XmlNode = Milestro.RichTextParser.AST.XmlNode;

namespace Milestro.RichTextParser
{
    public class RichTextParser
    {
        /// <summary>
        /// Font size multiplier applied to <c>&lt;sup&gt;</c> and <c>&lt;sub&gt;</c> content.
        /// Matches the TextMesh Pro default superscript/subscript size factor.
        /// </summary>
        public const float ScriptFontSizeScale = 0.5f;

        /// <summary>
        /// Baseline raise applied to <c>&lt;sup&gt;</c> content, in em units of the
        /// effective font size before script scaling.
        /// </summary>
        public const float SuperscriptBaselineShiftEm = 0.5f;

        /// <summary>
        /// Baseline drop applied to <c>&lt;sub&gt;</c> content, in em units of the
        /// effective font size before script scaling.
        /// </summary>
        public const float SubscriptBaselineShiftEm = -0.25f;

        public Stack<XmlNode> NodeStack { get; private set; } = new Stack<XmlNode>();

        public XmlNode? RootNode { get; private set; } = null;

        private void PushNode(XmlNode node)
        {
            if (NodeStack.Count == 0)
            {
                if (RootNode == null)
                {
                    RootNode = node;
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(node), "attempt to parse an XML with multi root node");
                }
            }
            else
            {
                var peek = NodeStack.Peek();
                if (peek is XmlElementNode s)
                {
                    s.Children.Add(node);
                }
            }

            NodeStack.Push(node);
        }

        private void PopNode()
        {
            if (NodeStack.Count == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(NodeStack), "attempt to pop from a empty stack");
            }

            NodeStack.Pop();
        }

        private void PopNode(string tag)
        {
            if (!NodeStack.TryPeek(out var t))
            {
                throw new ArgumentOutOfRangeException(nameof(NodeStack), "attempt to pop from a empty stack");
            }

            if (t.Tag != tag)
            {
                throw new ArgumentOutOfRangeException(nameof(tag),
                    $"attempt to pop a node but tag name mismatched, {t.Tag} expected but {tag} received");
            }

            PopNode();
        }

        private void PushElement(string tagName, bool isEmptyElement, Dictionary<string, string> attributes)
        {
            var node = new XmlElementNode(tagName, attributes, isEmptyElement);

            PushNode(node);
            if (isEmptyElement)
            {
                PopNode();
            }
        }

        private void PushTextElement(string text)
        {
            var node = new XmlTextNode(text);
            PushNode(node);
            PopNode();
        }

        public void ParseText(string str)
        {
            str = $"<root>{str}</root>";
            var data = new MemoryStream(Encoding.UTF8.GetBytes(str));
            try
            {
                ParseXml(data);
            }
            catch (Exception e)
            {
                Debug.LogWarning(e.Message);
                Debug.LogWarning(e);
#if UNITY_EDITOR
                RootNode = new XmlTextNode(e.Message + "\n\n" + e.StackTrace);
#else
                RootNode = new XmlTextNode("Render Error");
#endif
            }
        }

        private void ParseXml(Stream stream)
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                ConformanceLevel = ConformanceLevel.Fragment
            };

            using var reader = XmlReader.Create(stream, settings);

            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        var attributess = new Dictionary<string, string>();
                        var tagName = reader.Name;
                        var isEmptyElement = reader.IsEmptyElement;
                        for (int i = 0; i < reader.AttributeCount; i++)
                        {
                            reader.MoveToAttribute(i);
                            attributess[reader.Name] = reader.Value;
                        }

                        PushElement(tagName, isEmptyElement, attributess);
                        break;
                    case XmlNodeType.Text:
                        PushTextElement(reader.Value);
                        break;
                    case XmlNodeType.EndElement:
                        PopNode(reader.Name);
                        break;
                    case XmlNodeType.Attribute:
                    // ignore
                    case XmlNodeType.Whitespace:
                        // ignore
                        break;
                    default:
                        throw new ArgumentException($"unacceptable token, type: {reader.NodeType}");
                }
            }


            if (NodeStack.Count > 0)
            {
                throw new ArgumentException("unfinished xml document");
            }
        }

        public ParagraphPayload ConvertToSegments()
        {
            var rootNode = RootNode;
            if (rootNode is XmlTextNode textNode)
            {
                return ParagraphPayload.MakeText(textNode.Text);
            }

            if (rootNode is XmlElementNode root)
            {
                var ctx = new Context();
                ConvertToSegments(ctx, root);
                return ctx.Result;
            }

            throw new InvalidCastException("?");
        }

        private void ConvertToSegments(Context ctx, XmlElementNode node)
        {
            var state = ctx.TextStyleState.Clone();
            var isAnchor = false;
            var anchorHref = "";
            var anchorId = "";
            var anchorStartUtf16 = 0;
            if (node.Tag == "b" || node.Tag == "strong")
            {
                state.FontWeight = FontWeight.Bold;
            }
            else if (node.Tag == "s" || node.Tag == "del")
            {
                state.Strikethrough = true;
            }
            else if (node.Tag == "u")
            {
                state.Underline = true;
            }
            else if (node.Tag == "i" || node.Tag == "em")
            {
                state.FontSlant = FontSlant.Italic;
            }
            else if (node.Tag == "sup" || node.Tag == "sub")
            {
                state.FontSizeScale = (state.FontSizeScale ?? 1f) * ScriptFontSizeScale;
                state.BaselineShiftEm = (state.BaselineShiftEm ?? 0f) +
                    (node.Tag == "sup" ? SuperscriptBaselineShiftEm : SubscriptBaselineShiftEm);
            }
            else if (node.Tag == "font")
            {
                if (node.Attributes.TryGetValue("color", out var color))
                {
                    try
                    {
                        state.Color = ColorUtils.ParseColor(color);
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }

                if (node.Attributes.TryGetValue("size", out var size))
                {
                    if (TryParseFontSize(size, out var result))
                    {
                        state.FontSize = result;
                    }
                }

                if (node.Attributes.TryGetValue("weight", out var weight))
                {
                    if (TryParseFontWeight(weight, out var result))
                    {
                        state.FontWeight = result;
                    }
                }

                if (node.Attributes.TryGetValue("face", out var face)
                    && FontFamilyParser.TryParseFontFamilyList(face, out var fontFamilies))
                {
                    state.FontFamilyTokens = fontFamilies;
                    state.FontFamilies = FontFamilyParser.ToSourceFamilyList(fontFamilies);
                }

                if (node.Attributes.TryGetValue("style", out var fontDeclarations))
                {
                    CssParser.Apply(state, fontDeclarations);
                }
            }
            else if (node.Tag == "span")
            {
                if (node.Attributes.TryGetValue("cspace", out var cspace)
                    && TryParseFloat(cspace, out var letterSpacing))
                {
                    state.LetterSpacing = letterSpacing;
                }

                if (node.Attributes.TryGetValue("voffset", out var voffset)
                    && TryParseFloat(voffset, out var baselineShift))
                {
                    state.BaselineShift = baselineShift;
                }

                if (node.Attributes.TryGetValue("fscale", out var fscale)
                    && TryParseFloat(fscale, out var fontSizeScale)
                    && fontSizeScale > 0)
                {
                    state.FontSizeScale = fontSizeScale;
                }

                if (node.Attributes.TryGetValue("voffset-em", out var voffsetEm)
                    && TryParseFloat(voffsetEm, out var baselineShiftEm))
                {
                    state.BaselineShiftEm = baselineShiftEm;
                }

                if (node.Attributes.TryGetValue("lang", out var lang) && !string.IsNullOrWhiteSpace(lang))
                {
                    state.Locale = lang;
                }

                if (node.Attributes.TryGetValue("face", out var face)
                    && FontFamilyParser.TryParseFontFamilyList(face, out var fontFamilies))
                {
                    state.FontFamilyTokens = fontFamilies;
                    state.FontFamilies = FontFamilyParser.ToSourceFamilyList(fontFamilies);
                }

                if (node.Attributes.TryGetValue("weight", out var weight)
                    && TryParseFontWeight(weight, out var fontWeight))
                {
                    state.FontWeight = fontWeight;
                }

                if (node.Attributes.TryGetValue("width", out var width)
                    && TryParseEnum(width, out FontWidth fontWidth))
                {
                    state.FontWidth = fontWidth;
                }

                if (node.Attributes.TryGetValue("slant", out var slant)
                    && TryParseEnum(slant, out FontSlant fontSlant))
                {
                    state.FontSlant = fontSlant;
                }

                if (node.Attributes.TryGetValue("style", out var spanDeclarations))
                {
                    CssParser.Apply(state, spanDeclarations);
                }
            }
            else if (node.Tag == "p")
            {
                StartParagraph(ctx, state);

                if (node.Attributes.TryGetValue("align", out var alignDirection))
                {
                    if (TryParseEnum(alignDirection, out TextAlign textAlign))
                    {
                        ctx.Result.ParagraphStyle.TextAlign = textAlign;
                    }
                }

                if (node.Attributes.TryGetValue("dir", out var textDirection)
                    && TryParseEnum(textDirection, out TextDirection direction))
                {
                    ctx.Result.ParagraphStyle.TextDirection = direction;
                }

                if (node.Attributes.TryGetValue("style", out var paragraphDeclarations))
                {
                    CssParser.Apply(state, paragraphDeclarations, ctx.Result.ParagraphStyle);
                }
            }
            else if (node.Tag == "br")
            {
                AddText(ctx, state, "\n");
                return;
            }
            else if (node.Tag == "a")
            {
                ValidateAnchor(node, out anchorHref, out anchorId);
                if (ctx.HasActiveLink)
                {
                    throw new InvalidCastException("nested <a> elements are not supported");
                }

                isAnchor = true;
                anchorStartUtf16 = ctx.TextLengthUtf16;
                ctx.HasActiveLink = true;
            }
            else if (node.Tag == "ruby")
            {
                ConvertRuby(ctx, node, state);
                return;
            }
            else if (node.Tag == "rt")
            {
                throw new InvalidCastException("<rt> must be a direct child of <ruby>");
            }
            else if (node.Tag == "rp")
            {
                throw new InvalidCastException("<rp> must be a direct child of <ruby>");
            }
            else if (node.Tag == "root")
            {
                // ignore
            }
            else
            {
                throw new InvalidCastException($"unknown tag accepted: {node.Tag}{Environment.NewLine}{node}");
            }


            foreach (var child in node.Children)
            {
                ConvertChild(ctx, state, child);
            }

            if (isAnchor)
            {
                ctx.HasActiveLink = false;
                ctx.Result.AddLink(new LinkAnnotation(anchorHref,
                    anchorId,
                    anchorStartUtf16,
                    ctx.TextLengthUtf16,
                    ctx.Result.Links.Count));
            }
        }

        private void ConvertRuby(Context ctx, XmlElementNode node, TextStyleState state)
        {
            if (ctx.HasActiveRuby)
            {
                throw new InvalidCastException("nested <ruby> elements are not supported");
            }

            if (node.IsEmptyElement)
            {
                throw new InvalidCastException("<ruby> must use an explicit closing tag");
            }

            XmlElementNode? annotationNode = null;
            var annotationIndex = -1;
            for (var index = 0; index < node.Children.Count; index++)
            {
                if (node.Children[index] is not XmlElementNode element || element.Tag != "rt")
                {
                    continue;
                }

                if (annotationNode != null)
                {
                    throw new InvalidCastException("<ruby> accepts exactly one <rt> element");
                }

                annotationNode = element;
                annotationIndex = index;
            }

            if (annotationNode == null)
            {
                throw new InvalidCastException("<ruby> requires one <rt> element");
            }

            for (var index = annotationIndex + 1; index < node.Children.Count; index++)
            {
                if (node.Children[index] is not XmlElementNode fallback || fallback.Tag != "rp")
                {
                    throw new InvalidCastException("only <rp> fallback content may follow <rt>");
                }

                ValidateRubyFallback(fallback);
            }

            var annotation = ReadRubyAnnotation(annotationNode);
            var rubyStyle = ParseRubyStyle(node);
            var startUtf16 = ctx.TextLengthUtf16;
            ctx.HasActiveRuby = true;
            try
            {
                for (var index = 0; index < annotationIndex; index++)
                {
                    if (node.Children[index] is XmlElementNode fallback && fallback.Tag == "rp")
                    {
                        ValidateRubyFallback(fallback);
                        continue;
                    }

                    ConvertChild(ctx, state, node.Children[index]);
                }
            }
            finally
            {
                ctx.HasActiveRuby = false;
            }

            if (startUtf16 == ctx.TextLengthUtf16)
            {
                throw new InvalidCastException("<ruby> requires non-empty base text");
            }

            ctx.Result.AddRuby(new RubyAnnotation(startUtf16, ctx.TextLengthUtf16, annotation, rubyStyle));
        }

        private static void ValidateRubyFallback(XmlElementNode node)
        {
            if (node.IsEmptyElement || node.Attributes.Count != 0 || node.Children.Count == 0)
            {
                throw new InvalidCastException("<rp> must be non-empty and does not accept attributes");
            }

            foreach (var child in node.Children)
            {
                if (child is not XmlTextNode)
                {
                    throw new InvalidCastException("<rp> only accepts plain text");
                }
            }
        }

        private void ConvertChild(Context ctx, TextStyleState state, XmlNode child)
        {
            if (child is XmlElementNode element)
            {
                ctx.TextStyleState = state;
                ConvertToSegments(ctx, element);
            }
            else if (child is XmlTextNode textNode)
            {
                AddText(ctx, state, textNode.Text);
            }
        }

        private static string ReadRubyAnnotation(XmlElementNode node)
        {
            if (node.IsEmptyElement || node.Attributes.Count != 0)
            {
                throw new InvalidCastException("<rt> must be non-empty and does not accept attributes");
            }

            var annotation = new StringBuilder();
            foreach (var child in node.Children)
            {
                if (child is not XmlTextNode textNode)
                {
                    throw new InvalidCastException("<rt> only accepts plain text");
                }

                annotation.Append(textNode.Text);
            }

            if (annotation.Length == 0 || annotation.ToString().IndexOfAny(new[] { '\r', '\n', '\t' }) >= 0)
            {
                throw new InvalidCastException("<rt> must contain non-empty single-line text without tabs");
            }

            return annotation.ToString();
        }

        private static RubyStyle ParseRubyStyle(XmlElementNode node)
        {
            foreach (var attribute in node.Attributes)
            {
                if (attribute.Key != "style")
                {
                    throw new InvalidCastException($"<ruby> does not accept the '{attribute.Key}' attribute");
                }
            }

            return RubyCssParser.Parse(node.Attributes.TryGetValue("style", out var declarations)
                ? declarations
                : string.Empty);
        }

        private static void ValidateAnchor(XmlElementNode node, out string href, out string id)
        {
            if (node.IsEmptyElement)
            {
                throw new InvalidCastException("<a> must use an explicit closing tag");
            }

            if (!node.Attributes.TryGetValue("href", out href) || string.IsNullOrEmpty(href))
            {
                throw new InvalidCastException("<a> requires a non-empty href attribute");
            }

            var hasId = node.Attributes.TryGetValue("id", out id);
            if (hasId && string.IsNullOrEmpty(id))
            {
                throw new InvalidCastException("<a> id must be non-empty when provided");
            }

            id = hasId ? id : "";
            var expectedAttributeCount = hasId ? 2 : 1;
            if (node.Attributes.Count != expectedAttributeCount)
            {
                throw new InvalidCastException("<a> only accepts href and optional id attributes");
            }
        }

        private static void StartParagraph(Context ctx, TextStyleState state)
        {
            if (ctx.Result.Body.Count > 0)
            {
                AddText(ctx, state, "\n\n");
            }
        }

        private static void AddText(Context ctx, TextStyleState state, string text)
        {
            ctx.Result.Body.Add(TextSegment.MakeText(state, text));
            ctx.TextLengthUtf16 += text.Length;
        }

        private static readonly CultureInfo ParseCulture = CultureInfo.GetCultureInfo("en");

        private static bool TryParseFloat(string value, out float result)
        {
            return float.TryParse(value, NumberStyles.Any, ParseCulture, out result);
        }

        private static bool TryParseFontSize(string value, out float result)
        {
            return TryParseFloat(value, out result) && result > 0;
        }

        private static bool TryParseFontWeight(string value, out int result)
        {
            return int.TryParse(value, NumberStyles.Integer, ParseCulture, out result)
                   && result > FontWeight.Invisible
                   && result <= FontWeight.ExtraBlack;
        }

        private static bool TryParseEnum<T>(string value, out T result)
            where T : struct
        {
            try
            {
                var parsed = (T)Enum.Parse(typeof(T), value, true);
                if (Enum.IsDefined(typeof(T), parsed))
                {
                    result = parsed;
                    return true;
                }
            }
            catch (Exception)
            {
                // ignored
            }

            result = default;
            return false;
        }

    }
}
