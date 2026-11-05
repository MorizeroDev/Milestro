using Milestro.RichTextParser;

namespace Milestro.RichText.TMPro
{
    public sealed class TMProRichTextParserProvider : IRichTextParserProvider
    {
        public string Id => TMProRichTextParser.Id;

        public string DisplayName => TMProRichTextParser.DisplayName;

        public IRichTextParser CreateParser() => new TMProRichTextParser();
    }
}
