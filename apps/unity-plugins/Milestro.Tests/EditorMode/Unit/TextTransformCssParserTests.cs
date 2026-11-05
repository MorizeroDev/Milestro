using System.Linq;
using Milestro.Extensions;
using Milestro.Model;
using Milestro.RichTextParser;
using Milestro.Skia.TextLayout;
using NUnit.Framework;

namespace Milestro.Tests
{
    public class TextTransformCssParserTests
    {
        [TestCase("none", "AbCd", "AbCd", TextTransform.None)]
        [TestCase("lowercase", "AbCd", "abcd", TextTransform.Lowercase)]
        [TestCase("LOWERCASE", "AbCd", "abcd", TextTransform.Lowercase)]
        [TestCase("uppercase", "AbCd", "ABCD", TextTransform.Uppercase)]
        [TestCase("UPPERCASE", "AbCd", "ABCD", TextTransform.Uppercase)]
        [TestCase("uppercase", "Straße", "STRASSE", TextTransform.Uppercase)]
        [TestCase("lowercase", "İ", "i̇", TextTransform.Lowercase)]
        public void ParsesCaseTransforms(string value, string text, string expected,
            TextTransform expectedTransform)
        {
            var payload = Parse($"<span style=\"text-transform: {value}\">{text}</span>");

            Assert.That(PlainText(payload), Is.EqualTo(expected));
            Assert.That(payload.Body[0].TextStyle.TextTransform, Is.EqualTo(expectedTransform));
        }

        [Test]
        public void SmallCapsUsesTmproSizing()
        {
            var payload = Parse("<span style=\"text-transform: smallcaps\">AbcD</span>");

            Assert.That(PlainText(payload), Is.EqualTo("ABCD"));
            Assert.That(payload.Body.Select(segment => segment.Content),
                Is.EqualTo(new[] { "A", "BC", "D" }));
            Assert.That(payload.Body.Select(segment => segment.TextStyle.IsSmallCapsLowercase),
                Is.EqualTo(new[] { false, true, false }));

            using var baseStyle = new TextStyle { FontSize = 40 };
            using var fullSize = payload.Body[0].TextStyle.ToTextStyle(baseStyle);
            using var smallSize = payload.Body[1].TextStyle.ToTextStyle(baseStyle);
            Assert.That(fullSize.FontSize, Is.EqualTo(40f));
            Assert.That(smallSize.FontSize, Is.EqualTo(32f).Within(0.001f));
        }

        [Test]
        public void TransformIsInheritedAndCanBeReset()
        {
            var payload = Parse("<span style=\"text-transform: uppercase\">ab" +
                                "<span style=\"text-transform: none\">Cd</span>ef</span>");

            Assert.That(PlainText(payload), Is.EqualTo("ABCdEF"));
            Assert.That(payload.Body[0].TextStyle.TextTransform, Is.EqualTo(TextTransform.Uppercase));
            Assert.That(payload.Body[1].TextStyle.TextTransform, Is.EqualTo(TextTransform.None));
            Assert.That(payload.Body[2].TextStyle.TextTransform, Is.EqualTo(TextTransform.Uppercase));
        }

        [Test]
        public void InvalidValuePreservesInheritedTransform()
        {
            var payload = Parse("<span style=\"text-transform: uppercase\">ab" +
                                "<span style=\"text-transform: capitalize\">Cd</span></span>");

            Assert.That(PlainText(payload), Is.EqualTo("ABCD"));
            Assert.That(payload.Body[1].TextStyle.TextTransform, Is.EqualTo(TextTransform.Uppercase));
        }

        [Test]
        public void LangOverridesTheComponentLocale()
        {
            var payload = Parse("<span lang=\"tr\" style=\"text-transform: uppercase\">istanbul</span>");

            Assert.That(PlainText(payload), Is.EqualTo("İSTANBUL"));
        }

        [Test]
        public void ParseContextSuppliesTheComponentLocale()
        {
            var parser = new XmlRichTextParser();
            var payload = parser.Parse("<span style=\"text-transform: uppercase\">istanbul</span>",
                new RichTextParseContext(locale: "tr"));

            Assert.That(PlainText(payload), Is.EqualTo("İSTANBUL"));
        }

        [Test]
        public void SmallCapsSupportsMultiCharacterCaseMappings()
        {
            var payload = Parse("<span style=\"text-transform: smallcaps\">ß</span>");

            Assert.That(PlainText(payload), Is.EqualTo("SS"));
            Assert.That(payload.Body, Has.Count.EqualTo(1));
            Assert.That(payload.Body[0].TextStyle.IsSmallCapsLowercase, Is.True);
        }

        [Test]
        public void SmallCapsComposesWithScriptScaling()
        {
            var payload = Parse("<sup><span style=\"text-transform: smallcaps\">aA</span></sup>");

            using var baseStyle = new TextStyle { FontSize = 40 };
            using var smallSize = payload.Body[0].TextStyle.ToTextStyle(baseStyle);
            using var fullSize = payload.Body[1].TextStyle.ToTextStyle(baseStyle);
            Assert.That(smallSize.FontSize, Is.EqualTo(16f).Within(0.001f));
            Assert.That(fullSize.FontSize, Is.EqualTo(20f));
        }

        [Test]
        public void SmallCapsStateRoundTripsThroughGenerateRichText()
        {
            var payload = Parse("<span style=\"text-transform: smallcaps\">a</span>");
            var state = payload.Body[0].TextStyle;

            var reparsed = Parse(state.GenerateRichText() + "aA</span>");

            Assert.That(PlainText(reparsed), Is.EqualTo("AA"));
            Assert.That(reparsed.Body[0].TextStyle.IsSmallCapsLowercase, Is.True);
            Assert.That(reparsed.Body[1].TextStyle.IsSmallCapsLowercase, Is.False);
            Assert.That(reparsed.Body[0].TextStyle.FontSizeScale, Is.Null);
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
