namespace Milestro.RichTextParser
{
    public class Context
    {
        public TextStyleState TextStyleState { get; set; } = new TextStyleState();

        public ParagraphPayload Result { get; set; } = new ParagraphPayload();
    }
}