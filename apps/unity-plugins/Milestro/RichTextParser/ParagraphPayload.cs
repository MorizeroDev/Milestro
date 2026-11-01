using System;
using System.Collections.Generic;
using System.Text;
using Milestro.Skia.TextLayout;

namespace Milestro.RichTextParser
{
    internal readonly struct TateChuYokoAnnotation
    {
        internal TateChuYokoAnnotation(int startUtf16, int endUtf16,
            TateChuYokoSizing sizing = TateChuYokoSizing.Squeeze)
        {
            StartUtf16 = startUtf16;
            EndUtf16 = endUtf16;
            Sizing = sizing;
        }

        internal int StartUtf16 { get; }
        internal int EndUtf16 { get; }
        internal TateChuYokoSizing Sizing { get; }
    }

    public class ParagraphPayload
    {
        private static readonly IReadOnlyList<LinkAnnotation> EmptyLinks = Array.Empty<LinkAnnotation>();
        private static readonly IReadOnlyList<RubyAnnotation> EmptyRubyAnnotations = Array.Empty<RubyAnnotation>();
        private static readonly IReadOnlyList<TateChuYokoAnnotation> EmptyTateChuYokoAnnotations =
            Array.Empty<TateChuYokoAnnotation>();
        private List<LinkAnnotation>? links;
        private List<RubyAnnotation>? rubyAnnotations;
        private List<TateChuYokoAnnotation>? tateChuYokoAnnotations;
        private List<ParagraphBlock>? blocks;
        private readonly List<PlaceholderAnnotation> placeholders = new List<PlaceholderAnnotation>();
        public IReadOnlyList<PlaceholderAnnotation> Placeholders => placeholders.AsReadOnly();

        internal void AddPlaceholder(PlaceholderAnnotation annotation) => placeholders.Add(annotation);

        internal string GetPlainText(ParagraphBlock block)
        {
            var text = new StringBuilder();
            foreach (var segment in block.Segments)
            {
                text.Append(segment.Content);
            }
            foreach (var placeholder in placeholders)
            {
                if (block.ContainsRange(placeholder.StartUtf16, placeholder.EndUtf16))
                {
                    text.Insert(placeholder.StartUtf16 - block.StartUtf16, '\uFFFC');
                }
            }
            return text.ToString();
        }

        public ParagraphStyleState ParagraphStyle { get; set; } = new ParagraphStyleState();

        /// <summary>
        /// Block-level paragraphs in document order. A payload always contains at least one
        /// block; leading text that is not wrapped in a &lt;p&gt; element forms an anonymous
        /// block. Blocks join with no implicit separator: link and ruby annotation ranges
        /// index into the concatenation of all block texts.
        /// </summary>
        public IReadOnlyList<ParagraphBlock> Blocks => blocks ??= new List<ParagraphBlock> { new ParagraphBlock() };

        /// <summary>
        /// Read-only flattened snapshot of every block's segments, kept for single-paragraph
        /// consumers. Paragraph structure and content mutations belong on <see cref="Blocks"/>
        /// and each block's <see cref="ParagraphBlock.Segments"/> collection.
        /// </summary>
        public IReadOnlyList<TextSegment> Body
        {
            get
            {
                var all = new List<TextSegment>();
                foreach (var block in Blocks)
                {
                    all.AddRange(block.Segments);
                }
                return all;
            }
        }

        internal IReadOnlyList<LinkAnnotation> Links => links ?? EmptyLinks;

        internal IReadOnlyList<RubyAnnotation> RubyAnnotations => rubyAnnotations ?? EmptyRubyAnnotations;

        internal IReadOnlyList<TateChuYokoAnnotation> TateChuYokoAnnotations =>
            tateChuYokoAnnotations ?? EmptyTateChuYokoAnnotations;

        internal ParagraphBlock AddBlock(bool isExplicit, int startUtf16)
        {
            var list = blocks ??= new List<ParagraphBlock> { new ParagraphBlock() };
            if (list.Count == 1 && !list[0].IsExplicit && list[0].LengthUtf16 == 0)
            {
                if (isExplicit)
                {
                    list[0].IsExplicit = true;
                }
                return list[0];
            }

            var block = new ParagraphBlock
            {
                IsExplicit = isExplicit,
                StartUtf16 = startUtf16,
            };
            list.Add(block);
            return block;
        }

        internal void AddLink(LinkAnnotation link)
        {
            links ??= new List<LinkAnnotation>();
            links.Add(link);
        }

        internal void AddRuby(RubyAnnotation annotation)
        {
            rubyAnnotations ??= new List<RubyAnnotation>();
            rubyAnnotations.Add(annotation);
        }

        internal void AddTateChuYoko(TateChuYokoAnnotation annotation)
        {
            tateChuYokoAnnotations ??= new List<TateChuYokoAnnotation>();
            tateChuYokoAnnotations.Add(annotation);
        }

        public static ParagraphPayload MakeText(string text)
        {
            var ret = new ParagraphPayload();
            var block = ret.AddBlock(false, 0);
            block.Segments.Add(TextSegment.MakeText(text));
            block.LengthUtf16 = text.Length;
            return ret;
        }
    }
}
