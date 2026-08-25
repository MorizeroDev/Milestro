using System.Collections.Generic;
using Milestro.Skia;
using NUnit.Framework;

namespace Milestro.Tests
{
    public class FontFamilyParserTests
    {
        [Test]
        public void ParsesBareAndExactFamilies()
        {
            Assert.That(FontFamilyParser.TryParseFontFamilyList(
                "\"Microsoft YaHei\", system-ui",
                out var tokens), Is.True);

            AssertToken(tokens, 0, FontFamilyTokenKind.Exact, "Microsoft YaHei");
            AssertToken(tokens, 1, FontFamilyTokenKind.Bare, "system-ui");
            Assert.That(tokens, Has.Count.EqualTo(2));
        }

        [Test]
        public void KeepsQuotedCommasAndDecodesJsonEscapes()
        {
            Assert.That(FontFamilyParser.TryParseFontFamilyList(
                "\"A,B\", \"C\\\\D\\\"E\", \"\\u5fae\\u8f6f\"",
                out var tokens), Is.True);

            AssertToken(tokens, 0, FontFamilyTokenKind.Exact, "A,B");
            AssertToken(tokens, 1, FontFamilyTokenKind.Exact, "C\\D\"E");
            AssertToken(tokens, 2, FontFamilyTokenKind.Exact, "微软");
            Assert.That(tokens, Has.Count.EqualTo(3));
        }

        [Test]
        public void InvalidJsonEscapeFallsBackToWholeBareToken()
        {
            Assert.That(FontFamilyParser.TryParseFontFamilyList(
                "\"bad\\q\", next",
                out var tokens), Is.True);

            AssertToken(tokens, 0, FontFamilyTokenKind.Bare, "\"bad\\q\"");
            AssertToken(tokens, 1, FontFamilyTokenKind.Bare, "next");
            Assert.That(tokens, Has.Count.EqualTo(2));
        }

        [Test]
        public void UnclosedQuoteFallsBackWithoutSplittingItsComma()
        {
            Assert.That(FontFamilyParser.TryParseFontFamilyList(
                "\"broken, fallback",
                out var tokens), Is.True);

            AssertToken(tokens, 0, FontFamilyTokenKind.Bare, "\"broken, fallback");
            Assert.That(tokens, Has.Count.EqualTo(1));
        }

        [Test]
        public void QuoteInsideBareTokenDoesNotStartQuotedMode()
        {
            Assert.That(FontFamilyParser.TryParseFontFamilyList(
                "Foo\"Bar, Baz",
                out var tokens), Is.True);

            AssertToken(tokens, 0, FontFamilyTokenKind.Bare, "Foo\"Bar");
            AssertToken(tokens, 1, FontFamilyTokenKind.Bare, "Baz");
            Assert.That(tokens, Has.Count.EqualTo(2));
        }

        [Test]
        public void EmptyCommaSeparatedItemsAreIgnored()
        {
            Assert.That(FontFamilyParser.TryParseFontFamilyList(
                ", , Arial,,",
                out var tokens), Is.True);

            AssertToken(tokens, 0, FontFamilyTokenKind.Bare, "Arial");
            Assert.That(tokens, Has.Count.EqualTo(1));

            Assert.That(FontFamilyParser.TryParseFontFamilyList(", ,", out var empty), Is.False);
            Assert.That(empty, Is.Empty);
        }

        [Test]
        public void FormatterRoundTripsCanonicalDeclaration()
        {
            var source = new List<FontFamilyToken>
            {
                FontFamilyToken.Exact("A, B\\\"\n"),
                FontFamilyToken.Bare("system-ui"),
            };

            var formatted = FontFamilyParser.FormatFontFamilyList(source);
            Assert.That(FontFamilyParser.TryParseFontFamilyList(formatted, out var reparsed), Is.True);

            AssertToken(reparsed, 0, FontFamilyTokenKind.Exact, "A, B\\\"\n");
            AssertToken(reparsed, 1, FontFamilyTokenKind.Bare, "system-ui");
            Assert.That(reparsed, Has.Count.EqualTo(2));
        }

        [Test]
        public void LegacyFamilyListAlwaysProducesBareTokens()
        {
            var legacy = FontFamilyDeclaration.ToBareTokens(new[]
            {
                "\"system-ui\"",
                " system-ui ",
            });
            var declaration = FontFamilyParser.ParseFontFamilyToken("\"system-ui\"");

            AssertToken(legacy, 0, FontFamilyTokenKind.Bare, "\"system-ui\"");
            AssertToken(legacy, 1, FontFamilyTokenKind.Bare, "system-ui");
            Assert.That(declaration.Kind, Is.EqualTo(FontFamilyTokenKind.Exact));
            Assert.That(declaration.Value, Is.EqualTo("system-ui"));
            Assert.That((int)FontFamilyTokenKind.Bare, Is.EqualTo(0));
            Assert.That((int)FontFamilyTokenKind.Exact, Is.EqualTo(1));
        }

        private static void AssertToken(
            IReadOnlyList<FontFamilyToken> tokens,
            int index,
            FontFamilyTokenKind kind,
            string value)
        {
            Assert.That(tokens[index].Kind, Is.EqualTo(kind));
            Assert.That(tokens[index].Value, Is.EqualTo(value));
        }
    }
}
