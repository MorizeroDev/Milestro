using Milestro.RichTextParser;
using NUnit.Framework;

namespace Milestro.Tests
{
    public class WhiteSpaceCssParserTests
    {
        [TestCase("normal", false)]
        [TestCase("NORMAL", false)]
        [TestCase("nowrap", true)]
        [TestCase("NOWRAP", true)]
        public void ParsesSupportedValues(string value, bool expected)
        {
            var payload = Parse($"<span style=\"white-space: {value}\">text</span>");

            Assert.That(payload.Body[0].TextStyle.NoWrap, Is.EqualTo(expected));
            Assert.That(payload.NoWrapAnnotations, expected ? Has.Count.EqualTo(1) : Is.Empty);
        }

        [Test]
        public void DefaultAndInvalidValuesPreserveNormalWrapping()
        {
            var defaultPayload = Parse("plain text");
            var invalidPayload = Parse("<span style=\"white-space: pre\">plain text</span>");

            Assert.That(defaultPayload.Body[0].TextStyle.NoWrap, Is.False);
            Assert.That(invalidPayload.Body[0].TextStyle.NoWrap, Is.False);
            Assert.That(defaultPayload.NoWrapAnnotations, Is.Empty);
            Assert.That(invalidPayload.NoWrapAnnotations, Is.Empty);
        }

        [Test]
        public void NormalRestoresWrappingInsideNoWrap()
        {
            const string prefix = "A";
            const string first = "bb ";
            const string normal = "cc dd";
            const string last = " ee";
            var payload = Parse(prefix + "<span style=\"white-space: nowrap\">" + first +
                                "<span style=\"white-space: normal\">" + normal + "</span>" +
                                last + "</span>F");

            Assert.That(payload.NoWrapAnnotations, Has.Count.EqualTo(2));
            AssertAnnotation(payload.NoWrapAnnotations[0],
                prefix.Length, prefix.Length + first.Length);
            AssertAnnotation(payload.NoWrapAnnotations[1],
                prefix.Length + first.Length + normal.Length,
                prefix.Length + first.Length + normal.Length + last.Length);
        }

        [Test]
        public void NoWrapRangeIncludesInheritedPlaceholders()
        {
            var payload = Parse("<span style=\"white-space: nowrap\">aa" +
                                "<img width=\"12\" height=\"20\"/>bb</span>");

            Assert.That(payload.NoWrapAnnotations, Has.Count.EqualTo(1));
            AssertAnnotation(payload.NoWrapAnnotations[0], 0, 5);
        }

        [Test]
        public void InvalidNestedValueDoesNotClearInheritedNoWrap()
        {
            var payload = Parse("<span style=\"white-space: nowrap\">aa" +
                                "<span style=\"white-space: pre\">bb</span></span>");

            Assert.That(payload.Body[1].TextStyle.NoWrap, Is.True);
            Assert.That(payload.NoWrapAnnotations, Has.Count.EqualTo(1));
            AssertAnnotation(payload.NoWrapAnnotations[0], 0, 4);
        }

        [Test]
        public void BrDoesNotSplitNoWrapAnnotation()
        {
            const string text = "aa\nbb cc";
            var payload = Parse("<span style=\"white-space: nowrap\">aa<br/>bb cc</span>");

            Assert.That(payload.NoWrapAnnotations, Has.Count.EqualTo(1));
            AssertAnnotation(payload.NoWrapAnnotations[0], 0, text.Length);
        }

        private static void AssertAnnotation(NoWrapAnnotation annotation, int start, int end)
        {
            Assert.That(annotation.StartUtf16, Is.EqualTo(start));
            Assert.That(annotation.EndUtf16, Is.EqualTo(end));
        }

        private static ParagraphPayload Parse(string markup)
        {
            var parser = new RichTextParser.RichTextParser();
            parser.ParseText(markup);
            return parser.ConvertToSegments();
        }
    }
}
