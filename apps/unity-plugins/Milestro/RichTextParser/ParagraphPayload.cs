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

        public ParagraphStyleState ParagraphStyle { get; set; } = new ParagraphStyleState();

        public List<TextSegment> Body { get; set; } = new List<TextSegment>();

        internal IReadOnlyList<LinkAnnotation> Links => links ?? EmptyLinks;

        internal IReadOnlyList<RubyAnnotation> RubyAnnotations => rubyAnnotations ?? EmptyRubyAnnotations;

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
            ret.Body.Add(TextSegment.MakeText(text));
            return ret;
        }
    }
}
