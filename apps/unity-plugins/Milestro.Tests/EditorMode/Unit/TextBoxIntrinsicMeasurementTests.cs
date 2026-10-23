using Milestro.Components.Internal;
using Milestro.Extensions;
using Milestro.Model;
using NUnit.Framework;

namespace Milestro.Tests
{
    public sealed class TextBoxIntrinsicMeasurementTests
    {
        [TestCase(TextBoxWrapMode.Wrap)]
        [TestCase(TextBoxWrapMode.NoWrap)]
        public void NaturalHeightMatchesUnwrappedParagraphAtIntrinsicWidthBoundary(TextBoxWrapMode wrapMode)
        {
            const string content = "互联网小章鱼1";
            var settings = new TextBoxRenderTargetSettings(content, new Margin(),
                new ParagraphStyleSettings(), new TextStyleSettings { size = 36f },
                wrapMode, TextOverflow.Clip);
            using var measurement = new TextBoxIntrinsicMeasurement(settings);
            var parser = new Milestro.RichTextParser.RichTextParser();
            parser.ParseText(content);
            using var paragraphStyle = new ParagraphStyleSettings().ToParagraphStyle(false);
            using var textStyle = new TextStyleSettings { size = 36f }.ToTextStyle();
            var paragraphs = parser.ConvertToSegments().ToParagraphs(paragraphStyle, textStyle);
            try
            {
                Assert.That(paragraphs.Count, Is.EqualTo(1));
                var paragraph = paragraphs[0];
                paragraph.Layout(4096f);
                var expectedHeight = paragraph.Height;
                var natural = measurement.Measure(36f, float.PositiveInfinity);
                var atNaturalWidth = measurement.Measure(36f, natural.MaxContentWidth);

                Assert.That(natural.NaturalHeight, Is.EqualTo(expectedHeight).Within(0.01f));
                Assert.That(natural.ContentHeight, Is.EqualTo(expectedHeight).Within(0.01f));
                Assert.That(atNaturalWidth.ContentHeight, Is.EqualTo(expectedHeight).Within(0.01f));
                if (wrapMode == TextBoxWrapMode.NoWrap)
                    Assert.That(measurement.Measure(36f, 80f).ContentHeight,
                        Is.EqualTo(expectedHeight).Within(0.01f));
            }
            finally
            {
                foreach (var paragraph in paragraphs) paragraph.Dispose();
            }
        }

        [Test]
        public void RichTextCjkMarginsAndWrapProduceIntrinsicMetrics()
        {
            const string content = "<p style=\"margin-bottom: 8px\">An unbreakableword with spaces</p>" +
                                   "<p style=\"margin-top: 1em; margin-bottom: 10%\">第二段文字和 emoji 😀</p>";
            using var measurement = CreateMeasurement(content, TextOverflow.Clip, 12f);
            measurement.SetAvailableSize(new UnityEngine.Vector2(160f, 120f));

            var natural = measurement.Measure(20f, float.PositiveInfinity);
            var wrapped = measurement.Measure(20f, 44f);

            Assert.That(natural.MaxContentWidth, Is.GreaterThan(natural.MinContentWidth),
                $"Min width {natural.MinContentWidth}; max width {natural.MaxContentWidth}; " +
                $"natural height {natural.NaturalHeight}; wrapped height {wrapped.ContentHeight}");
            Assert.That(natural.NaturalHeight, Is.GreaterThan(0f));
            Assert.That(wrapped.ContentHeight,
                Is.GreaterThan(natural.NaturalHeight),
                $"Natural height {natural.NaturalHeight}; wrapped height {wrapped.ContentHeight}; " +
                $"max width {natural.MaxContentWidth}; content width {wrapped.ContentWidth}");
            Assert.That(wrapped.MinContentWidth, Is.EqualTo(natural.MinContentWidth));
            Assert.That(wrapped.MaxContentWidth, Is.EqualTo(natural.MaxContentWidth));
        }

        [Test]
        public void EllipsisDoesNotChangeIntrinsicMeasurement()
        {
            const string content = "A long line with CJK 漢字 and emoji 😀 that wraps naturally";
            using var clip = CreateMeasurement(content, TextOverflow.Clip, 0f);
            using var ellipsis = CreateMeasurement(content, TextOverflow.Ellipsis, 0f);
            var clipMetrics = clip.Measure(20f, 48f);
            var ellipsisMetrics = ellipsis.Measure(20f, 48f);

            Assert.That(ellipsisMetrics.MinContentWidth, Is.EqualTo(clipMetrics.MinContentWidth));
            Assert.That(ellipsisMetrics.MaxContentWidth, Is.EqualTo(clipMetrics.MaxContentWidth));
            Assert.That(ellipsisMetrics.NaturalHeight, Is.EqualTo(clipMetrics.NaturalHeight));
            Assert.That(ellipsisMetrics.ContentHeight, Is.EqualTo(clipMetrics.ContentHeight));
        }

        [Test]
        public void PercentageBlockMarginsResolveAgainstExternalAvailableHeight()
        {
            using var measurement = CreateMeasurement(
                "<p style=\"margin-bottom: 10%\">First block</p><p>Second block</p>",
                TextOverflow.Clip,
                0f);

            measurement.SetAvailableSize(new UnityEngine.Vector2(160f, 100f));
            var first = measurement.Measure(20f, float.PositiveInfinity).NaturalHeight;
            measurement.SetAvailableSize(new UnityEngine.Vector2(160f, 200f));
            var second = measurement.Measure(20f, float.PositiveInfinity).NaturalHeight;

            Assert.That(second - first, Is.EqualTo(10f).Within(0.1f));
        }

        private static TextBoxIntrinsicMeasurement CreateMeasurement(string content,
            TextOverflow overflow,
            float paragraphSpacing)
        {
            var paragraphStyle = new ParagraphStyleSettings { paragraphSpacing = paragraphSpacing };
            var textStyle = new TextStyleSettings { size = 20f };
            var settings = new TextBoxRenderTargetSettings(content,
                new Margin(),
                paragraphStyle,
                textStyle,
                TextBoxWrapMode.Wrap,
                overflow);
            return new TextBoxIntrinsicMeasurement(settings);
        }
    }
}
