using System.Linq;
using Milestro.RichText.Markdown;
using Milestro.RichTextParser;
using NUnit.Framework;

namespace Milestro.Tests
{
    public class MarkdownRichTextParserTests
    {
        [Test]
        public void ProviderRegistersMarkdownForEditorUse()
        {
            var provider = new MarkdownRichTextParserProvider();

            Assert.That(provider.Id, Is.EqualTo("markdown"));
            Assert.That(provider.DisplayName, Is.EqualTo("Markdown"));
            Assert.That(provider.CreateParser(), Is.TypeOf<MarkdownRichTextParser>());
            Assert.That(RichTextParserService.TryCreate(MarkdownRichTextParser.Id, out var registered), Is.True);
            Assert.That(registered, Is.TypeOf<MarkdownRichTextParser>());
        }

        [Test]
        public void EmptyInputProducesOneEmptyBlock()
        {
            var payload = Parse("");

            Assert.That(payload.Blocks.Count, Is.EqualTo(1));
            Assert.That(payload.GetPlainText(payload.Blocks[0]), Is.Empty);
            Assert.That(payload.Body, Is.Empty);
            Assert.That(payload.Links, Is.Empty);
            Assert.That(payload.Placeholders, Is.Empty);
        }

        [Test]
        public void ParagraphsAndHeadingsCreateIndependentlyStyledBlocks()
        {
            var payload = Parse("paragraph\n\n# h1\n\n## h2\n\n### h3\n\n#### h4\n\n##### h5\n\n###### h6");
            var expectedScales = new[] { 1.6f, 1.45f, 1.3f, 1.2f, 1.1f, 1.05f };

            Assert.That(PlainTexts(payload), Is.EqualTo(new[]
            {
                "paragraph", "h1", "h2", "h3", "h4", "h5", "h6",
            }));
            for (var index = 0; index < expectedScales.Length; index++)
            {
                var style = payload.Blocks[index + 1].Segments.Single().TextStyle;
                Assert.That(style.FontWeight, Is.EqualTo(700));
                Assert.That(style.FontSizeScale, Is.EqualTo(expectedScales[index]));
            }
        }

        [Test]
        public void EmphasisStrongDeleteAndNestedStylesAreCombined()
        {
            var payload = Parse("*em* **strong** ~~del~~ ***both***");

            Assert.That(Segment(payload, "em").TextStyle.Italic, Is.True);
            Assert.That(Segment(payload, "strong").TextStyle.FontWeight, Is.EqualTo(700));
            Assert.That(Segment(payload, "del").TextStyle.Strikethrough, Is.True);
            var both = Segment(payload, "both").TextStyle;
            Assert.That(both.Italic, Is.True);
            Assert.That(both.FontWeight, Is.EqualTo(700));
        }

        [Test]
        public void LinksAndAutolinksPublishVisibleTextAndRanges()
        {
            var payload = Parse("[text](https://example.com) <https://example.org> <a@b.com>");
            var secondStart = "text ".Length;
            var thirdStart = secondStart + "https://example.org ".Length;

            Assert.That(payload.GetPlainText(payload.Blocks[0]),
                Is.EqualTo("text https://example.org a@b.com"));
            Assert.That(payload.Links.Select(link => link.Href), Is.EqualTo(new[]
            {
                "https://example.com", "https://example.org", "mailto:a@b.com",
            }));
            Assert.That(payload.Links.Select(link => (link.StartUtf16, link.EndUtf16)).ToArray(), Is.EqualTo(new[]
            {
                (0, "text".Length),
                (secondStart, secondStart + "https://example.org".Length),
                (thirdStart, thirdStart + "a@b.com".Length),
            }));
        }

        [Test]
        public void ImagesBecomeDefaultPlaceholdersAndDiscardAltText()
        {
            var payload = Parse("before ![ignored](image.png) after");

            Assert.That(payload.GetPlainText(payload.Blocks[0]), Is.EqualTo("before \uFFFC after"));
            Assert.That(payload.Links, Is.Empty);
            Assert.That(payload.Placeholders.Count, Is.EqualTo(1));
            var placeholder = payload.Placeholders[0];
            Assert.That(placeholder.Width, Is.EqualTo(16f));
            Assert.That(placeholder.Height, Is.EqualTo(16f));
            Assert.That(placeholder.Key, Is.EqualTo("image.png"));
            Assert.That(placeholder.StartUtf16, Is.EqualTo("before ".Length));
        }

        [Test]
        public void InlineAndBlockCodeAreOrdinaryUnstyledText()
        {
            var payload = Parse("inline `x < y`\n\n```csharp\na < b\n```\n\n    second\n    line");

            Assert.That(PlainTexts(payload), Is.EqualTo(new[]
            {
                "inline x < y", "a < b", "second\nline",
            }));
            var inlineCode = Segment(payload, "x < y").TextStyle;
            Assert.That(inlineCode.FontWeight, Is.Null);
            Assert.That(inlineCode.Italic, Is.False);
            Assert.That(inlineCode.FontSizeScale, Is.Null);
            Assert.That(payload.Blocks[1].Segments.Single().TextStyle.FontWeight, Is.Null);
            Assert.That(payload.Blocks[2].Segments.Single().TextStyle.FontWeight, Is.Null);
        }

        [Test]
        public void ListsUseMarkersRealNumbersAndNestedIndentation()
        {
            var unordered = Parse("- one\n- two\n  - nested");
            var ordered = Parse("3. three\n4. four");

            Assert.That(PlainTexts(unordered), Is.EqualTo(new[]
            {
                "• one", "• two", "  • nested",
            }));
            Assert.That(PlainTexts(ordered), Is.EqualTo(new[] { "3. three", "4. four" }));
        }

        [Test]
        public void BlockquotesCreateBlocksIndentedByNestingLevel()
        {
            var payload = Parse("> outer\n>\n> > inner");

            Assert.That(PlainTexts(payload), Is.EqualTo(new[] { "  outer", "    inner" }));
        }

        [Test]
        public void BreaksRawHtmlAndThematicBreaksFollowPayloadRules()
        {
            var payload = Parse("soft\nbreak  \nhard\\\nline\n\n---\n\n" +
                                "inline <span>x</span>\n\n<div>\nraw\n</div>");

            Assert.That(PlainTexts(payload), Is.EqualTo(new[]
            {
                "soft\nbreak\nhard\nline", "inline <span>x</span>", "<div>\nraw\n</div>",
            }));
        }

        [Test]
        public void CjkAndEmojiKeepUtf16LinkAndPlaceholderOffsets()
        {
            const string prefix = "曲😀";
            const string linked = "链😀";
            var payload = Parse(prefix + "[" + linked + "](target)![alt](图😀.png)尾");

            var linkEnd = prefix.Length + linked.Length;
            Assert.That(payload.GetPlainText(payload.Blocks[0]), Is.EqualTo("曲😀链😀\uFFFC尾"));
            Assert.That(payload.Links[0].StartUtf16, Is.EqualTo(prefix.Length));
            Assert.That(payload.Links[0].EndUtf16, Is.EqualTo(linkEnd));
            Assert.That(payload.Placeholders[0].StartUtf16, Is.EqualTo(linkEnd));
            Assert.That(payload.Placeholders[0].EndUtf16, Is.EqualTo(linkEnd + 1));
            Assert.That(payload.Blocks[0].EndUtf16, Is.EqualTo(linkEnd + 2));
        }

        [Test]
        public void SameParserInstanceProducesEquivalentResultsAcrossCalls()
        {
            const string source = "# title\n\n**曲😀** [link](target) ![alt](image.png)";
            var parser = new MarkdownRichTextParser();

            var first = parser.Parse(source, new RichTextParseContext());
            var firstText = PlainTexts(first);
            var firstLink = (first.Links[0].StartUtf16, first.Links[0].EndUtf16);
            var second = parser.Parse(source, new RichTextParseContext());

            Assert.That(PlainTexts(second), Is.EqualTo(firstText));
            Assert.That(second.Blocks.Count, Is.EqualTo(first.Blocks.Count));
            Assert.That((second.Links[0].StartUtf16, second.Links[0].EndUtf16), Is.EqualTo(firstLink));
            Assert.That(second.Placeholders[0].StartUtf16, Is.EqualTo(first.Placeholders[0].StartUtf16));
            Assert.That(PlainTexts(first), Is.EqualTo(firstText));
        }

        [Test]
        public void UnsupportedTableAndTaskListSyntaxDegradeToMarkdownText()
        {
            var payload = Parse("| a | b |\n| --- | --- |\n\n- [ ] task");

            Assert.That(PlainTexts(payload), Is.EqualTo(new[]
            {
                "| a | b |\n| --- | --- |", "• [ ] task",
            }));
        }

        private static ParagraphPayload Parse(string source)
        {
            return new MarkdownRichTextParser().Parse(source, new RichTextParseContext());
        }

        private static string[] PlainTexts(ParagraphPayload payload)
        {
            return payload.Blocks.Select(payload.GetPlainText).ToArray();
        }

        private static TextSegment Segment(ParagraphPayload payload, string content)
        {
            return payload.Body.Single(segment => segment.Content == content);
        }
    }
}
