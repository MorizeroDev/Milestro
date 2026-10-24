using System;
using System.Collections.Generic;

namespace Milestro.RichTextParser
{
    public class ParagraphPayload
    {
        private static readonly IReadOnlyList<LinkAnnotation> EmptyLinks = Array.Empty<LinkAnnotation>();
        private static readonly IReadOnlyList<RubyAnnotation> EmptyRubyAnnotations = Array.Empty<RubyAnnotation>();
        private List<LinkAnnotation>? links;
        private List<RubyAnnotation>? rubyAnnotations;
        private List<ParagraphBlock>? blocks;

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

        internal ParagraphBlock AddBlock(bool isExplicit, int startUtf16)
        {
            var list = blocks ??= new List<ParagraphBlock> { new ParagraphBlock() };
            if (list.Count == 1 && !list[0].IsExplicit && list[0].Segments.Count == 0)
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
