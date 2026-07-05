using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using Milestro.Model;
using Milestro.RichTextParser.AST;
using Paraparty.Colors;
using UnityEngine;
using XmlNode = Milestro.RichTextParser.AST.XmlNode;

namespace Milestro.RichTextParser
{
    public class RichTextParser
    {
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
            var node = new XmlElementNode(tagName, attributes);

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
                    && TryParseFontFamilies(face, out var fontFamilies))
                {
                    state.FontFamilies = fontFamilies;
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

                if (node.Attributes.TryGetValue("lang", out var lang) && !string.IsNullOrWhiteSpace(lang))
                {
                    state.Locale = lang;
                }

                if (node.Attributes.TryGetValue("face", out var face)
                    && TryParseFontFamilies(face, out var fontFamilies))
                {
                    state.FontFamilies = fontFamilies;
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
            }
            else if (node.Tag == "br")
            {
                AddText(ctx, state, "\n");
                return;
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
                if (child is XmlElementNode t)
                {
                    ctx.TextStyleState = state;
                    ConvertToSegments(ctx, t);
                }
                else if (child is XmlTextNode textNode)
                {
                    AddText(ctx, state, textNode.Text);
                }
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

        private static bool TryParseFontFamilies(string value, out List<string> fontFamilies)
        {
            fontFamilies = new List<string>();

            foreach (var item in value.Split(','))
            {
                var family = item.Trim();
                if (family.Length >= 2
                    && ((family[0] == '"' && family[family.Length - 1] == '"')
                        || (family[0] == '\'' && family[family.Length - 1] == '\'')))
                {
                    family = family.Substring(1, family.Length - 2).Trim();
                }

                if (family.Length > 0)
                {
                    fontFamilies.Add(family);
                }
            }

            return fontFamilies.Count > 0;
        }
    }
}
