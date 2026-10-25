using System;
using System.Linq;
using Milestro.Extensions;
using Milestro.RichTextParser;
using Milestro.Skia.TextLayout;
using Milestro.Model;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Milestro.Tests
{
    public sealed class PlaceholderTests
    {
        private static ParagraphPayload Parse(string text)
        {
            var parser = new RichTextParser.RichTextParser();
            parser.ParseText(text);
            return parser.ConvertToSegments();
        }

        [TestCase("<img/>")]
        [TestCase("<img width=\"10\"/>")]
        [TestCase("<img height=\"10\"/>")]
        [TestCase("<img width=\"0\" height=\"10\"/>")]
        [TestCase("<img width=\"-1\" height=\"10\"/>")]
        [TestCase("<img width=\"NaN\" height=\"10\"/>")]
        [TestCase("<img width=\"Infinity\" height=\"10\"/>")]
        [TestCase("<img width=\"10px\" height=\"10\"/>")]
        [TestCase("<img width=\"10\" height=\"0\"/>")]
        [TestCase("<img width=\"10\" height=\"10\" src=\"image\"/>")]
        [TestCase("<img width=\"10\" height=\"10\" align=\"middle\"/>")]
        [TestCase("<img width=\"10\" height=\"10\" style=\"color:red\"/>")]
        [TestCase("<img width=\"10\" height=\"10\"></img>")]
        [TestCase("<img width=\"10\" height=\"10\">child</img>")]
        public void P13_InvalidImageTagsReject(string text)
        {
            Assert.Throws<InvalidCastException>(() => Parse(text));
        }

        [Test]
        public void P13_SourceModelCountsNativeSlotsAndKeepsKeysAndBareObjectCharacter()
        {
            var payload = Parse("😀<a href=\"object\">天<img width=\"12.5\" height=\"24\" key=\" A&amp;B \"/>地</a>\uFFFC");
            Assert.That(string.Concat(payload.Body.Select(segment => segment.Content)), Is.EqualTo("😀天地\uFFFC"));
            Assert.That(payload.Placeholders, Has.Count.EqualTo(1));
            Assert.That(payload.Placeholders[0].StartUtf16, Is.EqualTo(3));
            Assert.That(payload.Placeholders[0].Key, Is.EqualTo(" A&B "));
            Assert.That(payload.Links[0].StartUtf16, Is.EqualTo(2));
            Assert.That(payload.Links[0].EndUtf16, Is.EqualTo(5));
            Assert.That(payload.GetPlainText(payload.Blocks[0]), Is.EqualTo("😀天\uFFFC地\uFFFC"));
        }

        [Test]
        public void P13_RubyIgnoresImageAndPreservesRubyRange()
        {
            LogAssert.Expect(LogType.Error, "<img> inside <ruby> is ignored");
            var payload = Parse("<ruby>漢<img width=\"10\" height=\"10\"/>字<rt>かんじ</rt></ruby>");
            Assert.That(payload.Placeholders, Is.Empty);
            Assert.That(payload.RubyAnnotations[0].EndUtf16, Is.EqualTo(2));
        }

        [TestCase(WritingMode.VerticalRl)]
        [TestCase(WritingMode.HorizontalTb)]
        public void P08_P09_P15_TagPipelineQueriesAndHitsOneNativeSourceUnit(WritingMode mode)
        {
            var payload = Parse("<a href=\"object\">天<img width=\"23\" height=\"31\"/>地</a>");
            using var style = new ParagraphStyle { WritingMode = mode };
            using var textStyle = new TextStyle { FontSize = 30 };
            using var paragraph = payload.ToParagraph(style, textStyle);
            paragraph.LayoutWithInlineExtent(200);
            var boxes = paragraph.GetRectsForPlaceholders();
            Assert.That(boxes, Has.Count.EqualTo(1));
            Assert.That(boxes[0].width, Is.EqualTo(23).Within(0.01));
            Assert.That(boxes[0].height, Is.EqualTo(31).Within(0.01));
            Assert.That(paragraph.GetRectsForRange(1, 2)[0], Is.EqualTo(boxes[0]));
            Assert.That(paragraph.HitTestRange(0, 3, boxes[0].center), Is.True);
            Assert.That(paragraph.GetRectsForRange(3, 4), Is.Empty);
        }

        [Test]
        public void P11_AdjacentObjectsAndBlocksUseLocalUtf16AfterSurrogates()
        {
            var payload = Parse("<p>😀<img width=\"12\" height=\"20\"/><img width=\"14\" height=\"21\"/></p>" +
                                "<p><a href=\"second\"><img width=\"16\" height=\"22\"/>天</a></p>");
            Assert.That(payload.Blocks[0].LengthUtf16, Is.EqualTo(4));
            Assert.That(payload.Blocks[1].StartUtf16, Is.EqualTo(4));
            Assert.That(payload.Links[0].StartUtf16, Is.EqualTo(4));
            using var style = new ParagraphStyle { WritingMode = WritingMode.VerticalRl };
            using var textStyle = new TextStyle { FontSize = 30 };
            var paragraphs = payload.ToParagraphs(style, textStyle);
            try
            {
                foreach (var paragraph in paragraphs) paragraph.LayoutWithInlineExtent(200);
                Assert.That(paragraphs[0].GetRectsForPlaceholders(), Has.Count.EqualTo(2));
                Assert.That(paragraphs[1].GetRectsForPlaceholders(), Has.Count.EqualTo(1));
                Assert.That(paragraphs[1].GetRectsForRange(0, 1)[0].height, Is.EqualTo(22));
                Assert.That(paragraphs[1].HitTestRange(0, 2,
                    paragraphs[1].GetRectsForPlaceholders()[0].center), Is.True);
            }
            finally
            {
                foreach (var paragraph in paragraphs) paragraph.Dispose();
            }
        }

        [Test]
        public void P13_RubyAfterPlaceholderUsesNativeUtf8AndUtf16Tracking()
        {
            var payload = Parse("<img width=\"12\" height=\"20\"/><ruby>漢<rt>かん</rt></ruby>");
            Assert.That(payload.RubyAnnotations[0].StartUtf16, Is.EqualTo(1));
            using var style = new ParagraphStyle();
            using var textStyle = new TextStyle { FontSize = 30 };
            using var paragraph = payload.ToParagraph(style, textStyle);
            paragraph.Layout(200);
            Assert.That(paragraph.GetRectsForPlaceholders(), Has.Count.EqualTo(1));
            Assert.That(paragraph.GetRectsForRange(1, 2), Is.Not.Empty);
        }

        [Test]
        public void P06_EllipsisRemovesPlaceholderQueryAndLinkGeometry()
        {
            var payload = Parse("天<a href=\"hidden\"><img width=\"90\" height=\"90\"/>地</a>");
            using var style = new ParagraphStyle { WritingMode = WritingMode.VerticalRl, MaxLines = 1 };
            style.SetEllipsis("…");
            using var textStyle = new TextStyle { FontSize = 30 };
            using var paragraph = payload.ToParagraph(style, textStyle);
            paragraph.LayoutWithInlineExtent(60);
            Assert.That(paragraph.GetRectsForPlaceholders(), Is.Empty);
            Assert.That(paragraph.GetRectsForRange(1, 3), Is.Empty);
        }
    }
}
