using System.Collections.Generic;

namespace Milestro.RichTextParser
{
    public class ParagraphPayload
    {
        public ParagraphStyleState ParagraphStyle { get; set; } = new ParagraphStyleState();

        public List<TextSegment> Body { get; set; } = new List<TextSegment>();

        public static ParagraphPayload MakeText(string text)
        {
            var ret = new ParagraphPayload();
            ret.Body.Add(TextSegment.MakeText(text));
            return ret;
        }
    }
}
