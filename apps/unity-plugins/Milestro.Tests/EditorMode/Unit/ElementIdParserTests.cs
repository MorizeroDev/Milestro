using System;
using System.Linq;
using Milestro.RichTextParser;
using NUnit.Framework;

namespace Milestro.Tests
{
    public class ElementIdParserTests
    {
        [Test]
        public void RecordsElementsInOpeningOrderWithUtf16Ranges()
        {
            var payload = Parse("<p id=\"outer\">A<span id=\"empty\"></span><b id=\"bold\">B</b></p>" +
                                "<p id=\"empty-block\"></p>");

            Assert.That(payload.Elements.Select(element => element.Id),
                Is.EqualTo(new[] { "outer", "empty", "bold", "empty-block" }));
            Assert.That(payload.Elements.Select(element => element.Occurrence),
                Is.EqualTo(new[] { 0, 1, 2, 3 }));
            Assert.That(payload.Elements.Select(element => (element.StartUtf16, element.EndUtf16)),
                Is.EqualTo(new[] { (0, 2), (1, 1), (1, 2), (2, 2) }));
            Assert.That(payload.Elements[1].BlockIndex, Is.EqualTo(0));
            Assert.That(payload.Elements[3].BlockIndex, Is.EqualTo(1));
        }

        [Test]
        public void SupportsIdsOnLinksAndPlaceholders()
        {
            var payload = Parse("<a href=\"target\" id=\"link\">A</a>" +
                                "<img id=\"image\" width=\"20\" height=\"10\" key=\"icon\"/>");

            Assert.That(payload.Links, Has.Count.EqualTo(1));
            Assert.That(payload.Elements.Select(element => element.Id),
                Is.EqualTo(new[] { "link", "image" }));
            Assert.That(payload.Elements.Select(element => (element.StartUtf16, element.EndUtf16)),
                Is.EqualTo(new[] { (0, 1), (1, 2) }));
        }

        [TestCase("<span id=\"\">empty</span>")]
        [TestCase("<p id=\"\">empty</p>")]
        [TestCase("<img id=\"\" width=\"20\" height=\"10\"/>")]
        public void RejectsEmptyIds(string markup)
        {
            var parser = new RichTextParser.RichTextParser();
            parser.ParseText(markup);

            Assert.Throws<InvalidCastException>(() => parser.ConvertToSegments());
        }

        private static ParagraphPayload Parse(string markup)
        {
            var parser = new RichTextParser.RichTextParser();
            parser.ParseText(markup);
            return parser.ConvertToSegments();
        }
    }
}
