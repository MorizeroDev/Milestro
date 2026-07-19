using Milestro.Components.Internal;
using Milestro.Model;
using NUnit.Framework;
using UnityEngine;

namespace Milestro.Tests
{
    public class TextBoxNoWrapHorizontalLayoutTests
    {
        [TestCase(TextAlign.Left, TextDirection.Ltr, 0f, 0f, 0f, 0f)]
        [TestCase(TextAlign.Center, TextDirection.Ltr, 20f, 12f, 0f, 0f)]
        [TestCase(TextAlign.Right, TextDirection.Ltr, 40f, 24f, 0f, 0f)]
        [TestCase(TextAlign.Start, TextDirection.Ltr, 0f, 0f, 0f, 0f)]
        [TestCase(TextAlign.End, TextDirection.Ltr, 40f, 24f, 0f, 0f)]
        [TestCase(TextAlign.Start, TextDirection.Rtl, 40f, 24f, 0f, 0f)]
        [TestCase(TextAlign.End, TextDirection.Rtl, 0f, 0f, 0f, 0f)]
        public void ShortContentUsesViewportForLogicalAlignment(TextAlign align,
            TextDirection direction,
            float expectedVisualLeft,
            float expectedLayoutOffset,
            float expectedInitialScroll,
            float expectedMaxScroll)
        {
            var result = TextBoxNoWrapHorizontalLayout.Resolve(true,
                align,
                direction,
                viewportWidth: 100f,
                contentWidth: 60f,
                layoutWidth: 124f);

            AssertLayout(result,
                60f,
                expectedVisualLeft,
                expectedLayoutOffset,
                expectedInitialScroll,
                expectedMaxScroll);
        }

        [TestCase(TextAlign.Left, 0f, 0f, 0f)]
        [TestCase(TextAlign.Center, 32f, 0f, 0f)]
        [TestCase(TextAlign.Right, 64f, 0f, 0f)]
        public void ContentAtViewportWidthRemovesOnlySafetyAlignment(TextAlign align,
            float expectedLayoutOffset,
            float expectedInitialScroll,
            float expectedMaxScroll)
        {
            var result = TextBoxNoWrapHorizontalLayout.Resolve(true,
                align,
                TextDirection.Ltr,
                viewportWidth: 100f,
                contentWidth: 100f,
                layoutWidth: 164f);

            AssertLayout(result,
                100f,
                0f,
                expectedLayoutOffset,
                expectedInitialScroll,
                expectedMaxScroll);
        }

        [TestCase(TextAlign.Left, 0f, 0f, 20f)]
        [TestCase(TextAlign.Center, 32f, 10f, 20f)]
        [TestCase(TextAlign.Right, 64f, 20f, 20f)]
        [TestCase(TextAlign.Start, 0f, 0f, 20f)]
        [TestCase(TextAlign.End, 64f, 20f, 20f)]
        public void NarrowOverflowDoesNotExposeSafetyPaddingAsScrollLtr(TextAlign align,
            float expectedLayoutOffset,
            float expectedInitialScroll,
            float expectedMaxScroll)
        {
            var result = TextBoxNoWrapHorizontalLayout.Resolve(true,
                align,
                TextDirection.Ltr,
                viewportWidth: 100f,
                contentWidth: 120f,
                layoutWidth: 184f);

            AssertLayout(result,
                120f,
                0f,
                expectedLayoutOffset,
                expectedInitialScroll,
                expectedMaxScroll);
        }

        [TestCase(TextAlign.Start, 64f, 20f)]
        [TestCase(TextAlign.End, 0f, 0f)]
        public void NarrowOverflowResolvesStartEndForRtl(TextAlign align,
            float expectedLayoutOffset,
            float expectedInitialScroll)
        {
            var result = TextBoxNoWrapHorizontalLayout.Resolve(true,
                align,
                TextDirection.Rtl,
                100f,
                120f,
                184f);

            AssertLayout(result,
                120f,
                0f,
                expectedLayoutOffset,
                expectedInitialScroll,
                20f);
        }

        [TestCase(99.75f, 164f, 0.25f, 64f, 0f)]
        [TestCase(100.25f, 165f, 0f, 64.75f, 0.25f)]
        public void NearViewportBoundaryKeepsLogicalAndSafetyWidthsSeparate(float contentWidth,
            float layoutWidth,
            float expectedVisualLeft,
            float expectedLayoutOffset,
            float expectedMaxScroll)
        {
            var result = TextBoxNoWrapHorizontalLayout.Resolve(true,
                TextAlign.Right,
                TextDirection.Ltr,
                100f,
                contentWidth,
                layoutWidth);

            AssertLayout(result,
                contentWidth,
                expectedVisualLeft,
                expectedLayoutOffset,
                expectedMaxScroll,
                expectedMaxScroll);
        }

        [TestCase(TextAlign.Left, 0f, 0f, 80f)]
        [TestCase(TextAlign.Center, 32f, 40f, 80f)]
        [TestCase(TextAlign.Right, 64f, 80f, 80f)]
        public void WideOverflowStartsAtPhysicalAlignmentAnchor(TextAlign align,
            float expectedLayoutOffset,
            float expectedInitialScroll,
            float expectedMaxScroll)
        {
            var result = TextBoxNoWrapHorizontalLayout.Resolve(true,
                align,
                TextDirection.Ltr,
                viewportWidth: 100f,
                contentWidth: 180f,
                layoutWidth: 244f);

            AssertLayout(result,
                180f,
                0f,
                expectedLayoutOffset,
                expectedInitialScroll,
                expectedMaxScroll);
        }

        [Test]
        public void NonWideLayoutReturnsNoHorizontalOverride()
        {
            var result = TextBoxNoWrapHorizontalLayout.Resolve(false,
                TextAlign.Right,
                TextDirection.Ltr,
                viewportWidth: 100f,
                contentWidth: 180f,
                layoutWidth: 244f);

            AssertLayout(result, 0f, 0f, 0f, 0f, 0f);
        }

        [Test]
        public void RelayoutReanchorsOnlyAnUntouchedAlignmentPosition()
        {
            var previous = TextBoxNoWrapHorizontalLayout.Resolve(true,
                TextAlign.Right,
                TextDirection.Ltr,
                100f,
                120f,
                184f);
            var resized = TextBoxNoWrapHorizontalLayout.Resolve(true,
                TextAlign.Right,
                TextDirection.Ltr,
                80f,
                180f,
                244f);

            Assert.That(resized.ResolveScrollX(previous.InitialScrollX,
                true,
                previous.InitialScrollX), Is.EqualTo(100f));
            Assert.That(resized.ResolveScrollX(7f,
                true,
                previous.InitialScrollX), Is.EqualTo(7f));
            Assert.That(previous.ResolveScrollX(99f,
                true,
                7f), Is.EqualTo(20f));
        }

        [Test]
        public void FirstLayoutUsesAlignmentAnchorAndPaintComposesOffsetsIndependently()
        {
            var right = TextBoxNoWrapHorizontalLayout.Resolve(true,
                TextAlign.Right,
                TextDirection.Ltr,
                100f,
                120f,
                184f);
            var center = TextBoxNoWrapHorizontalLayout.Resolve(true,
                TextAlign.Center,
                TextDirection.Ltr,
                100f,
                120f,
                184f);

            var rightScroll = right.ResolveScrollX(0f, false, 0f);
            var centerScroll = center.ResolveScrollX(0f, false, 0f);
            Assert.That(rightScroll, Is.EqualTo(20f));
            Assert.That(centerScroll, Is.EqualTo(10f));
            Assert.That(64f + TextBoxNoWrapHorizontalLayout.ResolvePaintOffsetX(
                right.LayoutAlignmentOffset,
                rightScroll,
                0f), Is.EqualTo(-20f));
            Assert.That(32f + TextBoxNoWrapHorizontalLayout.ResolvePaintOffsetX(
                center.LayoutAlignmentOffset,
                centerScroll,
                0f), Is.EqualTo(-10f));
            Assert.That(64f + TextBoxNoWrapHorizontalLayout.ResolvePaintOffsetX(
                right.LayoutAlignmentOffset,
                7f,
                3f), Is.EqualTo(-10f));
            Assert.That(TextBoxNoWrapHorizontalLayout.ResolvePaintOffsetX(64f, 7f, float.NaN),
                Is.EqualTo(-71f));
        }

        [Test]
        public void FixedViewportKeepsLogicalAndElasticOffsetsSeparate()
        {
            var viewport = TextBoxRenderViewport.Fixed(new Vector2Int(100, 40),
                new Vector2(20f, 7f),
                new Vector2(3f, -2f));

            Assert.That(viewport.RequestedScrollOffset, Is.EqualTo(new Vector2(20f, 7f)));
            Assert.That(viewport.VisualScrollOffset, Is.EqualTo(new Vector2(3f, -2f)));
        }

        private static void AssertLayout(TextBoxNoWrapHorizontalLayout result,
            float contentWidth,
            float expectedVisualLeft,
            float expectedLayoutOffset,
            float expectedInitialScroll,
            float expectedMaxScroll)
        {
            Assert.That(result.ContentWidth, Is.EqualTo(contentWidth).Within(0.0001f));
            Assert.That(result.LogicalVisualLeft, Is.EqualTo(expectedVisualLeft).Within(0.0001f));
            Assert.That(result.LayoutAlignmentOffset, Is.EqualTo(expectedLayoutOffset).Within(0.0001f));
            Assert.That(result.InitialScrollX, Is.EqualTo(expectedInitialScroll).Within(0.0001f));
            Assert.That(result.MaxScrollX, Is.EqualTo(expectedMaxScroll).Within(0.0001f));
        }
    }
}
