using System;

namespace Milestro.RichTextParser
{
    /// <summary>
    /// <see cref="IRichTextParser"/> for Milestro's built-in XML-like markup.
    /// </summary>
    [Serializable]
    public sealed class XmlRichTextParser : IRichTextParser
    {
        /// <summary>
        /// Id this parser is registered under in <see cref="RichTextParserService"/>.
        /// </summary>
        public const string Id = "milestro_rich_text";

        /// <summary>
        /// Inspector display name shown for <see cref="Id"/>.
        /// </summary>
        public const string DisplayName = "Milestro Rich Text";

        public ParagraphPayload Parse(string source, RichTextParseContext context)
        {
            var parser = new RichTextParser();
            parser.ParseText(source);
            return parser.ConvertToSegments(context.AllowViewportBlockMargins, context.Issues, context.Locale);
        }
    }
}
