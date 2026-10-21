#nullable enable
using Milestro.Model;
using Milestro.Skia.TextLayout;

namespace Milestro.RichTextParser
{
    public class ParagraphStyleState
    {
        public TextAlign? TextAlign { get; set; } = null;

        public TextDirection? TextDirection { get; set; } = null;

        public TextIndent? TextIndent { get; set; } = null;

        public float? TextIndentEm { get; set; } = null;

        internal float? TextIndentFontSize { get; set; }

        internal float TextIndentFontSizeScale { get; set; } = 1f;

        /// <summary>
        /// Block top margin applied when the payload stacks multiple paragraphs.
        /// Adjacent vertical block margins collapse: the larger one wins.
        /// </summary>
        public MarginValue MarginTop { get; set; } = new MarginValue();

        /// <summary>
        /// Block bottom margin applied when the payload stacks multiple paragraphs.
        /// Adjacent vertical block margins collapse: the larger one wins.
        /// </summary>
        public MarginValue MarginBottom { get; set; } = new MarginValue();
    }
}
