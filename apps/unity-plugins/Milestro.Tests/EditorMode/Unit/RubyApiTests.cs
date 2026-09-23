using System;
using Milestro.Skia.TextLayout;
using NUnit.Framework;

namespace Milestro.Tests
{
    public class RubyApiTests
    {
        [Test]
        public void ConvertsUtf16RangeToUtf8WithoutSplittingSurrogates()
        {
            ParagraphBuilder.Utf16RangeToUtf8("前😀漢字", 3, 5, out var startUtf8, out var endUtf8);

            Assert.That(startUtf8, Is.EqualTo(7));
            Assert.That(endUtf8, Is.EqualTo(13));
            Assert.Throws<ArgumentException>(() =>
                ParagraphBuilder.Utf16RangeToUtf8("前😀漢字", 2, 5, out _, out _));
        }

        [Test]
        public void RubyStyleRejectsInvalidNumericAndEnumValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new RubyStyle { FontSizeMultiplier = 0 }.Validate());
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new RubyStyle { Gap = -1 }.Validate());
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new RubyStyle { Alignment = (RubyAlignment)99 }.Validate());
            Assert.DoesNotThrow(() => new RubyStyle().Validate());
        }

        [Test]
        public void StyledRubyBuildsAndLayoutsThroughNativeApi()
        {
            using var paragraphStyle = new ParagraphStyle();
            using var textStyle = new TextStyle { FontSize = 32 };
            paragraphStyle.SetTextStyle(textStyle);

            using var builder = new ParagraphBuilder(paragraphStyle);
            builder.AddRubyText("漢字", "かんじ", new RubyStyle
            {
                Position = RubyPosition.Below,
                Alignment = RubyAlignment.SpaceAround,
                Gap = 4,
            });

            using var paragraph = builder.Build();
            paragraph.Layout(400);

            Assert.That(paragraph.Height, Is.GreaterThan(0));
            Assert.That(paragraph.MaxIntrinsicWidth, Is.GreaterThan(0));
        }
    }
}
