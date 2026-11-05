using Milestro.Extensions;
using Milestro.RichTextParser;
using Milestro.Skia.TextLayout;
using NUnit.Framework;
using UnityEngine;

namespace Milestro.Tests
{
    public class BackgroundColorCssParserTests
    {
        [Test]
        public void MarkUsesYellowBackgroundWithoutChangingForeground()
        {
            var payload = Parse("plain<mark>marked</mark>");

            Assert.That(payload.Body[0].TextStyle.BackgroundColor, Is.Null);
            AssertColor(payload.Body[1].TextStyle.BackgroundColor, new Color32(255, 255, 0, 255));
            Assert.That(payload.Body[1].TextStyle.Color, Is.Null);

            using var baseStyle = new TextStyle { Color = new Color32(1, 2, 3, 4) };
            using var markedStyle = payload.Body[1].TextStyle.ToTextStyle(baseStyle);
            Assert.That(markedStyle.HasBackground, Is.True);
            Assert.That(markedStyle.BackgroundColor, Is.EqualTo(new Color32(255, 255, 0, 255)));
            Assert.That(markedStyle.Color, Is.EqualTo(new Color32(1, 2, 3, 4)));
        }

        [TestCase("#3366ff", 0x33, 0x66, 0xff, 0xff)]
        [TestCase("#3366ff80", 0x33, 0x66, 0xff, 0x80)]
        [TestCase("#3c8", 0x33, 0xcc, 0x88, 0xff)]
        [TestCase("#3c88", 0x33, 0xcc, 0x88, 0x88)]
        [TestCase("cyan", 0x00, 0xff, 0xff, 0xff)]
        [TestCase("TRANSPARENT", 0x00, 0x00, 0x00, 0x00)]
        public void ParsesBackgroundColorValues(string value, int r, int g, int b, int a)
        {
            var payload = Parse($"<span style=\"BACKGROUND-COLOR: {value}\">text</span>");

            AssertColor(payload.Body[0].TextStyle.BackgroundColor,
                new Color32((byte)r, (byte)g, (byte)b, (byte)a));
        }

        [Test]
        public void MarkStyleOverridesDefaultYellow()
        {
            var payload = Parse("<mark style=\"background-color: #123456\">text</mark>");

            AssertColor(payload.Body[0].TextStyle.BackgroundColor, new Color32(0x12, 0x34, 0x56, 0xff));
        }

        [Test]
        public void BackgroundIsInheritedAndInvalidValuesPreserveIt()
        {
            var payload = Parse("<mark>a<span style=\"background-color: not-a-color\">b</span>c</mark>");

            foreach (var segment in payload.Body)
            {
                AssertColor(segment.TextStyle.BackgroundColor, new Color32(255, 255, 0, 255));
            }
        }

        [Test]
        public void UnstyledRunsPreserveBaseBackground()
        {
            using var baseStyle = new TextStyle
            {
                BackgroundColor = new Color32(10, 20, 30, 40),
            };
            using var resolved = new TextStyleState().ToTextStyle(baseStyle);

            Assert.That(resolved.HasBackground, Is.True);
            Assert.That(resolved.BackgroundColor, Is.EqualTo(new Color32(10, 20, 30, 40)));

            baseStyle.ClearBackgroundColor();
            Assert.That(baseStyle.HasBackground, Is.False);
            Assert.That(resolved.HasBackground, Is.True);
        }

        private static void AssertColor(Color? actual, Color32 expected)
        {
            Assert.That(actual.HasValue, Is.True);
            Assert.That((Color32)actual.Value, Is.EqualTo(expected));
        }

        private static ParagraphPayload Parse(string markup)
        {
            var parser = new RichTextParser.RichTextParser();
            parser.ParseText(markup);
            return parser.ConvertToSegments();
        }
    }
}
