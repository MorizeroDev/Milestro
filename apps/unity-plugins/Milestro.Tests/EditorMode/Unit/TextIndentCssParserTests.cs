using Milestro.Extensions;
using Milestro.RichTextParser;
using Milestro.Skia.TextLayout;
using NUnit.Framework;

namespace Milestro.Tests
{
    public class TextIndentCssParserTests
    {
        [TestCase("24px", 24, false)]
        [TestCase("-3.5PX", -3.5f, false)]
        [TestCase("10%", 10, true)]
        [TestCase("0", 0, false)]
        [TestCase("-0", 0, false)]
        public void ParsesSupportedValues(string value, float expected, bool isPercentage)
        {
            var payload = Parse($"<p style=\"color: red; text-indent: {value}\">text</p>");

            var indent = payload.Blocks[0].Style.TextIndent;
            Assert.That(indent.HasValue, Is.True);
            Assert.That(indent.Value.Value, Is.EqualTo(expected));
            Assert.That(indent.Value.IsPercentage, Is.EqualTo(isPercentage));
        }

        [TestCase("12")]
        [TestCase("1rem")]
        [TestCase("1 em")]
        [TestCase("NaNem")]
        [TestCase("Infinityem")]
        [TestCase("NaNpx")]
        [TestCase("Infinitypx")]
        [TestCase("10px hanging")]
        [TestCase("10px each-line")]
        [TestCase("10 px")]
        [TestCase("10 %")]
        public void InvalidOrUnsupportedValuesAreIgnored(string value)
        {
            var payload = Parse($"<p style=\"text-indent: {value}\">text</p>");

            Assert.That(payload.Blocks[0].Style.TextIndent.HasValue, Is.False);
            Assert.That(payload.Blocks[0].Style.TextIndentEm.HasValue, Is.False);
        }

        [Test]
        public void LaterInvalidValueDoesNotClearEarlierValidValue()
        {
            var payload = Parse("<p style=\"text-indent: 8px; text-indent: invalid\">text</p>");

            Assert.That(payload.Blocks[0].Style.TextIndent.Value.Value, Is.EqualTo(8));
        }

        [TestCase("1em", 24f, 24f)]
        [TestCase("-2EM", 24f, -48f)]
        [TestCase("0.5em", 40f, 20f)]
        [TestCase("0em", 40f, 0f)]
        [TestCase("8px; text-indent: -2em", 24f, -48f)]
        [TestCase("-2em; text-indent: 8px", 24f, 8f)]
        [TestCase("-2em; text-indent: invalid", 24f, -48f)]
        public void EmIndentResolvesAgainstBaseFontSize(string value, float fontSize, float expected)
        {
            var payload = Parse($"<p style=\"text-indent: {value}\">test</p>");
            AssertFirstCharacterLeft(payload, fontSize, expected);
        }

        [TestCase("<font size=\"40\">", "</font>", "test", -80f)]
        [TestCase("<font size=\"40\"><span fscale=\"0.5\">", "</span></font>", "test", -40f)]
        [TestCase("<font size=\"40\">", "</font>", "<font size=\"80\">test</font>", -80f)]
        public void EmIndentUsesParagraphFontInsteadOfChildFont(string prefix,
            string suffix,
            string content,
            float expected)
        {
            var payload = Parse(prefix + "<p style=\"text-indent: -2em\">" + content + "</p>" + suffix);
            AssertFirstCharacterLeft(payload, 24f, expected);
        }

        [Test]
        public void EmIndentResolvesAgainWhenBaseFontSizeChanges()
        {
            var payload = Parse("<p style=\"text-indent: -2em\">test</p>");
            AssertFirstCharacterLeft(payload, 20f, -40f);
            AssertFirstCharacterLeft(payload, 32f, -64f);
        }

        private static void AssertFirstCharacterLeft(ParagraphPayload payload, float fontSize, float expected)
        {
            using var paragraphStyle = new ParagraphStyle();
            using var textStyle = new TextStyle { FontSize = fontSize };
            var paragraphs = payload.ToParagraphs(paragraphStyle, textStyle);
            try
            {
                paragraphs[0].Layout(400f);
                var rects = paragraphs[0].GetRectsForRange(0, 1);
                Assert.That(rects, Has.Count.EqualTo(1));
                Assert.That(rects[0].xMin, Is.EqualTo(expected).Within(0.01f));
            }
            finally
            {
                foreach (var paragraph in paragraphs)
                {
                    paragraph.Dispose();
                }
            }
        }

        private static ParagraphPayload Parse(string markup)
        {
            var parser = new RichTextParser.RichTextParser();
            parser.ParseText(markup);
            return parser.ConvertToSegments();
        }
    }
}
