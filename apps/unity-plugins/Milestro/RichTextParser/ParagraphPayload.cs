using System;
using System.Collections.Generic;

namespace Milestro.RichTextParser
{
    public class ParagraphPayload
    {
        private static readonly IReadOnlyList<LinkAnnotation> EmptyLinks = Array.Empty<LinkAnnotation>();
        private List<LinkAnnotation>? links;

        public ParagraphStyleState ParagraphStyle { get; set; } = new ParagraphStyleState();

        public List<TextSegment> Body { get; set; } = new List<TextSegment>();

        internal IReadOnlyList<LinkAnnotation> Links => links ?? EmptyLinks;

        internal void AddLink(LinkAnnotation link)
        {
            links ??= new List<LinkAnnotation>();
            links.Add(link);
        }

        public static ParagraphPayload MakeText(string text)
        {
            var ret = new ParagraphPayload();
            ret.Body.Add(TextSegment.MakeText(text));
            return ret;
        }
    }
}
