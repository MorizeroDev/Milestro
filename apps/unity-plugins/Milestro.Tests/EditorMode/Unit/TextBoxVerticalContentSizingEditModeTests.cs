using System;
using System.Text.RegularExpressions;
using Milestro.Components;
using Milestro.Components.Internal;
using Milestro.Model;
using Milestro.Skia;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using Canvas = UnityEngine.Canvas;

namespace Milestro.Tests
{
    public sealed class TextBoxVerticalContentSizingEditModeTests
    {
        private const string LongMixedText =
            "天地玄黃宇宙洪荒日月盈昃辰宿列張寒來暑往秋收冬藏閏餘成歲律呂調陽";

        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SerializedWidthModeValuesRemainUnchanged()
        {
            Assert.That((int)TextWidthMode.Fixed, Is.EqualTo(0));
            Assert.That((int)TextWidthMode.MaxContent, Is.EqualTo(1));
            Assert.That((int)TextWidthMode.FitContent, Is.EqualTo(2));
            Assert.That(Enum.GetValues(typeof(TextWidthMode)).Length, Is.EqualTo(3));
        }

        [Test]
        public void FixedWidthModePublishesNoPreferenceAndKeepsAssignedWidth()
        {
            var producer = CreateVertical(out var layout, out var rect);
            layout.heightMode = TextHeightMode.Fixed;
            layout.widthMode = TextWidthMode.Fixed;

            var fixedSizing = producer.ResolveSizing();
            Assert.That(fixedSizing.MinWidth, Is.EqualTo(-1f));
            Assert.That(fixedSizing.PreferredWidth, Is.EqualTo(-1f));
            Assert.That(fixedSizing.PreferredHeight, Is.EqualTo(-1f));
            Assert.That(fixedSizing.UsedWidth, Is.EqualTo(rect.rect.width).Within(0.01f));
            Assert.That(fixedSizing.ContentWidth, Is.GreaterThan(0f));
            Assert.That(layout.minWidth, Is.EqualTo(-1f));
            Assert.That(layout.preferredWidth, Is.EqualTo(-1f));
            Assert.That(layout.minHeight, Is.EqualTo(-1f));
            Assert.That(layout.preferredHeight, Is.EqualTo(-1f));

            layout.widthMode = TextWidthMode.MaxContent;
            var maxContentSizing = producer.ResolveSizing();
            Assert.That(maxContentSizing.MinWidth, Is.EqualTo(maxContentSizing.PreferredWidth));
            Assert.That(maxContentSizing.PreferredWidth,
                Is.EqualTo(maxContentSizing.ContentWidth +
                           maxContentSizing.Margin.FixedHorizontalSize).Within(0.01f));
            Assert.That(layout.minWidth,
                Is.EqualTo(maxContentSizing.MinWidth).Within(0.01f));
            Assert.That(layout.preferredWidth,
                Is.EqualTo(maxContentSizing.PreferredWidth).Within(0.01f));
            Assert.That(layout.preferredHeight, Is.EqualTo(-1f));

            layout.widthMode = TextWidthMode.FitContent;
            var fitContentSizing = producer.ResolveSizing();
            Assert.That(fitContentSizing.MinWidth, Is.EqualTo(maxContentSizing.MinWidth));
            Assert.That(fitContentSizing.PreferredWidth,
                Is.EqualTo(maxContentSizing.PreferredWidth).Within(0.01f));
            Assert.That(fitContentSizing.PreferredHeight, Is.EqualTo(-1f));
        }

        [Test]
        public void MeasurementAndRenderShareHeightMarginAndColumnWidth()
        {
            var producer = CreateVertical(out var layout, out var rect);
            layout.widthMode = TextWidthMode.MaxContent;
            layout.heightMode = TextHeightMode.Fixed;
            producer.paragraphStyle.textAlign = TextAlign.Center;
            producer.margin.Top = Pixels(8f);
            producer.margin.Bottom = Pixels(12f);
            producer.margin.Left = Pixels(5f);
            producer.margin.Right = Pixels(7f);

            var sizing = producer.ResolveSizing();
            var settings = producer.CurrentSettings();
            using var target = new TextBoxRenderTarget();
            Assert.That(target.Rebuild(TextBoxRenderViewport.Invisible(new Vector2Int(300, 200),
                default), ColorSpace.Gamma, settings, true, null), Is.True);

            Assert.That(sizing.ContentHeight, Is.EqualTo(180f).Within(0.01f));
            Assert.That(sizing.ContentWidth, Is.GreaterThan(0f));
            Assert.That(sizing.PreferredWidth,
                Is.EqualTo(sizing.ContentWidth + 12f).Within(0.01f));
            Assert.That(sizing.PreferredHeight, Is.EqualTo(-1f));
            Assert.That(target.ContentSize.x, Is.EqualTo(sizing.ContentWidth).Within(0.01f));
            Assert.That(target.ContentSize.y, Is.EqualTo(sizing.ContentHeight).Within(0.01f));
            Assert.That(target.ViewportSize.y, Is.EqualTo(sizing.ContentHeight).Within(0.01f));

            var tallWidth = sizing.ContentWidth;
            rect.sizeDelta = new Vector2(300f, 120f);
            var shortSizing = producer.ResolveSizing();
            Assert.That(shortSizing.ContentHeight, Is.EqualTo(100f).Within(0.01f));
            Assert.That(shortSizing.ContentWidth, Is.GreaterThan(tallWidth),
                "A smaller fixed inline extent must add columns and widen the complete column box.");
        }

        [Test]
        public void FitContentAndMaxContentUseTheSameFullColumnContribution()
        {
            var producer = CreateVertical(out var layout, out _);
            layout.heightMode = TextHeightMode.Fixed;
            layout.widthMode = TextWidthMode.MaxContent;
            var maxContent = producer.ResolveSizing();
            layout.widthMode = TextWidthMode.FitContent;

            foreach (var parentWidth in new[] { 80f, 400f, 900f })
            {
                SetContainingBlockWidth(parentWidth);
                var fitContent = producer.ResolveSizing();
                Assert.That(fitContent.PreferredWidth,
                    Is.EqualTo(maxContent.PreferredWidth).Within(0.01f));
                Assert.That(fitContent.ContentWidth,
                    Is.EqualTo(maxContent.ContentWidth).Within(0.01f));
            }
        }

        [Test]
        public void BlockMarginViewportChangesInvalidateSameHeightIntrinsicMeasurement()
        {
            var producer = CreateVertical(out var layout, out var rect,
                "<a href=\"units\"><p style=\"margin-top: 10vw; margin-bottom: 2vh\">" + LongMixedText +
                "</p><p style=\"margin-top: 10%; margin-bottom: 1em\">天地</p></a>");
            layout.widthMode = TextWidthMode.MaxContent;
            var first = producer.ResolveSizing();
            var rootRect = (RectTransform)root.transform;
            rootRect.sizeDelta = new Vector2(1000f, 700f);
            var second = producer.ResolveSizing();
            Assert.That(second.ContentWidth, Is.GreaterThan(first.ContentWidth));
            using var target = new TextBoxRenderTarget();
            var viewport = TextBoxRenderViewport.Invisible(new Vector2Int(300, 200), default);
            Assert.That(target.Rebuild(viewport, ColorSpace.Gamma, producer.CurrentSettings(), true, null), Is.True);
            Assert.That(target.ContentSize.x, Is.EqualTo(second.ContentWidth));
            SetContainingBlockWidth(900f);
            var reparentedSize = producer.ResolveSizing();
            Assert.That(reparentedSize.ContentWidth, Is.EqualTo(second.ContentWidth));
            producer.textStyle.size = 28f;
            var resizedFont = producer.ResolveSizing();
            Assert.That(resizedFont.ContentWidth, Is.Not.EqualTo(second.ContentWidth));
            rect.sizeDelta = new Vector2(300f, 300f);
            var resizedHeight = producer.ResolveSizing();
            Assert.That(resizedHeight.ContentWidth, Is.LessThan(resizedFont.ContentWidth));
        }

        [Test]
        public void MarginUnitsUseContainingBlockAndRootCanvasViewport()
        {
            var producer = CreateVertical(out var layout, out _);
            layout.widthMode = TextWidthMode.MaxContent;
            layout.heightMode = TextHeightMode.Fixed;
            producer.margin.Left = new MarginValue
                { Value = 10f, Unit = MarginUnit.ContainerPercent };
            producer.margin.Top = new MarginValue { Value = 10f, Unit = MarginUnit.Vw };
            producer.margin.Right = new MarginValue { Value = 10f, Unit = MarginUnit.Vh };
            producer.margin.Bottom = new MarginValue { Value = 1f, Unit = MarginUnit.Em };

            var sizing = producer.ResolveSizing();

            Assert.That(sizing.Margin.Left, Is.EqualTo(40f).Within(0.01f));
            Assert.That(sizing.Margin.Top, Is.EqualTo(64f).Within(0.01f));
            Assert.That(sizing.Margin.Right, Is.EqualTo(48f).Within(0.01f));
            Assert.That(sizing.Margin.Bottom, Is.EqualTo(20f).Within(0.01f));
            Assert.That(sizing.ContentHeight, Is.EqualTo(116f).Within(0.01f));
        }

        [Test]
        public void AutoMarginsContributeZeroWithoutChangingStoredUnits()
        {
            var producer = CreateVertical(out var layout, out _);
            layout.widthMode = TextWidthMode.MaxContent;
            layout.heightMode = TextHeightMode.Fixed;
            producer.margin.Left = new MarginValue
                { Auto = true, Value = 9f, Unit = MarginUnit.ContainerPercent };
            producer.margin.Right = new MarginValue
                { Auto = true, Value = 2f, Unit = MarginUnit.Em };
            var automatic = producer.ResolveSizing();

            producer.margin.Left.Auto = false;
            producer.margin.Right.Auto = false;
            producer.margin.Left.Value = 0f;
            producer.margin.Right.Value = 0f;
            var fixedZero = producer.ResolveSizing();

            Assert.That(automatic.PreferredWidth,
                Is.EqualTo(fixedZero.PreferredWidth).Within(0.01f));
            Assert.That(automatic.Margin.FixedHorizontalSize, Is.EqualTo(0f).Within(0.01f));
            Assert.That(producer.margin.Left.Unit, Is.EqualTo(MarginUnit.ContainerPercent));
            Assert.That(producer.margin.Right.Unit, Is.EqualTo(MarginUnit.Em));
        }

        [Test]
        public void AutoHeightPublishesNoContentPreferencesAndRendersFixedRect()
        {
            var producer = CreateVertical(out var layout, out var rect);
            layout.widthMode = TextWidthMode.MaxContent;
            layout.heightMode = TextHeightMode.Auto;

            var sizing = producer.ResolveSizing();
            var settings = producer.CurrentSettings();
            var preparation = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(settings);
            using var target = new TextBoxRenderTarget();

            Assert.That(sizing.MinWidth, Is.EqualTo(-1f));
            Assert.That(sizing.PreferredWidth, Is.EqualTo(-1f));
            Assert.That(sizing.PreferredHeight, Is.EqualTo(-1f));
            Assert.That(layout.minWidth, Is.EqualTo(-1f));
            Assert.That(layout.preferredWidth, Is.EqualTo(-1f));
            Assert.That(layout.preferredHeight, Is.EqualTo(-1f));
            Assert.That(preparation.Diagnostic, Is.Not.Null);
            Assert.That(preparation.Diagnostic!.Message, Does.Contain("heightMode=Auto"));
            Assert.That(preparation.Settings.Sizing.HasValue, Is.False);
            Assert.That(producer.flowMode, Is.False);
            Assert.That(producer.CurrentViewport(new Vector2Int(300, 200)).SliceOutput, Is.False);
            LogAssert.Expect(LogType.Exception, new Regex("heightMode=Auto"));
            Assert.That(target.Rebuild(TextBoxRenderViewport.Invisible(new Vector2Int(300, 200),
                default), ColorSpace.Gamma, settings, true, null), Is.True);

            layout.heightMode = TextHeightMode.Fixed;
            var fixedSizing = producer.ResolveSizing();
            var fixedSettings = producer.CurrentSettings();
            var fixedPreparation = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(fixedSettings);
            Assert.That(fixedSizing.PreferredWidth, Is.GreaterThan(0f));
            Assert.That(fixedSizing.PreferredHeight, Is.EqualTo(-1f));
            Assert.That(fixedPreparation.Settings.Sizing.HasValue, Is.True);
            Assert.That(fixedPreparation.Diagnostic?.Message ?? "", Does.Not.Contain("heightMode=Auto"));
        }

        [Test]
        public void UnavailableContainingBlockAndViewportDefaultOnlyAffectedMargins()
        {
            var margin = new Margin
            {
                Left = new MarginValue { Value = 10f, Unit = MarginUnit.ContainerPercent },
                Top = new MarginValue { Value = 3f, Unit = MarginUnit.Px },
                Right = new MarginValue { Value = 2f, Unit = MarginUnit.Vw },
                Bottom = new MarginValue { Value = 1f, Unit = MarginUnit.Em },
            };
            var settings = new TextBoxRenderTargetSettings(LongMixedText,
                margin,
                VerticalStyle(),
                TextStyle(),
                TextBoxWrapMode.Wrap,
                TextOverflow.Clip,
                sizing: new TextSizingResult(
                    new TextSizingRequest(TextWidthMode.MaxContent, TextHeightMode.Fixed,
                        200f, 200f, 400f, true, 20f),
                    200f, 200f, 20f, new ResolvedMargin(0f, 3f, 0f, 20f),
                    default, 100f, 100f),
                verticalHeightMode: TextHeightMode.Fixed,
                verticalMarginContext: new MarginResolveContext(0f, 0f, 20f,
                    0f, 0f, hasContainerWidth: false, hasViewport: false),
                sizingUsesVerticalLayout: true);
            var preparation = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(settings);

            var resolved = margin.ResolveVertical(settings.VerticalMarginContext!.Value);
            Assert.That(resolved.Left, Is.EqualTo(0f));
            Assert.That(resolved.Top, Is.EqualTo(3f));
            Assert.That(resolved.Right, Is.EqualTo(0f));
            Assert.That(resolved.Bottom, Is.EqualTo(20f));
            Assert.That(preparation.Diagnostic, Is.Not.Null);
            Assert.That(preparation.Diagnostic!.Message, Does.Contain("margin.left"));
            Assert.That(preparation.Diagnostic.Message, Does.Contain("margin.right"));
            Assert.That(preparation.Diagnostic.Message, Does.Not.Contain("margin.top"));
            Assert.That(preparation.Diagnostic.Message, Does.Not.Contain("margin.bottom"));
        }

        [Test]
        public void ContainingBlockAndViewportRecoveryReevaluatesMargins()
        {
            root = new GameObject("Detached vertical", typeof(RectTransform), typeof(Canvas));
            var rootRect = (RectTransform)root.transform;
            rootRect.sizeDelta = new Vector2(640f, 480f);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var producer = root.AddComponent<TextBoxRenderTextureProducer>();
            root.AddComponent<TextBox>();
            var layout = root.AddComponent<TextBoxLayoutElement>();
            producer.content = LongMixedText;
            producer.paragraphStyle = VerticalStyle();
            producer.textStyle = TextStyle();
            producer.margin.Left = new MarginValue
                { Value = 10f, Unit = MarginUnit.ContainerPercent };
            producer.margin.Right = new MarginValue { Value = 10f, Unit = MarginUnit.Vw };
            layout.widthMode = TextWidthMode.MaxContent;
            layout.heightMode = TextHeightMode.Fixed;

            var unavailable = producer.CurrentSettings();
            var unavailablePreparation =
                TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(unavailable);
            Assert.That(unavailable.VerticalMarginContext!.Value.HasContainerWidth, Is.False);
            Assert.That(unavailablePreparation.Diagnostic!.Message, Does.Contain("margin.left"));

            var parent = new GameObject("Recovered parent", typeof(RectTransform));
            ((RectTransform)parent.transform).sizeDelta = new Vector2(320f, 300f);
            root.transform.SetParent(parent.transform, true);
            var recovered = producer.CurrentSettings();
            var recoveredPreparation =
                TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(recovered);
            Assert.That(recovered.VerticalMarginContext!.Value.HasContainerWidth, Is.True);
            Assert.That(recovered.VerticalMarginContext.Value.HasViewport, Is.True);
            Assert.That(recoveredPreparation.Diagnostic?.Message ?? "",
                Does.Not.Contain("margin.left"));
            Assert.That(recoveredPreparation.Diagnostic?.Message ?? "",
                Does.Not.Contain("margin.right"));
        }

        [Test]
        public void VerticalLayoutElementKeepsFixedViewportAndClearsFlowAcrossModeChanges()
        {
            var producer = CreateVertical(out var layout, out _);
            layout.widthMode = TextWidthMode.MaxContent;
            layout.heightMode = TextHeightMode.Fixed;
            Assert.That(producer.flowMode, Is.False);
            var verticalViewport = producer.CurrentViewport(new Vector2Int(300, 200));
            Assert.That(verticalViewport.SliceOutput, Is.False);
            Assert.That(verticalViewport.OutputSizePixels, Is.EqualTo(verticalViewport.LayoutSizePixels));

            producer.paragraphStyle.writingMode = WritingMode.HorizontalTb;
            Assert.That(producer.paragraphStyle.writingMode,
                Is.EqualTo(WritingMode.HorizontalTb));
            Assert.That(producer.flowMode, Is.True);
            producer.paragraphStyle.writingMode = WritingMode.VerticalRl;
            Assert.That(producer.flowMode, Is.False);

            layout.enabled = false;
            Assert.That(producer.flowMode, Is.False);
            layout.enabled = true;
            Assert.That(producer.flowMode, Is.False);
        }

        [Test]
        public void StructuralFallbackUsesHorizontalSizingWithoutRequestedFlow()
        {
            LogAssert.Expect(LogType.Exception, new Regex("strong RTL content"));
            LogAssert.Expect(LogType.Exception, new Regex("strong RTL content"));
            var producer = CreateVertical(out var layout, out var rect,
                "<p>first مرحبا</p><p><ruby>漢<rt>かん</rt></ruby> second paragraph</p>");
            layout.widthMode = TextWidthMode.MaxContent;
            layout.heightMode = TextHeightMode.Fixed;
            rect.sizeDelta = new Vector2(200f, 100f);

            var sizing = producer.ResolveSizing();
            var settings = producer.CurrentSettings();
            var preparation = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(settings);
            using var target = new TextBoxRenderTarget();
            target.Rebuild(TextBoxRenderViewport.Invisible(new Vector2Int(200, 100), default),
                ColorSpace.Gamma, settings, true, null);

            Assert.That(preparation.UsesHorizontalFallback, Is.True);
            Assert.That(sizing.PreferredWidth, Is.GreaterThan(0f));
            Assert.That(target.IsVertical, Is.False);
            Assert.That(producer.flowMode, Is.False);
            Assert.That(producer.CurrentViewport(new Vector2Int(200, 100)).SliceOutput, Is.False);
        }

        [TestCase(TextWidthMode.MaxContent)]
        [TestCase(TextWidthMode.FitContent)]
        public void MultiBlockPreferredWidthUpdatesSpacingAndAllBlocksAtSameHeight(TextWidthMode mode)
        {
            var producer = CreateVertical(out var layout, out var rect, "<p>天地玄黃</p><p>宇宙洪荒</p>");
            layout.heightMode = TextHeightMode.Fixed;
            layout.widthMode = mode;
            producer.paragraphStyle.paragraphSpacing = 19f;
            var first = producer.ResolveSizing();
            using var target = new TextBoxRenderTarget();
            Assert.That(target.Rebuild(TextBoxRenderViewport.Invisible(new Vector2Int(300, 200), default),
                ColorSpace.Gamma, producer.CurrentSettings(), true, null), Is.True);
            Assert.That(first.ContentWidth, Is.EqualTo(target.ContentSize.x).Within(0.01f));
            producer.paragraphStyle.paragraphSpacing = 37f;
            var spaced = producer.ResolveSizing();
            Assert.That(spaced.PreferredWidth - first.PreferredWidth, Is.EqualTo(18f).Within(0.01f));
            producer.content = "<p>天地玄黃</p><p>宇宙洪荒日月盈昃辰宿列張寒來暑往秋收冬藏</p>";
            var longer = producer.ResolveSizing();
            Assert.That(longer.PreferredWidth, Is.GreaterThan(spaced.PreferredWidth));
            rect.sizeDelta = new Vector2(300f, 100f);
            Assert.That(producer.ResolveSizing().PreferredWidth, Is.GreaterThan(longer.PreferredWidth));
            Assert.That(producer.flowMode, Is.False);
            Assert.That(producer.CurrentViewport(new Vector2Int(300, 100)).SliceOutput, Is.False);
        }

        [TestCase(TextWidthMode.MaxContent)]
        [TestCase(TextWidthMode.FitContent)]
        public void AF07_AF08_ExplicitSizingExcludesContentWidthFromAutoFontSize(TextWidthMode widthMode)
        {
            var producer = CreateVertical(out var layout, out _);
            layout.widthMode = widthMode;
            layout.heightMode = TextHeightMode.Fixed;
            producer.enableAutoFontSize = true;
            producer.fontSizeMin = 10f;
            producer.fontSizeMax = 30f;

            var settings = producer.CurrentSettings();
            var preparation = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(settings);

            Assert.That(preparation.Diagnostic, Is.Null);
            Assert.That(preparation.Settings.AutoFontSize, Is.True);
            Assert.That(preparation.Settings.Sizing.HasValue, Is.True);
            Assert.That(preparation.Settings.Sizing.Value.PreferredWidth,
                Is.GreaterThan(0f));
            using var target = new TextBoxRenderTarget();
            target.Rebuild(TextBoxRenderViewport.Invisible(new Vector2Int(10, 200), default),
                ColorSpace.Gamma, settings, true, null);
            Assert.That(target.ContentSize.x, Is.EqualTo(settings.Sizing.Value.ContentWidth).Within(0.01f));
        }

        [Test]
        public void AssignedWidthChangesDoNotChangePreferredOrColumnBox()
        {
            var producer = CreateVertical(out var layout, out var rect);
            layout.widthMode = TextWidthMode.MaxContent;
            layout.heightMode = TextHeightMode.Fixed;
            rect.sizeDelta = new Vector2(500f, 140f);
            var wide = producer.ResolveSizing();
            rect.sizeDelta = new Vector2(70f, 140f);
            var narrow = producer.ResolveSizing();

            Assert.That(narrow.PreferredWidth, Is.EqualTo(wide.PreferredWidth).Within(0.01f));
            Assert.That(narrow.ContentWidth, Is.EqualTo(wide.ContentWidth).Within(0.01f));
            Assert.That(narrow.ContentHeight, Is.EqualTo(wide.ContentHeight).Within(0.01f));
            Assert.That(narrow.EffectiveFontSize, Is.EqualTo(wide.EffectiveFontSize));
        }

        [Test]
        public void EmptyAndHardBreakContentKeepFiniteBoundedMetrics()
        {
            var producer = CreateVertical(out var layout, out _);
            layout.widthMode = TextWidthMode.MaxContent;
            layout.heightMode = TextHeightMode.Fixed;
            foreach (var content in new[] { "", "   ", "A\n", "\n\n", "A\n\nB" })
            {
                producer.content = content;
                var sizing = producer.ResolveSizing();
                Assert.That(float.IsFinite(sizing.PreferredWidth), Is.True, content);
                Assert.That(float.IsFinite(sizing.ContentWidth), Is.True, content);
                Assert.That(float.IsFinite(sizing.ContentHeight), Is.True, content);
                Assert.That(sizing.PreferredWidth, Is.GreaterThanOrEqualTo(0f), content);
                Assert.That(sizing.ContentHeight, Is.EqualTo(200f).Within(0.01f), content);
            }
        }

        [Test]
        public void MarginGreaterThanHeightClampsContentHeightToZero()
        {
            var producer = CreateVertical(out var layout, out _);
            layout.widthMode = TextWidthMode.MaxContent;
            layout.heightMode = TextHeightMode.Fixed;
            producer.margin.Top = Pixels(120f);
            producer.margin.Bottom = Pixels(120f);
            var sizing = producer.ResolveSizing();

            Assert.That(sizing.ContentHeight, Is.EqualTo(0f));
            Assert.That(sizing.PreferredWidth, Is.GreaterThanOrEqualTo(0f));
            Assert.That(float.IsFinite(sizing.PreferredWidth), Is.True);
            Assert.That(float.IsFinite(sizing.ContentWidth), Is.True);
        }

        [Test]
        public void ContentSizeFitterConvergesWithFixedHeight()
        {
            var producer = CreateVertical(out var layout, out var rect);
            layout.widthMode = TextWidthMode.FitContent;
            layout.heightMode = TextHeightMode.Fixed;
            var fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            float previousWidth = -1f;
            for (var pass = 0; pass < 8; ++pass)
            {
                LayoutRebuilder.MarkLayoutForRebuild(rect);
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)root.transform);
                Canvas.ForceUpdateCanvases();
                Assert.That(rect.rect.height, Is.EqualTo(200f).Within(0.01f));
                Assert.That(rect.rect.width,
                    Is.EqualTo(producer.ResolveSizing().PreferredWidth).Within(0.01f));
                if (pass > 1)
                {
                    Assert.That(rect.rect.width, Is.EqualTo(previousWidth).Within(0.01f));
                }
                previousWidth = rect.rect.width;
            }
        }

        [Test]
        public void LateAssignedHeightRetestsAndConvergesWithoutOscillation()
        {
            var producer = CreateVertical(out var layout, out var rect);
            layout.widthMode = TextWidthMode.FitContent;
            layout.heightMode = TextHeightMode.Fixed;
            var fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            rect.sizeDelta = new Vector2(300f, 0f);
            LayoutRebuilder.MarkLayoutForRebuild(rect);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)root.transform);
            Canvas.ForceUpdateCanvases();
            var zeroHeightWidth = producer.ResolveSizing().PreferredWidth;
            Assert.That(producer.ResolveSizing().ContentHeight, Is.EqualTo(0f));

            rect.sizeDelta = new Vector2(300f, 140f);
            LayoutRebuilder.MarkLayoutForRebuild(rect);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)root.transform);
            Canvas.ForceUpdateCanvases();
            var lateHeightSizing = producer.ResolveSizing();
            Assert.That(lateHeightSizing.ContentHeight, Is.EqualTo(140f).Within(0.01f));
            Assert.That(lateHeightSizing.PreferredWidth,
                Is.LessThan(zeroHeightWidth),
                "A late non-zero height must retest the finite inline extent.");

            var convergedWidth = rect.rect.width;
            for (var pass = 0; pass < 8; ++pass)
            {
                LayoutRebuilder.MarkLayoutForRebuild(rect);
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)root.transform);
                Canvas.ForceUpdateCanvases();
                Assert.That(rect.rect.height, Is.EqualTo(140f).Within(0.01f));
                Assert.That(rect.rect.width, Is.EqualTo(convergedWidth).Within(0.01f));
                Assert.That(producer.ResolveSizing().PreferredWidth,
                    Is.EqualTo(lateHeightSizing.PreferredWidth).Within(0.01f));
                Assert.That(producer.ResolveSizing().ContentHeight,
                    Is.EqualTo(140f).Within(0.01f));
            }
        }

        [Test]
        public void NoWrapSingleLineAndHardBreaksKeepTheFixedAllocatedHeight()
        {
            var producer = CreateVertical(out var layout, out _,
                "A\nBCDEFGHIJKLMNOPQRSTUVWXYZ0123456789");
            layout.widthMode = TextWidthMode.MaxContent;
            layout.heightMode = TextHeightMode.Fixed;
            producer.wrapMode = TextBoxWrapMode.NoWrap;
            using var target = new TextBoxRenderTarget();

            var noWrap = producer.ResolveSizing();
            var noWrapSettings = producer.CurrentSettings();
            Assert.That(target.Rebuild(TextBoxRenderViewport.Invisible(
                new Vector2Int(300, 200), default), ColorSpace.Gamma,
                noWrapSettings, true, null), Is.True);
            Assert.That(noWrap.ContentHeight, Is.EqualTo(200f).Within(0.01f));
            Assert.That(noWrap.PreferredHeight, Is.EqualTo(-1f));
            Assert.That(target.ContentSize.x, Is.EqualTo(noWrap.ContentWidth).Within(0.01f));
            Assert.That(target.ViewportSize.y, Is.EqualTo(200f).Within(0.01f));
            Assert.That(target.ContentSize.y, Is.GreaterThan(200f),
                "NoWrap overflow stays in the physical inline axis.");

            producer.content = LongMixedText;
            producer.paragraphStyle.singleLine = true;
            producer.wrapMode = TextBoxWrapMode.Wrap;
            var singleLine = producer.ResolveSizing();
            var singleLineSettings = producer.CurrentSettings();
            var singleLineTarget = new TextBoxRenderTarget();
            Assert.That(singleLineTarget.Rebuild(TextBoxRenderViewport.Invisible(
                new Vector2Int(300, 200), default), ColorSpace.Gamma,
                singleLineSettings, true, null), Is.True);
            Assert.That(singleLine.ContentHeight, Is.EqualTo(200f).Within(0.01f));
            Assert.That(singleLine.PreferredHeight, Is.EqualTo(-1f));
            Assert.That(singleLineTarget.ContentSize.x,
                Is.EqualTo(singleLine.ContentWidth).Within(0.01f));
            Assert.That(singleLineTarget.ViewportSize.y, Is.EqualTo(200f).Within(0.01f));
            Assert.That(singleLineTarget.ContentSize.y, Is.GreaterThan(200f),
                "singleLine uses an infinite inline extent without replacing the fixed height.");
            singleLineTarget.Dispose();
        }

        [Test]
        public void UnbreakableLatinWordOverflowsInlineWithoutEmergencyColumns()
        {
            var producer = CreateVertical(out var layout, out var rect,
                "abcdefghijklmnopqrstuvwxyz");
            layout.widthMode = TextWidthMode.MaxContent;
            layout.heightMode = TextHeightMode.Fixed;
            rect.sizeDelta = new Vector2(300f, 60f);
            producer.textStyle = new TextStyleSettings
            {
                size = 20f,
                lineHeight = 2f,
                locale = "en",
            };
            Canvas.ForceUpdateCanvases();

            var sizing = producer.ResolveSizing();
            Assert.That(sizing.ContentHeight, Is.EqualTo(60f).Within(0.01f));
            Assert.That(sizing.ContentWidth, Is.EqualTo(40f).Within(0.01f));
            Assert.That(sizing.PreferredWidth, Is.EqualTo(40f).Within(0.01f));

            using var target = new TextBoxRenderTarget();
            Assert.That(target.Rebuild(TextBoxRenderViewport.Invisible(
                new Vector2Int(300, 60), default), ColorSpace.Gamma,
                producer.CurrentSettings(), true, null), Is.True);
            Assert.That(target.IsVertical, Is.True);
            Assert.That(target.ContentSize.x, Is.EqualTo(40f).Within(0.01f));
            Assert.That(target.ContentSize.y, Is.EqualTo(60f).Within(0.01f));
        }

        [Test]
        public void TrailingHardBreakKeepsEmptyColumnObservableAcrossNativeAndUnity()
        {
            var producer = CreateVertical(out var layout, out var rect,
                "A" + (char)10);
            layout.widthMode = TextWidthMode.MaxContent;
            layout.heightMode = TextHeightMode.Fixed;
            rect.sizeDelta = new Vector2(300f, 60f);
            producer.textStyle = new TextStyleSettings
            {
                size = 20f,
                lineHeight = 2f,
                locale = "en",
            };
            Canvas.ForceUpdateCanvases();

            var sizing = producer.ResolveSizing();
            Assert.That(sizing.ContentHeight, Is.EqualTo(60f).Within(0.01f));
            Assert.That(sizing.ContentWidth, Is.EqualTo(80f).Within(0.01f));
            Assert.That(sizing.PreferredWidth, Is.EqualTo(80f).Within(0.01f));

            using var target = new TextBoxRenderTarget();
            Assert.That(target.Rebuild(TextBoxRenderViewport.Invisible(
                new Vector2Int(300, 60), default), ColorSpace.Gamma,
                producer.CurrentSettings(), true, null), Is.True);
            Assert.That(target.IsVertical, Is.True);
            Assert.That(target.ContentSize.x, Is.EqualTo(80f).Within(0.01f));
            Assert.That(target.ContentSize.y, Is.EqualTo(60f).Within(0.01f));
        }

        [Test]
        public void PercentageIndentMeasurementIsZeroWhileFinalLayoutUsesContentHeight()
        {
            var plainSettings = VerticalSizingSettings(
                LongMixedText, VerticalStyle(), TextStyle());
            var percentSettings = VerticalSizingSettings(
                "<p style=\"text-indent: 100%\">" + LongMixedText + "</p>",
                VerticalStyle(), TextStyle());
            var pixelSettings = VerticalSizingSettings(
                "<p style=\"text-indent: 200px\">" + LongMixedText + "</p>",
                VerticalStyle(), TextStyle());

            using var plainMeasurement = new TextBoxVerticalIntrinsicMeasurement(plainSettings);
            using var percentMeasurement = new TextBoxVerticalIntrinsicMeasurement(percentSettings);
            using var pixelMeasurement = new TextBoxVerticalIntrinsicMeasurement(pixelSettings);
            var plain = plainMeasurement.Measure(200f);
            var percent = percentMeasurement.Measure(200f);
            var pixels = pixelMeasurement.Measure(200f);

            Assert.That(percent.PhysicalWidth, Is.EqualTo(plain.PhysicalWidth).Within(0.01f));
            Assert.That(pixels.PhysicalWidth, Is.GreaterThan(percent.PhysicalWidth));

            var request = new TextSizingRequest(TextWidthMode.MaxContent, TextHeightMode.Fixed,
                0f, 200f, 300f, true, 20f);
            var margin = new ResolvedMargin(0f, 0f, 0f, 0f);
            var maxContent = TextBoxVerticalSizingResolver.Resolve(percentMeasurement,
                request, margin, 200f, null);
            var fitRequest = new TextSizingRequest(TextWidthMode.FitContent, TextHeightMode.Fixed,
                0f, 200f, 300f, true, 20f);
            var fitContent = TextBoxVerticalSizingResolver.Resolve(percentMeasurement,
                fitRequest, margin, 200f, null);
            Assert.That(maxContent.ContentWidth, Is.EqualTo(percent.PhysicalWidth).Within(0.01f));
            Assert.That(maxContent.PreferredWidth, Is.EqualTo(percent.PhysicalWidth).Within(0.01f));
            Assert.That(fitContent.PreferredWidth, Is.EqualTo(maxContent.PreferredWidth).Within(0.01f));

            var viewport = TextBoxRenderViewport.Invisible(new Vector2Int(300, 200), default);
            using var target = new TextBoxRenderTarget();
            Assert.That(target.Rebuild(viewport, ColorSpace.Gamma, percentSettings, true, null), Is.True);
            Assert.That(target.ContentSize.y, Is.EqualTo(200f).Within(0.01f));
            Assert.That(target.ContentSize.x, Is.EqualTo(pixels.PhysicalWidth).Within(0.01f));
            Assert.That(target.ContentSize.x, Is.GreaterThan(percent.PhysicalWidth),
                "Final percentage layout must not feed its wider result back into preferredWidth.");
        }

        [Test]
        public void PercentageIndentOverflowDefaultsToZeroAndRecoversWithNormalHeight()
        {
            var percent = "<p style=\"text-indent: 200%\">" + LongMixedText + "</p>";
            var zero = "<p style=\"text-indent: 0\">" + LongMixedText + "</p>";
            var pixels = "<p style=\"text-indent: 600px\">" + LongMixedText + "</p>";
            var hugePercent = WithVerticalSizingHeight(
                VerticalSizingSettings(percent, VerticalStyle(), TextStyle()),
                float.MaxValue);
            var hugeZero = WithVerticalSizingHeight(
                VerticalSizingSettings(zero, VerticalStyle(), TextStyle()),
                float.MaxValue);
            var viewport = TextBoxRenderViewport.Invisible(new Vector2Int(300, 300), default);
            using var target = new TextBoxRenderTarget();

            LogAssert.Expect(LogType.Exception,
                new Regex("text indent is not representable"));
            Assert.That(target.Rebuild(viewport, ColorSpace.Gamma, hugePercent, true, null),
                Is.True);
            Assert.That(target.LastLayoutError, Is.Null);
            var hugePercentSize = target.ContentSize;

            Assert.That(target.Rebuild(viewport, ColorSpace.Gamma, hugeZero, true, null), Is.True);
            Assert.That(target.ContentSize.x, Is.EqualTo(hugePercentSize.x).Within(0.01f));
            Assert.That(target.ContentSize.y, Is.EqualTo(hugePercentSize.y).Within(0.01f));

            var normalPercent = WithVerticalSizingHeight(
                VerticalSizingSettings(percent, VerticalStyle(), TextStyle()),
                300f);
            var normalPixels = WithVerticalSizingHeight(
                VerticalSizingSettings(pixels, VerticalStyle(), TextStyle()),
                300f);
            Assert.That(target.Rebuild(viewport, ColorSpace.Gamma, normalPercent, true, null),
                Is.True);
            var normalPercentSize = target.ContentSize;
            Assert.That(normalPercentSize.y, Is.EqualTo(300f).Within(0.01f));

            Assert.That(target.Rebuild(viewport, ColorSpace.Gamma, normalPixels, true, null),
                Is.True);
            Assert.That(target.ContentSize.x, Is.EqualTo(normalPercentSize.x).Within(0.01f));
            Assert.That(target.ContentSize.y, Is.EqualTo(300f).Within(0.01f));
        }

        [Test]
        public void PixelAndEmIndentRemainInTheIntrinsicMeasurementSnapshot()
        {
            var pixelSettings = VerticalSizingSettings(
                "<p style=\"text-indent: 40px\">" + LongMixedText + "</p>",
                VerticalStyle(), TextStyle());
            var emSettings = VerticalSizingSettings(
                "<p style=\"text-indent: 2em\">" + LongMixedText + "</p>",
                VerticalStyle(), TextStyle());
            var plainSettings = VerticalSizingSettings(LongMixedText, VerticalStyle(), TextStyle());

            using var pixelMeasurement = new TextBoxVerticalIntrinsicMeasurement(pixelSettings);
            using var emMeasurement = new TextBoxVerticalIntrinsicMeasurement(emSettings);
            using var plainMeasurement = new TextBoxVerticalIntrinsicMeasurement(plainSettings);
            var pixels = pixelMeasurement.Measure(180f);
            var em = emMeasurement.Measure(180f);
            var plain = plainMeasurement.Measure(180f);

            Assert.That(em.PhysicalWidth, Is.EqualTo(pixels.PhysicalWidth).Within(0.01f));
            Assert.That(pixels.PhysicalWidth, Is.GreaterThanOrEqualTo(plain.PhysicalWidth));
        }

        private TextBoxRenderTextureProducer CreateVertical(out TextBoxLayoutElement layout,
            out RectTransform rect,
            string content = LongMixedText)
        {
            root = new GameObject("Vertical sizing root", typeof(RectTransform), typeof(Canvas));
            var rootRect = (RectTransform)root.transform;
            rootRect.sizeDelta = new Vector2(640f, 480f);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var containingBlock = new GameObject("Containing block", typeof(RectTransform));
            containingBlock.transform.SetParent(root.transform, false);
            ((RectTransform)containingBlock.transform).sizeDelta = new Vector2(400f, 300f);
            var child = new GameObject("Vertical text", typeof(RectTransform));
            child.SetActive(false);
            child.transform.SetParent(containingBlock.transform, false);
            rect = (RectTransform)child.transform;
            rect.sizeDelta = new Vector2(300f, 200f);
            var producer = child.AddComponent<TextBoxRenderTextureProducer>();
            producer.content = content;
            producer.paragraphStyle = VerticalStyle();
            producer.textStyle = TextStyle();
            child.AddComponent<TextBox>();
            layout = child.AddComponent<TextBoxLayoutElement>();
            layout.widthMode = TextWidthMode.MaxContent;
            layout.heightMode = TextHeightMode.Fixed;
            child.SetActive(true);
            Canvas.ForceUpdateCanvases();
            return producer;
        }

        private void SetContainingBlockWidth(float width)
        {
            var containingBlock = (RectTransform)root.transform.GetChild(0);
            containingBlock.sizeDelta = new Vector2(width, 300f);
            Canvas.ForceUpdateCanvases();
        }

        private static ParagraphStyleSettings VerticalStyle()
        {
            return new ParagraphStyleSettings
            {
                writingMode = WritingMode.VerticalRl,
                textDirection = TextDirection.Ltr,
                textAlign = TextAlign.Start,
                singleLine = false,
            };
        }

        private static TextStyleSettings TextStyle()
        {
            return new TextStyleSettings
            {
                size = 20f,
                lineHeight = 0f,
                locale = "zh-Hans",
            };
        }

        private static TextBoxRenderTargetSettings VerticalSizingSettings(string content,
            ParagraphStyleSettings paragraphStyle,
            TextStyleSettings textStyle)
        {
            return new TextBoxRenderTargetSettings(content,
                new Margin(),
                paragraphStyle,
                textStyle,
                TextBoxWrapMode.Wrap,
                TextOverflow.Clip);
        }

        private static TextBoxRenderTargetSettings WithVerticalSizingHeight(
            TextBoxRenderTargetSettings settings,
            float height)
        {
            var request = new TextSizingRequest(TextWidthMode.Fixed, TextHeightMode.Fixed,
                300f, height, 300f, true, settings.TextStyle.size);
            var sizing = new TextSizingResult(request, 300f, height, settings.TextStyle.size,
                new ResolvedMargin(0f, 0f, 0f, 0f), default, 0f, 0f);
            return settings.WithSizing(sizing, new Vector2(300f, height), TextHeightMode.Fixed,
                null, true);
        }

        private static MarginValue Pixels(float value)
        {
            return new MarginValue { Value = value, Unit = MarginUnit.Px };
        }
    }
}
