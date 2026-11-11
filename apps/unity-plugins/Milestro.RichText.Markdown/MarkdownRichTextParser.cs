#nullable enable
using System;
using System.Globalization;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Milestro.RichTextParser;
using MarkdownParagraphBlock = Markdig.Syntax.ParagraphBlock;

namespace Milestro.RichText.Markdown
{
    /// <summary>
    /// Parses CommonMark with Markdig's emphasis extras into Milestro paragraph payloads.
    /// </summary>
    public sealed class MarkdownRichTextParser : IRichTextParser
    {
        public const string Id = "markdown";
        public const string DisplayName = "Markdown";

        private static readonly MarkdownPipeline Pipeline =
            new MarkdownPipelineBuilder().UseEmphasisExtras().Build();

        public ParagraphPayload Parse(string source, RichTextParseContext context)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var document = Markdig.Markdown.Parse(source, Pipeline);
            var builder = new ParagraphPayloadBuilder();
            new PayloadRenderer(builder, source).Render(document);
            return builder.Build();
        }

        private sealed class PayloadRenderer
        {
            private static readonly float[] HeadingScales = { 1.6f, 1.45f, 1.3f, 1.2f, 1.1f, 1.05f };

            private readonly ParagraphPayloadBuilder builder;
            private readonly string source;

            internal PayloadRenderer(ParagraphPayloadBuilder builder, string source)
            {
                this.builder = builder;
                this.source = source;
            }

            internal void Render(MarkdownDocument document)
            {
                foreach (var block in document)
                {
                    RenderBlock(block, 0);
                }
            }

            private void RenderBlock(Block block, int indentLevel)
            {
                switch (block)
                {
                    case MarkdownParagraphBlock paragraph:
                        RenderLeafBlock(paragraph, indentLevel, null);
                        break;
                    case HeadingBlock heading:
                        RenderLeafBlock(heading, indentLevel, HeadingStyle(heading.Level));
                        break;
                    case CodeBlock code:
                        RenderLiteralBlock(code.Lines.ToString(), indentLevel);
                        break;
                    case HtmlBlock html:
                        RenderLiteralBlock(html.Lines.ToString(), indentLevel);
                        break;
                    case ThematicBreakBlock:
                    case BlankLineBlock:
                    case LinkReferenceDefinitionGroup:
                        break;
                    case ListBlock list:
                        RenderList(list, indentLevel);
                        break;
                    case QuoteBlock quote:
                        RenderChildren(quote, indentLevel + 1);
                        break;
                    case ContainerBlock container:
                        RenderChildren(container, indentLevel);
                        break;
                    case LeafBlock leaf:
                        RenderLeafBlock(leaf, indentLevel, null);
                        break;
                    default:
                        RenderLiteralBlock(SourceText(block.Span), indentLevel);
                        break;
                }
            }

            private void RenderChildren(ContainerBlock container, int indentLevel)
            {
                foreach (var child in container)
                {
                    RenderBlock(child, indentLevel);
                }
            }

            private void RenderLeafBlock(LeafBlock leaf, int indentLevel, TextStyleState? style)
            {
                builder.BeginBlock();
                AppendIndent(indentLevel);
                if (leaf.Inline != null)
                {
                    RenderInlines(leaf.Inline, style ?? new TextStyleState());
                }
                else
                {
                    builder.AppendText(leaf.Lines.ToString(), style);
                }
            }

            private void RenderLiteralBlock(string text, int indentLevel)
            {
                builder.BeginBlock();
                AppendIndent(indentLevel);
                builder.AppendText(text);
            }

            private void RenderList(ListBlock list, int indentLevel)
            {
                foreach (var child in list)
                {
                    if (child is ListItemBlock item)
                    {
                        RenderListItem(list, item, indentLevel);
                    }
                    else
                    {
                        RenderBlock(child, indentLevel);
                    }
                }
            }

            private void RenderListItem(ListBlock list, ListItemBlock item, int indentLevel)
            {
                var marker = list.IsOrdered
                    ? item.Order.ToString(CultureInfo.InvariantCulture) + ". "
                    : "• ";
                var markerPending = true;

                foreach (var child in item)
                {
                    if (child is MarkdownParagraphBlock || child is HeadingBlock)
                    {
                        var leaf = (LeafBlock)child;
                        var style = child is HeadingBlock heading ? HeadingStyle(heading.Level) : null;
                        builder.BeginBlock();
                        builder.AppendText(Indent(indentLevel) + (markerPending ? marker : ""));
                        markerPending = false;
                        RenderInlines(leaf.Inline, style ?? new TextStyleState());
                    }
                    else
                    {
                        AppendPendingListMarker(indentLevel, marker, ref markerPending);
                        RenderBlock(child, child is ListBlock ? indentLevel + 1 : indentLevel);
                    }
                }

                AppendPendingListMarker(indentLevel, marker, ref markerPending);
            }

            private void AppendPendingListMarker(int indentLevel, string marker, ref bool markerPending)
            {
                if (!markerPending)
                {
                    return;
                }

                builder.BeginBlock();
                builder.AppendText(Indent(indentLevel) + marker);
                markerPending = false;
            }

            private void RenderInlines(ContainerInline? container, TextStyleState style)
            {
                if (container == null)
                {
                    return;
                }

                for (var inline = container.FirstChild; inline != null; inline = inline.NextSibling)
                {
                    RenderInline(inline, style);
                }
            }

            private void RenderInline(Inline inline, TextStyleState style)
            {
                switch (inline)
                {
                    case LiteralInline literal:
                        builder.AppendText(literal.Content.ToString(), style);
                        break;
                    case CodeInline code:
                        builder.AppendText(code.Content, style);
                        break;
                    case HtmlInline html:
                        builder.AppendText(html.Tag, style);
                        break;
                    case HtmlEntityInline entity:
                        builder.AppendText(entity.Transcoded.ToString(), style);
                        break;
                    case LineBreakInline:
                        builder.AppendText("\n", style);
                        break;
                    case AutolinkInline autolink:
                        RenderAutolink(autolink, style);
                        break;
                    case LinkInline link:
                        RenderLink(link, style);
                        break;
                    case EmphasisInline emphasis:
                        RenderEmphasis(emphasis, style);
                        break;
                    case ContainerInline container:
                        RenderInlines(container, style);
                        break;
                    default:
                        builder.AppendText(SourceText(inline.Span), style);
                        break;
                }
            }

            private void RenderAutolink(AutolinkInline autolink, TextStyleState style)
            {
                var href = autolink.IsEmail ? "mailto:" + autolink.Url : autolink.Url;
                if (string.IsNullOrEmpty(href))
                {
                    builder.AppendText(autolink.Url, style);
                    return;
                }

                builder.BeginLink(href);
                builder.AppendText(autolink.Url, style);
                builder.EndLink();
            }

            private void RenderLink(LinkInline link, TextStyleState style)
            {
                if (link.IsImage)
                {
                    AppendImage(link, style);
                    return;
                }

                var url = link.Url;
                if (url == null || url.Length == 0)
                {
                    RenderInlines(link, style);
                    return;
                }

                builder.BeginLink(url);
                RenderInlines(link, style);
                builder.EndLink();
            }

            /// <summary>
            /// Markdown images have no intrinsic dimensions in the syntax, so v1 reserves a
            /// 16x16 box and intentionally discards alt text.
            /// </summary>
            private void AppendImage(LinkInline image, TextStyleState style)
            {
                builder.AppendPlaceholder(16f, 16f, image.Url ?? "", style);
            }

            private void RenderEmphasis(EmphasisInline emphasis, TextStyleState style)
            {
                var nested = style.Clone();
                if (emphasis.DelimiterChar == '*' || emphasis.DelimiterChar == '_')
                {
                    if (emphasis.DelimiterCount == 1 || emphasis.DelimiterCount >= 3)
                    {
                        nested.Italic = true;
                    }

                    if (emphasis.DelimiterCount >= 2)
                    {
                        nested.FontWeight = 700;
                    }
                }
                else if (emphasis.DelimiterChar == '~' && emphasis.DelimiterCount == 2)
                {
                    nested.Strikethrough = true;
                }

                RenderInlines(emphasis, nested);
            }

            private void AppendIndent(int indentLevel)
            {
                builder.AppendText(Indent(indentLevel));
            }

            private string SourceText(SourceSpan span)
            {
                if (span.Start < 0 || span.End < span.Start || span.End >= source.Length)
                {
                    return "";
                }

                return source.Substring(span.Start, span.End - span.Start + 1);
            }

            private static TextStyleState HeadingStyle(int level)
            {
                var index = Math.Max(0, Math.Min(HeadingScales.Length - 1, level - 1));
                return new TextStyleState
                {
                    FontWeight = 700,
                    FontSizeScale = HeadingScales[index],
                };
            }

            private static string Indent(int level) => new string(' ', level * 2);
        }
    }
}
