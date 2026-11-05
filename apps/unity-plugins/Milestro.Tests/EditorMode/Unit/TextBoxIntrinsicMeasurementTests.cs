using Milestro.Components.Internal;
using Milestro.Extensions;
using Milestro.Model;
using Milestro.RichTextParser;
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
                XmlRichTextParser.Id, wrapMode, TextOverflow.Clip);
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

        [TestCase(TextBoxWrapMode.Wrap)]
        [TestCase(TextBoxWrapMode.NoWrap)]
        public void HorizontalBlockMarginsContributeToIntrinsicWidths(TextBoxWrapMode wrapMode)
        {
            const string text = "Short text";
            var baselineSettings = new TextBoxRenderTargetSettings(text, new Margin(),
                new ParagraphStyleSettings(), new TextStyleSettings { size = 20f },
                XmlRichTextParser.Id, wrapMode, TextOverflow.Clip);
            var marginedSettings = new TextBoxRenderTargetSettings(
                "<p style=\"margin-left: 20px; margin-right: 30px\">Short text</p>",
                new Margin(), new ParagraphStyleSettings(), new TextStyleSettings { size = 20f },
                XmlRichTextParser.Id, wrapMode, TextOverflow.Clip);
            using var baseline = new TextBoxIntrinsicMeasurement(baselineSettings);
            using var margined = new TextBoxIntrinsicMeasurement(marginedSettings);
            baseline.SetAvailableSize(new UnityEngine.Vector2(200f, 100f));
            margined.SetAvailableSize(new UnityEngine.Vector2(200f, 100f));

            var expected = baseline.Measure(20f, 200f);
            var actual = margined.Measure(20f, 200f);

            Assert.That(actual.MinContentWidth - expected.MinContentWidth,
                Is.EqualTo(50f).Within(0.01f));
            Assert.That(actual.MaxContentWidth - expected.MaxContentWidth,
                Is.EqualTo(50f).Within(0.01f));
            Assert.That(actual.ContentWidth - expected.ContentWidth,
                Is.EqualTo(50f).Within(0.01f));
        }

        [TestCase("vw", TextBoxWrapMode.Wrap)]
        [TestCase("vh", TextBoxWrapMode.Wrap)]
        [TestCase("vw", TextBoxWrapMode.NoWrap)]
        [TestCase("vh", TextBoxWrapMode.NoWrap)]
        public void HorizontalViewportBlockMarginsRemainIgnoredInMeasurementAndRendering(string unit,
            TextBoxWrapMode wrapMode)
        {
            const string baseline = "<p style=\"margin-top: 7px; margin-bottom: 12px\">First block</p>" +
                                    "<p style=\"margin-top: 1em; margin-bottom: 10%\">Second block</p>";
            var content = $"<p style=\"margin-top: 7px; margin-top: 20{unit}; margin-bottom: 12px; margin-bottom: 30{unit}\">First block</p>" +
                          $"<p style=\"margin-top: 1em; margin-top: 40{unit}; margin-bottom: 10%; margin-bottom: 50{unit}\">Second block</p>";
            var style = new ParagraphStyleSettings { paragraphSpacing = 9f };
            var textStyle = new TextStyleSettings { size = 20f };
            var baselineSettings = new TextBoxRenderTargetSettings(baseline, new Margin(), style,
                textStyle, XmlRichTextParser.Id, wrapMode, TextOverflow.Clip);
            var settings = new TextBoxRenderTargetSettings(content, new Margin(), style,
                textStyle, XmlRichTextParser.Id, wrapMode, TextOverflow.Clip);
            using var expected = new TextBoxIntrinsicMeasurement(baselineSettings);
            using var actual = new TextBoxIntrinsicMeasurement(settings);
            using var baselineTarget = new TextBoxRenderTarget();
            using var target = new TextBoxRenderTarget();
            foreach (var height in new[] { 100, 200 })
            {
                var available = new UnityEngine.Vector2(160f, height);
                expected.SetAvailableSize(available);
                actual.SetAvailableSize(available);
                var expectedMetrics = expected.Measure(20f, 160f);
                var actualMetrics = actual.Measure(20f, 160f);
                Assert.That(actualMetrics.ContentHeight, Is.EqualTo(expectedMetrics.ContentHeight));
                Assert.That(actualMetrics.NaturalHeight, Is.EqualTo(expectedMetrics.NaturalHeight));
                Assert.That(actualMetrics.ContentWidth, Is.EqualTo(expectedMetrics.ContentWidth));
                var viewport = TextBoxRenderViewport.Invisible(new UnityEngine.Vector2Int(160, height), default);
                Assert.That(baselineTarget.Rebuild(viewport, UnityEngine.ColorSpace.Gamma,
                    baselineSettings, true, null), Is.True);
                Assert.That(target.Rebuild(viewport, UnityEngine.ColorSpace.Gamma,
                    settings, true, null), Is.True);
                Assert.That(target.ContentSize, Is.EqualTo(baselineTarget.ContentSize));
            }
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
                XmlRichTextParser.Id,
                TextBoxWrapMode.Wrap,
                overflow);
            return new TextBoxIntrinsicMeasurement(settings);
        }
    }
}
