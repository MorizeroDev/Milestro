using Milestro.RichTextParser;
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

            var indent = payload.ParagraphStyle.TextIndent;
            Assert.That(indent.HasValue, Is.True);
            Assert.That(indent.Value.Value, Is.EqualTo(expected));
            Assert.That(indent.Value.IsPercentage, Is.EqualTo(isPercentage));
        }

        [TestCase("12")]
        [TestCase("1em")]
        [TestCase("NaNpx")]
        [TestCase("Infinitypx")]
        [TestCase("10px hanging")]
        [TestCase("10px each-line")]
        [TestCase("10 px")]
        [TestCase("10 %")]
        public void InvalidOrUnsupportedValuesAreIgnored(string value)
        {
            var payload = Parse($"<p style=\"text-indent: {value}\">text</p>");

            Assert.That(payload.ParagraphStyle.TextIndent.HasValue, Is.False);
        }

        [Test]
        public void LaterInvalidValueDoesNotClearEarlierValidValue()
        {
            var payload = Parse("<p style=\"text-indent: 8px; text-indent: invalid\">text</p>");

            Assert.That(payload.ParagraphStyle.TextIndent.Value.Value, Is.EqualTo(8));
        }

        private static ParagraphPayload Parse(string markup)
        {
            var parser = new RichTextParser.RichTextParser();
            parser.ParseText(markup);
            return parser.ConvertToSegments();
        }
    }
}
