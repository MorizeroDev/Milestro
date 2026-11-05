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
using Milestro.Unicode;
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

        public const float SmallCapsFontSizeScale = 0.8f;

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
            ParseText(str, true);
        }

        public void ParseText(string str, bool enableRichText)
        {
            str ??= "";
            if (!enableRichText || (str.IndexOf('<') < 0 && str.IndexOf('&') < 0))
            {
                RootNode = new XmlTextNode(str);
                return;
            }

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
            return ConvertToSegments(allowViewportBlockMargins: false);
        }

        internal ParagraphPayload ConvertToSegments(bool allowViewportBlockMargins,
            ICollection<string>? issues = null,
            string locale = "en")
        {
            var rootNode = RootNode;
            if (rootNode is XmlTextNode textNode)
            {
                return ParagraphPayload.MakeText(textNode.Text);
            }

            if (rootNode is XmlElementNode root)
            {
                var ctx = new Context
                {
                    AllowViewportBlockMargins = allowViewportBlockMargins,
                    Issues = issues,
                    Locale = locale,
                };
                ConvertToSegments(ctx, root);
                CollectTateChuYokoRanges(ctx.Result);
                CollectOrientationRanges(ctx.Result);
                CollectNoWrapRanges(ctx.Result);
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
            if (node.Tag == "img")
            {
                if (ctx.HasActiveRuby)
                {
                    Debug.LogError("<img> inside <ruby> is ignored");
                    return;
                }
                if (!node.IsEmptyElement || node.Children.Count != 0)
                    throw new InvalidCastException("<img> must be an empty element");
                foreach (var attribute in node.Attributes)
                {
                    if (attribute.Key != "width" && attribute.Key != "height" && attribute.Key != "key" &&
                        attribute.Key != "id")
                        throw new InvalidCastException($"<img> does not accept the '{attribute.Key}' attribute");
                }
                var width = ReadPlaceholderDimension(node, "width");
                var height = ReadPlaceholderDimension(node, "height");
                var block = ctx.CurrentBlock ?? ctx.Result.AddBlock(false, ctx.TextLengthUtf16);
                ctx.CurrentBlock = block;
                ctx.Result.AddPlaceholder(new PlaceholderAnnotation(ctx.TextLengthUtf16, width, height,
                    node.Attributes.TryGetValue("key", out var key) ? key : "", state));
                ++block.LengthUtf16;
                ++ctx.TextLengthUtf16;
                return;
            }
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
            else if (node.Tag == "mark")
            {
                state.BackgroundColor = new Color32(255, 255, 0, 255);
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

                if (node.Attributes.TryGetValue("lheight", out var lineHeight)
                    && TryParseFloat(lineHeight, out var lineHeightValue)
                    && lineHeightValue > 0)
                {
                    state.LineHeight = lineHeightValue;
                }

                if (node.Attributes.TryGetValue("alpha", out var alpha)
                    && TryParseFloat(alpha, out var alphaValue)
                    && alphaValue >= 0 && alphaValue <= 1)
                {
                    state.Alpha = alphaValue;
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

            }
            else if (node.Tag == "p")
            {
                if (ctx.HasActiveRuby)
                {
                    Debug.LogError("<p> is not allowed inside <ruby>; the <p> element is ignored");
                    foreach (var child in node.Children)
                    {
                        ConvertChild(ctx, state, child);
                    }
                    return;
                }

                // Like HTML, a <p> start tag implicitly closes any open paragraph: each
                // <p> simply starts a fresh block. <a> is transparent and may wrap <p>.
                var block = ctx.Result.AddBlock(true, ctx.TextLengthUtf16);
                ctx.CurrentBlock = block;

                if (node.Attributes.TryGetValue("align", out var alignDirection))
                {
                    if (TryParseEnum(alignDirection, out TextAlign textAlign))
                    {
                        block.Style.TextAlign = textAlign;
                    }
                }

                if (node.Attributes.TryGetValue("dir", out var textDirection)
                    && TryParseEnum(textDirection, out TextDirection direction))
                {
                    block.Style.TextDirection = direction;
                }

                if (node.Attributes.TryGetValue("style", out var paragraphDeclarations))
                {
                    CssParser.Apply(state, paragraphDeclarations, block.Style,
                        ctx.AllowViewportBlockMargins, ctx.Issues);
                }

                foreach (var child in node.Children)
                {
                    ConvertChild(ctx, state, child);
                }

                ctx.CurrentBlock = null;
                return;
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

            // The <p> branch already applied its style attribute (with the
            // paragraph-level argument); applying it again here would duplicate
            // feature operations and diagnostic entries.
            if (node.Tag != "p" && node.Attributes.TryGetValue("style", out var inlineDeclarations))
            {
                CssParser.Apply(state, inlineDeclarations, issues: ctx.Issues);
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

        private static void CollectTateChuYokoRanges(ParagraphPayload payload)
        {
            foreach (var block in payload.Blocks)
            {
                if (block.LengthUtf16 <= 0) continue;

                var groupable = new bool[block.LengthUtf16];
                var combine = new bool[block.LengthUtf16];
                var sizing = new TateChuYokoSizing[block.LengthUtf16];
                var placeholders = new List<int>();
                foreach (var placeholder in payload.Placeholders)
                {
                    if (block.ContainsRange(placeholder.StartUtf16, placeholder.EndUtf16))
                    {
                        placeholders.Add(placeholder.StartUtf16);
                    }
                }

                placeholders.Sort();
                var nextPlaceholder = 0;
                var cursor = block.StartUtf16;

                void ConsumePlaceholders()
                {
                    while (nextPlaceholder < placeholders.Count && placeholders[nextPlaceholder] == cursor)
                    {
                        ++cursor;
                        ++nextPlaceholder;
                    }
                }

                foreach (var segment in block.Segments)
                {
                    var content = segment.Content ?? string.Empty;
                    var isAll = segment.TextStyle?.TextCombineUprightAll == true;
                    var segmentSizing = segment.TextStyle?.TextCombineUprightSizing ??
                                        TateChuYokoSizing.Squeeze;
                    var offset = 0;
                    while (offset < content.Length)
                    {
                        ConsumePlaceholders();
                        var position = cursor - block.StartUtf16;
                        if (position < 0 || position >= block.LengthUtf16)
                        {
                            throw new InvalidOperationException("Invalid tate-chu-yoko source position");
                        }

                        var character = content[offset];
                        groupable[position] = character != '\n' && character != '\r';
                        combine[position] = isAll;
                        sizing[position] = segmentSizing;
                        ++cursor;
                        ++offset;
                    }
                }

                ConsumePlaceholders();

                foreach (var ruby in payload.RubyAnnotations)
                {
                    if (!block.ContainsRange(ruby.StartUtf16, ruby.EndUtf16)) continue;
                    var start = ruby.StartUtf16 - block.StartUtf16;
                    var end = ruby.EndUtf16 - block.StartUtf16;
                    for (var index = start; index < end; ++index)
                    {
                        groupable[index] = false;
                    }
                }

                var groupStart = -1;
                var groupSizing = TateChuYokoSizing.Squeeze;
                for (var index = 0; index <= groupable.Length; ++index)
                {
                    var active = index < groupable.Length && groupable[index] && combine[index];
                    if (active && groupStart >= 0 && sizing[index] != groupSizing)
                    {
                        payload.AddTateChuYoko(new TateChuYokoAnnotation(
                            block.StartUtf16 + groupStart, block.StartUtf16 + index, groupSizing));
                        groupStart = -1;
                    }

                    if (active)
                    {
                        if (groupStart < 0)
                        {
                            groupStart = index;
                            groupSizing = sizing[index];
                        }
                        continue;
                    }

                    if (groupStart >= 0)
                    {
                        payload.AddTateChuYoko(new TateChuYokoAnnotation(
                            block.StartUtf16 + groupStart, block.StartUtf16 + index, groupSizing));
                        groupStart = -1;
                    }
                }
            }
        }

        private static void CollectOrientationRanges(ParagraphPayload payload)
        {
            foreach (var block in payload.Blocks)
            {
                if (block.LengthUtf16 <= 0) continue;

                var orientation = new VerticalTextOrientation[block.LengthUtf16];
                var active = new bool[block.LengthUtf16];
                var placeholders = new List<int>();
                foreach (var placeholder in payload.Placeholders)
                {
                    if (block.ContainsRange(placeholder.StartUtf16, placeholder.EndUtf16))
                    {
                        placeholders.Add(placeholder.StartUtf16);
                    }
                }

                placeholders.Sort();
                var nextPlaceholder = 0;
                var cursor = block.StartUtf16;

                void ConsumePlaceholders()
                {
                    while (nextPlaceholder < placeholders.Count &&
                           placeholders[nextPlaceholder] == cursor)
                    {
                        ++cursor;
                        ++nextPlaceholder;
                    }
                }

                foreach (var segment in block.Segments)
                {
                    var content = segment.Content ?? string.Empty;
                    var segmentOrientation = segment.TextStyle?.TextOrientation ??
                                             VerticalTextOrientation.Mixed;
                    var offset = 0;
                    while (offset < content.Length)
                    {
                        ConsumePlaceholders();
                        var position = cursor - block.StartUtf16;
                        if (position < 0 || position >= block.LengthUtf16)
                        {
                            throw new InvalidOperationException("Invalid text-orientation source position");
                        }

                        active[position] = true;
                        orientation[position] = segmentOrientation;
                        ++cursor;
                        ++offset;
                    }
                }

                ConsumePlaceholders();

                var groupStart = -1;
                var groupOrientation = VerticalTextOrientation.Mixed;
                for (var index = 0; index <= active.Length; ++index)
                {
                    var participates = index < active.Length && active[index] &&
                                       orientation[index] != VerticalTextOrientation.Mixed;
                    if (participates && groupStart >= 0 && orientation[index] == groupOrientation)
                    {
                        continue;
                    }

                    if (groupStart >= 0)
                    {
                        payload.AddOrientation(new OrientationAnnotation(
                            block.StartUtf16 + groupStart, block.StartUtf16 + index, groupOrientation));
                        groupStart = -1;
                    }

                    if (participates)
                    {
                        groupStart = index;
                        groupOrientation = orientation[index];
                    }
                }
            }
        }

        private static void CollectNoWrapRanges(ParagraphPayload payload)
        {
            foreach (var block in payload.Blocks)
            {
                if (block.LengthUtf16 <= 0) continue;

                var noWrap = new bool[block.LengthUtf16];
                var placeholders = new List<PlaceholderAnnotation>();
                foreach (var placeholder in payload.Placeholders)
                {
                    if (block.ContainsRange(placeholder.StartUtf16, placeholder.EndUtf16))
                    {
                        placeholders.Add(placeholder);
                    }
                }

                placeholders.Sort((left, right) => left.StartUtf16.CompareTo(right.StartUtf16));
                var nextPlaceholder = 0;
                var cursor = block.StartUtf16;

                void ConsumePlaceholders()
                {
                    while (nextPlaceholder < placeholders.Count &&
                           placeholders[nextPlaceholder].StartUtf16 == cursor)
                    {
                        noWrap[cursor - block.StartUtf16] = placeholders[nextPlaceholder].TextStyle.NoWrap;
                        ++cursor;
                        ++nextPlaceholder;
                    }
                }

                foreach (var segment in block.Segments)
                {
                    var content = segment.Content ?? string.Empty;
                    var segmentNoWrap = segment.TextStyle?.NoWrap == true;
                    var offset = 0;
                    while (offset < content.Length)
                    {
                        ConsumePlaceholders();
                        var position = cursor - block.StartUtf16;
                        if (position < 0 || position >= block.LengthUtf16)
                        {
                            throw new InvalidOperationException("Invalid white-space source position");
                        }

                        noWrap[position] = segmentNoWrap;
                        ++cursor;
                        ++offset;
                    }
                }

                ConsumePlaceholders();

                var groupStart = -1;
                for (var index = 0; index <= noWrap.Length; ++index)
                {
                    var active = index < noWrap.Length && noWrap[index];
                    if (active && groupStart < 0)
                    {
                        groupStart = index;
                    }
                    else if (!active && groupStart >= 0)
                    {
                        payload.AddNoWrap(new NoWrapAnnotation(
                            block.StartUtf16 + groupStart, block.StartUtf16 + index));
                        groupStart = -1;
                    }
                }
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
                var elementOccurrence = BeginElement(ctx, element);
                try
                {
                    ConvertToSegments(ctx, element);
                }
                finally
                {
                    if (elementOccurrence >= 0)
                    {
                        EndElement(ctx, elementOccurrence);
                    }
                }
            }
            else if (child is XmlTextNode textNode)
            {
                AddText(ctx, state, textNode.Text);
            }
        }

        private static int BeginElement(Context ctx, XmlElementNode node)
        {
            if (!node.Attributes.TryGetValue("id", out var id))
            {
                return -1;
            }

            if (string.IsNullOrEmpty(id))
            {
                throw new InvalidCastException($"<{node.Tag}> id must be non-empty when provided");
            }

            return ctx.Result.BeginElement(id, ctx.TextLengthUtf16);
        }

        private static void EndElement(Context ctx, int occurrence)
        {
            var element = ctx.Result.Elements[occurrence];
            var blockIndex = -1;
            if (element.StartUtf16 == ctx.TextLengthUtf16)
            {
                if (ctx.CurrentBlock != null)
                {
                    for (var index = 0; index < ctx.Result.Blocks.Count; ++index)
                    {
                        if (ReferenceEquals(ctx.Result.Blocks[index], ctx.CurrentBlock))
                        {
                            blockIndex = index;
                            break;
                        }
                    }
                }
                else
                {
                    for (var index = ctx.Result.Blocks.Count - 1; index >= 0; --index)
                    {
                        if (ctx.Result.Blocks[index].StartUtf16 == element.StartUtf16)
                        {
                            blockIndex = index;
                            break;
                        }
                    }
                }
            }

            ctx.Result.EndElement(occurrence, ctx.TextLengthUtf16, blockIndex);
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
                if (attribute.Key != "style" && attribute.Key != "id")
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
            var hasStyle = node.Attributes.ContainsKey("style");
            var expectedAttributeCount = 1 + (hasId ? 1 : 0) + (hasStyle ? 1 : 0);
            if (node.Attributes.Count != expectedAttributeCount)
            {
                throw new InvalidCastException(
                    "<a> only accepts href and optional id and style attributes");
            }
        }

        private static void AddText(Context ctx, TextStyleState state, string text)
        {
            var block = ctx.CurrentBlock ?? ctx.Result.AddBlock(false, ctx.TextLengthUtf16);
            ctx.CurrentBlock = block;

            if (state.TextTransform == TextTransform.SmallCaps && text.Length > 0)
            {
                var runStart = 0;
                while (runStart < text.Length)
                {
                    var isLowercase = char.IsLower(text[runStart]);
                    var runEnd = runStart + 1;
                    while (runEnd < text.Length && char.IsLower(text[runEnd]) == isLowercase)
                    {
                        runEnd++;
                    }

                    var runText = text.Substring(runStart, runEnd - runStart);
                    var runState = state;
                    if (isLowercase)
                    {
                        runText = TransformCase(runText, TextTransform.Uppercase,
                            state.Locale ?? ctx.Locale);
                        runState = state.Clone();
                        runState.IsSmallCapsLowercase = true;
                    }

                    AddTextSegment(ctx, block, runState, runText);
                    runStart = runEnd;
                }

                return;
            }

            if (state.TextTransform == TextTransform.Lowercase ||
                state.TextTransform == TextTransform.Uppercase)
            {
                text = TransformCase(text, state.TextTransform, state.Locale ?? ctx.Locale);
            }

            AddTextSegment(ctx, block, state, text);
        }

        private static void AddTextSegment(Context ctx, ParagraphBlock block, TextStyleState state,
            string text)
        {
            block.Segments.Add(TextSegment.MakeText(state, text));
            block.LengthUtf16 += text.Length;
            ctx.TextLengthUtf16 += text.Length;
        }

        private static string TransformCase(string text, TextTransform textTransform, string locale)
        {
            return textTransform == TextTransform.Uppercase
                ? text.IcuToUpper(locale)
                : text.IcuToLower(locale);
        }

        private static readonly CultureInfo ParseCulture = CultureInfo.GetCultureInfo("en");

        private static float ReadPlaceholderDimension(XmlElementNode node, string name)
        {
            if (!node.Attributes.TryGetValue(name, out var text) ||
                !float.TryParse(text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture, out var value) || !float.IsFinite(value) || value <= 0)
                throw new InvalidCastException($"<img> requires a positive finite decimal {name}");
            return value;
        }

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
