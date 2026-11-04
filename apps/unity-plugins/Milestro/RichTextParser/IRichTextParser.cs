namespace Milestro.RichTextParser
{
    /// <summary>
    /// Parses rich text source into a <see cref="ParagraphPayload"/>. Implementations must be
    /// stateless across calls: one instance may be shared by many text boxes and reused for
    /// every reparse. Expose implementations through an <see cref="IRichTextParserProvider"/>
    /// registered with <see cref="RichTextParserService"/>; text boxes select the parser by
    /// id, and an empty id renders
    /// the content literally.
    /// </summary>
    public interface IRichTextParser
    {
        ParagraphPayload Parse(string source, RichTextParseContext context);
    }
}
