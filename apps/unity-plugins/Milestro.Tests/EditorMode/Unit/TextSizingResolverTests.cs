using System;
using Milestro.Model;
using NUnit.Framework;

namespace Milestro.Tests
{
    public sealed class TextSizingResolverTests
    {
        private const float DesignFontSize = 20f;

        [Test]
        public void ResolvesEveryWidthAndHeightModePair()
        {
            var measurement = new SyntheticMeasurement(40f, 120f, 20f);
            var margin = new ResolvedMargin(5f, 2f, 7f, 3f);

            var fixedFixed = Resolve(measurement, TextWidthMode.Fixed, TextHeightMode.Fixed,
                fixedWidth: 100f, fixedHeight: 50f, availableWidth: 80f, wrap: true, margin: margin);
            Assert.That(fixedFixed.UsedWidth, Is.EqualTo(100f));
            Assert.That(fixedFixed.UsedHeight, Is.EqualTo(50f));
            Assert.That(fixedFixed.PreferredWidth, Is.EqualTo(-1f));
            Assert.That(fixedFixed.PreferredHeight, Is.EqualTo(-1f));

            var fixedAuto = Resolve(measurement, TextWidthMode.Fixed, TextHeightMode.Auto,
                fixedWidth: 100f, fixedHeight: 50f, availableWidth: 80f, wrap: true, margin: margin);
            Assert.That(fixedAuto.UsedWidth, Is.EqualTo(100f));
            Assert.That(fixedAuto.UsedHeight, Is.EqualTo(45f));
            Assert.That(fixedAuto.PreferredWidth, Is.EqualTo(-1f));
            Assert.That(fixedAuto.PreferredHeight, Is.EqualTo(45f));

            var maxFixed = Resolve(measurement, TextWidthMode.MaxContent, TextHeightMode.Fixed,
                fixedWidth: 100f, fixedHeight: 50f, availableWidth: 80f, wrap: true, margin: margin);
            Assert.That(maxFixed.UsedWidth, Is.EqualTo(132f));
            Assert.That(maxFixed.UsedHeight, Is.EqualTo(50f));

            var maxAuto = Resolve(measurement, TextWidthMode.MaxContent, TextHeightMode.Auto,
                fixedWidth: 100f, fixedHeight: 50f, availableWidth: 80f, wrap: true, margin: margin);
            Assert.That(maxAuto.UsedWidth, Is.EqualTo(132f));
            Assert.That(maxAuto.UsedHeight, Is.EqualTo(25f));

            var fitFixed = Resolve(measurement, TextWidthMode.FitContent, TextHeightMode.Fixed,
                fixedWidth: 100f, fixedHeight: 50f, availableWidth: 80f, wrap: true, margin: margin);
            Assert.That(fitFixed.UsedWidth, Is.EqualTo(80f));
            Assert.That(fitFixed.UsedHeight, Is.EqualTo(50f));

            var fitAuto = Resolve(measurement, TextWidthMode.FitContent, TextHeightMode.Auto,
                fixedWidth: 100f, fixedHeight: 50f, availableWidth: 80f, wrap: true, margin: margin);
            Assert.That(fitAuto.UsedWidth, Is.EqualTo(80f));
            Assert.That(fitAuto.UsedHeight, Is.EqualTo(45f));
        }

        [Test]
        public void FitContentKeepsMinContentWhenParentIsNarrower()
        {
            var result = Resolve(new SyntheticMeasurement(90f, 120f, 20f),
                TextWidthMode.FitContent,
                TextHeightMode.Auto,
                fixedWidth: 100f,
                fixedHeight: 50f,
                availableWidth: 60f,
                wrap: true,
                margin: new ResolvedMargin(5f, 0f, 7f, 0f));

            Assert.That(result.UsedWidth, Is.EqualTo(102f));
            Assert.That(result.MinWidth, Is.EqualTo(102f));
        }

        [Test]
        public void FitContentUsesMaxContentWhenAvailableWidthIsUnbounded()
        {
            var result = Resolve(new SyntheticMeasurement(40f, 120f, 20f),
                TextWidthMode.FitContent,
                TextHeightMode.Auto,
                fixedWidth: 100f,
                fixedHeight: 50f,
                availableWidth: float.PositiveInfinity,
                wrap: true,
                margin: new ResolvedMargin(5f, 0f, 7f, 0f));

            Assert.That(result.UsedWidth, Is.EqualTo(132f));
        }

        [Test]
        public void NoWrapFitContentUsesSingleLineIntrinsicWidth()
        {
            var result = Resolve(new SingleLineMeasurement(120f, 20f),
                TextWidthMode.FitContent,
                TextHeightMode.Auto,
                fixedWidth: 100f,
                fixedHeight: 50f,
                availableWidth: 80f,
                wrap: false,
                margin: new ResolvedMargin(5f, 0f, 7f, 0f));

            Assert.That(result.UsedWidth, Is.EqualTo(132f));
            Assert.That(result.UsedHeight, Is.EqualTo(20f));
        }

        [Test]
        public void MaxContentWrapReflowsWhenParentAssignsNarrowerWidth()
        {
            var request = new TextSizingRequest(TextWidthMode.MaxContent, TextHeightMode.Auto,
                100f, 50f, 300f, true, DesignFontSize);
            var result = TextSizingResolver.Resolve(new SyntheticMeasurement(40f, 120f, 20f),
                request, _ => default, assignedWidth: 52f);

            Assert.That(result.PreferredWidth, Is.EqualTo(120f));
            Assert.That(result.UsedWidth, Is.EqualTo(52f));
            Assert.That(result.UsedHeight, Is.EqualTo(60f));
        }

        [Test]
        public void MaxContentNoWrapKeepsSingleLineWhenParentAssignsNarrowerWidth()
        {
            var request = new TextSizingRequest(TextWidthMode.MaxContent, TextHeightMode.Auto,
                100f, 50f, 300f, false, DesignFontSize);
            var result = TextSizingResolver.Resolve(new SyntheticMeasurement(40f, 120f, 20f),
                request, _ => default, assignedWidth: 52f);

            Assert.That(result.PreferredWidth, Is.EqualTo(120f));
            Assert.That(result.UsedWidth, Is.EqualTo(52f));
            Assert.That(result.UsedHeight, Is.EqualTo(20f));
        }

        [Test]
        public void AssignedWidthControlsReflowWithoutChangingPreferredWidthOrGrowingFont()
        {
            var request = new TextSizingRequest(TextWidthMode.FitContent, TextHeightMode.Auto,
                100f, 50f, 80f, true, DesignFontSize, true, 10f, 40f);
            var margin = new ResolvedMargin(5f, 2f, 7f, 3f);
            var result = TextSizingResolver.Resolve(new SyntheticMeasurement(40f, 120f, 20f),
                request, _ => margin, assignedWidth: 52f);

            Assert.That(result.UsedWidth, Is.EqualTo(52f));
            Assert.That(result.PreferredWidth, Is.EqualTo(80f));
            Assert.That(result.UsedHeight, Is.EqualTo(65f));
            Assert.That(result.EffectiveFontSize, Is.EqualTo(DesignFontSize));
        }

        [Test]
        public void AutoFontSizeIsNoOpWhenBothAxesUseContentSizing()
        {
            var result = Resolve(new SyntheticMeasurement(40f, 240f, 20f),
                TextWidthMode.FitContent,
                TextHeightMode.Auto,
                fixedWidth: 100f,
                fixedHeight: 50f,
                availableWidth: 300f,
                wrap: true,
                autoFontSize: true,
                minFontSize: 10f,
                maxFontSize: 40f);

            Assert.That(result.EffectiveFontSize, Is.EqualTo(DesignFontSize));
        }

        [Test]
        public void ContentSizingNeverGrowsAutoFontSize()
        {
            var result = Resolve(new SyntheticMeasurement(10f, 20f, 10f),
                TextWidthMode.MaxContent,
                TextHeightMode.Auto,
                fixedWidth: 100f,
                fixedHeight: 50f,
                availableWidth: 100f,
                wrap: false,
                autoFontSize: true,
                minFontSize: 10f,
                maxFontSize: 40f);

            Assert.That(result.EffectiveFontSize, Is.EqualTo(DesignFontSize));
        }

        [Test]
        public void AutoFontSizeCanGrowInsideFullyFixedBounds()
        {
            var result = Resolve(new SyntheticMeasurement(20f, 120f, 20f),
                TextWidthMode.Fixed,
                TextHeightMode.Fixed,
                fixedWidth: 200f,
                fixedHeight: 50f,
                availableWidth: 200f,
                wrap: false,
                autoFontSize: true,
                minFontSize: 10f,
                maxFontSize: 40f);

            Assert.That(result.EffectiveFontSize, Is.GreaterThan(DesignFontSize));
            Assert.That(result.EffectiveFontSize, Is.LessThan(40f));
        }

        [Test]
        public void AutoFontSizeShrinksFullyFixedBoundsToConfiguredMinimum()
        {
            var result = Resolve(new SyntheticMeasurement(20f, 240f, 40f),
                TextWidthMode.Fixed,
                TextHeightMode.Fixed,
                fixedWidth: 60f,
                fixedHeight: 10f,
                availableWidth: 60f,
                wrap: false,
                autoFontSize: true,
                minFontSize: 10f,
                maxFontSize: 40f);

            Assert.That(result.EffectiveFontSize, Is.EqualTo(10f));
        }

        [Test]
        public void FixedWidthAndAutoHeightReflowWithoutFontShrink()
        {
            var result = Resolve(new SyntheticMeasurement(20f, 200f, 20f),
                TextWidthMode.Fixed,
                TextHeightMode.Auto,
                fixedWidth: 60f,
                fixedHeight: 50f,
                availableWidth: 300f,
                wrap: true,
                autoFontSize: true,
                minFontSize: 10f,
                maxFontSize: 40f);

            Assert.That(result.EffectiveFontSize, Is.EqualTo(DesignFontSize));
            Assert.That(result.UsedHeight, Is.GreaterThan(20f));
        }

        [Test]
        public void FixedWidthNoWrapCanShrinkAutoFontSize()
        {
            var result = Resolve(new SyntheticMeasurement(20f, 200f, 20f),
                TextWidthMode.Fixed,
                TextHeightMode.Auto,
                fixedWidth: 112f,
                fixedHeight: 50f,
                availableWidth: 300f,
                wrap: false,
                autoFontSize: true,
                minFontSize: 10f,
                maxFontSize: 40f,
                margin: new ResolvedMargin(5f, 0f, 7f, 0f));

            Assert.That(result.EffectiveFontSize, Is.LessThan(DesignFontSize));
            Assert.That(result.EffectiveFontSize, Is.GreaterThanOrEqualTo(10f));
        }

        [Test]
        public void FixedHeightCanShrinkFontWithContentWidth()
        {
            var result = Resolve(new SyntheticMeasurement(30f, 120f, 40f),
                TextWidthMode.MaxContent,
                TextHeightMode.Fixed,
                fixedWidth: 100f,
                fixedHeight: 20f,
                availableWidth: 300f,
                wrap: false,
                autoFontSize: true,
                minFontSize: 10f,
                maxFontSize: 40f);

            Assert.That(result.EffectiveFontSize, Is.LessThan(DesignFontSize));
            Assert.That(result.UsedHeight, Is.EqualTo(20f));
        }

        [Test]
        public void RepeatedResolutionProducesIdenticalSizeAndFont()
        {
            var measurement = new SyntheticMeasurement(40f, 240f, 20f);
            var first = Resolve(measurement, TextWidthMode.Fixed, TextHeightMode.Auto,
                fixedWidth: 100f, fixedHeight: 40f, availableWidth: 200f, wrap: true,
                autoFontSize: true, minFontSize: 10f, maxFontSize: 40f);

            for (var pass = 0; pass < 20; ++pass)
            {
                var next = Resolve(measurement, TextWidthMode.Fixed, TextHeightMode.Auto,
                    fixedWidth: 100f, fixedHeight: 40f, availableWidth: 200f, wrap: true,
                    autoFontSize: true, minFontSize: 10f, maxFontSize: 40f);
                Assert.That(next.UsedWidth, Is.EqualTo(first.UsedWidth));
                Assert.That(next.UsedHeight, Is.EqualTo(first.UsedHeight));
                Assert.That(next.EffectiveFontSize, Is.EqualTo(first.EffectiveFontSize));
            }
        }

        [Test]
        public void AutoFontSizeIsStableAcrossWrappedLineBreaks()
        {
            var measurement = new SyntheticMeasurement(40f, 240f, 20f);
            var first = Resolve(measurement,
                TextWidthMode.Fixed,
                TextHeightMode.Fixed,
                fixedWidth: 100f,
                fixedHeight: 40f,
                availableWidth: 100f,
                wrap: true,
                autoFontSize: true,
                minFontSize: 10f,
                maxFontSize: 40f);

            for (var pass = 0; pass < 20; ++pass)
            {
                var next = Resolve(measurement,
                    TextWidthMode.Fixed,
                    TextHeightMode.Fixed,
                    fixedWidth: 100f,
                    fixedHeight: 40f,
                    availableWidth: 100f,
                    wrap: true,
                    autoFontSize: true,
                    minFontSize: 10f,
                    maxFontSize: 40f);

                Assert.That(next.EffectiveFontSize, Is.EqualTo(first.EffectiveFontSize));
                Assert.That(next.UsedWidth, Is.EqualTo(first.UsedWidth));
                Assert.That(next.UsedHeight, Is.EqualTo(first.UsedHeight));
            }

            Assert.That(first.EffectiveFontSize, Is.GreaterThan(16f));
            Assert.That(first.EffectiveFontSize, Is.LessThan(17f));
        }

        [Test]
        public void AutoFontSizeUsesParagraphNoWrapModeDuringFit()
        {
            var result = Resolve(new SyntheticMeasurement(40f, 240f, 20f),
                TextWidthMode.Fixed,
                TextHeightMode.Fixed,
                fixedWidth: 100f,
                fixedHeight: 40f,
                availableWidth: 100f,
                wrap: false,
                autoFontSize: true,
                minFontSize: 10f,
                maxFontSize: 40f);

            Assert.That(result.EffectiveFontSize, Is.EqualTo(10f));
        }

        private static TextSizingResult Resolve(ITextIntrinsicMeasurement measurement,
            TextWidthMode widthMode,
            TextHeightMode heightMode,
            float fixedWidth,
            float fixedHeight,
            float availableWidth,
            bool wrap,
            bool autoFontSize = false,
            float minFontSize = 10f,
            float maxFontSize = 40f,
            ResolvedMargin margin = default)
        {
            var request = new TextSizingRequest(widthMode,
                heightMode,
                fixedWidth,
                fixedHeight,
                availableWidth,
                wrap,
                DesignFontSize,
                autoFontSize,
                minFontSize,
                maxFontSize,
                widthCap: widthMode == TextWidthMode.FitContent
                    ? availableWidth
                    : float.PositiveInfinity,
                heightCap: float.PositiveInfinity);
            return TextSizingResolver.Resolve(measurement, request, _ => margin);
        }

        private sealed class SyntheticMeasurement : ITextIntrinsicMeasurement
        {
            private readonly float minContentWidth;
            private readonly float maxContentWidth;
            private readonly float naturalHeight;

            public SyntheticMeasurement(float minContentWidth, float maxContentWidth, float naturalHeight)
            {
                this.minContentWidth = minContentWidth;
                this.maxContentWidth = maxContentWidth;
                this.naturalHeight = naturalHeight;
            }

            public TextIntrinsicMetrics Measure(float fontSize, float contentWidth)
            {
                var scale = fontSize / DesignFontSize;
                var minWidth = minContentWidth * scale;
                var maxWidth = maxContentWidth * scale;
                var natural = naturalHeight * scale;
                var lines = float.IsInfinity(contentWidth)
                    ? 1f
                    : Math.Max(1f, (float)Math.Ceiling(maxWidth / Math.Max(0.01f, contentWidth)));
                return new TextIntrinsicMetrics(minWidth,
                    maxWidth,
                    natural,
                    float.IsInfinity(contentWidth) ? maxWidth : Math.Min(maxWidth, contentWidth),
                    natural * lines);
            }
        }

        private sealed class SingleLineMeasurement : ITextIntrinsicMeasurement
        {
            private readonly float width;
            private readonly float height;

            public SingleLineMeasurement(float width, float height)
            {
                this.width = width;
                this.height = height;
            }

            public TextIntrinsicMetrics Measure(float fontSize, float contentWidth)
            {
                var scale = fontSize / DesignFontSize;
                return new TextIntrinsicMetrics(width * scale, width * scale,
                    height * scale, width * scale, height * scale);
            }
        }
    }
}
