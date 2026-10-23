using Milestro.Components.Internal;
using NUnit.Framework;
using UnityEngine;

namespace Milestro.Tests
{
    public class TextAutoFontSizeScaleTests
    {
        [Test]
        public void ContentFittingAtBaseSizeWithShrinkOnlyRangeStaysAtOne()
        {
            var scale = TextAutoFontSizeScale.Resolve(50f, 20f, 100f, 40f, 36f, 18f, 36f);
            Assert.AreEqual(1f, scale, 1e-4f);
        }

        [Test]
        public void OverflowingContentShrinksToFitTheTighterAxis()
        {
            var widthDriven = TextAutoFontSizeScale.Resolve(200f, 40f, 100f, 40f, 36f, 18f, 36f);
            Assert.AreEqual(0.5f, widthDriven, 1e-4f);

            var heightDriven = TextAutoFontSizeScale.Resolve(100f, 80f, 100f, 40f, 36f, 18f, 36f);
            Assert.AreEqual(0.5f, heightDriven, 1e-4f);

            var widthTighter = TextAutoFontSizeScale.Resolve(200f, 60f, 100f, 40f, 36f, 18f, 36f);
            Assert.AreEqual(0.5f, widthTighter, 1e-4f);
        }

        [Test]
        public void ExtremeOverflowClampsToMinScale()
        {
            var scale = TextAutoFontSizeScale.Resolve(10000f, 4000f, 100f, 40f, 36f, 18f, 36f);
            Assert.AreEqual(0.5f, scale, 1e-4f);
        }

        [Test]
        public void SmallContentGrowsUpToMaxScale()
        {
            var scale = TextAutoFontSizeScale.Resolve(10f, 5f, 100f, 40f, 18f, 18f, 36f);
            Assert.AreEqual(2f, scale, 1e-4f);
        }

        [Test]
        public void ContentBetweenBaseAndMaxGrowsPartially()
        {
            var scale = TextAutoFontSizeScale.Resolve(100f, 40f, 150f, 60f, 18f, 18f, 36f);
            Assert.AreEqual(1.5f, scale, 1e-4f);
        }

        [Test]
        public void EmptyContentUsesMaxScale()
        {
            var scale = TextAutoFontSizeScale.Resolve(0f, 0f, 100f, 40f, 18f, 18f, 36f);
            Assert.AreEqual(2f, scale, 1e-4f);
        }

        [Test]
        public void UnmeasuredAxisDoesNotConstrain()
        {
            var scale = TextAutoFontSizeScale.Resolve(0f, 80f, 100f, 40f, 36f, 18f, 36f);
            Assert.AreEqual(0.5f, scale, 1e-4f);
        }

        [Test]
        public void NonFiniteInputsFallBackToOne()
        {
            Assert.AreEqual(1f, TextAutoFontSizeScale.Resolve(float.NaN, 20f, 100f, 40f, 36f, 18f, 36f), 1e-4f);
            Assert.AreEqual(1f, TextAutoFontSizeScale.Resolve(50f, 20f, float.PositiveInfinity, 40f, 36f, 18f, 36f), 1e-4f);
            Assert.AreEqual(1f, TextAutoFontSizeScale.Resolve(50f, 20f, 100f, 40f, 0f, 18f, 36f), 1e-4f);
            Assert.AreEqual(1f, TextAutoFontSizeScale.Resolve(50f, 20f, 100f, 40f, 36f, 36f, 18f), 1e-4f);
        }

        [Test]
        public void SearchFitReturnsMaxScaleWhenEverythingFits()
        {
            var scale = TextAutoFontSizeScale.SearchFit(_ => new Vector2(10f, 5f), 100f, 40f, 0.5f, 2f);
            Assert.AreEqual(2f, scale, 1e-4f);
        }

        [Test]
        public void SearchFitReturnsMinScaleWhenNothingFits()
        {
            var scale = TextAutoFontSizeScale.SearchFit(_ => new Vector2(10000f, 4000f), 100f, 40f, 0.5f, 2f);
            Assert.AreEqual(0.5f, scale, 1e-4f);
        }

        [Test]
        public void SearchFitFindsContinuousBoundary()
        {
            // Content whose laid-out height at virtual width W/s is 200/s pixels
            // tall (i.e. exactly like scaling a fixed 200px block). Fits while
            // s * (200/s) = 200 <= ... never fits above 1 within height 40? Use
            // a simpler model: height(s) = 40/s -> s*height = 40, always fits;
            // width(s) = 100 constant -> needs s <= 100/100 = 1.
            var scale = TextAutoFontSizeScale.SearchFit(s2 => new Vector2(100f, 40f / s2), 100f, 400f, 0.25f, 4f);
            Assert.AreEqual(1f, scale, 0.02f);
        }

        [Test]
        public void SearchFitIsDeterministicAcrossLineBreakDiscontinuity()
        {
            // Regression: a line-count step function makes naive fixed-point
            // iteration oscillate forever between two scales (visible flicker).
            // Model: 500px of advance, 100px viewport, 10px line height:
            // laid out at width 100/s the line count is ceil(5s).
            Vector2 Measure(float scale)
            {
                var lines = Mathf.CeilToInt(5f * scale);
                return new Vector2(500f / lines, lines * 10f);
            }

            var scale = TextAutoFontSizeScale.SearchFit(Measure, 100f, 25f, 0.25f, 4f);

            // Largest fitting scale: ceil(5s)*10*s <= 25 holds up to s=0.625.
            Assert.GreaterOrEqual(scale, 0.6f);
            Assert.LessOrEqual(scale, 0.65f);

            // Deterministic: repeating the search yields the same value.
            var again = TextAutoFontSizeScale.SearchFit(Measure, 100f, 25f, 0.25f, 4f);
            Assert.AreEqual(scale, again, 1e-6f);
        }

        [Test]
        public void SearchFitFallsBackToOneForInvalidRange()
        {
            var scale = TextAutoFontSizeScale.SearchFit(_ => new Vector2(10f, 10f), 100f, 40f, 2f, 0.5f);
            Assert.AreEqual(1f, scale, 1e-4f);
        }

        [Test]
        public void CollapsedViewportClampsToMinScale()
        {
            var scale = TextAutoFontSizeScale.Resolve(50f, 20f, 0f, 0f, 36f, 18f, 36f);
            Assert.AreEqual(0.5f, scale, 1e-4f);
        }
    }
}
