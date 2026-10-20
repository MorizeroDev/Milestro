
using System.Linq;
using Milestro.Extensions;
using Milestro.Model;
using Milestro.RichTextParser;
using Milestro.Skia.TextLayout;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Milestro.Tests
{
    public class ParagraphBlockParserTests
    {
        [Test]
        public void PlainTextFormsSingleAnonymousBlock()
        {
            var payload = Parse("hello\nworld");

            Assert.That(payload.Blocks, Has.Count.EqualTo(1));
            Assert.That(payload.Blocks[0].IsExplicit, Is.False);
            Assert.That(PlainText(payload), Is.EqualTo("hello\nworld"));
        }

        [Test]
        public void ParagraphElementsBecomeBlocksWithoutInjectedNewlines()
        {
            var payload = Parse("<p>alpha</p><p>beta</p>");

            Assert.That(payload.Blocks, Has.Count.EqualTo(2));
            Assert.That(payload.Blocks.All(block => block.IsExplicit), Is.True);
            Assert.That(PlainText(payload), Is.EqualTo("alphabeta"));
        }

        [Test]
        public void BareTextBetweenParagraphsFormsAnonymousBlock()
        {
            var payload = Parse("<p>alpha</p>middle<p>beta</p>");

            Assert.That(payload.Blocks, Has.Count.EqualTo(3));
            Assert.That(payload.Blocks[1].IsExplicit, Is.False);
            Assert.That(PlainText(payload), Is.EqualTo("alphamiddlebeta"));
        }

        [Test]
        public void NestedParagraphStartTagAutoClosesCurrentBlock()
        {
            var payload = Parse("<p>alpha<p>beta</p>gamma</p>");

            Assert.That(payload.Blocks, Has.Count.EqualTo(3));
            Assert.That(PlainText(payload), Is.EqualTo("alphabetagamma"));
        }

        [Test]
        public void ParagraphStylesAreScopedPerBlock()
        {
            var payload = Parse("<p align=\"center\">alpha</p><p>beta</p>");

            Assert.That(payload.Blocks[0].Style.TextAlign, Is.EqualTo(TextAlign.Center));
            Assert.That(payload.Blocks[1].Style.TextAlign, Is.Null);
            Assert.That(payload.ParagraphStyle.TextAlign, Is.Null);
        }

        [Test]
        public void BlockMarginsParseCssLengths()
        {
            var payload = Parse("<p style=\"margin-bottom: 24px\">alpha</p>" +
                                "<p style=\"margin-top: 1.5em\">beta</p>" +
                                "<p style=\"margin-bottom: 10%\">gamma</p>");

            Assert.That(payload.Blocks[0].Style.MarginBottom.Value, Is.EqualTo(24f));
            Assert.That(payload.Blocks[0].Style.MarginBottom.Unit, Is.EqualTo(MarginUnit.Px));
            Assert.That(payload.Blocks[1].Style.MarginTop.Value, Is.EqualTo(1.5f));
            Assert.That(payload.Blocks[1].Style.MarginTop.Unit, Is.EqualTo(MarginUnit.Em));
            Assert.That(payload.Blocks[2].Style.MarginBottom.Value, Is.EqualTo(10f));
            Assert.That(payload.Blocks[2].Style.MarginBottom.Unit, Is.EqualTo(MarginUnit.ContainerPercent));
        }

        [TestCase("-5px")]
        [TestCase("abc")]
        [TestCase("NaNpx")]
        [TestCase("10 px")]
        public void InvalidBlockMarginsAreIgnored(string margin)
        {
            var payload = Parse($"<p style=\"margin-bottom: {margin}\">alpha</p>");

            Assert.That(payload.Blocks[0].Style.MarginBottom.Value, Is.EqualTo(0f));
        }

        [Test]
        public void AnchorMaySpanParagraphBlocks()
        {
            var payload = Parse("<a href=\"https://example.com\"><p>alpha</p><p>beta</p></a>");

            Assert.That(payload.Blocks, Has.Count.EqualTo(2));
            Assert.That(payload.Links, Has.Count.EqualTo(1));
            Assert.That(payload.Links[0].StartUtf16, Is.EqualTo(0));
            Assert.That(payload.Links[0].EndUtf16, Is.EqualTo("alphabeta".Length));
        }

        [Test]
        public void ParagraphInsideRubyIsIgnoredWithError()
        {
            LogAssert.Expect(LogType.Error, "<p> is not allowed inside <ruby>; the <p> element is ignored");

            var payload = Parse("<ruby>漢<p>字</p><rt>かん</rt></ruby>");

            Assert.That(payload.Blocks, Has.Count.EqualTo(1));
            Assert.That(payload.RubyAnnotations, Has.Count.EqualTo(1));
            Assert.That(payload.RubyAnnotations[0].EndUtf16, Is.EqualTo(2));
        }

        [Test]
        public void ToParagraphsBuildsOneParagraphPerBlock()
        {
            var payload = Parse("<p>alpha</p><p style=\"margin-bottom: 12px\">beta</p>tail");

            using var paragraphStyle = new ParagraphStyle();
            using var textStyle = new TextStyle { FontSize = 32 };
            var paragraphs = payload.ToParagraphs(paragraphStyle, textStyle);
            try
            {
                Assert.That(paragraphs, Has.Count.EqualTo(3));
                foreach (var paragraph in paragraphs)
                {
                    paragraph.Layout(400);
                    Assert.That(paragraph.Height, Is.GreaterThan(0f));
                }
            }
            finally
            {
                foreach (var paragraph in paragraphs)
                {
                    paragraph.Dispose();
                }
            }
        }

        private static string PlainText(ParagraphPayload payload)
        {
            return string.Concat(payload.Body.Select(segment => segment.Content));
        }

        private static ParagraphPayload Parse(string markup)
        {
            var parser = new RichTextParser.RichTextParser();
            parser.ParseText(markup);
            return parser.ConvertToSegments();
        }
    }
}
