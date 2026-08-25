using System;
using System.Linq;
using Milestro.RichTextParser;
using NUnit.Framework;

namespace Milestro.Tests
{
    public class HyperlinkParserTests
    {
        [Test]
        public void ParsesStrictAnchorAndPreservesUtf16Range()
        {
            var payload = Parse("prefix 😀 <a href=\"milthm://song/1?x=1&amp;y=2\" id=\"song-link\">曲😀</a> suffix");

            Assert.That(PlainText(payload), Is.EqualTo("prefix 😀 曲😀 suffix"));
            Assert.That(payload.Links, Has.Count.EqualTo(1));
            var link = payload.Links[0];
            Assert.That(link.Href, Is.EqualTo("milthm://song/1?x=1&y=2"));
            Assert.That(link.Id, Is.EqualTo("song-link"));
            Assert.That(link.StartUtf16, Is.EqualTo("prefix 😀 ".Length));
            Assert.That(link.EndUtf16, Is.EqualTo("prefix 😀 曲😀".Length));
            Assert.That(link.Occurrence, Is.EqualTo(0));
        }

        [Test]
        public void OmittedIdMapsToEmptyAndStyleNestingKeepsOneRange()
        {
            var payload = Parse("<b>before <a href='target'><i>linked</i> text</a> after</b>");

            Assert.That(PlainText(payload), Is.EqualTo("before linked text after"));
            Assert.That(payload.Links, Has.Count.EqualTo(1));
            Assert.That(payload.Links[0].Id, Is.Empty);
            Assert.That(payload.Links[0].StartUtf16, Is.EqualTo("before ".Length));
            Assert.That(payload.Links[0].EndUtf16, Is.EqualTo("before linked text".Length));
        }

        [Test]
        public void EqualHrefAndIdRemainIndependentOccurrences()
        {
            var payload = Parse("<a href=\"same\" id=\"same\">one</a> / <a href=\"same\" id=\"same\">two</a>");

            Assert.That(payload.Links.Select(link => link.Occurrence), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(payload.Links.Select(link => (link.StartUtf16, link.EndUtf16)),
                Is.EqualTo(new[] { (0, 3), (6, 9) }));
        }

        [Test]
        public void TextWithoutAnchorHasNoLinkMetadata()
        {
            var payload = Parse("plain <b>styled</b> text");

            Assert.That(PlainText(payload), Is.EqualTo("plain styled text"));
            Assert.That(payload.Links, Is.Empty);
        }

        [TestCase("<a>missing href</a>")]
        [TestCase("<a href=\"\">empty href</a>")]
        [TestCase("<a href=\"target\" id=\"\">empty id</a>")]
        [TestCase("<a href=\"target\" unknown=\"value\">unknown</a>")]
        [TestCase("<a HREF=\"target\">case-sensitive</a>")]
        [TestCase("<A href=\"target\">case-sensitive</A>")]
        [TestCase("<a href=\"outer\">before <a href=\"inner\">inner</a></a>")]
        [TestCase("<a href=\"target\"/>")]
        public void RejectsSemanticallyInvalidAnchor(string markup)
        {
            var parser = new RichTextParser.RichTextParser();
            parser.ParseText(markup);

            Assert.Throws<InvalidCastException>(() => parser.ConvertToSegments());
        }

        [TestCase("<a=\"target\">unnamed</a>")]
        [TestCase("<a href=\"one\" href=\"two\">duplicate</a>")]
        [TestCase("<a href=\"target\"><b>crossed</a></b>")]
        [TestCase("<a href=\"target\">unfinished")]
        public void MalformedXmlNeverProducesClickableMetadata(string markup)
        {
            var payload = Parse(markup);

            Assert.That(payload.Links, Is.Empty);
        }

        private static ParagraphPayload Parse(string markup)
        {
            var parser = new RichTextParser.RichTextParser();
            parser.ParseText(markup);
            return parser.ConvertToSegments();
        }

        private static string PlainText(ParagraphPayload payload)
        {
            return string.Concat(payload.Body.Select(segment => segment.Content ?? ""));
        }
    }
}
