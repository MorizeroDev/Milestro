using Milestro.RichTextParser;

namespace Milestro.RichText.Markdown
{
    public sealed class MarkdownRichTextParserProvider : IRichTextParserProvider
    {
        public string Id => MarkdownRichTextParser.Id;

        public string DisplayName => MarkdownRichTextParser.DisplayName;

        public IRichTextParser CreateParser() => new MarkdownRichTextParser();
    }
}
