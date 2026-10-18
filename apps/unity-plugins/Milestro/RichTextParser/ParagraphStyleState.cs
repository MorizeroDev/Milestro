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
    }
}
