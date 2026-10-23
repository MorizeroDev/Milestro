using Milestro.Components.Internal;
using Milestro.Model;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Milestro.Tests
{
    public sealed class SlimTextIntrinsicMeasurementTests
    {
        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(TextWidthMode.Fixed, TextHeightMode.Fixed)]
        [TestCase(TextWidthMode.Fixed, TextHeightMode.Auto)]
        [TestCase(TextWidthMode.MaxContent, TextHeightMode.Fixed)]
        [TestCase(TextWidthMode.MaxContent, TextHeightMode.Auto)]
        [TestCase(TextWidthMode.FitContent, TextHeightMode.Fixed)]
        [TestCase(TextWidthMode.FitContent, TextHeightMode.Auto)]
        public void LayoutElementAppliesEveryModePairAndRemainsStable(TextWidthMode widthMode,
            TextHeightMode heightMode)
        {
            root = new GameObject("Slim parent", typeof(RectTransform));
            ((RectTransform)root.transform).sizeDelta = new Vector2(150f, 60f);
            var child = new GameObject("Slim text", typeof(RectTransform));
            child.transform.SetParent(root.transform, false);
            ((RectTransform)child.transform).sizeDelta = new Vector2(120f, 24f);
            var producer = child.AddComponent<SlimTextRenderTextureProducer>();
            producer.text = "Slim text layout with CJK 漢字";
            producer.fontSize = 24f;
            producer.widthMode = widthMode;
            producer.heightMode = heightMode;
            var fitter = child.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = widthMode == TextWidthMode.Fixed
                ? ContentSizeFitter.FitMode.Unconstrained
                : ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = heightMode == TextHeightMode.Fixed
                ? ContentSizeFitter.FitMode.Unconstrained
                : ContentSizeFitter.FitMode.PreferredSize;
            var expected = producer.ResolveSizing();
            var rect = (RectTransform)producer.transform;

            for (var pass = 0; pass < 8; ++pass)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                Assert.That(rect.rect.width, Is.EqualTo(expected.UsedWidth).Within(0.01f));
                Assert.That(rect.rect.height, Is.EqualTo(expected.UsedHeight).Within(0.01f));
                Assert.That(producer.effectiveFontSize, Is.EqualTo(24f));
            }
        }

        [Test]
        public void SingleLineIntrinsicWidthsAreEqualAndIgnoreRequestedWidth()
        {
            using var measurement = new SlimTextIntrinsicMeasurement("Slim label 漢字", "system-ui", 400, true);

            var natural = measurement.Measure(20f, float.PositiveInfinity);
            var constrained = measurement.Measure(20f, 1f);

            Assert.That(natural.MinContentWidth, Is.GreaterThan(0f));
            Assert.That(natural.MaxContentWidth, Is.EqualTo(natural.MinContentWidth));
            Assert.That(natural.NaturalHeight, Is.GreaterThan(0f));
            Assert.That(constrained.ContentWidth, Is.EqualTo(natural.ContentWidth));
            Assert.That(constrained.ContentHeight, Is.EqualTo(natural.ContentHeight));
        }
    }
}
