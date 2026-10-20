#nullable enable
using System.Collections.Generic;

namespace Milestro.RichTextParser
{
    /// <summary>
    /// One block-level paragraph inside a <see cref="ParagraphPayload"/>. A block owns an
    /// independent paragraph style (alignment, direction, text indent) and an optional
    /// bottom margin. Blocks stack vertically when the payload is laid out.
    /// </summary>
    public sealed class ParagraphBlock
    {
        public ParagraphStyleState Style { get; } = new ParagraphStyleState();

        public List<TextSegment> Segments { get; } = new List<TextSegment>();

        internal int StartUtf16 { get; set; }

        internal int LengthUtf16 { get; set; }

        internal bool IsExplicit { get; set; }

        internal int EndUtf16 => StartUtf16 + LengthUtf16;

        internal bool ContainsRange(int startUtf16, int endUtf16)
        {
            return startUtf16 >= StartUtf16 && endUtf16 <= EndUtf16;
        }
    }
}
