using System.Linq;
using Milestro.Extensions;
using Milestro.RichTextParser;
using Milestro.Skia.TextLayout;
using NUnit.Framework;

namespace Milestro.Tests
{
    public class RubyParserTests
    {
        [Test]
        public void HtmlRubyKeepsAnnotationOutOfPlainTextAndAddsAnnotationLine()
        {
            var rubyPayload = Parse("<ruby>漢字<rt>かんじ</rt></ruby>");
            var plainPayload = Parse("漢字");

            Assert.That(PlainText(rubyPayload), Is.EqualTo("漢字"));

            using var paragraphStyle = new ParagraphStyle();
            using var textStyle = new TextStyle { FontSize = 32 };
            using var rubyParagraph = rubyPayload.ToParagraph(paragraphStyle, textStyle);
            using var plainParagraph = plainPayload.ToParagraph(paragraphStyle, textStyle);
            rubyParagraph.Layout(400);
            plainParagraph.Layout(400);

            Assert.That(rubyParagraph.Height, Is.GreaterThan(plainParagraph.Height));
        }

        [Test]
        public void CssRubyPropertiesMapToNativeStyle()
        {
            var payload = Parse("<ruby style=\"ruby-position: under; ruby-align: space-around; " +
                                "ruby-overhang: auto; --milestro-ruby-size: 0.75; " +
                                "--milestro-ruby-gap: 4; --milestro-ruby-overflow: inline\">" +
                                "漢<rt>かん</rt></ruby>");

            Assert.That(payload.RubyAnnotations, Has.Count.EqualTo(1));
            var ruby = payload.RubyAnnotations[0];
            Assert.That(ruby.StartUtf16, Is.EqualTo(0));
            Assert.That(ruby.EndUtf16, Is.EqualTo(1));
            Assert.That(ruby.Text, Is.EqualTo("かん"));
            Assert.That(ruby.Style.Position, Is.EqualTo(RubyPosition.Below));
            Assert.That(ruby.Style.Alignment, Is.EqualTo(RubyAlignment.SpaceAround));
            Assert.That(ruby.Style.Overhang, Is.EqualTo(RubyOverhang.Allow));
            Assert.That(ruby.Style.Overflow, Is.EqualTo(RubyOverflow.Inline));
            Assert.That(ruby.Style.FontSizeMultiplier, Is.EqualTo(0.75f));
            Assert.That(ruby.Style.Gap, Is.EqualTo(4));
        }

        [Test]
        public void HtmlRubyUsesCssInitialAlignmentAndOverhang()
        {
            var payload = Parse("<ruby>base<rt>annotation</rt></ruby>");

            Assert.That(payload.RubyAnnotations, Has.Count.EqualTo(1));
            var style = payload.RubyAnnotations[0].Style;
            Assert.That(style.Position, Is.EqualTo(RubyPosition.Above));
            Assert.That(style.Alignment, Is.EqualTo(RubyAlignment.SpaceAround));
            Assert.That(style.Overhang, Is.EqualTo(RubyOverhang.Allow));
        }

        [Test]
        public void StandardCssRubyNamesAndKeywordsAreCaseInsensitive()
        {
            var payload = Parse("<ruby style=\"RUBY-POSITION: UNDER; RUBY-ALIGN: CENTER; " +
                                "RUBY-OVERHANG: NONE\">base<rt>annotation</rt></ruby>");

            var style = payload.RubyAnnotations[0].Style;
            Assert.That(style.Position, Is.EqualTo(RubyPosition.Below));
            Assert.That(style.Alignment, Is.EqualTo(RubyAlignment.Center));
            Assert.That(style.Overhang, Is.EqualTo(RubyOverhang.None));
        }

        [Test]
        public void HtmlRubyFallbackParenthesesAreNotRendered()
        {
            var payload = Parse("<ruby>漢<rp>(</rp><rt>かん</rt><rp>)</rp></ruby>");

            Assert.That(PlainText(payload), Is.EqualTo("漢"));
            Assert.That(payload.RubyAnnotations, Has.Count.EqualTo(1));
            Assert.That(payload.RubyAnnotations[0].Text, Is.EqualTo("かん"));
        }

        [Test]
        public void RubyRangeUsesUtf16OffsetsAcrossEmojiAndInlineBaseStyles()
        {
            var prefix = "前😀";
            var payload = Parse(prefix + "<ruby><b>漢</b>字<rt>かんじ</rt></ruby>後");

            Assert.That(PlainText(payload), Is.EqualTo(prefix + "漢字後"));
            Assert.That(payload.RubyAnnotations, Has.Count.EqualTo(1));
            Assert.That(payload.RubyAnnotations[0].StartUtf16, Is.EqualTo(prefix.Length));
            Assert.That(payload.RubyAnnotations[0].EndUtf16, Is.EqualTo((prefix + "漢字").Length));
        }

        [TestCase("<rt>orphan</rt>")]
        [TestCase("<rp>(</rp>")]
        [TestCase("<ruby>base only</ruby>")]
        [TestCase("<ruby><rt>annotation only</rt></ruby>")]
        [TestCase("<ruby>base<rt/></ruby>")]
        [TestCase("<ruby>base<rt>one</rt><rt>two</rt></ruby>")]
        [TestCase("<ruby>base<rt><b>styled</b></rt></ruby>")]
        [TestCase("<ruby unknown=\"value\">base<rt>annotation</rt></ruby>")]
        [TestCase("<ruby style=\"ruby-position: sideways\">base<rt>annotation</rt></ruby>")]
        [TestCase("<ruby style=\"--milestro-ruby-gap: -1\">base<rt>annotation</rt></ruby>")]
        public void RejectsRubyMarkupOutsideSupportedHtmlSubset(string markup)
        {
            var parser = new RichTextParser.RichTextParser();
            parser.ParseText(markup);

            Assert.Throws<System.InvalidCastException>(() => parser.ConvertToSegments());
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
