namespace Milestro.RichTextParser
{
    public class Context
    {
        public TextStyleState TextStyleState { get; set; } = new TextStyleState();

        public ParagraphPayload Result { get; set; } = new ParagraphPayload();

        internal int TextLengthUtf16 { get; set; }

        internal ParagraphBlock? CurrentBlock { get; set; }

        internal bool HasActiveLink { get; set; }

        internal bool HasActiveRuby { get; set; }
    }
}
