using Milestro.Components;
using Milestro.Components.Internal;
using Milestro.Model;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Milestro.Tests
{
    public sealed class TextBoxContentSizingLayoutTests
    {
        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        [TestCase(TextWidthMode.Fixed, TextHeightMode.Fixed)]
        [TestCase(TextWidthMode.Fixed, TextHeightMode.Auto)]
        [TestCase(TextWidthMode.MaxContent, TextHeightMode.Fixed)]
        [TestCase(TextWidthMode.MaxContent, TextHeightMode.Auto)]
        [TestCase(TextWidthMode.FitContent, TextHeightMode.Fixed)]
        [TestCase(TextWidthMode.FitContent, TextHeightMode.Auto)]
        public void PreferredSizeFitterAppliesModesAndRemainsStable(TextWidthMode widthMode,
            TextHeightMode heightMode)
        {
            var producer = CreateText(out var layout, out var fitter);
            layout.widthMode = widthMode;
            layout.heightMode = heightMode;
            fitter.horizontalFit = widthMode == TextWidthMode.Fixed
                ? ContentSizeFitter.FitMode.Unconstrained : ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = heightMode == TextHeightMode.Fixed
                ? ContentSizeFitter.FitMode.Unconstrained : ContentSizeFitter.FitMode.PreferredSize;
            var expected = producer.ResolveSizing();
            var rect = (RectTransform)producer.transform;

            for (var pass = 0; pass < 8; ++pass)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                Assert.That(rect.rect.width, Is.EqualTo(expected.UsedWidth).Within(0.01f));
                Assert.That(rect.rect.height, Is.EqualTo(expected.UsedHeight).Within(0.01f));
                Assert.That(producer.effectiveFontSize, Is.EqualTo(36f));
            }
        }

        [TestCase(TextWidthMode.FitContent, false)]
        [TestCase(TextWidthMode.FitContent, true)]
        [TestCase(TextWidthMode.MaxContent, false)]
        [TestCase(TextWidthMode.MaxContent, true)]
        public void MinSizeFitterMeasuresAutoHeightAtAssignedWidth(TextWidthMode widthMode, bool withMargin)
        {
            var producer = CreateText(out var layout, out var fitter);
            if (withMargin)
            {
                var margin = new Margin();
                margin.Left = new MarginValue { Value = 5f };
                margin.Right = new MarginValue { Value = 7f };
                margin.Top = new MarginValue { Value = 4f };
                margin.Bottom = new MarginValue { Value = 6f };
                producer.margin = margin;
            }
            layout.widthMode = widthMode;
            layout.heightMode = TextHeightMode.Auto;
            fitter.horizontalFit = ContentSizeFitter.FitMode.MinSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var rect = (RectTransform)producer.transform;

            for (var pass = 0; pass < 8; ++pass)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                var margin = producer.ResolveSizing().Margin;
                var expectedHeight = producer.Measure(36f, rect.rect.width - margin.FixedHorizontalSize)
                    .ContentHeight + margin.FixedVerticalSize;
                Assert.That(rect.rect.width, Is.EqualTo(layout.minWidth).Within(0.01f));
                Assert.That(rect.rect.height, Is.EqualTo(expectedHeight).Within(0.01f),
                    "Auto height must include every line at the width assigned by Unity layout.");
                Assert.That(producer.effectiveFontSize, Is.EqualTo(36f));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LayoutGroupMeasuresHeightAfterAllocatingSiblingWidths(bool autoFontSize)
        {
            var producer = CreateText(out var layout, out var fitter);
            fitter.enabled = false;
            layout.widthMode = TextWidthMode.FitContent;
            layout.heightMode = TextHeightMode.Auto;
            producer.enableAutoFontSize = autoFontSize;
            producer.fontSizeMin = 12f;
            producer.fontSizeMax = 72f;
            var group = root.AddComponent<HorizontalLayoutGroup>();
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
            var sibling = new GameObject("Sibling", typeof(RectTransform), typeof(LayoutElement));
            sibling.transform.SetParent(root.transform, false);
            var siblingLayout = sibling.GetComponent<LayoutElement>();
            siblingLayout.minWidth = 100f;
            siblingLayout.preferredWidth = 100f;
            var rect = (RectTransform)producer.transform;

            for (var pass = 0; pass < 8; ++pass)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)root.transform);
                Assert.That(rect.rect.width, Is.EqualTo(50f).Within(0.01f));
                var expectedHeight = producer.Measure(36f, rect.rect.width).ContentHeight;
                Assert.That(rect.rect.height, Is.EqualTo(expectedHeight).Within(0.01f));
                Assert.That(producer.effectiveFontSize, Is.EqualTo(36f));
            }
        }

        [TestCase(TextWidthMode.MaxContent, TextBoxWrapMode.Wrap)]
        [TestCase(TextWidthMode.MaxContent, TextBoxWrapMode.NoWrap)]
        [TestCase(TextWidthMode.FitContent, TextBoxWrapMode.Wrap)]
        [TestCase(TextWidthMode.FitContent, TextBoxWrapMode.NoWrap)]
        public void PreferredWidthAndAutoHeightFollowWidthAndWrapModes(TextWidthMode widthMode,
            TextBoxWrapMode wrapMode)
        {
            var producer = CreateText(out var layout, out var fitter);
            producer.wrapMode = wrapMode;
            layout.widthMode = widthMode;
            layout.heightMode = TextHeightMode.Auto;
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var natural = producer.Measure(36f, float.PositiveInfinity);
            TestContext.WriteLine($"plain intrinsic min={natural.MinContentWidth} max={natural.MaxContentWidth} " +
                                  $"natural={natural.NaturalHeight}");
            var constrained = widthMode == TextWidthMode.FitContent && wrapMode == TextBoxWrapMode.Wrap;
            var expectedWidth = constrained ? 150f : natural.MaxContentWidth;
            var rect = (RectTransform)producer.transform;

            Assert.That(natural.MaxContentWidth, Is.GreaterThan(150f));
            for (var pass = 0; pass < 8; ++pass)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                Assert.That(rect.rect.width, Is.EqualTo(expectedWidth).Within(0.01f));
                Assert.That(rect.rect.height, constrained
                    ? Is.GreaterThan(natural.NaturalHeight)
                    : Is.EqualTo(natural.NaturalHeight).Within(0.01f));
                Assert.That(producer.effectiveFontSize, Is.EqualTo(36f));
            }
        }

        [TestCase(TextWidthMode.Fixed, TextBoxWrapMode.Wrap)]
        [TestCase(TextWidthMode.Fixed, TextBoxWrapMode.NoWrap)]
        [TestCase(TextWidthMode.MaxContent, TextBoxWrapMode.Wrap)]
        [TestCase(TextWidthMode.MaxContent, TextBoxWrapMode.NoWrap)]
        [TestCase(TextWidthMode.FitContent, TextBoxWrapMode.Wrap)]
        [TestCase(TextWidthMode.FitContent, TextBoxWrapMode.NoWrap)]
        public void AssignedWidthHonorsWrapInMeasurementAndRenderLayout(TextWidthMode widthMode,
            TextBoxWrapMode wrapMode)
        {
            var producer = CreateText(out var layout, out var fitter);
            fitter.enabled = false;
            producer.wrapMode = wrapMode;
            layout.widthMode = widthMode;
            layout.heightMode = TextHeightMode.Auto;
            var sizing = producer.ResolveSizingAtLayoutWidth();
            var natural = producer.Measure(36f, float.PositiveInfinity);
            var settings = TextBoxRenderTargetSettingsBuilder.BuildFrom(producer)
                .WithSizing(sizing, new Vector2(150f, 1000f));
            var viewport = TextBoxRenderViewport.Invisible(new Vector2Int(120, 200), default);
            using var target = new TextBoxRenderTarget();

            for (var pass = 0; pass < 8; ++pass)
            {
                Assert.That(target.Rebuild(viewport, ColorSpace.Gamma, settings, pass == 0, null), Is.True);
                Assert.That(target.ContentSize.y, Is.EqualTo(sizing.ContentHeight).Within(0.01f),
                    "Render layout and Auto height must use the same paragraph wrap setting at the assigned width.");
                Assert.That(target.ContentSize.y, wrapMode == TextBoxWrapMode.Wrap
                    ? Is.GreaterThan(natural.NaturalHeight)
                    : Is.EqualTo(natural.NaturalHeight).Within(0.01f));
            }
        }

        [TestCase(TextWidthMode.MaxContent, TextBoxWrapMode.Wrap)]
        [TestCase(TextWidthMode.FitContent, TextBoxWrapMode.NoWrap)]
        public void ContentChangeUpdatesPreferredSizeAndFittedRect(TextWidthMode widthMode,
            TextBoxWrapMode wrapMode)
        {
            var producer = CreateText(out var layout, out var fitter);
            root.AddComponent<Canvas>();
            producer.wrapMode = wrapMode;
            layout.widthMode = widthMode;
            layout.heightMode = TextHeightMode.Auto;
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var rect = (RectTransform)producer.transform;
            producer.content = "A";
            Canvas.ForceUpdateCanvases();
            var shortWidth = rect.rect.width;

            producer.content = "A much longer paragraph that must make the fitted rect wider";
            Canvas.ForceUpdateCanvases();

            Assert.That(rect.rect.width, Is.GreaterThan(shortWidth + 1f),
                "Changing content must update the fitted rect");
            Assert.That(layout.preferredWidth, Is.GreaterThan(shortWidth + 1f),
                "Changing content must invalidate the cached layout measurement");
            Assert.That(layout.preferredWidth, Is.EqualTo(rect.rect.width).Within(0.01f));
        }

        [Test]
        public void SerializedContentChangeUpdatesPreferredSizeAndFittedRect()
        {
            var producer = CreateText(out var layout, out var fitter);
            root.AddComponent<Canvas>();
            layout.widthMode = TextWidthMode.FitContent;
            layout.heightMode = TextHeightMode.Auto;
            producer.wrapMode = TextBoxWrapMode.NoWrap;
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var rect = (RectTransform)producer.transform;
            using var serialized = new SerializedObject(producer);
            var content = serialized.FindProperty("m_content");

            content.stringValue = "A";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Canvas.ForceUpdateCanvases();
            var shortWidth = rect.rect.width;

            serialized.Update();
            content = serialized.FindProperty("m_content");
            content.stringValue = "A much longer paragraph changed through the inspector";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Canvas.ForceUpdateCanvases();

            Assert.That(rect.rect.width, Is.GreaterThan(shortWidth + 1f),
                "Inspector content changes must update the fitted rect");
            Assert.That(layout.preferredWidth, Is.GreaterThan(shortWidth + 1f),
                "Inspector content changes must invalidate the cached layout measurement");
            Assert.That(layout.preferredWidth, Is.EqualTo(rect.rect.width).Within(0.01f));
        }

        [Test]
        public void ExternalSizeSkipsContentSizedAncestorOnFittedAxis()
        {
            root = new GameObject("Stable root", typeof(RectTransform));
            var rootRect = (RectTransform)root.transform;
            rootRect.sizeDelta = new Vector2(400f, 200f);
            var contentSizedParent = new GameObject("Content-sized parent", typeof(RectTransform));
            contentSizedParent.transform.SetParent(root.transform, false);
            var parentRect = (RectTransform)contentSizedParent.transform;
            parentRect.sizeDelta = new Vector2(75f, 200f);
            contentSizedParent.AddComponent<ContentSizeFitter>().horizontalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            var child = new GameObject("Child", typeof(RectTransform));
            child.transform.SetParent(contentSizedParent.transform, false);

            var available = TextSizingBounds.ExternalAvailableSize((RectTransform)child.transform);

            Assert.That(available.x, Is.EqualTo(400f).Within(0.01f));
            Assert.That(available.y, Is.EqualTo(200f).Within(0.01f));
        }

        [Test]
        public void ExternalSizeSubtractsStableOuterLayoutGroupPaddingThroughContentSizedParent()
        {
            root = new GameObject("Padded stable root", typeof(RectTransform));
            var rootRect = (RectTransform)root.transform;
            rootRect.sizeDelta = new Vector2(400f, 200f);
            var group = root.AddComponent<HorizontalLayoutGroup>();
            group.padding = new RectOffset(11, 17, 5, 7);

            var contentSizedParent = new GameObject("Content-sized parent", typeof(RectTransform));
            contentSizedParent.transform.SetParent(root.transform, false);
            var parentRect = (RectTransform)contentSizedParent.transform;
            parentRect.sizeDelta = new Vector2(75f, 200f);
            contentSizedParent.AddComponent<ContentSizeFitter>().horizontalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            var child = new GameObject("Child", typeof(RectTransform));
            child.transform.SetParent(contentSizedParent.transform, false);

            var available = TextSizingBounds.ExternalAvailableSize((RectTransform)child.transform);

            Assert.That(available.x, Is.EqualTo(372f).Within(0.01f));
        }

        [Test]
        public void ExternalSizeSubtractsOwningLayoutGroupPadding()
        {
            root = new GameObject("Padded root", typeof(RectTransform));
            var rootRect = (RectTransform)root.transform;
            rootRect.sizeDelta = new Vector2(400f, 200f);
            var group = root.AddComponent<HorizontalLayoutGroup>();
            group.padding = new RectOffset(11, 17, 5, 7);
            var child = new GameObject("Child", typeof(RectTransform));
            child.transform.SetParent(root.transform, false);

            var available = TextSizingBounds.ExternalAvailableSize((RectTransform)child.transform);

            Assert.That(available.x, Is.EqualTo(372f).Within(0.01f));
            Assert.That(available.y, Is.EqualTo(188f).Within(0.01f));
        }

        [Test]
        public void FullyFixedAutoFontSizeAttachesResolvedSizingToRenderer()
        {
            var producer = CreateText(out var layout, out var fitter);
            fitter.enabled = false;
            layout.widthMode = TextWidthMode.Fixed;
            layout.heightMode = TextHeightMode.Fixed;
            producer.enableAutoFontSize = true;
            producer.fontSizeMin = 12f;
            producer.fontSizeMax = 36f;
            var rect = (RectTransform)producer.transform;
            rect.sizeDelta = new Vector2(80f, 40f);

            var sizing = producer.ResolveSizingAtLayoutWidth();
            var settings = producer.CurrentSettings();

            Assert.That(sizing.EffectiveFontSize, Is.LessThan(36f));
            Assert.That(settings.Sizing.HasValue, Is.True);
            Assert.That(settings.Sizing.Value.EffectiveFontSize,
                Is.EqualTo(sizing.EffectiveFontSize).Within(0.001f));
        }

        private TextBoxRenderTextureProducer CreateText(out TextBoxLayoutElement layout,
            out ContentSizeFitter fitter)
        {
            root = new GameObject("Content sizing parent", typeof(RectTransform));
            ((RectTransform)root.transform).sizeDelta = new Vector2(150f, 1000f);
            var child = new GameObject("Content sizing text", typeof(RectTransform));
            child.SetActive(false);
            child.transform.SetParent(root.transform, false);
            ((RectTransform)child.transform).sizeDelta = new Vector2(120f, 200f);
            var producer = child.AddComponent<TextBoxRenderTextureProducer>();
            producer.content = "互联网小章鱼1";
            producer.textStyle.size = 36f;
            child.AddComponent<TextBox>();
            layout = child.AddComponent<TextBoxLayoutElement>();
            fitter = child.AddComponent<ContentSizeFitter>();
            child.SetActive(true);
            return producer;
        }
    }
}
