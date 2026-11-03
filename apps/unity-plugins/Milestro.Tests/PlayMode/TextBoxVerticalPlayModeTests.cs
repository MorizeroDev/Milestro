using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using IOPath = System.IO.Path;
using System.Reflection;
using System.Text.RegularExpressions;
using Milestro.Components;
using Milestro.Components.Internal;
using Milestro.Configuration;
using Milestro.Extensions;
using Milestro.Model;
using Milestro.Skia;
using Milestro.Skia.TextLayout;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Canvas = UnityEngine.Canvas;
using Object = UnityEngine.Object;

namespace Milestro.Tests
{
    public sealed class TextBoxVerticalPlayModeTests
    {
        private const string MixedText = "「漢字ABC2026かな」、。ー";
        private const string LongMixedText = MixedText + MixedText + MixedText;
        private const string CjkWrapText =
            "天地玄黃宇宙洪荒日月盈昃辰宿列張寒來暑往秋收冬藏閏餘成歲律呂調陽";
        private const string LinkedMixedText = "<a href=\"vertical-link\">" + LongMixedText + "</a>";

        private GameObject root;
        private RenderSurfaceConfiguration previousConfiguration;

        [UnityTest]
        public IEnumerator RB01_RB02_RB09_RubyPixelsSourceHitAndHover()
        {
            var textBox = CreateTextBox(TextBoxVertical.TextBoxVerticalDegradePlayerProbe.RubyContent,
                out var producer, MarginAll(0));
            textBox.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 160);
            producer.textStyle.size = 30;
            yield return WaitForProducer(producer, true);
            Assert.That(TextBoxVertical.TextBoxVerticalDegradePlayerProbe.EvaluateRubyOutput(textBox,
                producer, out var annotationPixels, out var hit), Is.True);
            Assert.That(annotationPixels, Is.GreaterThan(10));
            Assert.That(hit, Is.True);
            Assert.That(producer.TryGetContentSnapshot(out var content), Is.True);
            Assert.That(content.PlainText, Is.EqualTo("漢字"));
            var indicator = HoverFirstLink(textBox);
            Assert.That(indicator.enabled, Is.True);
            Assert.That(LinkHoverIndicatorVertical(indicator), Is.True);
        }

        [UnityTest]
        public IEnumerator RB08_LineHeightKeepsRubyPixelsAndAnnotationLinkHit()
        {
            var textBox = CreateTextBox(TextBoxVertical.TextBoxVerticalDegradePlayerProbe.RubyContent,
                out var producer, MarginAll(0));
            textBox.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 160);
            producer.textStyle.size = 30;
            foreach (var height in new[] { 0f, 1f, 1.5f, 2f, 3f })
            {
                var previousVersion = producer.OutputVersion;
                producer.textStyle.lineHeight = height;
                if (height != 0f) yield return WaitForProducerAfter(producer, previousVersion, true);
                else yield return WaitForProducer(producer, true);
                Assert.That(TextBoxVertical.TextBoxVerticalDegradePlayerProbe.EvaluateRubyOutput(textBox,
                    producer, out var annotationPixels, out var hit), Is.True, $"line-height: {height}");
                Assert.That(annotationPixels, Is.GreaterThan(10));
                Assert.That(hit, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator RB14_UnderDefaultsOnceAndRendersNewRuby()
        {
            var textBox = CreateTextBox("<ruby style=\"ruby-position: under\">漢字<rt>かな</rt></ruby>",
                out var producer, MarginAll(0));
            LogAssert.Expect(LogType.Exception, new Regex("ruby-position under defaulted to Above"));
            yield return WaitForProducer(producer, false);
            Assert.That(producer.isVerticalLayout, Is.True);
            Assert.That(producer.TryGetContentSnapshot(out var content), Is.True);
            Assert.That(content.PlainText, Is.EqualTo("漢字"));
            for (var frame = 0; frame < 5; ++frame) { producer.RebuildOutput(false); yield return null; }
            Assert.That(producer.lastLayoutError, Is.Null);
            Assert.That(textBox.Texture, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator RB07_EllipsisHidesRubyLinkAndItsCompleteBaseRange()
        {
            var textBox = CreateTextBox("天<a href=\"hidden-ruby\"><ruby style=\"ruby-overhang: none\">" +
                "漢字<rt>かな</rt></ruby></a>地", out var producer, MarginAll(0));
            producer.textStyle.size = 30;
            producer.paragraphStyle.singleLine = true;
            producer.textOverflow = TextOverflow.Ellipsis;
            textBox.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 70);
            yield return WaitForProducer(producer, true);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var geometry, out _), Is.True);
            Assert.That(geometry.Paragraphs[0].GetRectsForRange(1, 3), Is.Empty);
            Assert.That(TryGetVisibleLinkPoint(geometry, out _), Is.False);
            Assert.That(producer.TryHitLink(new Vector2(0.9f, 0.7f), out _), Is.False);
            var indicator = textBox.GetComponentInChildren<TextBoxLinkHoverIndicator>(true);
            Assert.That(indicator == null || !indicator.enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator RB11_RB12_RB15_RubyMultiBlockAutoSizeAndRuntimeInvalidation()
        {
            const string ruby = "<ruby style=\"ruby-overhang: none\">漢字<rt>かんじABC</rt></ruby>";
            var textBox = CreateTextBox("<a href=\"ruby-runtime\"><p>" + ruby + "</p><p>" + ruby + "</p></a>",
                out var producer, MarginAll(0));
            producer.paragraphStyle.paragraphSpacing = 17;
            producer.enableAutoFontSize = true;
            producer.fontSizeMin = 8;
            producer.fontSizeMax = 60;
            yield return WaitForProducer(producer, true);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var initial, out _), Is.True);
            Assert.That(initial.Paragraphs.Count, Is.EqualTo(2));
            Assert.That(initial.FitScale, Is.GreaterThan(0));
            Assert.That(initial.Paragraphs[0].LayoutMetrics.Columns[0].BlockThickness,
                Is.GreaterThan(producer.textStyle.size));
            Assert.That(TryGetVisibleLinkPoint(initial, out var point), Is.True);
            Assert.That(producer.TryHitLink(point, out var hit), Is.True);
            Assert.That(hit.Href, Is.EqualTo("ruby-runtime"));
            var version = producer.OutputVersion;
            textBox.GetComponent<RectTransform>().sizeDelta = new Vector2(260, 180);
            producer.textStyle.size = 35;
            producer.content = "<a href=\"ruby-link\">" + ruby + "</a>";
            yield return WaitForProducerAfter(producer, version, true);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var resized, out _), Is.True);
            Assert.That(resized.Paragraphs.Count, Is.EqualTo(1));
            foreach (var mode in new[] { WritingMode.HorizontalTb, WritingMode.VerticalRl })
            {
                version = producer.OutputVersion;
                producer.paragraphStyle.writingMode = mode;
                yield return WaitForProducerAfter(producer, version, true);
                Assert.That(producer.isVerticalLayout, Is.EqualTo(mode == WritingMode.VerticalRl));
                Assert.That(producer.lastLayoutError, Is.Null);
            }
        }

        [SetUp]
        public void SetUp()
        {
            previousConfiguration = MilestroConfiguration.Configuration.RenderSurface;
            MilestroConfiguration.Configuration.RenderSurface = new RenderSurfaceConfiguration();
            root = new GameObject("Vertical TextBox test", typeof(RectTransform), typeof(Canvas));
            root.SetActive(false);
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            MilestroConfiguration.Configuration.RenderSurface = previousConfiguration;
        }

        [UnityTest]
        public IEnumerator CanvasTextBoxPaintsScrollsAndHitsUtf16LinkRanges()
        {
            var textBox = CreateTextBox(LinkedMixedText, out var producer, MarginAll(10f));
            yield return WaitForProducer(producer, true);

            Assert.That(producer.HasOutput, Is.True);
            Assert.That(producer.isVerticalLayout, Is.True);
            Assert.That(producer.maxScrollX, Is.GreaterThan(0f));
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var initial, out _), Is.True);
            Assert.That(producer.TryGetContentSnapshot(out var content), Is.True);
            Assert.That(content.PlainText, Is.EqualTo(LongMixedText));
            Assert.That(content.TryGetLink(initial.Links[0].Occurrence, out var link), Is.True);
            Assert.That(content.GetLinkLabel(link), Is.EqualTo(LongMixedText));
            Assert.That(link.StartUtf16, Is.EqualTo(0));
            Assert.That(link.EndUtf16, Is.EqualTo(LongMixedText.Length));
            Assert.That(TryGetVisibleLinkPoint(initial, out var initialPoint), Is.True);
            Assert.That(producer.TryHitLink(initialPoint, out var initialHit), Is.True);
            Assert.That(initialHit.Href, Is.EqualTo("vertical-link"));
            Assert.That(initial.PaintPosition.x, Is.LessThan(10f),
                "The initial vertical-rl view must be anchored to the rightmost column.");

            var initialPaintX = initial.PaintPosition.x;
            textBox.ScrollToPercentX(1f);
            yield return WaitForProducer(producer, true);

            Assert.That(producer.scrollX, Is.EqualTo(producer.maxScrollX).Within(0.01f));
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var scrolled, out _), Is.True);
            Assert.That(scrolled.PaintPosition.x, Is.GreaterThan(initialPaintX));
            Assert.That(scrolled.PaintPosition.x, Is.EqualTo(10f).Within(0.5f));
            Assert.That(TryGetVisibleLinkPoint(scrolled, out var scrolledPoint), Is.True);
            Assert.That(producer.TryHitLink(scrolledPoint, out _), Is.True);
            Assert.That(producer.TryHitLink(new Vector2(5f / 200f, 0.5f), out _), Is.False,
                "A point outside the physical margin clip must not hit a link.");
        }

        [UnityTest]
        public IEnumerator HorizontalScrollbarTracksPhysicalPositionAcrossWritingModes()
        {
            var textBox = CreateTextBox(LongMixedText, out var producer, MarginAll(10f));
            var scrollbarObject = new GameObject("Horizontal scrollbar", typeof(RectTransform),
                typeof(UnityEngine.UI.Scrollbar));
            scrollbarObject.transform.SetParent(root.transform, false);
            var scrollbar = scrollbarObject.GetComponent<UnityEngine.UI.Scrollbar>();
            scrollbar.direction = UnityEngine.UI.Scrollbar.Direction.LeftToRight;
            var bindingObject = new GameObject("Scrollbar binding");
            bindingObject.SetActive(false);
            bindingObject.transform.SetParent(root.transform, false);
            var binding = bindingObject.AddComponent<TextBoxScrollbar>();
            typeof(TextBoxScrollbar).GetField("m_textBox", DeclaredInstance)!.SetValue(binding, textBox);
            typeof(TextBoxScrollbar).GetField("m_horizontalScrollbar", DeclaredInstance)!
                .SetValue(binding, scrollbar);
            bindingObject.SetActive(true);

            foreach (var mode in new[] { WritingMode.VerticalRl, WritingMode.HorizontalTb, WritingMode.VerticalRl })
            {
                producer.paragraphStyle.writingMode = mode;
                producer.wrapMode = mode == WritingMode.HorizontalTb ? TextBoxWrapMode.NoWrap : TextBoxWrapMode.Wrap;
                yield return WaitForProducer(producer, false);
                Assert.That(producer.maxScrollX, Is.GreaterThan(0f));

                foreach (var percent in new[] { 0f, 0.25f, 1f })
                {
                    textBox.ScrollToPercentX(percent);
                    typeof(TextBoxScrollbar).GetMethod("LateUpdate", DeclaredInstance)!.Invoke(binding, null);
                    var expectedValue = mode == WritingMode.VerticalRl ? 1f - percent : percent;
                    Assert.That(scrollbar.value, Is.EqualTo(expectedValue).Within(0.001f),
                        $"{mode}: the thumb must represent the physical viewport position.");
                }

                foreach (var value in new[] { 0.25f, 0f, 1f })
                {
                    scrollbar.value = value;
                    var expectedPercent = mode == WritingMode.VerticalRl ? 1f - value : value;
                    Assert.That(textBox.GetScrollPercentX(), Is.EqualTo(expectedPercent).Within(0.001f),
                        $"{mode}: dragging the thumb must apply the inverse physical mapping.");
                }
            }
        }

        [UnityTest]
        public IEnumerator ControlCharactersRenderWithoutRejectingVerticalLayout()
        {
            const string content = "漢\t字\u00ADABC\u200E\u200F\u061C";
            var textBox = CreateTextBox(content, out var producer,
                MarginSides(0f, 0f, 0f, 0f, true, true, true, true));
            yield return WaitForProducer(producer, false);

            Assert.That(producer.HasOutput, Is.True);
            Assert.That(producer.lastLayoutError, Is.Null);
            Assert.That(textBox.Texture, Is.Not.Null);
            Assert.That(producer.contentWidth, Is.GreaterThan(0f));
            Assert.That(producer.TryGetContentSnapshot(out var snapshot), Is.True);
            Assert.That(snapshot.PlainText, Is.EqualTo(content));
        }

        [UnityTest]
        public IEnumerator StructuralUnsupportedContentUsesHorizontalGeometryAndPreservesPayload()
        {
            const string content =
                "<p><a href=\"block-link\">first مرحبا</a></p>" +
                "<p><ruby>漢<rt>かん</rt></ruby> second</p>";
            var textBox = CreateTextBox(content, out var producer,
                MarginSides(0f, 0f, 0f, 0f, true, true, true, true));
            LogAssert.Expect(LogType.Exception, new Regex("strong RTL content requires horizontal fallback"));
            yield return WaitForProducer(producer, true);

            Assert.That(producer.HasOutput, Is.True);
            Assert.That(producer.lastLayoutError, Is.Null);
            Assert.That(producer.isVerticalLayout, Is.False,
                "Structural vertical fallback must publish real horizontal geometry.");
            Assert.That(textBox.Texture, Is.Not.Null);
            Assert.That(producer.TryGetContentSnapshot(out var snapshot), Is.True);
            Assert.That(snapshot.PlainText, Does.Contain("first"));
            Assert.That(snapshot.PlainText, Does.Contain("漢"));
            Assert.That(snapshot.PlainText, Does.Contain("second"));
            Assert.That(snapshot.Links.Count, Is.EqualTo(1));
            Assert.That(snapshot.Links[0].Href, Is.EqualTo("block-link"));
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var geometry, out _), Is.True);
            Assert.That(TryGetVisibleLinkPoint(geometry, out var point), Is.True);
            Assert.That(producer.TryHitLink(point, out var hit), Is.True);
            Assert.That(hit.Href, Is.EqualTo("block-link"));
        }

        [UnityTest]
        public IEnumerator InvalidDirectionAndAlignmentStillUseVerticalGeometry()
        {
            var textBox = CreateTextBox(LinkedMixedText, out var producer,
                MarginSides(0f, 0f, 0f, 0f, true, true, true, true));
            LogAssert.Expect(LogType.Exception,
                new Regex("textDirection=Rtl defaulted to Ltr.*textAlign=Left defaulted to Start"));
            producer.paragraphStyle.textDirection = TextDirection.Rtl;
            producer.paragraphStyle.textAlign = TextAlign.Left;
            yield return WaitForProducer(producer, true);

            Assert.That(producer.HasOutput, Is.True);
            Assert.That(producer.lastLayoutError, Is.Null);
            Assert.That(producer.isVerticalLayout, Is.True,
                "Direction and alignment defaults must preserve vertical-rl layout.");
            Assert.That(textBox.Texture, Is.Not.Null);
            Assert.That(producer.TryGetContentSnapshot(out var content), Is.True);
            var linkRects = new List<Rect>();
            Assert.That(textBox.TryGetLinkLocalRects(content.ContentVersion, 0, linkRects), Is.True);

            var screenPoint = RectTransformUtility.WorldToScreenPoint(null,
                textBox.rectTransform.TransformPoint(linkRects[0].center));
            var pointer = new PointerEventData(null)
            {
                pointerId = -1,
                button = PointerEventData.InputButton.Left,
                eligibleForClick = true,
                position = screenPoint,
            };
            pointer.pointerCurrentRaycast = new RaycastResult { gameObject = textBox.gameObject };
            string? clickedHref = null;
            textBox.onLinkClicked.AddListener(value => clickedHref = value.Href);

            textBox.OnPointerDown(pointer);
            textBox.OnPointerClick(pointer);

            Assert.That(clickedHref, Is.EqualTo("vertical-link"));
        }

        [UnityTest]
        public IEnumerator JustifyIsAcceptedAndStretchesOnlySoftWrappedLinkColumns()
        {
            const string content = "<a href=\"justify-link\">" + CjkWrapText + "</a>";
            var textBox = CreateTextBox(content, out var producer,
                MarginSides(0f, 0f, 0f, 0f, true, true, true, true));
            yield return WaitForProducer(producer, true);

            Assert.That(producer.HasOutput, Is.True);
            Assert.That(producer.isVerticalLayout, Is.True);
            Assert.That(producer.lastLayoutError, Is.Null);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var startGeometry, out _), Is.True);
            var startRects = startGeometry.Geometry[0].Rects;
            Assert.That(startRects.Count, Is.GreaterThan(1));
            var naturalFirstHeight = startRects[0].height;
            Assert.That(naturalFirstHeight, Is.LessThan(producer.viewportHeight - 1f));
            var startVersion = producer.OutputVersion;

            producer.paragraphStyle.textAlign = TextAlign.Justify;
            yield return WaitForProducerAfter(producer, startVersion, true);

            Assert.That(producer.HasOutput, Is.True);
            Assert.That(producer.isVerticalLayout, Is.True);
            Assert.That(producer.lastLayoutError, Is.Null);
            Assert.That(textBox.Texture, Is.Not.Null);
            Assert.That(producer.OutputVersion, Is.Not.EqualTo(startVersion));
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var justifyGeometry, out _), Is.True);
            var justifyRects = justifyGeometry.Geometry[0].Rects;
            Assert.That(justifyRects.Count, Is.GreaterThan(1));
            Assert.That(justifyRects[0].height,
                Is.EqualTo(producer.viewportHeight).Within(1f),
                "A soft-wrapped link column must consume the full justification extent.");
            Assert.That(justifyRects[^1].height, Is.LessThan(justifyRects[0].height - 1f),
                "The source-end column must keep its natural extent.");
            Assert.That(TryGetVisibleLinkPoint(justifyGeometry, out var point), Is.True);
            Assert.That(producer.TryHitLink(point, out var hit), Is.True);
            Assert.That(hit.Href, Is.EqualTo("justify-link"));

            var justifyVersion = producer.OutputVersion;
            producer.paragraphStyle.textAlign = TextAlign.Start;
            yield return WaitForProducerAfter(producer, justifyVersion, true);

            Assert.That(producer.isVerticalLayout, Is.True);
            Assert.That(producer.lastLayoutError, Is.Null);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var restoredGeometry, out _), Is.True);
            var restoredRects = restoredGeometry.Geometry[0].Rects;
            Assert.That(restoredRects.Count, Is.EqualTo(startRects.Count));
            Assert.That(restoredRects[0].height, Is.EqualTo(naturalFirstHeight).Within(0.5f));
        }

        [UnityTest]
        public IEnumerator SingleLineEllipsisUsesFiniteHeightAndRetainsOnlyVisibleLinkGeometry()
        {
            const string plainText = "天地玄黃宇宙日月";
            var settings = VerticalSettings("<a href=\"ellipsis-link\">" + plainText + "</a>",
                MarginAll(10f),
                TextOverflow.Ellipsis,
                TextBoxWrapMode.NoWrap,
                30f);
            settings.ParagraphStyle.singleLine = true;
            var size = new Vector2Int(200, 100);
            var viewport = TextBoxRenderViewport.Fixed(size, default, Vector2.zero, Vector2.zero);
            using var target = new TextBoxRenderTarget();
            target.Rebuild(viewport, ColorSpace.Gamma, settings, true, null);
            yield return WaitForTarget(target, viewport, settings);

            Assert.That(target.HasOutput, Is.True);
            Assert.That(target.IsVertical, Is.True);
            Assert.That(target.LastLayoutError, Is.Null);
            Assert.That(target.ViewportSize.y, Is.EqualTo(80f).Within(0.1f));
            Assert.That(target.ContentSize.y, Is.EqualTo(80f).Within(0.05f));
            Assert.That(target.TryGetLinkGeometrySnapshot(out var snapshot, out _), Is.True);
            Assert.That(snapshot.Paragraphs.Count, Is.EqualTo(1));
            var metrics = snapshot.Paragraphs[0].LayoutMetrics;
            Assert.That(metrics.Columns.Count, Is.EqualTo(1));
            var column = metrics.Columns[0];
            Assert.That(column.Utf16End, Is.LessThan((ulong)plainText.Length));
            Assert.That(column.InlineExtent, Is.LessThanOrEqualTo(80f + 0.05f));
            Assert.That(metrics.PhysicalSize.y, Is.EqualTo(80f).Within(0.05f));
            Assert.That(snapshot.Paragraphs[0].GetRectsForRange((int)column.Utf16End,
                plainText.Length), Is.Empty);
            Assert.That(snapshot.Geometry.Count, Is.EqualTo(1));
            Assert.That(snapshot.Geometry[0].Rects, Is.Not.Empty);
            Assert.That(TryGetVisibleLinkPoint(snapshot, out var point), Is.True);
            Assert.That(target.TryHitLink(point, out var hit), Is.True);
            Assert.That(hit.Href, Is.EqualTo("ellipsis-link"));
        }

        [UnityTest]
        public IEnumerator EllipsisAffixNeverActivatesRetainedLink()
        {
            const string plainText = "天地玄黃宇宙日月";
            var textBox = CreateTextBox("<a href=\"ellipsis-link\">" + plainText + "</a>",
                out var producer,
                MarginAll(10f));
            producer.textOverflow = TextOverflow.Ellipsis;
            producer.wrapMode = TextBoxWrapMode.NoWrap;
            producer.paragraphStyle.singleLine = true;
            yield return WaitForProducer(producer, true);

            Assert.That(producer.HasOutput, Is.True);
            Assert.That(producer.isVerticalLayout, Is.True);
            Assert.That(producer.lastLayoutError, Is.Null);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var snapshot, out _), Is.True);
            Assert.That(snapshot.Paragraphs.Count, Is.EqualTo(1));
            var column = snapshot.Paragraphs[0].LayoutMetrics.Columns[0];
            Assert.That(column.Utf16End, Is.LessThan((ulong)plainText.Length));
            Assert.That(snapshot.Geometry[0].Rects, Is.Not.Empty);

            var retainedEnd = float.NegativeInfinity;
            foreach (var rect in snapshot.Geometry[0].Rects)
            {
                retainedEnd = Mathf.Max(retainedEnd, rect.yMax);
            }
            var affixEnd = column.PhysicalBounds.yMax;
            Assert.That(affixEnd, Is.GreaterThan(retainedEnd + 0.5f),
                "The test requires a non-empty affix region after the retained source glyphs.");
            var paragraphPoint = new Vector2(column.PhysicalBounds.center.x,
                (retainedEnd + affixEnd) * 0.5f);
            var outputPoint = paragraphPoint * snapshot.FitScale + snapshot.PaintPosition;
            Assert.That(snapshot.ClipRect.Contains(outputPoint), Is.True);
            var affixPoint = new Vector2(outputPoint.x / snapshot.VisibleOutputSize.x,
                outputPoint.y / snapshot.VisibleOutputSize.y);
            Assert.That(producer.TryHitLink(affixPoint, out _), Is.False,
                "Affix must not own or extend the truncated link's source range.");

            Assert.That(TryGetVisibleLinkPoint(snapshot, out var retainedPoint), Is.True);
            Assert.That(producer.TryHitLink(retainedPoint, out var retainedHit), Is.True);
            Assert.That(retainedHit.Href, Is.EqualTo("ellipsis-link"));

            string? clickedHref = null;
            textBox.onLinkClicked.AddListener(value => clickedHref = value.Href);
            var retainedPointer = PointerAtNormalizedPoint(textBox, retainedPoint);
            textBox.OnPointerDown(retainedPointer);
            textBox.OnPointerClick(retainedPointer);
            Assert.That(clickedHref, Is.EqualTo("ellipsis-link"));

            clickedHref = null;
            var affixPointer = PointerAtNormalizedPoint(textBox, affixPoint);
            textBox.OnPointerDown(affixPointer);
            Assert.That(LinkPressCount(textBox), Is.EqualTo(0));
            textBox.OnPointerClick(affixPointer);
            Assert.That(clickedHref, Is.Null,
                "Clicking the synthetic ellipsis must not activate the retained link.");
        }

        [UnityTest]
        public IEnumerator EllipsisReconfigurationDoesNotReuseStaleAffixOrWritingModeGeometry()
        {
            const string plainText = "天地玄黃宇宙日月盈昃辰宿列張";
            var settings = VerticalSettings("<a href=\"ellipsis-state\">" + plainText + "</a>",
                MarginAll(10f),
                TextOverflow.Ellipsis,
                TextBoxWrapMode.NoWrap,
                30f);
            settings.ParagraphStyle.singleLine = true;
            var size = new Vector2Int(200, 100);
            var viewport = TextBoxRenderViewport.Fixed(size, default, Vector2.zero, Vector2.zero);
            using var target = new TextBoxRenderTarget();
            target.Rebuild(viewport, ColorSpace.Gamma, settings, true, null);
            yield return WaitForTarget(target, viewport, settings);

            Assert.That(target.TryGetLinkGeometrySnapshot(out var initial, out _), Is.True);
            var initialMetrics = initial.Paragraphs[0].LayoutMetrics;
            Assert.That(initialMetrics.Columns.Count, Is.EqualTo(1));
            Assert.That(initialMetrics.Columns[0].Utf16End,
                Is.LessThan((ulong)plainText.Length));
            var initialEnd = initialMetrics.Columns[0].Utf16End;
            var initialExtent = initialMetrics.Columns[0].InlineExtent;
            var initialVersion = target.OutputVersion;

            var widerAffix = VerticalSettings("<a href=\"ellipsis-state\">" + plainText + "</a>",
                MarginAll(10f),
                TextOverflow.Ellipsis,
                TextBoxWrapMode.NoWrap,
                30f);
            widerAffix.ParagraphStyle.singleLine = true;
            widerAffix.ParagraphStyle.ellipsisString = "......";
            target.MarkPropertiesChanged();
            target.Rebuild(viewport, ColorSpace.Gamma, widerAffix, false, null);
            yield return WaitForTargetAfter(target, viewport, widerAffix, initialVersion);
            Assert.That(target.TryGetLinkGeometrySnapshot(out var widerSnapshot, out _), Is.True);
            var widerMetrics = widerSnapshot.Paragraphs[0].LayoutMetrics;
            Assert.That(widerMetrics.Columns[0].InlineExtent,
                Is.LessThanOrEqualTo(80f + 0.05f));
            Assert.That(widerMetrics.Columns[0].Utf16End, Is.LessThanOrEqualTo(initialEnd));
            Assert.That(widerMetrics.Columns[0].InlineExtent,
                Is.Not.EqualTo(initialExtent).Within(0.01f));

            var clipped = VerticalSettings("<a href=\"ellipsis-state\">" + plainText + "</a>",
                MarginAll(10f),
                TextOverflow.Clip,
                TextBoxWrapMode.NoWrap,
                30f);
            clipped.ParagraphStyle.singleLine = true;
            var widerVersion = target.OutputVersion;
            target.MarkPropertiesChanged();
            target.Rebuild(viewport, ColorSpace.Gamma, clipped, false, null);
            yield return WaitForTargetAfter(target, viewport, clipped, widerVersion);
            Assert.That(target.TryGetLinkGeometrySnapshot(out var clippedSnapshot, out _), Is.True);
            var clippedMetrics = clippedSnapshot.Paragraphs[0].LayoutMetrics;
            Assert.That(clippedMetrics.Columns[0].Utf16End, Is.EqualTo((ulong)plainText.Length));
            Assert.That(clippedMetrics.PhysicalSize.y, Is.GreaterThan(80f));
            Assert.That(target.ContentSize.y, Is.GreaterThan(80f));

            var restoredVersion = target.OutputVersion;
            target.MarkPropertiesChanged();
            target.Rebuild(viewport, ColorSpace.Gamma, settings, false, null);
            yield return WaitForTargetAfter(target, viewport, settings, restoredVersion);
            Assert.That(target.TryGetLinkGeometrySnapshot(out var restoredSnapshot, out _), Is.True);
            var restoredMetrics = restoredSnapshot.Paragraphs[0].LayoutMetrics;
            Assert.That(restoredMetrics.Columns[0].Utf16End, Is.EqualTo(initialEnd));
            Assert.That(restoredMetrics.Columns[0].InlineExtent,
                Is.EqualTo(initialExtent).Within(0.05f));
            Assert.That(restoredMetrics.PhysicalSize.y, Is.EqualTo(80f).Within(0.05f));

            var horizontalVersion = target.OutputVersion;
            settings.ParagraphStyle.writingMode = WritingMode.HorizontalTb;
            target.MarkPropertiesChanged();
            target.Rebuild(viewport, ColorSpace.Gamma, settings, false, null);
            yield return WaitForTargetAfter(target, viewport, settings, horizontalVersion);
            Assert.That(target.IsVertical, Is.False);
            Assert.That(target.HasOutput, Is.True);

            settings.ParagraphStyle.writingMode = WritingMode.VerticalRl;
            var backVersion = target.OutputVersion;
            target.MarkPropertiesChanged();
            target.Rebuild(viewport, ColorSpace.Gamma, settings, false, null);
            yield return WaitForTargetAfter(target, viewport, settings, backVersion);
            Assert.That(target.IsVertical, Is.True);
            Assert.That(target.TryGetLinkGeometrySnapshot(out var roundTripSnapshot, out _), Is.True);
            var roundTrip = roundTripSnapshot.Paragraphs[0].LayoutMetrics;
            Assert.That(roundTrip.Columns[0].Utf16End, Is.EqualTo(initialEnd));
        }

        [UnityTest]
        public IEnumerator StrongRtlContentUsesHorizontalFallbackWithWorkingLinks()
        {
            const string content = "<a href=\"rtl-link\">مرحبا بالعالم</a>";
            var textBox = CreateTextBox(content, out var producer,
                MarginSides(0f, 0f, 0f, 0f, true, true, true, true));
            LogAssert.Expect(LogType.Exception,
                new Regex("strong RTL content requires horizontal fallback"));
            yield return WaitForProducer(producer, true);

            Assert.That(producer.HasOutput, Is.True);
            Assert.That(producer.lastLayoutError, Is.Null);
            Assert.That(producer.isVerticalLayout, Is.False,
                "Strong RTL content must use the actual horizontal output mode.");
            Assert.That(textBox.Texture, Is.Not.Null);
            Assert.That(producer.TryGetContentSnapshot(out var contentSnapshot), Is.True);
            Assert.That(contentSnapshot.PlainText, Is.EqualTo("مرحبا بالعالم"));
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var geometry, out _), Is.True);
            Assert.That(TryGetVisibleLinkPoint(geometry, out var point), Is.True);
            Assert.That(producer.TryHitLink(point, out var hit), Is.True);
            Assert.That(hit.Href, Is.EqualTo("rtl-link"));
        }

        [UnityTest]
        public IEnumerator EnsureLinkVisibleScrollsTowardAnOffscreenLeftColumn()
        {
            const string content = "<a href=\"first\">" + MixedText + "</a>" +
                                   "中間の長い本文です。" + MixedText + MixedText +
                                   "<a href=\"last\">末尾リンク漢字</a>";
            var textBox = CreateTextBox(content, out var producer, MarginAll(10f));
            yield return WaitForProducer(producer, true);

            Assert.That(producer.TryGetContentSnapshot(out var snapshot), Is.True);
            Assert.That(snapshot.Links.Count, Is.EqualTo(2));
            var last = snapshot.Links[1];
            Assert.That(textBox.EnsureLinkVisible(snapshot.ContentVersion, last.Occurrence), Is.True);
            yield return WaitForProducer(producer, true);

            Assert.That(producer.scrollX, Is.GreaterThan(0f));
            Assert.That(textBox.TryGetLinkLocalRects(snapshot.ContentVersion,
                    last.Occurrence,
                    new System.Collections.Generic.List<Rect>()), Is.True);
        }

        [UnityTest]
        public IEnumerator ClipAndOverflowUseTheDocumentedViewportRectangles()
        {
            var margin = MarginAll(10f);
            var size = new Vector2Int(200, 100);
            var viewport = TextBoxRenderViewport.Fixed(size, default, Vector2.zero, Vector2.zero);
            var clipSettings = VerticalSettings(LinkedMixedText, margin, TextOverflow.Clip);

            using var target = new TextBoxRenderTarget();
            target.Rebuild(viewport, ColorSpace.Gamma, clipSettings, true, null);
            yield return WaitForTarget(target, viewport, clipSettings);

            Assert.That(target.TryGetLinkGeometrySnapshot(out var clipped, out _), Is.True);
            Assert.That(clipped.ClipRect, Is.EqualTo(new Rect(10f, 10f, 180f, 80f)));

            var overflowSettings = VerticalSettings(LinkedMixedText, margin, TextOverflow.Overflow);
            target.Rebuild(viewport, ColorSpace.Gamma, overflowSettings, true, null);
            yield return WaitForTarget(target, viewport, overflowSettings);

            Assert.That(target.TryGetLinkGeometrySnapshot(out var overflow, out _), Is.True);
            Assert.That(overflow.ClipRect, Is.EqualTo(new Rect(0f, 0f, 200f, 100f)));
        }

        [UnityTest]
        public IEnumerator MarginSymmetryChangeUpdatesClipWithoutRelayout()
        {
            var size = new Vector2Int(200, 100);
            var viewport = TextBoxRenderViewport.Fixed(size, default, Vector2.zero, Vector2.zero);
            var firstMargin = new Margin
            {
                Left = new MarginValue { Value = 10f },
                Top = new MarginValue { Value = 10f },
                Right = new MarginValue { Value = 30f },
                Bottom = new MarginValue { Value = 10f },
            };
            var firstSettings = VerticalSettings(LinkedMixedText, firstMargin, TextOverflow.Clip);
            using var target = new TextBoxRenderTarget();
            target.Rebuild(viewport, ColorSpace.Gamma, firstSettings, true, null);
            yield return WaitForTarget(target, viewport, firstSettings);
            var contentWidth = target.ContentSize.x;
            Assert.That(target.TryGetLinkGeometrySnapshot(out var first, out _), Is.True);
            Assert.That(first.ClipRect, Is.EqualTo(new Rect(10f, 10f, 160f, 80f)));

            var secondMargin = new Margin
            {
                Left = new MarginValue { Value = 30f },
                Top = new MarginValue { Value = 10f },
                Right = new MarginValue { Value = 10f },
                Bottom = new MarginValue { Value = 10f },
            };
            var secondSettings = VerticalSettings(LinkedMixedText, secondMargin, TextOverflow.Clip);
            target.Rebuild(viewport, ColorSpace.Gamma, secondSettings, false, null);
            yield return WaitForTarget(target, viewport, secondSettings);

            Assert.That(target.ContentSize.x, Is.EqualTo(contentWidth).Within(0.1f));
            Assert.That(target.TryGetLinkGeometrySnapshot(out var second, out _), Is.True);
            Assert.That(second.ClipRect, Is.EqualTo(new Rect(30f, 10f, 160f, 80f)));
        }

        [UnityTest]
        public IEnumerator AutoMarginsPlaceWholePhysicalParagraphOnXAxis()
        {
            const string content = "<a href=\"auto-x\">漢字ABC</a>";
            var size = new Vector2Int(240, 180);
            var viewport = TextBoxRenderViewport.Fixed(size, default, Vector2.zero, Vector2.zero);
            using var target = new TextBoxRenderTarget();

            var noAuto = VerticalSettings(content, MarginSides(20f, 10f, 20f, 10f),
                TextOverflow.Clip, TextBoxWrapMode.Wrap, 30f);
            target.Rebuild(viewport, ColorSpace.Gamma, noAuto, true, null);
            yield return WaitForTarget(target, viewport, noAuto);
            Assert.That(target.TryGetLinkGeometrySnapshot(out var baseline, out _), Is.True);
            var paragraphWidth = target.ContentSize.x;
            Assert.That(paragraphWidth, Is.LessThan(200f));
            Assert.That(baseline.PaintPosition.x,
                Is.EqualTo(20f + 200f - paragraphWidth).Within(0.25f));

            var leftAuto = VerticalSettings(content, MarginSides(0f, 10f, 20f, 10f, true),
                TextOverflow.Clip, TextBoxWrapMode.Wrap, 30f);
            target.Rebuild(viewport, ColorSpace.Gamma, leftAuto, true, null);
            yield return WaitForTarget(target, viewport, leftAuto);
            Assert.That(target.TryGetLinkGeometrySnapshot(out var leftOnly, out _), Is.True);
            Assert.That(target.ContentSize.x, Is.EqualTo(paragraphWidth).Within(0.1f));
            Assert.That(leftOnly.PaintPosition.x,
                Is.EqualTo(baseline.PaintPosition.x).Within(0.25f),
                "Left-auto alone must keep the existing right edge anchor.");

            var rightAuto = VerticalSettings(content, MarginSides(20f, 10f, 0f, 10f, false, false, true),
                TextOverflow.Clip, TextBoxWrapMode.Wrap, 30f);
            target.Rebuild(viewport, ColorSpace.Gamma, rightAuto, true, null);
            yield return WaitForTarget(target, viewport, rightAuto);
            Assert.That(target.TryGetLinkGeometrySnapshot(out var rightOnly, out _), Is.True);
            Assert.That(target.ContentSize.x, Is.EqualTo(paragraphWidth).Within(0.1f));
            Assert.That(rightOnly.PaintPosition.x, Is.EqualTo(20f).Within(0.25f));

            var bothAuto = VerticalSettings(content, MarginSides(0f, 10f, 0f, 10f, true, false, true),
                TextOverflow.Clip, TextBoxWrapMode.Wrap, 30f);
            target.Rebuild(viewport, ColorSpace.Gamma, bothAuto, true, null);
            yield return WaitForTarget(target, viewport, bothAuto);
            Assert.That(target.TryGetLinkGeometrySnapshot(out var centered, out _), Is.True);
            Assert.That(target.ContentSize.x, Is.EqualTo(paragraphWidth).Within(0.1f));
            Assert.That(centered.PaintPosition.x,
                Is.EqualTo((240f - paragraphWidth) * 0.5f).Within(0.25f));
            Assert.That(centered.PaintPosition.y, Is.EqualTo(10f).Within(0.25f));
        }

        [UnityTest]
        public IEnumerator AutoMarginsCenterShortWrappedContentOnPhysicalYAxis()
        {
            const string content = "<a href=\"auto-y\">漢字ABC</a>";
            var size = new Vector2Int(240, 220);
            var viewport = TextBoxRenderViewport.Fixed(size, default, Vector2.zero, Vector2.zero);
            using var target = new TextBoxRenderTarget();
            var cases = new[]
            {
                (Top: false, Bottom: false),
                (Top: true, Bottom: false),
                (Top: false, Bottom: true),
                (Top: true, Bottom: true),
            };

            foreach (var item in cases)
            {
                var settings = VerticalSettings(content,
                    MarginSides(0f, 0f, 0f, 0f, false, item.Top, false, item.Bottom),
                    TextOverflow.Clip,
                    TextBoxWrapMode.Wrap,
                    30f);
                using var reference = BuildVerticalParagraph(settings);
                reference.LayoutWithInlineExtent(220f);
                var metrics = reference.LayoutMetrics;
                var spare = 220f - metrics.ContentBounds.height;
                var expectedY = 0f;
                if (item.Top && item.Bottom)
                {
                    expectedY = spare * 0.5f - metrics.ContentBounds.yMin;
                }
                else if (item.Top)
                {
                    expectedY = spare - metrics.ContentBounds.yMin;
                }
                else if (item.Bottom)
                {
                    expectedY = -metrics.ContentBounds.yMin;
                }

                target.Rebuild(viewport, ColorSpace.Gamma, settings, true, null);
                yield return WaitForTarget(target, viewport, settings);
                Assert.That(target.TryGetLinkGeometrySnapshot(out var snapshot, out _), Is.True);
                Assert.That(snapshot.PaintPosition.y, Is.EqualTo(expectedY).Within(0.25f));
            }
        }

        [UnityTest]
        public IEnumerator AutoMarginsApplyOneCommonTranslationAfterColumnAlignment()
        {
            const string content = "<a href=\"align\">漢\n字ABC\n天地玄黃</a>";
            const string plainText = "漢\n字ABC\n天地玄黃";
            var size = new Vector2Int(220, 180);
            var viewport = TextBoxRenderViewport.Fixed(size, default, Vector2.zero, Vector2.zero);
            using var target = new TextBoxRenderTarget();

            foreach (var alignment in new[] { TextAlign.Start, TextAlign.End, TextAlign.Center })
            {
                var settings = VerticalSettings(content, MarginSides(0f, 0f, 0f, 0f, false, true, false, true),
                    TextOverflow.Clip, TextBoxWrapMode.Wrap, 30f);
                settings.ParagraphStyle.textAlign = alignment;
                using var reference = BuildVerticalParagraph(settings);
                reference.LayoutWithInlineExtent(180f);
                var metrics = reference.LayoutMetrics;
                var expectedY = (180f - metrics.ContentBounds.height) * 0.5f -
                                metrics.ContentBounds.yMin;

                target.Rebuild(viewport, ColorSpace.Gamma, settings, true, null);
                yield return WaitForTarget(target, viewport, settings);
                Assert.That(target.TryGetLinkGeometrySnapshot(out var snapshot, out _), Is.True);
                Assert.That(snapshot.PaintPosition.y, Is.EqualTo(expectedY).Within(0.25f));
                Assert.That(snapshot.Geometry[0].Rects.Count, Is.EqualTo(reference.GetRectsForRange(0,
                    plainText.Length).Count));
                for (var index = 0; index < snapshot.Geometry[0].Rects.Count; ++index)
                {
                    Assert.That(snapshot.Geometry[0].Rects[index],
                        Is.EqualTo(reference.GetRectsForRange(0, plainText.Length)[index]),
                        "Auto margins must not alter per-column relative geometry.");
                }
            }
        }

        [UnityTest]
        public IEnumerator MixedMarginUnitsAndScaleUseTheSameAutoOrigin()
        {
            const string content = "<a href=\"mixed-units\">漢字ABC</a>";
            var size = new Vector2Int(200, 200);
            var viewport = TextBoxRenderViewport.Fixed(size, default, Vector2.zero, Vector2.zero)
                .WithScreenSpaceRasterization(1.5f);
            var margin = new Margin
            {
                Left = new MarginValue { Auto = true, Value = 0.5f, Unit = MarginUnit.Em },
                Top = new MarginValue { Auto = true, Value = 5f, Unit = MarginUnit.Vh },
                Right = new MarginValue { Value = 5f, Unit = MarginUnit.Vw },
                Bottom = new MarginValue { Value = 5f, Unit = MarginUnit.ContainerPercent },
            };
            var settings = VerticalSettings(content, margin, TextOverflow.Clip,
                TextBoxWrapMode.Wrap, 30f)
                .WithVerticalLayout(new MarginResolveContext(200f, 200f, 30f,
                    200f, 200f, hasContainerWidth: true, hasViewport: true),
                    TextHeightMode.Fixed);
            using var target = new TextBoxRenderTarget();
            target.Rebuild(viewport, ColorSpace.Gamma, settings, true, null);
            yield return WaitForTarget(target, viewport, settings);

            using var reference = BuildVerticalParagraph(settings);
            reference.LayoutWithInlineExtent(190f);
            var metrics = reference.LayoutMetrics;
            Assert.That(target.TryGetLinkGeometrySnapshot(out var snapshot, out _), Is.True);
            Assert.That(target.ContentSize.x, Is.EqualTo(metrics.PhysicalSize.x).Within(0.25f));
            Assert.That(snapshot.PaintPosition.x,
                Is.EqualTo(190f - metrics.PhysicalSize.x).Within(0.25f));
            Assert.That(snapshot.PaintPosition.y,
                Is.EqualTo(190f - metrics.ContentBounds.height - metrics.ContentBounds.yMin)
                    .Within(0.25f));
            Assert.That(TryGetVisibleLinkPoint(snapshot, out var point), Is.True);
            Assert.That(target.TryHitLink(point, out var hit), Is.True);
            Assert.That(hit.Href, Is.EqualTo("mixed-units"));
        }

        [UnityTest]
        public IEnumerator OverflowDoesNotAllocateNegativeAutoSpace()
        {
            var size = new Vector2Int(200, 100);
            var viewport = TextBoxRenderViewport.Fixed(size, default, Vector2.zero, Vector2.zero);
            using var target = new TextBoxRenderTarget();
            var auto = VerticalSettings(LinkedMixedText, MarginAll(0f), TextOverflow.Clip,
                TextBoxWrapMode.Wrap, 30f);
            auto.Margin.Left.Auto = true;
            auto.Margin.Right.Auto = true;
            target.Rebuild(viewport, ColorSpace.Gamma, auto, true, null);
            yield return WaitForTarget(target, viewport, auto);

            var paragraphWidth = target.ContentSize.x;
            Assert.That(paragraphWidth, Is.GreaterThan(target.ViewportSize.x));
            Assert.That(target.TryGetLinkGeometrySnapshot(out var snapshot, out _), Is.True);
            Assert.That(snapshot.PaintPosition.x,
                Is.EqualTo(target.ViewportSize.x - paragraphWidth).Within(0.25f));
            Assert.That(target.MaxScrollOffset.x,
                Is.EqualTo(paragraphWidth - target.ViewportSize.x).Within(0.25f));
            Assert.That(target.ScrollOffset.x, Is.EqualTo(0f).Within(0.01f));

            var noWrap = VerticalSettings(LinkedMixedText, MarginAll(0f), TextOverflow.Clip,
                TextBoxWrapMode.NoWrap, 30f);
            noWrap.Margin.Top.Auto = true;
            noWrap.Margin.Bottom.Auto = true;
            target.Rebuild(viewport, ColorSpace.Gamma, noWrap, true, null);
            yield return WaitForTarget(target, viewport, noWrap);
            Assert.That(target.ContentSize.y, Is.GreaterThan(target.ViewportSize.y));
            Assert.That(target.TryGetLinkGeometrySnapshot(out var tall, out _), Is.True);
            Assert.That(tall.PaintPosition.y, Is.EqualTo(0f).Within(0.01f));
            Assert.That(target.MaxScrollOffset.y, Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator EmptyAndBreakOnlyContentRemainFiniteWithAutoMargins()
        {
            var size = new Vector2Int(200, 100);
            var viewport = TextBoxRenderViewport.Fixed(size, default, Vector2.zero, Vector2.zero);
            using var target = new TextBoxRenderTarget();
            foreach (var content in new[] { "", " ", "\n", "A\n\nB", "A\n" })
            {
                var settings = VerticalSettings(content, MarginSides(0f, 0f, 0f, 0f,
                    true, true, true, true), TextOverflow.Clip);
                target.Rebuild(viewport, ColorSpace.Gamma, settings, true, null);
                yield return WaitForTarget(target, viewport, settings);
                Assert.That(target.HasOutput, Is.True);
                Assert.That(target.LastLayoutError, Is.Null);
                Assert.That(float.IsFinite(target.ContentSize.x), Is.True);
                Assert.That(float.IsFinite(target.ContentSize.y), Is.True);
                Assert.That(target.ContentSize.x, Is.GreaterThanOrEqualTo(0f));
                Assert.That(target.ContentSize.y, Is.GreaterThanOrEqualTo(0f));
            }

            var breakOnly = VerticalSettings("A\n\nB", MarginAll(0f), TextOverflow.Clip);
            target.Rebuild(viewport, ColorSpace.Gamma, breakOnly, true, null);
            yield return WaitForTarget(target, viewport, breakOnly);
            Assert.That(target.ContentSize.x, Is.GreaterThan(0f),
                "Empty hard-break columns still contribute physical X thickness.");
        }

        [UnityTest]
        public IEnumerator AutoMarginsShareScrollVisualOffsetAndLinkGeometry()
        {
            const string content = "<a href=\"auto-visual\">漢字ABC</a>" + LongMixedText;
            var size = new Vector2Int(200, 160);
            var visualOffset = new Vector2(3f, -2f);
            var viewport = TextBoxRenderViewport.Fixed(size, default,
                    new Vector2(0f, 12f), visualOffset)
                .WithScreenSpaceRasterization(1.5f);
            var settings = VerticalSettings(content, MarginSides(0f, 0f, 0f, 0f,
                true, true, true, true), TextOverflow.Clip, TextBoxWrapMode.NoWrap);
            using var target = new TextBoxRenderTarget();
            target.Rebuild(viewport, ColorSpace.Gamma, settings, true, null);
            yield return WaitForTarget(target, viewport, settings);

            using var reference = BuildVerticalParagraph(settings);
            reference.LayoutWithInlineExtent(float.PositiveInfinity);
            var metrics = reference.LayoutMetrics;
            Assert.That(target.ScrollOffset.y, Is.GreaterThan(0f));
            Assert.That(target.TryGetLinkGeometrySnapshot(out var snapshot, out _), Is.True);
            Assert.That(snapshot.PaintPosition.x,
                Is.EqualTo((200f - metrics.PhysicalSize.x) * 0.5f +
                           target.ScrollOffset.x + visualOffset.x).Within(0.25f));
            Assert.That(snapshot.PaintPosition.y,
                Is.EqualTo(-target.ScrollOffset.y - visualOffset.y).Within(0.25f),
                "Overflowing NoWrap content must keep the existing top anchor when Auto Y has no spare.");
            Assert.That(TryGetVisibleLinkPoint(snapshot, out var point), Is.True);
            Assert.That(target.TryHitLink(point, out var hit), Is.True);
            Assert.That(hit.Href, Is.EqualTo("auto-visual"));

            var linkRect = snapshot.Geometry[0].Rects[0];
            var outputRect = new Rect(linkRect.x * snapshot.FitScale + snapshot.PaintPosition.x,
                linkRect.y * snapshot.FitScale + snapshot.PaintPosition.y,
                linkRect.width * snapshot.FitScale,
                linkRect.height * snapshot.FitScale);
            var readback = ReadOutput(target, size);
            try
            {
                var pixels = readback.GetPixels32();
                var painted = 0;
                var xMin = Mathf.Clamp(Mathf.FloorToInt(outputRect.xMin), 0, size.x - 1);
                var xMax = Mathf.Clamp(Mathf.CeilToInt(outputRect.xMax), 0, size.x);
                for (var y = 0; y < size.y; ++y)
                {
                    for (var x = xMin; x < xMax; ++x)
                    {
                        if (pixels[y * size.x + x].a > 16)
                        {
                            ++painted;
                        }
                    }
                }

                Assert.That(painted, Is.GreaterThan(0),
                    "The painted link column must follow the same auto origin as hit geometry.");
            }
            finally
            {
                Object.Destroy(readback);
            }
        }

        [UnityTest]
        public IEnumerator AutoMarginVisualSamplesWriteBuildArtifacts()
        {
            var outputDirectory = IOPath.GetFullPath(IOPath.Combine(Application.dataPath,
                "..", "TestResults", "vertical-margin-auto-visual"));
            Directory.CreateDirectory(outputDirectory);
            using var target = new TextBoxRenderTarget();

            var horizontal = VerticalSettings("<a href=\"visual-x\">漢字ABC</a>",
                MarginSides(0f, 0f, 0f, 0f, true, false, true, false),
                TextOverflow.Clip, TextBoxWrapMode.Wrap, 30f);
            yield return CaptureAutoVisualSample(target, outputDirectory, "horizontal-double-auto",
                TextBoxRenderViewport.Fixed(new Vector2Int(240, 180), default,
                    Vector2.zero, Vector2.zero), horizontal);

            var vertical = VerticalSettings("<a href=\"visual-y\">漢字ABC</a>",
                MarginSides(0f, 0f, 0f, 0f, false, true, false, true),
                TextOverflow.Clip, TextBoxWrapMode.Wrap, 30f);
            yield return CaptureAutoVisualSample(target, outputDirectory, "vertical-double-auto",
                TextBoxRenderViewport.Fixed(new Vector2Int(200, 220), default,
                    Vector2.zero, Vector2.zero), vertical);

            var fourSides = VerticalSettings("<a href=\"visual-all\">漢\n字ABC\n天地玄黃</a>",
                MarginSides(0f, 0f, 0f, 0f, true, true, true, true),
                TextOverflow.Clip, TextBoxWrapMode.Wrap, 30f);
            fourSides.ParagraphStyle.textAlign = TextAlign.Center;
            yield return CaptureAutoVisualSample(target, outputDirectory, "four-sides-auto",
                TextBoxRenderViewport.Fixed(new Vector2Int(220, 180), default,
                    Vector2.zero, Vector2.zero), fourSides);

            var unevenColumns = VerticalSettings("<a href=\"visual-uneven\">漢\n字ABC\n天地玄黃宇宙</a>",
                MarginSides(0f, 0f, 0f, 0f, true, false, true, false),
                TextOverflow.Clip, TextBoxWrapMode.Wrap, 30f);
            unevenColumns.ParagraphStyle.textAlign = TextAlign.End;
            yield return CaptureAutoVisualSample(target, outputDirectory, "uneven-columns-auto",
                TextBoxRenderViewport.Fixed(new Vector2Int(200, 180), default,
                    Vector2.zero, Vector2.zero), unevenColumns);

            var mixedUnits = VerticalSettings("<a href=\"visual-mixed\">漢字ABC</a>",
                new Margin
                {
                    Left = new MarginValue { Auto = true, Value = 0.25f, Unit = MarginUnit.Em },
                    Top = new MarginValue { Auto = true, Value = 4f, Unit = MarginUnit.Vh },
                    Right = new MarginValue { Value = 2f, Unit = MarginUnit.Vw },
                    Bottom = new MarginValue { Value = 5f, Unit = MarginUnit.ContainerPercent },
                }, TextOverflow.Clip, TextBoxWrapMode.Wrap, 30f);
            yield return CaptureAutoVisualSample(target, outputDirectory, "mixed-units-auto",
                TextBoxRenderViewport.Fixed(new Vector2Int(200, 200), default,
                    Vector2.zero, Vector2.zero).WithScreenSpaceRasterization(1.5f), mixedUnits);

            var overflow = VerticalSettings(LinkedMixedText,
                MarginSides(0f, 0f, 0f, 0f, true, true, true, true),
                TextOverflow.Clip, TextBoxWrapMode.Wrap, 30f);
            yield return CaptureAutoVisualSample(target, outputDirectory, "overflow-auto",
                TextBoxRenderViewport.Fixed(new Vector2Int(200, 100), default,
                    Vector2.zero, Vector2.zero), overflow);

            var smallLineHeight = VerticalSettings("<a href=\"visual-small\">漢字ABC</a>",
                MarginSides(20f, 20f, 20f, 20f, true, true, true, true),
                TextOverflow.Overflow, TextBoxWrapMode.Wrap, 30f);
            smallLineHeight.TextStyle.lineHeight = 0.35f;
            yield return CaptureAutoVisualSample(target, outputDirectory, "small-line-height-auto",
                TextBoxRenderViewport.Fixed(new Vector2Int(200, 180), default,
                    Vector2.zero, Vector2.zero), smallLineHeight);
        }

        [UnityTest]
        public IEnumerator LineHeightAndMixedFontSizeUsePhysicalWidthForAutoX()
        {
            const string content =
                "<a href=\"auto-line-height\"><font size=\"48\">漢</font><font size=\"24\">ABC</font></a>";
            var size = new Vector2Int(260, 220);
            var viewport = TextBoxRenderViewport.Fixed(size, default, Vector2.zero, Vector2.zero);
            using var target = new TextBoxRenderTarget();
            foreach (var lineHeight in new[] { 0f, 1.5f, 0.5f })
            {
                var settings = VerticalSettings(content, MarginSides(0f, 0f, 0f, 0f,
                    true, true, true, true), TextOverflow.Clip, TextBoxWrapMode.Wrap, 30f);
                settings.TextStyle.lineHeight = lineHeight;
                using var reference = BuildVerticalParagraph(settings);
                reference.LayoutWithInlineExtent(220f);
                var metrics = reference.LayoutMetrics;

                target.Rebuild(viewport, ColorSpace.Gamma, settings, true, null);
                yield return WaitForTarget(target, viewport, settings);
                Assert.That(target.TryGetLinkGeometrySnapshot(out var snapshot, out _), Is.True);
                Assert.That(target.ContentSize.x, Is.EqualTo(metrics.PhysicalSize.x).Within(0.25f));
                Assert.That(snapshot.PaintPosition.x,
                    Is.EqualTo((260f - metrics.PhysicalSize.x) * 0.5f).Within(0.25f));
                Assert.That(snapshot.PaintPosition.y,
                    Is.EqualTo((220f - metrics.ContentBounds.height) * 0.5f -
                               metrics.ContentBounds.yMin).Within(0.25f));
            }
        }

        [UnityTest]
        public IEnumerator WidthChangesUpdateClipAndScrollWithoutReflowingColumns()
        {
            var margin = MarginAll(10f);
            var settings = VerticalSettings(LinkedMixedText, margin, TextOverflow.Clip);
            var size = new Vector2Int(200, 100);
            var viewport = TextBoxRenderViewport.Fixed(size, default, Vector2.zero, Vector2.zero);
            using var target = new TextBoxRenderTarget();
            target.Rebuild(viewport, ColorSpace.Gamma, settings, true, null);
            yield return WaitForTarget(target, viewport, settings);

            var contentWidth = target.ContentSize.x;
            var maxScrollX = target.MaxScrollOffset.x;
            Assert.That(target.ViewportSize.x, Is.EqualTo(180f).Within(0.1f));
            Assert.That(maxScrollX, Is.GreaterThan(0f));

            size.x = 160;
            viewport = TextBoxRenderViewport.Fixed(size, default, Vector2.zero, Vector2.zero);
            target.Rebuild(viewport, ColorSpace.Gamma, settings, false, null);
            yield return WaitForTarget(target, viewport, settings);

            Assert.That(target.ContentSize.x, Is.EqualTo(contentWidth).Within(0.1f));
            Assert.That(target.ViewportSize.x, Is.EqualTo(140f).Within(0.1f));
            Assert.That(target.MaxScrollOffset.x, Is.EqualTo(maxScrollX + 40f).Within(0.2f));
            Assert.That(target.TryGetLinkGeometrySnapshot(out var resized, out _), Is.True);
            Assert.That(resized.ClipRect, Is.EqualTo(new Rect(10f, 10f, 140f, 80f)));
        }

        [UnityTest]
        public IEnumerator HeightChangesAddColumnsWithoutChangingPhysicalWidthRules()
        {
            var margin = MarginAll(10f);
            var settings = VerticalSettings(CjkWrapText, margin, TextOverflow.Clip);
            var size = new Vector2Int(200, 100);
            var viewport = TextBoxRenderViewport.Fixed(size, default, Vector2.zero, Vector2.zero);
            using var target = new TextBoxRenderTarget();
            target.Rebuild(viewport, ColorSpace.Gamma, settings, true, null);
            yield return WaitForTarget(target, viewport, settings);
            var wideHeightContentWidth = target.ContentSize.x;

            size.y = 70;
            viewport = TextBoxRenderViewport.Fixed(size, default, Vector2.zero, Vector2.zero);
            target.Rebuild(viewport, ColorSpace.Gamma, settings, false, null);
            yield return WaitForTarget(target, viewport, settings);

            Assert.That(target.ViewportSize.y, Is.EqualTo(50f).Within(0.1f));
            Assert.That(target.ContentSize.y, Is.EqualTo(50f).Within(1f));
            Assert.That(target.ContentSize.x, Is.GreaterThan(wideHeightContentWidth));
        }

        [UnityTest]
        public IEnumerator LineHeightChangesRebuildWidthScrollAndLinkGeometry()
        {
            var textBox = CreateTextBox(LinkedMixedText, out var producer, MarginAll(10f));
            yield return WaitForProducer(producer, true);

            var initialContentWidth = producer.contentWidth;
            var initialMaxScrollX = producer.maxScrollX;
            var initialVersion = producer.OutputVersion;
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var initialGeometry, out _), Is.True);
            Assert.That(initialGeometry.Geometry[0].Rects, Is.Not.Empty);
            var initialLinkWidth = initialGeometry.Geometry[0].Rects[0].width;

            producer.textStyle.lineHeight = 1.5f;
            yield return WaitForProducer(producer, true);

            Assert.That(producer.isVerticalLayout, Is.True);
            Assert.That(producer.OutputVersion, Is.Not.EqualTo(initialVersion));
            Assert.That(producer.contentWidth, Is.EqualTo(initialContentWidth * 1.5f).Within(1f));
            Assert.That(producer.maxScrollX, Is.GreaterThan(initialMaxScrollX));
            Assert.That(textBox.Texture, Is.Not.Null);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var scaledGeometry, out _), Is.True);
            Assert.That(scaledGeometry.Geometry[0].Rects, Is.Not.Empty);
            Assert.That(scaledGeometry.Geometry[0].Rects[0].width,
                Is.EqualTo(initialLinkWidth * 1.5f).Within(1f));
            Assert.That(TryGetVisibleLinkPoint(scaledGeometry, out var scaledPoint), Is.True);
            Assert.That(producer.TryHitLink(scaledPoint, out var scaledHit), Is.True);
            Assert.That(scaledHit.Href, Is.EqualTo("vertical-link"));

            producer.textStyle.lineHeight = 2f;
            yield return WaitForProducer(producer, true);
            var wideWidth = producer.contentWidth;
            var wideMaxScrollX = producer.maxScrollX;
            textBox.ScrollToPercentX(1f);
            yield return WaitForProducer(producer, true);
            Assert.That(producer.scrollX, Is.EqualTo(wideMaxScrollX).Within(0.5f));

            producer.textStyle.lineHeight = 0.5f;
            yield return WaitForProducer(producer, true);
            Assert.That(producer.contentWidth, Is.EqualTo(wideWidth * 0.25f).Within(1f));
            Assert.That(producer.maxScrollX, Is.LessThan(wideMaxScrollX));
            Assert.That(producer.scrollX, Is.LessThanOrEqualTo(producer.maxScrollX + 0.01f));
            Assert.That(producer.scrollX, Is.GreaterThanOrEqualTo(0f));
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var narrowGeometry, out _), Is.True);
            Assert.That(TryGetVisibleLinkPoint(narrowGeometry, out var narrowPoint), Is.True);
            Assert.That(producer.TryHitLink(narrowPoint, out _), Is.True);

            producer.textStyle.lineHeight = 0f;
            yield return WaitForProducer(producer, true);
            Assert.That(producer.contentWidth, Is.EqualTo(initialContentWidth).Within(1f));
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var restoredGeometry, out _), Is.True);
            Assert.That(restoredGeometry.Geometry[0].Rects[0].width,
                Is.EqualTo(initialLinkWidth).Within(1f));
            Assert.That(textBox.Texture, Is.Not.Null);
            Assert.That(textBox.UvRect.width, Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator NoWrapUsesInfiniteInlineExtentAndKeepsHardBreaksScrollable()
        {
            var margin = MarginSides(10f, 10f, 10f, 10f,
                leftAuto: true, rightAuto: true);
            var settings = VerticalSettings("ABC\nDEF", margin, TextOverflow.Clip,
                TextBoxWrapMode.NoWrap, 48f);
            var size = new Vector2Int(200, 100);
            var viewport = TextBoxRenderViewport.Fixed(size, default, Vector2.zero, Vector2.zero);
            using var target = new TextBoxRenderTarget();
            target.Rebuild(viewport, ColorSpace.Gamma, settings, true, null);
            yield return WaitForTarget(target, viewport, settings);

            Assert.That(target.TryGetContentSnapshot(out var content), Is.True);
            Assert.That(content.PlainText, Is.EqualTo("ABC\nDEF"));
            Assert.That(target.ContentSize.y, Is.GreaterThan(target.ViewportSize.y));
            Assert.That(target.MaxScrollOffset.y, Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator TextIndentPaintsAndHitsLinksAtTheFirstColumnOrigin()
        {
            var size = new Vector2Int(200, 120);
            var viewport = TextBoxRenderViewport.Fixed(size, default, Vector2.zero, Vector2.zero);
            using var target = new TextBoxRenderTarget();

            foreach (var item in new[]
                     {
                         (Indent: "30px", Expected: 30f),
                         (Indent: "-20px", Expected: -20f),
                         (Indent: "25%", Expected: 25f),
                     })
            {
                var settings = VerticalSettings(
                    "<p style=\"text-indent: " + item.Indent + "\"><a href=\"indent-v\">" +
                    LongMixedText + "</a></p>",
                    MarginAll(10f), TextOverflow.Clip, TextBoxWrapMode.Wrap, 30f);
                target.Rebuild(viewport, ColorSpace.Gamma, settings, true, null);
                yield return WaitForTarget(target, viewport, settings);

                using var reference = BuildVerticalParagraph(settings);
                reference.LayoutWithInlineExtent(100f);
                var metrics = reference.LayoutMetrics;
                Assert.That(metrics.Columns[0].PhysicalBounds.yMin,
                    Is.EqualTo(item.Expected).Within(0.05f), item.Indent);

                Assert.That(target.TryGetLinkGeometrySnapshot(out var snapshot, out _), Is.True);
                var expectedRects = reference.GetRectsForRange(0, LongMixedText.Length);
                Assert.That(snapshot.Geometry[0].Rects, Has.Count.EqualTo(expectedRects.Count), item.Indent);
                for (var index = 0; index < expectedRects.Count; ++index)
                {
                    Assert.That(snapshot.Geometry[0].Rects[index].yMin,
                        Is.EqualTo(expectedRects[index].yMin).Within(0.05f), item.Indent);
                    Assert.That(snapshot.Geometry[0].Rects[index].height,
                        Is.EqualTo(expectedRects[index].height).Within(0.05f), item.Indent);
                }

                Assert.That(TryGetVisibleLinkPoint(snapshot, out var point), Is.True, item.Indent);
                Assert.That(target.TryHitLink(point, out var hit), Is.True, item.Indent);
                Assert.That(hit.Href, Is.EqualTo("indent-v"));

                var readback = ReadOutput(target, size);
                try
                {
                    Assert.That(TryGetPaintedBounds(readback, out _), Is.True, item.Indent);
                }
                finally
                {
                    Object.Destroy(readback);
                }
            }
        }

        [UnityTest]
        public IEnumerator TextIndentValueUnitAndHeightUpdatesInvalidateVerticalGeometry()
        {
            var textBox = CreateTextBox(
                "<p style=\"text-indent: 25%\"><a href=\"indent-update\">" +
                LongMixedText + "</a></p>",
                out var producer, MarginAll(10f));
            yield return WaitForProducer(producer, true);

            Assert.That(producer.TryGetLinkGeometrySnapshot(out var initial, out _), Is.True);
            Assert.That(initial.Geometry[0].Rects[0].yMin, Is.EqualTo(20f).Within(0.1f));
            var initialVersion = producer.OutputVersion;

            textBox.rectTransform.sizeDelta = new Vector2(200f, 140f);
            yield return WaitForProducerAfter(producer, initialVersion, true);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var taller, out _), Is.True);
            Assert.That(taller.Geometry[0].Rects[0].yMin, Is.EqualTo(30f).Within(0.1f));
            Assert.That(producer.OutputVersion, Is.Not.EqualTo(initialVersion));

            var heightVersion = producer.OutputVersion;
            producer.content = "<p style=\"text-indent: 40px\"><a href=\"indent-update\">" +
                               LongMixedText + "</a></p>";
            yield return WaitForProducerAfter(producer, heightVersion, true);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var pixels, out _), Is.True);
            Assert.That(pixels.Geometry[0].Rects[0].yMin, Is.EqualTo(40f).Within(0.1f));

            var pixelVersion = producer.OutputVersion;
            producer.content = "<p style=\"text-indent: -2em\"><a href=\"indent-update\">" +
                               LongMixedText + "</a></p>";
            yield return WaitForProducerAfter(producer, pixelVersion, true);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var em, out _), Is.True);
            Assert.That(em.Geometry[0].Rects[0].yMin, Is.EqualTo(-60f).Within(0.1f));

            var emVersion = producer.OutputVersion;
            producer.textStyle.size = 40f;
            yield return WaitForProducerAfter(producer, emVersion, true);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var largerEm, out _), Is.True);
            Assert.That(largerEm.Geometry[0].Rects[0].yMin, Is.EqualTo(-80f).Within(0.1f));
            Assert.That(producer.OutputVersion, Is.Not.EqualTo(emVersion));

            var fontVersion = producer.OutputVersion;
            producer.paragraphStyle.writingMode = WritingMode.HorizontalTb;
            yield return WaitForProducerAfter(producer, fontVersion, false);
            Assert.That(producer.isVerticalLayout, Is.False);
            producer.paragraphStyle.writingMode = WritingMode.VerticalRl;
            yield return WaitForProducer(producer, true);
            Assert.That(producer.isVerticalLayout, Is.True);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var restored, out _), Is.True);
            Assert.That(restored.Geometry[0].Rects[0].yMin, Is.EqualTo(-80f).Within(0.1f));
            Assert.That(TryGetVisibleLinkPoint(restored, out var restoredPoint), Is.True);
            Assert.That(producer.TryHitLink(restoredPoint, out var restoredHit), Is.True);
            Assert.That(restoredHit.Href, Is.EqualTo("indent-update"));
            Assert.That(textBox.Texture, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator SingleLineOverridesWrapWithOneScrollableVerticalColumn()
        {
            var margin = MarginSides(10f, 10f, 10f, 10f,
                leftAuto: true, rightAuto: true);
            var wrapSettings = VerticalSettings(LongMixedText, margin, TextOverflow.Overflow,
                TextBoxWrapMode.Wrap, 48f);
            wrapSettings.ParagraphStyle.singleLine = true;
            var noWrapSettings = VerticalSettings(LongMixedText, margin, TextOverflow.Overflow,
                TextBoxWrapMode.NoWrap, 48f);
            noWrapSettings.ParagraphStyle.singleLine = true;
            var size = new Vector2Int(200, 100);
            var viewport = TextBoxRenderViewport.Fixed(size, default, Vector2.zero, Vector2.zero);

            using var wrappedTarget = new TextBoxRenderTarget();
            wrappedTarget.Rebuild(viewport, ColorSpace.Gamma, wrapSettings, true, null);
            yield return WaitForTarget(wrappedTarget, viewport, wrapSettings);

            using var noWrapTarget = new TextBoxRenderTarget();
            noWrapTarget.Rebuild(viewport, ColorSpace.Gamma, noWrapSettings, true, null);
            yield return WaitForTarget(noWrapTarget, viewport, noWrapSettings);

            Assert.That(wrappedTarget.ContentSize.y, Is.GreaterThan(wrappedTarget.ViewportSize.y));
            Assert.That(wrappedTarget.MaxScrollOffset.y, Is.GreaterThan(0f));
            Assert.That(wrappedTarget.ContentSize.y, Is.EqualTo(noWrapTarget.ContentSize.y).Within(0.5f));
            Assert.That(wrappedTarget.ContentSize.x, Is.EqualTo(noWrapTarget.ContentSize.x).Within(0.5f));
        }

        [UnityTest]
        public IEnumerator MarginAndScreenSpaceScaleKeepLinkHitGeometryConsistent()
        {
            foreach (var content in new[] { LinkedMixedText,
                         "<a href=\"vertical-link\"><p>" + MixedText + "</p><p>" + MixedText + "</p></a>" })
            {
                var settings = VerticalSettings(content, MarginAll(10f), TextOverflow.Clip,
                    TextBoxWrapMode.Wrap, 30f);
                settings.TextStyle.lineHeight = 1.4f;
                settings.ParagraphStyle.paragraphSpacing = 17f;
                var size = new Vector2Int(200, 100);
                using var target = new TextBoxRenderTarget();
                var origin = Vector2.zero;
                foreach (var visualOffset in new[] { Vector2.zero, new Vector2(7f, 9f) })
                {
                    var viewport = TextBoxRenderViewport.Fixed(size, default, Vector2.zero, visualOffset)
                        .WithScreenSpaceRasterization(1.5f);
                    var previousVersion = target.OutputVersion;
                    target.MarkPaintChanged();
                    target.Rebuild(viewport, ColorSpace.Gamma, settings, false, null);
                    yield return WaitForTargetAfter(target, viewport, settings, previousVersion);
                    Assert.That(target.TryGetLinkGeometrySnapshot(out var snapshot, out _), Is.True);
                    Assert.That(snapshot.ClipRect, Is.EqualTo(new Rect(10f, 10f, 180f, 80f)));
                    Assert.That(snapshot.Geometry[0].BlockRanges.Count, Is.EqualTo(snapshot.Paragraphs.Count));
                    if (visualOffset == Vector2.zero) origin = snapshot.PaintPosition;
                    else Assert.That(snapshot.PaintPosition, Is.EqualTo(origin + new Vector2(7f, -9f)));
                    Assert.That(TryGetVisibleLinkPoint(snapshot, out var point), Is.True);
                    Assert.That(target.TryHitLink(point, out var hit), Is.True);
                    Assert.That(hit.Href, Is.EqualTo("vertical-link"));
                    Assert.That(target.TryHitLink(new Vector2(0.01f, 0.01f), out _), Is.False);
                }
            }
        }

        [UnityTest]
        public IEnumerator WritingModeTextAndResizeRebuildAndResetScroll()
        {
            var textBox = CreateTextBox(LinkedMixedText, out var producer, MarginAll(10f));
            yield return WaitForProducer(producer, true);
            Assert.That(producer.isVerticalLayout, Is.True);
            textBox.ScrollToPercentX(1f);
            yield return WaitForProducer(producer, true);
            Assert.That(producer.scrollX, Is.GreaterThan(0f));

            producer.paragraphStyle.writingMode = WritingMode.HorizontalTb;
            yield return WaitForProducer(producer, true);
            Assert.That(producer.isVerticalLayout, Is.False);
            Assert.That(producer.HasOutput, Is.True);

            producer.paragraphStyle.writingMode = WritingMode.VerticalRl;
            yield return WaitForProducer(producer, true);
            Assert.That(producer.isVerticalLayout, Is.True);
            Assert.That(producer.scrollX, Is.EqualTo(0f).Within(0.01f));

            var previousVersion = producer.OutputVersion;
            producer.content = "<a href=\"updated\">更新された縦書き漢字</a>";
            yield return WaitForProducer(producer, true);
            Assert.That(producer.OutputVersion, Is.Not.EqualTo(previousVersion));
            Assert.That(producer.TryGetContentSnapshot(out var updated), Is.True);
            Assert.That(updated.PlainText, Is.EqualTo("更新された縦書き漢字"));
            Assert.That(updated.TryGetLink(0, out var updatedLink), Is.True);
            Assert.That(updated.GetLinkLabel(updatedLink), Is.EqualTo("更新された縦書き漢字"));

            textBox.GetComponent<RectTransform>().sizeDelta = new Vector2(160f, 80f);
            yield return WaitForProducer(producer, true);
            Assert.That(producer.viewportWidth, Is.EqualTo(140f).Within(0.1f));
            Assert.That(producer.viewportHeight, Is.EqualTo(60f).Within(0.1f));
        }

        [UnityTest]
        public IEnumerator ConfigurationFallbackInvalidatesOldOutputAndPublishesCurrentContent()
        {
            var margin = MarginAll(10f);
            var valid = VerticalSettings(LinkedMixedText, margin, TextOverflow.Clip);
            var size = new Vector2Int(200, 100);
            var viewport = TextBoxRenderViewport.Fixed(size, default, Vector2.zero, Vector2.zero);
            using var target = new TextBoxRenderTarget();
            target.Rebuild(viewport, ColorSpace.Gamma, valid, true, null);
            yield return WaitForTarget(target, viewport, valid);
            Assert.That(target.HasOutput, Is.True);
            Assert.That(target.TryGetLinkGeometrySnapshot(out _, out _), Is.True);
            Assert.That(target.TryGetContentSnapshot(out var initialContent), Is.True);
            var initialVersion = target.OutputVersion;

            const string updatedText = "更新されたリンク";
            var invalid = VerticalSettings("<a href=\"updated\">" + updatedText + "</a>",
                margin,
                TextOverflow.Clip);
            invalid.ParagraphStyle.textAlign = TextAlign.Left;
            LogAssert.Expect(LogType.Exception, new Regex("textAlign=Left defaulted to Start"));
            Assert.DoesNotThrow(() =>
                target.Rebuild(viewport, ColorSpace.Gamma, invalid, true, null));
            Assert.That(target.HasOutput, Is.False);
            Assert.That(target.OutputVersion, Is.GreaterThan(initialVersion));
            Assert.That(target.LastLayoutError, Is.Null);
            if (target.TryGetContentSnapshot(out var pendingContent))
            {
                Assert.That(pendingContent.ContentVersion,
                    Is.Not.EqualTo(initialContent.ContentVersion));
                Assert.That(pendingContent.PlainText, Is.EqualTo(updatedText));
            }
            Assert.That(target.TryGetLinkGeometrySnapshot(out _, out _), Is.False);

            yield return WaitForTarget(target, viewport, invalid);
            Assert.That(target.HasOutput, Is.True);
            Assert.That(target.LastLayoutError, Is.Null);
            Assert.That(target.IsVertical, Is.True);
            Assert.That(target.TryGetContentSnapshot(out var content), Is.True);
            Assert.That(content.PlainText, Is.EqualTo(updatedText));
            Assert.That(target.TryGetLinkGeometrySnapshot(out _, out _), Is.True);

            target.Rebuild(viewport, ColorSpace.Gamma, valid, true, null);
            yield return WaitForTarget(target, viewport, valid);
            Assert.That(target.TryGetContentSnapshot(out var recovered), Is.True);
            Assert.That(recovered.PlainText, Is.EqualTo(LongMixedText));
        }

        [UnityTest]
        public IEnumerator InvalidLineHeightPublishesNaturalGeometryWithoutDroppingTextBox()
        {
            var textBox = CreateTextBox(LinkedMixedText, out var producer, MarginAll(10f));
            yield return WaitForProducer(producer, true);

            Assert.That(producer.HasOutput, Is.True);
            Assert.That(textBox.Texture, Is.Not.Null);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out _, out _), Is.True);
            var initialVersion = producer.OutputVersion;

            LogAssert.Expect(LogType.Exception, new Regex("lineHeight=NaN defaulted to 0"));
            producer.textStyle.lineHeight = float.NaN;
            Assert.DoesNotThrow(() => textBox.ScrollToPercentX(0.5f));

            Assert.That(producer.lastLayoutError, Is.Null);
            Assert.That(producer.HasOutput, Is.False);
            Assert.That(textBox.Texture, Is.Null,
                "A degraded rebuild must not leave the previous atlas region visible.");
            Assert.That(textBox.UvRect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
            if (producer.TryGetContentSnapshot(out var pendingContent))
            {
                Assert.That(pendingContent.PlainText, Is.EqualTo(LongMixedText));
            }
            Assert.That(producer.TryGetLinkGeometrySnapshot(out _, out _), Is.False);

            yield return WaitForProducer(producer, true);
            Assert.That(textBox.Texture, Is.Not.Null);
            Assert.That(producer.lastLayoutError, Is.Null);
            Assert.That(producer.OutputVersion, Is.GreaterThan(initialVersion));
            Assert.That(producer.isVerticalLayout, Is.True);
            Assert.That(producer.TryGetContentSnapshot(out var content), Is.True);
            Assert.That(content.PlainText, Is.EqualTo(LongMixedText));
            Assert.That(producer.TryGetLinkGeometrySnapshot(out _, out _), Is.True);
        }

        [UnityTest]
        public IEnumerator NonFiniteAndOverflowingLineHeightsPublishNaturalGeometry()
        {
            var textBox = CreateTextBox(LinkedMixedText, out var producer, MarginAll(10f));
            yield return WaitForProducer(producer, true);
            var naturalWidth = producer.contentWidth;
            Assert.That(naturalWidth, Is.GreaterThan(0f));

            foreach (var lineHeight in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                LogAssert.Expect(LogType.Exception,
                    new Regex("lineHeight=.* defaulted to 0"));
                producer.textStyle.lineHeight = lineHeight;
                yield return WaitForProducer(producer, true);

                Assert.That(producer.HasOutput, Is.True);
                Assert.That(producer.lastLayoutError, Is.Null);
                Assert.That(producer.isVerticalLayout, Is.True);
                Assert.That(producer.contentWidth, Is.EqualTo(naturalWidth).Within(0.1f));
            }

            LogAssert.Expect(LogType.Exception,
                new Regex("lineHeight=.* could not produce a valid vertical layout"));
            producer.textStyle.lineHeight = float.MaxValue;
            yield return WaitForProducer(producer, true);

            Assert.That(producer.HasOutput, Is.True);
            Assert.That(producer.lastLayoutError, Is.Null);
            Assert.That(producer.isVerticalLayout, Is.True);
            Assert.That(textBox.Texture, Is.Not.Null);
            Assert.That(producer.contentWidth, Is.EqualTo(naturalWidth).Within(0.1f));

            producer.textStyle.lineHeight = 0.5f;
            yield return WaitForProducer(producer, true);
            Assert.That(producer.HasOutput, Is.True);
            Assert.That(producer.isVerticalLayout, Is.True);
            Assert.That(producer.contentWidth, Is.GreaterThan(0f));
            Assert.That(producer.contentWidth, Is.LessThan(naturalWidth));
        }

        [UnityTest]
        public IEnumerator UnrecoverableDefaultLayoutClearsInteractionAndRecovers()
        {
            var textBox = CreateTextBox(LinkedMixedText, out var producer, MarginAll(10f));
            yield return WaitForProducer(producer, true);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out _, out _), Is.True);

            LogAssert.Expect(LogType.Exception,
                new Regex("lineHeight=.* could not produce a valid vertical layout"));
            producer.textStyle.size = float.MaxValue;
            producer.textStyle.lineHeight = float.MaxValue;
            Assert.DoesNotThrow(() => producer.RebuildOutput(false));

            Assert.That(producer.HasOutput, Is.False);
            Assert.That(producer.lastLayoutError, Is.Not.Null);
            Assert.That(textBox.Texture, Is.Null);
            Assert.That(textBox.UvRect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
            Assert.That(producer.TryGetLinkGeometrySnapshot(out _, out _), Is.False);

            for (var frame = 0; frame < 3; ++frame)
            {
                producer.RebuildOutput(false);
            }
            Assert.That(producer.HasOutput, Is.False,
                "The same failed effective configuration must not be retried every frame.");

            producer.textStyle.size = 30f;
            producer.textStyle.lineHeight = 0f;
            yield return WaitForProducer(producer, true);
            Assert.That(producer.HasOutput, Is.True);
            Assert.That(producer.lastLayoutError, Is.Null);
            Assert.That(producer.isVerticalLayout, Is.True);
            Assert.That(textBox.Texture, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator HoverClearsOnLayoutFailureAndReturnsAfterRecovery()
        {
            var textBox = CreateTextBox(LinkedMixedText, out var producer, MarginAll(10f));
            yield return WaitForProducer(producer, true);

            Assert.That(producer.TryGetContentSnapshot(out var content), Is.True);
            var linkRects = new List<Rect>();
            Assert.That(textBox.TryGetLinkLocalRects(content.ContentVersion, 0, linkRects), Is.True,
                "The linked vertical text must publish a visible link rectangle.");
            var screenPoint = RectTransformUtility.WorldToScreenPoint(null,
                textBox.rectTransform.TransformPoint(linkRects[0].center));
            var pointer = new PointerEventData(null)
            {
                pointerId = -1,
                button = PointerEventData.InputButton.Left,
                eligibleForClick = true,
                position = screenPoint,
            };
            pointer.pointerCurrentRaycast = new RaycastResult { gameObject = textBox.gameObject };

            var hoverEvents = new List<bool>();
            textBox.onLinkHoverChanged.AddListener(value => hoverEvents.Add(value.Hovered));
            textBox.OnPointerEnter(pointer);
            textBox.OnPointerDown(pointer);

            var indicator = textBox.GetComponentInChildren<TextBoxLinkHoverIndicator>(true);
            Assert.That(indicator, Is.Not.Null);
            Assert.That(indicator!.enabled, Is.True);
            Assert.That(AppliedLinkHoverCursor(textBox), Is.Not.Null);
            Assert.That(hoverEvents, Is.EqualTo(new[] { true }));
            Assert.That(LinkPressCount(textBox), Is.EqualTo(1));

            LogAssert.Expect(LogType.Exception, new Regex("lineHeight=NaN defaulted to 0"));
            producer.textStyle.lineHeight = float.NaN;
            Assert.That(LinkPressCount(textBox), Is.EqualTo(0),
                "A layout-affecting style change must discard presses against the old geometry.");
            Assert.DoesNotThrow(() => producer.RebuildOutput(forceText: false));
            Assert.That(producer.lastLayoutError, Is.Null);
            Assert.That(producer.HasOutput, Is.False);
            Assert.That(textBox.Texture, Is.Null);
            Assert.That(indicator.enabled, Is.False,
                "A failed rebuild must hide the link hover highlight.");
            Assert.That(AppliedLinkHoverCursor(textBox), Is.Null,
                "A failed rebuild must restore the default cursor.");
            Assert.That(hoverEvents, Is.EqualTo(new[] { true, false }));
            Assert.That(HoverPointer(textBox), Is.Not.Null,
                "The pointer must be retained so recovery can restore hover without moving it.");

            yield return WaitForProducer(producer, true);
            for (var frame = 0; frame < 10 && !indicator.enabled; ++frame)
            {
                yield return null;
            }

            Assert.That(producer.HasOutput, Is.True);
            Assert.That(indicator.enabled, Is.True,
                "Hover must return after a successful rebuild while the pointer stays over the link.");
            Assert.That(AppliedLinkHoverCursor(textBox), Is.Not.Null);
            Assert.That(hoverEvents, Is.EqualTo(new[] { true, false, true }));
        }

        [UnityTest]
        public IEnumerator LinkHoverIndicatorUsesActualLayoutDirection()
        {
            var vertical = CreateTextBox("<a href=\"vertical-direction\">漢字</a>",
                out var verticalProducer, MarginAll(0f));
            yield return WaitForProducer(verticalProducer, true);
            Assert.That(verticalProducer.isVerticalLayout, Is.True);
            var verticalIndicator = HoverFirstLink(vertical);
            Assert.That(verticalIndicator.enabled, Is.True);
            Assert.That(LinkHoverIndicatorVertical(verticalIndicator), Is.True,
                "Actual vertical layout must place the hover underline on the physical left.");

            const string fallback = "<p><a href=\"horizontal-direction\">First مرحبا</a></p><p><ruby>漢<rt>かん</rt></ruby>Second</p>";
            LogAssert.Expect(LogType.Exception, new Regex("strong RTL content requires horizontal fallback"));
            var horizontal = CreateTextBox(fallback, out var horizontalProducer, MarginAll(0f));
            yield return WaitForProducer(horizontalProducer, true);
            Assert.That(horizontalProducer.isVerticalLayout, Is.False,
                "Structural fallback must expose real horizontal layout.");
            var horizontalIndicator = HoverFirstLink(horizontal);
            Assert.That(horizontalIndicator.enabled, Is.True);
            Assert.That(LinkHoverIndicatorVertical(horizontalIndicator), Is.False,
                "Horizontal fallback must retain the horizontal hover underline.");
        }

        [UnityTest]
        public IEnumerator RuntimeAutoMarginToggleInvalidatesOriginLinksAndPressedHover()
        {
            const string content = "<a href=\"runtime-auto\">漢字ABC</a>";
            var textBox = CreateTextBox(content, out var producer, MarginAll(0f));
            yield return WaitForProducer(producer, true);

            Assert.That(producer.TryGetLinkGeometrySnapshot(out var initial, out _), Is.True);
            Assert.That(TryGetVisibleLinkPoint(initial, out var initialPoint), Is.True);
            Assert.That(textBox.TryGetContentSnapshot(out var snapshot), Is.True);
            var localRects = new List<Rect>();
            Assert.That(textBox.TryGetLinkLocalRects(snapshot.ContentVersion, 0, localRects), Is.True);
            var pointer = new PointerEventData(null)
            {
                pointerId = -1,
                button = PointerEventData.InputButton.Left,
                eligibleForClick = true,
                position = RectTransformUtility.WorldToScreenPoint(null,
                    textBox.rectTransform.TransformPoint(localRects[0].center)),
            };
            pointer.pointerCurrentRaycast = new RaycastResult { gameObject = textBox.gameObject };
            textBox.OnPointerEnter(pointer);
            textBox.OnPointerDown(pointer);
            var indicator = textBox.GetComponentInChildren<TextBoxLinkHoverIndicator>(true);
            Assert.That(indicator, Is.Not.Null);
            Assert.That(indicator!.enabled, Is.True);
            Assert.That(LinkPressCount(textBox), Is.EqualTo(1));
            var initialVersion = producer.OutputVersion;
            var initialPaintX = initial.PaintPosition.x;

            producer.margin.Left.Auto = true;
            producer.margin.Right.Auto = true;
            yield return WaitForProducer(producer, true);
            for (var frame = 0; frame < 10 && indicator.enabled; ++frame)
            {
                yield return null;
            }

            Assert.That(producer.OutputVersion, Is.Not.EqualTo(initialVersion));
            Assert.That(producer.lastLayoutError, Is.Null);
            Assert.That(producer.isVerticalLayout, Is.True);
            Assert.That(LinkPressCount(textBox), Is.EqualTo(0),
                "Changing only Auto flags must discard presses against the old origin.");
            Assert.That(HoverPointer(textBox), Is.Not.Null,
                "The stationary pointer must be retained after auto placement changes.");
            Assert.That(indicator.enabled, Is.False,
                "The hover highlight must be re-evaluated after the paragraph moves.");
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var moved, out _), Is.True);
            Assert.That(moved.PaintPosition.x, Is.Not.EqualTo(initialPaintX).Within(0.5f));
            Assert.That(producer.TryHitLink(initialPoint, out _), Is.False,
                "The old normalized point must not retain a stale link hit.");
            Assert.That(TryGetVisibleLinkPoint(moved, out var movedPoint), Is.True);
            Assert.That(producer.TryHitLink(movedPoint, out var movedHit), Is.True);
            Assert.That(movedHit.Href, Is.EqualTo("runtime-auto"));
            Assert.That(textBox.Texture, Is.Not.Null);

            const float restoredLeft = 12f;
            const float restoredTop = 0.25f;
            const float restoredRight = 0.2f;
            const float restoredBottom = 5f;
            var autoValueVersion = producer.OutputVersion;
            producer.margin.Left = new MarginValue
            {
                Auto = true,
                Value = restoredLeft,
                Unit = MarginUnit.Px,
            };
            producer.margin.Top = new MarginValue
            {
                Auto = true,
                Value = restoredTop,
                Unit = MarginUnit.Em,
            };
            producer.margin.Right = new MarginValue
            {
                Auto = true,
                Value = restoredRight,
                Unit = MarginUnit.Em,
            };
            producer.margin.Bottom = new MarginValue
            {
                Auto = true,
                Value = restoredBottom,
                Unit = MarginUnit.Px,
            };
            yield return WaitForProducer(producer, true);

            Assert.That(producer.OutputVersion, Is.Not.EqualTo(autoValueVersion));
            Assert.That(producer.margin.Left.Auto, Is.True);
            Assert.That(producer.margin.Top.Auto, Is.True);
            Assert.That(producer.margin.Right.Auto, Is.True);
            Assert.That(producer.margin.Bottom.Auto, Is.True);
            Assert.That(producer.margin.Left.Value, Is.EqualTo(restoredLeft));
            Assert.That(producer.margin.Top.Value, Is.EqualTo(restoredTop));
            Assert.That(producer.margin.Right.Value, Is.EqualTo(restoredRight));
            Assert.That(producer.margin.Bottom.Value, Is.EqualTo(restoredBottom));
            Assert.That(producer.margin.Left.Unit, Is.EqualTo(MarginUnit.Px));
            Assert.That(producer.margin.Top.Unit, Is.EqualTo(MarginUnit.Em));
            Assert.That(producer.margin.Right.Unit, Is.EqualTo(MarginUnit.Em));
            Assert.That(producer.margin.Bottom.Unit, Is.EqualTo(MarginUnit.Px));
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var autoValues, out _), Is.True);
            Assert.That(autoValues.PaintPosition.x,
                Is.EqualTo((producer.viewportWidth - producer.contentWidth) * 0.5f +
                           producer.scrollX).Within(0.25f),
                "Stored non-zero values must not affect Auto placement.");

            var autoViewportWidth = producer.viewportWidth;
            var fixedValueVersion = producer.OutputVersion;
            producer.margin.Left.Auto = false;
            producer.margin.Top.Auto = false;
            producer.margin.Right.Auto = false;
            producer.margin.Bottom.Auto = false;
            yield return WaitForProducer(producer, true);

            Assert.That(producer.OutputVersion, Is.Not.EqualTo(fixedValueVersion));
            Assert.That(producer.margin.Left.Value, Is.EqualTo(restoredLeft));
            Assert.That(producer.margin.Top.Value, Is.EqualTo(restoredTop));
            Assert.That(producer.margin.Right.Value, Is.EqualTo(restoredRight));
            Assert.That(producer.margin.Bottom.Value, Is.EqualTo(restoredBottom));
            Assert.That(producer.margin.Left.Unit, Is.EqualTo(MarginUnit.Px));
            Assert.That(producer.margin.Top.Unit, Is.EqualTo(MarginUnit.Em));
            Assert.That(producer.margin.Right.Unit, Is.EqualTo(MarginUnit.Em));
            Assert.That(producer.margin.Bottom.Unit, Is.EqualTo(MarginUnit.Px));
            var resolvedTop = restoredTop * producer.textStyle.size;
            var resolvedRight = restoredRight * producer.textStyle.size;
            Assert.That(producer.viewportWidth,
                Is.EqualTo(autoViewportWidth - restoredLeft - resolvedRight).Within(0.25f),
                "Disabling Auto must restore the stored px/em horizontal contributions.");
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var restored, out _), Is.True);
            Assert.That(restored.PaintPosition.x,
                Is.EqualTo(restoredLeft + producer.viewportWidth - producer.contentWidth +
                           producer.scrollX).Within(0.25f),
                "Disabling Auto must restore the stored left/right contributions.");
            Assert.That(restored.PaintPosition.y,
                Is.EqualTo(resolvedTop - producer.scrollY -
                           producer.visualScrollOffset.y).Within(0.25f),
                "Disabling Auto must restore the stored top/bottom contributions.");
            Assert.That(TryGetVisibleLinkPoint(restored, out var restoredPoint), Is.True);
            Assert.That(producer.TryHitLink(restoredPoint, out var restoredHit), Is.True);
            Assert.That(restoredHit.Href, Is.EqualTo("runtime-auto"));
            Assert.That(textBox.Texture, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator SharedAutoMarginComponentsRemainIsolatedAcrossUpdatesAndRecovery()
        {
            var shared = new Margin
            {
                Left = new MarginValue { Auto = true, Value = 11f, Unit = MarginUnit.Em },
                Top = new MarginValue { Auto = true, Value = 13f, Unit = MarginUnit.Vh },
                Right = new MarginValue { Auto = true, Value = 12f, Unit = MarginUnit.Vw },
                Bottom = new MarginValue { Auto = true, Value = 14f, Unit = MarginUnit.ContainerPercent },
            };
            var first = CreateTextBox(LinkedMixedText, out var firstProducer, shared);
            var second = CreateTextBox("<a href=\"shared-second\">二番目ABC</a>",
                out var secondProducer, shared);
            yield return WaitForProducer(firstProducer, true);
            yield return WaitForProducer(secondProducer, true);

            Assert.That(secondProducer.TryGetLinkGeometrySnapshot(out var secondInitial, out _), Is.True);
            Assert.That(TryGetVisibleLinkPoint(secondInitial, out var secondPoint), Is.True);
            Assert.That(secondProducer.TryHitLink(secondPoint, out var secondHit), Is.True);
            Assert.That(secondHit.Href, Is.EqualTo("shared-second"));
            var secondVersion = secondProducer.OutputVersion;
            var secondPaintPosition = secondInitial.PaintPosition;

            first.rectTransform.sizeDelta = new Vector2(160f, 120f);
            yield return WaitForProducer(firstProducer, true);
            Assert.That(secondProducer.OutputVersion, Is.EqualTo(secondVersion));

            firstProducer.paragraphStyle.writingMode = WritingMode.HorizontalTb;
            yield return WaitForProducer(firstProducer, true);
            firstProducer.paragraphStyle.writingMode = WritingMode.VerticalRl;
            yield return WaitForProducer(firstProducer, true);
            Assert.That(secondProducer.OutputVersion, Is.EqualTo(secondVersion));

            LogAssert.Expect(LogType.Exception, new Regex("lineHeight=NaN defaulted to 0"));
            firstProducer.textStyle.lineHeight = float.NaN;
            yield return WaitForProducer(firstProducer, true);
            Assert.That(firstProducer.lastLayoutError, Is.Null);
            Assert.That(firstProducer.isVerticalLayout, Is.True);
            firstProducer.textStyle.lineHeight = 0f;
            yield return WaitForProducer(firstProducer, true);

            Assert.That(shared.Left.Auto, Is.True);
            Assert.That(shared.Left.Value, Is.EqualTo(11f));
            Assert.That(shared.Left.Unit, Is.EqualTo(MarginUnit.Em));
            Assert.That(shared.Top.Auto, Is.True);
            Assert.That(shared.Top.Value, Is.EqualTo(13f));
            Assert.That(shared.Top.Unit, Is.EqualTo(MarginUnit.Vh));
            Assert.That(shared.Right.Auto, Is.True);
            Assert.That(shared.Right.Value, Is.EqualTo(12f));
            Assert.That(shared.Right.Unit, Is.EqualTo(MarginUnit.Vw));
            Assert.That(shared.Bottom.Auto, Is.True);
            Assert.That(shared.Bottom.Value, Is.EqualTo(14f));
            Assert.That(shared.Bottom.Unit, Is.EqualTo(MarginUnit.ContainerPercent));
            Assert.That(secondProducer.OutputVersion, Is.EqualTo(secondVersion),
                "Updates to the other component must not republish unchanged output.");
            Assert.That(secondProducer.TryGetLinkGeometrySnapshot(out var secondAfter, out _), Is.True);
            Assert.That(secondAfter.PaintPosition, Is.EqualTo(secondPaintPosition));
            Assert.That(TryGetVisibleLinkPoint(secondAfter, out var secondPointAfter), Is.True);
            Assert.That(secondProducer.TryHitLink(secondPointAfter, out var secondHitAfter), Is.True);
            Assert.That(secondHitAfter.Href, Is.EqualTo("shared-second"));
            Assert.That(second.Texture, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator VerticalTrackpadMovesContentInTheSameDirectionAsHorizontalTextBox()
        {
            var textBox = CreateTextBox(LinkedMixedText, out var producer, MarginAll(10f));
            typeof(TextBox).GetField("m_smoothScroll", DeclaredInstance)!.SetValue(textBox, false);
            yield return WaitForProducer(producer, true);

            var reference = CreateTextBox(LinkedMixedText, out var referenceProducer, MarginAll(10f));
            referenceProducer.paragraphStyle.writingMode = WritingMode.HorizontalTb;
            referenceProducer.wrapMode = TextBoxWrapMode.NoWrap;
            typeof(TextBox).GetField("m_smoothScroll", DeclaredInstance)!.SetValue(reference, false);
            yield return WaitForProducer(referenceProducer, true);
            Assert.That(referenceProducer.maxScrollX, Is.GreaterThan(0f));

            foreach (var delta in new[] { 0.25f, -0.25f })
            {
                textBox.ScrollToPercentX(0.5f);
                yield return WaitForProducer(producer, true);
                Assert.That(producer.TryGetLinkGeometrySnapshot(out var before, out _), Is.True);
                reference.ScrollToPercentX(0.5f);
                yield return WaitForProducer(referenceProducer, true);
                Assert.That(referenceProducer.TryGetLinkGeometrySnapshot(out var referenceBefore, out _), Is.True);
                reference.OnScroll(new PointerEventData(null) { scrollDelta = new Vector2(delta, 0f) });
                textBox.OnScroll(new PointerEventData(null) { scrollDelta = new Vector2(delta, 0f) });
                yield return WaitForProducer(producer, true);
                yield return WaitForProducer(referenceProducer, true);
                Assert.That(producer.TryGetLinkGeometrySnapshot(out var after, out _), Is.True);
                Assert.That(referenceProducer.TryGetLinkGeometrySnapshot(out var referenceAfter, out _), Is.True);

                var referenceBeforeX = referenceBefore.Geometry[0].Rects[0].xMin * referenceBefore.FitScale +
                    referenceBefore.PaintPosition.x;
                var referenceAfterX = referenceAfter.Geometry[0].Rects[0].xMin * referenceAfter.FitScale +
                    referenceAfter.PaintPosition.x;
                var expectedDirection = Mathf.Sign(referenceAfterX - referenceBeforeX);
                Assert.That(expectedDirection, Is.EqualTo(-Mathf.Sign(delta)));
                Assert.That(Mathf.Sign(after.PaintPosition.x - before.PaintPosition.x),
                    Is.EqualTo(expectedDirection),
                    "Vertical RL must preserve the physical direction of a two-finger horizontal gesture.");
            }
        }

        [UnityTest]
        public IEnumerator VerticalHorizontalWheelMovesContentLeftWhenScrollIsAvailable()
        {
            var textBox = CreateTextBox(LongMixedText, out var producer, MarginAll(10f));
            yield return WaitForProducer(producer, false);
            Assert.That(producer.maxScrollX, Is.GreaterThan(0f));

            textBox.ScrollToPercentX(0.5f);
            yield return WaitForProducer(producer, false);
            var initialScrollX = producer.scrollX;
            Assert.That(initialScrollX, Is.GreaterThan(0f));

            var eventData = new PointerEventData(null)
            {
                scrollDelta = new Vector2(2f, 0f),
            };
            textBox.OnScroll(eventData);

            for (var frame = 0; frame < 180 && producer.scrollX >= initialScrollX; ++frame)
            {
                yield return null;
            }

            Assert.That(eventData.used, Is.True);
            Assert.That(producer.scrollX, Is.LessThan(initialScrollX),
                "A positive horizontal wheel input must move vertical content left (decrease scrollX).");
            Assert.That(producer.scrollX, Is.GreaterThanOrEqualTo(0f));
        }

        [UnityTest]
        public IEnumerator VerticalWheelAtRightEdgeForwardsThePhysicalDeltaToParent()
        {
            var parent = new GameObject("Scroll parent", typeof(RectTransform));
            parent.transform.SetParent(root.transform, false);
            var capture = parent.AddComponent<ScrollCaptureBehaviour>();
            var textBox = CreateTextBox(LinkedMixedText, out var producer, MarginAll(10f), parent.transform);
            yield return WaitForProducer(producer, true);
            Assert.That(producer.scrollX, Is.EqualTo(0f).Within(0.01f));

            var eventData = new PointerEventData(null)
            {
                scrollDelta = new Vector2(2f, 0f),
            };
            textBox.OnScroll(eventData);

            Assert.That(producer.scrollX, Is.EqualTo(0f).Within(0.01f));
            Assert.That(capture.Calls, Is.EqualTo(1));
            Assert.That(capture.LastDelta.x, Is.EqualTo(2f).Within(0.001f),
                "Parent forwarding must keep the physical wheel delta.");
        }

        [Test]
        public void WorldSpaceTextBoxStillRejectsVerticalConfigurationAtItsBoundary()
        {
            var gameObject = new GameObject("World space vertical rejection", typeof(RectTransform));
            try
            {
                gameObject.transform.SetParent(root.transform, false);
                var producer = gameObject.AddComponent<TextBoxRenderTextureProducer>();
                producer.paragraphStyle.writingMode = WritingMode.VerticalRl;
                producer.content = MixedText;
                gameObject.AddComponent<WorldSpaceTextBox>();

                LogAssert.Expect(LogType.Exception, new Regex("WorldSpaceTextBox does not support vertical-rl"));
                Assert.DoesNotThrow(() => producer.RebuildOutput(true));
                Assert.That(producer.lastLayoutError, Is.InstanceOf<NotSupportedException>());
                Assert.That(producer.HasOutput, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator ContentSizingLayoutElementPublishesColumnsAndRealOutput()
        {
            const string content = "<a href=\"layout-sizing\">" + LongMixedText + "</a>";
            var textBox = CreateTextBox(content, out var producer, MarginSides(6f, 8f, 4f, 12f));
            var rect = textBox.rectTransform;
            var layout = textBox.gameObject.AddComponent<TextBoxLayoutElement>();
            layout.widthMode = TextWidthMode.FitContent;
            layout.heightMode = TextHeightMode.Fixed;
            var fitter = textBox.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            LayoutRebuilder.MarkLayoutForRebuild(rect);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)root.transform);
            Canvas.ForceUpdateCanvases();
            yield return WaitForProducer(producer, true);

            var sizing = producer.ResolveSizing();
            Assert.That(layout.minWidth,
                Is.EqualTo(sizing.PreferredWidth).Within(0.01f));
            Assert.That(layout.preferredWidth,
                Is.EqualTo(sizing.PreferredWidth).Within(0.01f));
            Assert.That(layout.preferredHeight, Is.EqualTo(-1f));
            Assert.That(rect.rect.width,
                Is.EqualTo(sizing.PreferredWidth).Within(0.5f));
            Assert.That(rect.rect.height, Is.EqualTo(100f).Within(0.01f));
            Assert.That(producer.isVerticalLayout, Is.True);
            Assert.That(producer.contentWidth,
                Is.EqualTo(sizing.ContentWidth).Within(0.01f));
            Assert.That(producer.contentHeight,
                Is.EqualTo(sizing.ContentHeight).Within(0.01f));
            Assert.That(producer.viewportWidth,
                Is.EqualTo(rect.rect.width - 10f).Within(0.5f));
            Assert.That(producer.viewportHeight, Is.EqualTo(80f).Within(0.01f));
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var link, out _), Is.True);
            Assert.That(TryGetVisibleLinkPoint(link, out var point), Is.True);
            Assert.That(producer.TryHitLink(point, out var hit), Is.True);
            Assert.That(hit.Href, Is.EqualTo("layout-sizing"));
            Assert.That(textBox.Texture, Is.Not.Null);

            using var target = new TextBoxRenderTarget();
            var targetSettings = producer.CurrentSettings();
            var targetViewport = producer.CurrentViewport(new Vector2Int(
                Mathf.CeilToInt(rect.rect.width),
                Mathf.CeilToInt(rect.rect.height)));
            target.Rebuild(targetViewport, ColorSpace.Gamma, targetSettings, true, null);
            yield return WaitForTarget(target, targetViewport, targetSettings);
            Assert.That(target.TryGetLinkGeometrySnapshot(out var renderedLink, out _), Is.True);
            Assert.That(TryGetVisibleLinkPoint(renderedLink, out var renderedPoint), Is.True);
            Assert.That(target.TryHitLink(renderedPoint, out var renderedHit), Is.True);
            Assert.That(renderedHit.Href, Is.EqualTo("layout-sizing"));

            var readback = ReadOutput(target, targetViewport.VisibleOutputSizePixels);
            try
            {
                Assert.That(TryGetPaintedBounds(readback, out var painted), Is.True);
                Assert.That(painted.Overlaps(renderedLink.ClipRect), Is.True,
                    "Painted text must remain inside the shared content clip.");
            }
            finally
            {
                Object.Destroy(readback);
            }
        }

        [UnityTest]
        public IEnumerator LayoutElementAllocationWidthsDoNotReflowColumns()
        {
            var textBox = CreateTextBox(LinkedMixedText, out var producer,
                MarginSides(6f, 8f, 4f, 12f));
            var rect = textBox.rectTransform;
            var layout = textBox.gameObject.AddComponent<TextBoxLayoutElement>();
            layout.widthMode = TextWidthMode.MaxContent;
            layout.heightMode = TextHeightMode.Fixed;

            var narrowVersion = producer.OutputVersion;
            rect.sizeDelta = new Vector2(60f, 100f);
            Canvas.ForceUpdateCanvases();
            yield return WaitForProducerAfter(producer, narrowVersion, true);
            var narrow = producer.ResolveSizing();
            Assert.That(producer.contentWidth, Is.EqualTo(narrow.ContentWidth).Within(0.01f));
            Assert.That(producer.maxScrollX, Is.GreaterThan(0f));

            var exactVersion = producer.OutputVersion;
            rect.sizeDelta = new Vector2(narrow.PreferredWidth, 100f);
            Canvas.ForceUpdateCanvases();
            yield return WaitForProducerAfter(producer, exactVersion, true);
            var exact = producer.ResolveSizing();
            Assert.That(exact.ContentWidth, Is.EqualTo(narrow.ContentWidth).Within(0.01f));
            Assert.That(exact.ContentHeight, Is.EqualTo(narrow.ContentHeight).Within(0.01f));
            Assert.That(exact.EffectiveFontSize, Is.EqualTo(narrow.EffectiveFontSize));
            Assert.That(producer.maxScrollX, Is.EqualTo(0f).Within(0.1f));

            var wideVersion = producer.OutputVersion;
            var oversizedWidth = narrow.PreferredWidth + 100f;
            rect.sizeDelta = new Vector2(oversizedWidth, 100f);
            Canvas.ForceUpdateCanvases();
            yield return WaitForProducerAfter(producer, wideVersion, true);
            var wide = producer.ResolveSizing();
            Assert.That(wide.ContentWidth, Is.EqualTo(narrow.ContentWidth).Within(0.01f));
            Assert.That(wide.ContentHeight, Is.EqualTo(narrow.ContentHeight).Within(0.01f));
            Assert.That(wide.EffectiveFontSize, Is.EqualTo(narrow.EffectiveFontSize));
            Assert.That(producer.viewportWidth,
                Is.EqualTo(oversizedWidth - 10f).Within(0.01f));
            Assert.That(producer.maxScrollX, Is.EqualTo(0f).Within(0.1f));
            Assert.That(textBox.Texture, Is.Not.Null);

            var tallVersion = producer.OutputVersion;
            rect.sizeDelta = new Vector2(600f, 140f);
            Canvas.ForceUpdateCanvases();
            yield return WaitForProducerAfter(producer, tallVersion, true);
            var taller = producer.ResolveSizing();
            Assert.That(taller.ContentHeight, Is.EqualTo(120f).Within(0.01f));
            Assert.That(taller.ContentWidth, Is.LessThan(narrow.ContentWidth),
                "A larger fixed inline extent must reduce the complete column-box width.");
        }

        [UnityTest]
        public IEnumerator LayoutElementAutoHeightRendersThenFixedHeightSizesColumns()
        {
            var textBox = CreateTextBox(LinkedMixedText, out var producer, MarginAll(0f));
            var layout = textBox.gameObject.AddComponent<TextBoxLayoutElement>();
            layout.widthMode = TextWidthMode.MaxContent;
            layout.heightMode = TextHeightMode.Auto;
            LogAssert.Expect(LogType.Exception,
                new Regex("heightMode=Auto has no fixed vertical content height"));
            yield return WaitForProducer(producer, true);

            Assert.That(layout.preferredWidth, Is.EqualTo(-1f));
            Assert.That(layout.preferredHeight, Is.EqualTo(-1f));
            Assert.That(producer.isVerticalLayout, Is.True);
            Assert.That(textBox.Texture, Is.Not.Null);
            Assert.That(producer.CurrentViewport(new Vector2Int(200, 100)).SliceOutput, Is.False);

            layout.heightMode = TextHeightMode.Fixed;
            yield return WaitForProducer(producer, true);
            var sizing = producer.ResolveSizing();
            Assert.That(layout.preferredWidth,
                Is.EqualTo(sizing.PreferredWidth).Within(0.01f));
            Assert.That(layout.preferredHeight, Is.EqualTo(-1f));
            Assert.That(producer.contentHeight,
                Is.EqualTo(sizing.ContentHeight).Within(0.01f));
            Assert.That(producer.isVerticalLayout, Is.True);
            Assert.That(textBox.Texture, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator BaselineShiftPixelsLinksAndModeChangesUseCurrentGeometry()
        {
            var textBox = CreateTextBox(TextBoxVertical.TextBoxVerticalDegradePlayerProbe.BaselineShiftContent,
                out var producer, MarginAll(20f));
            textBox.rectTransform.sizeDelta = new Vector2(200f, 200f);
            yield return WaitForProducer(producer, true);
            Assert.That(TextBoxVertical.TextBoxVerticalDegradePlayerProbe.EvaluateBaselineShiftOutput(
                textBox, producer, true, out var verticalOffsets), Is.True,
                string.Join(",", verticalOffsets));
            var hover = HoverFirstLink(textBox);
            Assert.That(LinkHoverIndicatorVertical(hover), Is.True);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var snapshot, out _), Is.True);
            Assert.That(TryGetVisibleLinkPoint(snapshot, out var point), Is.True);
            var clicks = 0;
            textBox.onLinkClicked.AddListener(_ => ++clicks);
            var pointer = PointerAtNormalizedPoint(textBox, point);
            textBox.OnPointerDown(pointer);
            textBox.OnPointerClick(pointer);
            Assert.That(clicks, Is.EqualTo(1));

            var version = producer.OutputVersion;
            producer.paragraphStyle.writingMode = WritingMode.HorizontalTb;
            yield return WaitForProducerAfter(producer, version, true);
            Assert.That(TextBoxVertical.TextBoxVerticalDegradePlayerProbe.EvaluateBaselineShiftOutput(
                textBox, producer, false, out var horizontalOffsets), Is.True,
                string.Join(",", horizontalOffsets));

            version = producer.OutputVersion;
            producer.paragraphStyle.writingMode = WritingMode.VerticalRl;
            producer.content = "<a href=\"baseline-link\">天地</a>";
            yield return WaitForProducerAfter(producer, version, true);
            var plainWidth = producer.contentWidth;
            version = producer.OutputVersion;
            producer.content = TextBoxVertical.TextBoxVerticalDegradePlayerProbe.BaselineShiftContent;
            yield return WaitForProducerAfter(producer, version, true);
            Assert.That(TextBoxVertical.TextBoxVerticalDegradePlayerProbe.EvaluateBaselineShiftOutput(
                textBox, producer, true, out _), Is.True);
            Assert.That(producer.contentWidth, Is.GreaterThan(plainWidth));
            Assert.That(producer.maxScrollY, Is.EqualTo(0f).Within(0.1f));
        }

        [UnityTest]
        public IEnumerator BaselineShiftColumnGrowthDrivesSizingAlignmentAndBlockScrolling()
        {
            var markup = TextBoxVertical.TextBoxVerticalDegradePlayerProbe.BaselineShiftContent;
            var textBox = CreateTextBox(markup, out var producer, MarginAll(0f));
            textBox.rectTransform.sizeDelta = new Vector2(25f, 200f);
            var layout = textBox.gameObject.AddComponent<TextBoxLayoutElement>();
            layout.widthMode = TextWidthMode.MaxContent;
            layout.heightMode = TextHeightMode.Fixed;
            yield return WaitForProducer(producer, true);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var snapshot, out _), Is.True);
            var metrics = snapshot.Paragraphs[0].LayoutMetrics;
            Assert.That(metrics.Columns.Count, Is.EqualTo(1));
            Assert.That(metrics.PhysicalSize.x, Is.EqualTo(42f).Within(0.2f));
            Assert.That(metrics.ContentBounds.width, Is.EqualTo(metrics.PhysicalSize.x).Within(0.1f));
            Assert.That(producer.contentWidth, Is.EqualTo(metrics.PhysicalSize.x).Within(0.1f));
            Assert.That(layout.preferredWidth, Is.EqualTo(producer.contentWidth).Within(0.1f));
            Assert.That(producer.maxScrollX, Is.EqualTo(17f).Within(0.2f));
            Assert.That(producer.maxScrollY, Is.EqualTo(0f).Within(0.1f));
            Assert.That(textBox.EnsureLinkVisible(snapshot.ContentVersion, snapshot.Links[0].Occurrence), Is.True);
            textBox.ScrollToPercentX(1f);
            yield return WaitForProducer(producer, true);
            Assert.That(producer.scrollX, Is.EqualTo(producer.maxScrollX).Within(0.1f));
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var scrolled, out _), Is.True);
            Assert.That(TryGetVisibleLinkPoint(scrolled, out var point), Is.True);
            Assert.That(producer.TryHitLink(point, out var hit), Is.True);
            Assert.That(hit.Href, Is.EqualTo("baseline-link"));

            var version = producer.OutputVersion;
            textBox.rectTransform.sizeDelta = new Vector2(100f, 200f);
            producer.margin.Left.Auto = true;
            producer.margin.Right.Auto = true;
            yield return WaitForProducerAfter(producer, version, true);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var centered, out _), Is.True);
            Assert.That(centered.PaintPosition.x, Is.EqualTo((100f - 42f) * 0.5f).Within(0.2f));
            Assert.That(producer.maxScrollX, Is.EqualTo(0f).Within(0.1f));
        }

        [UnityTest]
        public IEnumerator BaselineShiftOverflowClearsOutputAndRecoversAtTheSameExtent()
        {
            var textBox = CreateTextBox(TextBoxVertical.TextBoxVerticalDegradePlayerProbe.BaselineShiftContent,
                out var producer, MarginAll(20f));
            textBox.rectTransform.sizeDelta = new Vector2(200f, 200f);
            yield return WaitForProducer(producer, true);
            var initialVersion = producer.OutputVersion;
            producer.content = "<span voffset-em=\"3e38\">天</span>";
            LogAssert.Expect(LogType.Exception, new Regex("Milestro native call failed with status -1"));
            producer.RebuildOutput(false);
            Assert.That(producer.lastLayoutError, Is.Not.Null);
            Assert.That(producer.HasOutput, Is.False);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out _, out _), Is.False);
            producer.content = TextBoxVertical.TextBoxVerticalDegradePlayerProbe.BaselineShiftContent;
            yield return WaitForProducerAfter(producer, initialVersion, true);
            Assert.That(TextBoxVertical.TextBoxVerticalDegradePlayerProbe.EvaluateBaselineShiftOutput(
                textBox, producer, true, out _), Is.True);
            var version = producer.OutputVersion;
            producer.textStyle.size = 40f;
            yield return WaitForProducerAfter(producer, version, true);
            Assert.That(producer.lastLayoutError, Is.Null);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var resized, out _), Is.True);
            Assert.That(resized.Paragraphs[0].LayoutMetrics.PhysicalSize.x, Is.EqualTo(56f).Within(0.2f));
        }

        [UnityTest]
        public IEnumerator ViewportBlockMarginsStayVerticalAcrossWritingModeChanges()
        {
            const string content = "<a href=\"scoped-margins\">" +
                                   "<p style=\"margin-top: 7px; margin-top: 2vw; margin-bottom: 12px; margin-bottom: 3vh\">天地</p>" +
                                   "<p style=\"margin-top: 1em; margin-top: 4vh; margin-bottom: 9px; margin-bottom: 5vw\">玄黃</p></a>";
            const string horizontalBaseline = "<a href=\"scoped-margins\">" +
                                              "<p style=\"margin-top: 7px; margin-bottom: 12px\">天地</p>" +
                                              "<p style=\"margin-top: 1em; margin-bottom: 9px\">玄黃</p></a>";
            CreateTextBox(content, out var producer, MarginAll(0f));
            producer.wrapMode = TextBoxWrapMode.NoWrap;
            yield return WaitForProducer(producer, true);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var vertical, out _), Is.True);
            var frame = vertical.Geometry[0].BlockRanges[0].FrameOffset;
            Assert.That(frame.y, Is.EqualTo(root.GetComponent<RectTransform>().rect.width * 0.02f).Within(0.01f));

            var version = producer.OutputVersion;
            producer.paragraphStyle.writingMode = WritingMode.HorizontalTb;
            yield return WaitForProducerAfter(producer, version, true);
            Assert.That(producer.isVerticalLayout, Is.False);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var horizontal, out _), Is.True);
            var width = producer.contentWidth;
            var height = producer.contentHeight;
            Assert.That(horizontal.Geometry[0].BlockRanges[0].FrameOffset.y, Is.EqualTo(7f));
            version = producer.OutputVersion;
            producer.content = horizontalBaseline;
            yield return WaitForProducerAfter(producer, version, true);
            Assert.That(producer.contentWidth, Is.EqualTo(width));
            Assert.That(producer.contentHeight, Is.EqualTo(height));
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var baseline, out _), Is.True);
            Assert.That(horizontal.Geometry[0].Rects, Is.EqualTo(baseline.Geometry[0].Rects));
            Assert.That(horizontal.Geometry[0].BlockRanges[1].FrameOffset,
                Is.EqualTo(baseline.Geometry[0].BlockRanges[1].FrameOffset));

            version = producer.OutputVersion;
            producer.content = content;
            producer.paragraphStyle.writingMode = WritingMode.VerticalRl;
            yield return WaitForProducerAfter(producer, version, true);
            Assert.That(producer.isVerticalLayout, Is.True);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var restored, out _), Is.True);
            Assert.That(restored.Geometry[0].BlockRanges[0].FrameOffset, Is.EqualTo(frame));
            Assert.That(restored.Geometry[0].BlockRanges[1].FrameOffset,
                Is.EqualTo(vertical.Geometry[0].BlockRanges[1].FrameOffset));
        }

        [UnityTest]
        public IEnumerator BlockMarginsMoveLinkFramesAndScrollWithoutCollapsing()
        {
            var textBox = CreateTextBox("<a href=\"margins\"><p style=\"margin-top: 12px; margin-bottom: 9px\">天地</p>" +
                "<p style=\"margin-top: 31px; margin-bottom: 17px\">玄黃</p></a>", out var producer, MarginAll(10f));
            producer.wrapMode = TextBoxWrapMode.NoWrap;
            producer.paragraphStyle.paragraphSpacing = 23f;
            yield return WaitForProducer(producer, true);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var geometry, out _), Is.True);
            var ranges = geometry.Geometry[0].BlockRanges;
            Assert.That(ranges[0].FrameOffset.y, Is.EqualTo(12f));
            Assert.That(ranges[1].FrameOffset, Is.EqualTo(new Vector2(0f, 31f)));
            Assert.That(ranges[0].FrameOffset.x, Is.EqualTo(geometry.Paragraphs[1].LayoutMetrics.PhysicalSize.x + 23f));
            var expectedHeight = Math.Max(12f + geometry.Paragraphs[0].LayoutMetrics.PhysicalSize.y + 9f,
                31f + geometry.Paragraphs[1].LayoutMetrics.PhysicalSize.y + 17f);
            Assert.That(producer.contentHeight, Is.EqualTo(expectedHeight));
            Assert.That(producer.maxScrollY, Is.EqualTo(Math.Max(0f, expectedHeight - 80f)));
            foreach (var range in ranges)
            {
                var nativeRects = geometry.Paragraphs[range.BlockIndex].GetRectsForRange(range.StartUtf16, range.EndUtf16);
                Assert.That(nativeRects, Is.Not.Empty);
                var translated = nativeRects[0];
                translated.position += range.FrameOffset;
                Assert.That(geometry.Geometry[0].Rects, Does.Contain(translated));
            }
            Assert.That(TryGetVisibleLinkPoint(geometry, out var point), Is.True);
            Assert.That(producer.TryHitLink(point, out var hit), Is.True);
            Assert.That(hit.Href, Is.EqualTo("margins"));
            string clicked = null;
            textBox.onLinkClicked.AddListener(value => clicked = value.Href);
            var pointer = PointerAtNormalizedPoint(textBox, point);
            textBox.OnPointerEnter(pointer);
            Assert.That(textBox.GetComponentInChildren<TextBoxLinkHoverIndicator>(true).enabled, Is.True);
            textBox.OnPointerDown(pointer);
            textBox.OnPointerClick(pointer);
            Assert.That(clicked, Is.EqualTo("margins"));
            var width = producer.contentWidth;
            var version = producer.OutputVersion;
            producer.content = "<p style=\"margin-top: 150px; margin-bottom: 27px\"><a href=\"later\">天地</a></p><p>玄黃</p>";
            yield return WaitForProducerAfter(producer, version, true);
            Assert.That(producer.contentWidth, Is.EqualTo(width));
            Assert.That(producer.TryGetContentSnapshot(out var content), Is.True);
            Assert.That(textBox.EnsureLinkVisible(content.ContentVersion, 0), Is.True);
            yield return WaitForProducer(producer, true);
            Assert.That(producer.scrollY, Is.GreaterThan(0f));
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var scrolled, out _), Is.True);
            Assert.That(scrolled.Geometry[0].BlockRanges[0].FrameOffset.y, Is.EqualTo(150f));
            Assert.That(TryGetVisibleLinkPoint(scrolled, out point), Is.True);
            Assert.That(producer.TryHitLink(point, out hit), Is.True);
            Assert.That(hit.Href, Is.EqualTo("later"));
        }

        [UnityTest]
        public IEnumerator BlockMarginUnitsInvalidateWithViewportFontAndExternalMargin()
        {
            CreateTextBox("<a href=\"units\"><p style=\"margin-top: 10%; margin-bottom: 1em\">天地</p>" +
                "<p style=\"margin-top: 2vw; margin-bottom: 3vh\">玄黃</p></a>", out var producer, MarginAll(10f));
            producer.wrapMode = TextBoxWrapMode.NoWrap;
            yield return WaitForProducer(producer, true);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var initial, out _), Is.True);
            Assert.That(initial.Geometry[0].BlockRanges[0].FrameOffset.y, Is.EqualTo(8f));
            Assert.That(initial.Geometry[0].BlockRanges[1].FrameOffset.y, Is.EqualTo(root.GetComponent<RectTransform>().rect.width * 0.02f).Within(0.01f));
            var version = producer.OutputVersion;
            producer.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 200f);
            producer.textStyle.size = 40f;
            producer.margin.Top.Value = 20f;
            yield return WaitForProducerAfter(producer, version, true);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var resized, out _), Is.True);
            Assert.That(resized.Geometry[0].BlockRanges[0].FrameOffset.y, Is.EqualTo(17f));
            version = producer.OutputVersion;
            producer.content = "<a href=\"changed\"><p style=\"margin-top: 2em\">天地</p><p style=\"margin-top: 7px\">玄黃</p></a>";
            yield return WaitForProducerAfter(producer, version, true);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var changed, out _), Is.True);
            Assert.That(changed.Geometry[0].BlockRanges[0].FrameOffset.y, Is.EqualTo(80f));
            Assert.That(changed.Geometry[0].BlockRanges[1].FrameOffset.y, Is.EqualTo(7f));
            version = producer.OutputVersion;
            producer.content = "<a href=\"viewport\"><p style=\"margin-top: 2vw\">天</p><p style=\"margin-top: 3vh\">地</p></a>";
            root.GetComponent<Canvas>().scaleFactor = 2f;
            Canvas.ForceUpdateCanvases();
            yield return WaitForProducerAfter(producer, version, true);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var viewportChanged, out _), Is.True);
            Assert.That(viewportChanged.Geometry[0].BlockRanges[0].FrameOffset.y,
                Is.EqualTo(root.GetComponent<RectTransform>().rect.width * 0.02f).Within(0.01f));
            Assert.That(viewportChanged.Geometry[0].BlockRanges[1].FrameOffset.y,
                Is.EqualTo(root.GetComponent<RectTransform>().rect.height * 0.03f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator BlockMarginsAutoCenterVisibleBoundsAndKeepClipAndEmptySpaceUnlinked()
        {
            var textBox = CreateTextBox("<a href=\"centered\"><p style=\"margin-top: 8px; margin-bottom: 5px\">天</p>" +
                "<p style=\"margin-top: 20px; margin-bottom: 30px\">地</p></a>", out var producer, MarginAll(0f));
            producer.wrapMode = TextBoxWrapMode.NoWrap;
            producer.margin.Top.Auto = true;
            producer.margin.Bottom.Auto = true;
            producer.margin.Left.Auto = true;
            producer.margin.Right.Auto = true;
            yield return WaitForProducer(producer, true);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var snapshot, out _), Is.True);
            var first = snapshot.Paragraphs[0].LayoutMetrics.ContentBounds;
            var second = snapshot.Paragraphs[1].LayoutMetrics.ContentBounds;
            var top = Math.Min(first.yMin + 8f, second.yMin + 20f);
            var bottom = Math.Max(first.yMax + 8f, second.yMax + 20f);
            Assert.That(snapshot.PaintPosition.y, Is.EqualTo((100f - (bottom - top)) * 0.5f - top).Within(0.01f));
            Assert.That(snapshot.PaintPosition.x, Is.EqualTo((200f - producer.contentWidth) * 0.5f).Within(0.01f));
            Assert.That(snapshot.ClipRect, Is.EqualTo(new Rect(0f, 0f, 200f, 100f)));
            var frame = snapshot.Geometry[0].BlockRanges[1].FrameOffset;
            var marginPoint = snapshot.PaintPosition + frame + new Vector2(second.center.x, -5f);
            Assert.That(producer.TryHitLink(new Vector2(marginPoint.x / 200f, marginPoint.y / 100f), out _), Is.False);
            var version = producer.OutputVersion;
            producer.textOverflow = TextOverflow.Overflow;
            yield return WaitForProducerAfter(producer, version, true);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var overflow, out _), Is.True);
            Assert.That(overflow.Geometry[0].BlockRanges[1].FrameOffset, Is.EqualTo(frame));
            Assert.That(textBox.Texture, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator BlockMarginOverflowClearsTextureAndLinksAndRecoversAllBlocks()
        {
            var textBox = CreateTextBox("<a href=\"before\"><p>天地</p><p style=\"margin-top: 5px\">玄黃</p></a>",
                out var producer, MarginAll(0f));
            yield return WaitForProducer(producer, true);
            LogAssert.Expect(LogType.Exception, new Regex("Vertical block layout is not finite"));
            producer.content = "<p>天地</p><p style=\"margin-top: 3e38em\"><a href=\"bad\">玄黃</a></p>";
            producer.RebuildOutput(false);
            Assert.That(producer.HasOutput, Is.False);
            Assert.That(textBox.Texture, Is.Null);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out _, out _), Is.False);
            var version = producer.OutputVersion;
            producer.content = "<a href=\"restored\"><p style=\"margin-top: 11px\">天</p><p style=\"margin-top: 22px\">地</p><p style=\"margin-top: 33px\">玄</p></a>";
            yield return WaitForProducerAfter(producer, version, true);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var recovered, out _), Is.True);
            Assert.That(recovered.Paragraphs.Count, Is.EqualTo(3));
            for (var index = 0; index < 3; ++index)
                Assert.That(recovered.Geometry[0].BlockRanges[index].FrameOffset.y, Is.EqualTo(11f * (index + 1)));
            Assert.That(recovered.Links[0].Href, Is.EqualTo("restored"));
            LogAssert.Expect(LogType.Exception, new Regex("Milestro native call failed with status -1"));
            producer.content = "<p style=\"margin-top: 4px\">天</p><p style=\"margin-top: 5px\"><span voffset-em=\"3e38\">地</span></p>";
            producer.RebuildOutput(false);
            Assert.That(producer.HasOutput, Is.False);
            Assert.That(textBox.Texture, Is.Null);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out _, out _), Is.False);
            version = producer.OutputVersion;
            producer.content = "<a href=\"native-restored\"><p style=\"margin-top: 3px\">天</p><p style=\"margin-top: 9px\">地</p></a>";
            yield return WaitForProducerAfter(producer, version, true);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out recovered, out _), Is.True);
            Assert.That(recovered.Paragraphs.Count, Is.EqualTo(2));
            Assert.That(recovered.Geometry[0].BlockRanges[0].FrameOffset.y, Is.EqualTo(3f));
            Assert.That(recovered.Geometry[0].BlockRanges[1].FrameOffset.y, Is.EqualTo(9f));
            Assert.That(recovered.Links[0].Href, Is.EqualTo("native-restored"));
        }

        [UnityTest]
        public IEnumerator MultiBlockCrossBlockLinkSharesFramesHoverClickAndScroll()
        {
            const string first = "天地\U0001F600玄黃宇宙洪荒日月盈昃";
            const string second = "辰宿列張寒來暑往秋收冬藏";
            var textBox = CreateTextBox("<a href=\"across\"><p>" + first + "</p><p>" + second + "</p></a>",
                out var producer, MarginAll(10f));
            producer.paragraphStyle.paragraphSpacing = 23f;
            yield return WaitForProducer(producer, true);
            Assert.That(producer.isVerticalLayout, Is.True);
            Assert.That(producer.TryGetContentSnapshot(out var content), Is.True);
            Assert.That(content.PlainText, Is.EqualTo(first + second));
            Assert.That(content.Links.Count, Is.EqualTo(1));
            Assert.That(content.Links[0].EndUtf16, Is.EqualTo(first.Length + second.Length));
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var initial, out _), Is.True);
            Assert.That(initial.Paragraphs.Count, Is.EqualTo(2));
            var ranges = initial.Geometry[0].BlockRanges;
            Assert.That(ranges.Count, Is.EqualTo(2));
            Assert.That(ranges[0].StartUtf16, Is.Zero);
            Assert.That(ranges[0].EndUtf16, Is.EqualTo(first.Length));
            Assert.That(ranges[1].StartUtf16, Is.Zero);
            Assert.That(ranges[1].EndUtf16, Is.EqualTo(second.Length));
            Assert.That(ranges[1].FrameOffset, Is.EqualTo(Vector2.zero));
            Assert.That(ranges[0].FrameOffset.x,
                Is.EqualTo(initial.Paragraphs[1].LayoutMetrics.PhysicalSize.x + 23f).Within(0.01f));
            Assert.That(producer.contentWidth,
                Is.EqualTo(initial.Paragraphs[0].LayoutMetrics.PhysicalSize.x + ranges[0].FrameOffset.x).Within(0.01f));
            Assert.That(producer.scrollX, Is.Zero);
            string clicked = null;
            textBox.onLinkClicked.AddListener(value => clicked = value.Href);
            foreach (var percent in new[] { 0f, 1f })
            {
                var version = producer.OutputVersion;
                textBox.ScrollToPercentX(percent);
                if (percent > 0f) yield return WaitForProducerAfter(producer, version, true);
                Assert.That(producer.TryGetLinkGeometrySnapshot(out var geometry, out _), Is.True);
                Assert.That(TryGetVisibleLinkPoint(geometry, out var point), Is.True);
                Assert.That(producer.TryHitLink(point, out var hit), Is.True);
                Assert.That(hit.Href, Is.EqualTo("across"));
                var pointer = PointerAtNormalizedPoint(textBox, point);
                textBox.OnPointerEnter(pointer);
                var hover = textBox.GetComponentInChildren<TextBoxLinkHoverIndicator>(true);
                Assert.That(hover, Is.Not.Null);
                Assert.That(hover.enabled, Is.True);
                Assert.That(LinkHoverIndicatorVertical(hover), Is.True);
                textBox.OnPointerDown(pointer);
                textBox.OnPointerClick(pointer);
                Assert.That(clicked, Is.EqualTo("across"));
                clicked = null;
            }
            Assert.That(textBox.Texture, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator MultiBlockEnsureLinkVisibleAndSpacingUpdateUseLaterBlock()
        {
            var textBox = CreateTextBox("<p>" + CjkWrapText + "</p><p><a href=\"later\">天地</a></p>",
                out var producer, MarginAll(0f));
            producer.paragraphStyle.paragraphSpacing = 31f;
            yield return WaitForProducer(producer, true);
            Assert.That(producer.TryGetContentSnapshot(out var content), Is.True);
            Assert.That(textBox.EnsureLinkVisible(content.ContentVersion, 0), Is.True);
            yield return WaitForProducer(producer, true);
            Assert.That(producer.scrollX, Is.GreaterThan(0f));
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var geometry, out _), Is.True);
            Assert.That(TryGetVisibleLinkPoint(geometry, out var point), Is.True);
            Assert.That(producer.TryHitLink(point, out var hit), Is.True);
            Assert.That(hit.Href, Is.EqualTo("later"));
            var width = producer.contentWidth;
            var version = producer.OutputVersion;
            producer.paragraphStyle.paragraphSpacing = 57f;
            yield return WaitForProducerAfter(producer, version, true);
            Assert.That(producer.contentWidth - width, Is.EqualTo(26f).Within(0.01f));
            version = producer.OutputVersion;
            producer.content = "<p><a href=\"new\">天</a></p>";
            yield return WaitForProducerAfter(producer, version, true);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var updated, out _), Is.True);
            Assert.That(updated.Paragraphs.Count, Is.EqualTo(1));
            Assert.That(updated.Geometry[0].BlockRanges[0].FrameOffset, Is.EqualTo(Vector2.zero));
            Assert.That(updated.Links[0].Href, Is.EqualTo("new"));
        }

        [UnityTest]
        public IEnumerator MultiBlockEllipsisIsPerParagraphAndEmptyBlockKeepsSpacing()
        {
            var textBox = CreateTextBox("<a href=\"ellipsis-blocks\"><p>天地玄黃宇宙</p><p></p><p>洪荒日月盈昃</p></a>",
                out var producer, MarginAll(0f));
            producer.paragraphStyle.singleLine = true;
            producer.paragraphStyle.paragraphSpacing = 13f;
            producer.textOverflow = TextOverflow.Ellipsis;
            yield return WaitForProducer(producer, true);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var geometry, out _), Is.True);
            Assert.That(geometry.Paragraphs.Count, Is.EqualTo(3));
            Assert.That(geometry.Geometry[0].BlockRanges.Count, Is.EqualTo(2));
            var width = 26f;
            foreach (var paragraph in geometry.Paragraphs)
            {
                var metrics = paragraph.LayoutMetrics;
                width += metrics.PhysicalSize.x;
                Assert.That(paragraph.GetRectsForRange(4, 6), Is.Empty);
            }
            Assert.That(geometry.Paragraphs[0].LayoutMetrics.Columns[0].Utf16End, Is.LessThan(6));
            Assert.That(geometry.Paragraphs[2].LayoutMetrics.Columns[0].Utf16End, Is.LessThan(6));
            Assert.That(producer.contentWidth, Is.EqualTo(width).Within(0.01f));
            Assert.That(producer.maxScrollY, Is.Zero);
            Assert.That(textBox.Texture, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator MultiBlockNoWrapScrollYAndAutoMarginsUseAggregateBounds()
        {
            var textBox = CreateTextBox("<p><a href=\"tall\">天地玄黃宇宙洪荒</a></p><p><a href=\"short\">日月</a></p>",
                out var producer, MarginAll(10f));
            producer.paragraphStyle.paragraphSpacing = 23f;
            producer.wrapMode = TextBoxWrapMode.NoWrap;
            yield return WaitForProducer(producer, true);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var initial, out _), Is.True);
            Assert.That(initial.Paragraphs[0].LayoutMetrics.Columns.Count, Is.EqualTo(1));
            Assert.That(initial.Paragraphs[1].LayoutMetrics.Columns.Count, Is.EqualTo(1));
            Assert.That(producer.contentHeight, Is.EqualTo(initial.Paragraphs[0].LayoutMetrics.PhysicalSize.y));
            Assert.That(producer.maxScrollY, Is.EqualTo(producer.contentHeight - 80f).Within(0.01f));
            var version = producer.OutputVersion;
            textBox.ScrollToPercentY(1f);
            yield return WaitForProducerAfter(producer, version, true);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var scrolled, out _), Is.True);
            Assert.That(scrolled.PaintPosition.y, Is.EqualTo(10f - producer.maxScrollY).Within(0.01f));
            version = producer.OutputVersion;
            producer.content = "<a href=\"both\"><p>天地</p><p>日月</p></a>";
            producer.margin.Left.Auto = true;
            producer.margin.Right.Auto = true;
            producer.margin.Top.Auto = true;
            producer.margin.Bottom.Auto = true;
            yield return WaitForProducerAfter(producer, version, true);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var centered, out _), Is.True);
            Assert.That(centered.PaintPosition.x, Is.EqualTo((200f - producer.contentWidth) * 0.5f).Within(0.01f));
            Assert.That(centered.PaintPosition.y, Is.EqualTo((100f - producer.contentHeight) * 0.5f).Within(0.01f));
            var gap = new Vector2(centered.PaintPosition.x +
                centered.Paragraphs[1].LayoutMetrics.PhysicalSize.x + 11.5f,
                centered.PaintPosition.y + 15f);
            Assert.That(producer.TryHitLink(new Vector2(gap.x / 200f, gap.y / 100f), out _), Is.False);
        }

        [UnityTest]
        public IEnumerator MultiBlockFailureClearsAllOutputAndRecoversCurrentFrames()
        {
            var textBox = CreateTextBox("<p>天地</p><p><a href=\"before\">玄黃</a></p>",
                out var producer, MarginAll(0f));
            producer.enableAutoFontSize = true;
            producer.fontSizeMin = 6f;
            producer.fontSizeMax = 60f;
            yield return WaitForProducer(producer, true);
            LogAssert.Expect(LogType.Exception, new Regex("Milestro native call failed with status -1"));
            producer.content = "<p>天地</p><p><a href=\"bad\"><span voffset-em=\"3e38\">玄</span></a></p>";
            producer.RebuildOutput(false);
            Assert.That(producer.HasOutput, Is.False);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out _, out _), Is.False);
            Assert.That(textBox.Texture, Is.Null);
            var version = producer.OutputVersion;
            producer.content = "<p>天</p><p>地</p><p><a href=\"recovered\">玄</a></p>";
            yield return WaitForProducerAfter(producer, version, true);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var recovered, out _), Is.True);
            Assert.That(recovered.Paragraphs.Count, Is.EqualTo(3));
            Assert.That(recovered.Links[0].Href, Is.EqualTo("recovered"));
            Assert.That(recovered.Geometry[0].BlockRanges[0].BlockIndex, Is.EqualTo(2));
            Assert.That(recovered.Geometry[0].BlockRanges[0].FrameOffset, Is.EqualTo(Vector2.zero));
            Assert.That(recovered.FitScale, Is.EqualTo(2f));
        }

        [UnityTest]
        public IEnumerator P09_P11_P14_PlaceholderSnapshotsPublishLinksAndInvalidateAcrossUpdates()
        {
            var textBox = CreateTextBox("<a href=\"object\"><p>天<img width=\"70\" height=\"20\" key=\"first\"/>地</p>" +
                "<p><img width=\"30\" height=\"150\" key=\"tall\"/></p></a>", out var producer, MarginAll(0));
            yield return WaitForProducer(producer, true);
            Assert.That(textBox.TryGetPlaceholderGeometrySnapshot(out var first), Is.True);
            Assert.That(first.Geometry, Has.Count.EqualTo(2));
            Assert.That(first.Geometry[0].Key, Is.EqualTo("first"));
            Assert.That(first.Geometry[0].StartUtf16, Is.EqualTo(1));
            Assert.That(first.Geometry[1].StartUtf16, Is.EqualTo(3));
            Assert.That(first.Geometry[0].Rect.size, Is.EqualTo(new Vector2(70, 20)));
            Assert.That(first.Geometry[1].Rect.size, Is.EqualTo(new Vector2(30, 150)));
            Assert.That(first.Geometry[0].Visible, Is.True);
            Assert.That(first.Geometry is IList<TextBoxPlaceholderGeometry> list && list.IsReadOnly, Is.True);
            var output = first.PaintPosition + first.Geometry[0].Rect.center * first.FitScale;
            Assert.That(producer.TryHitLink(new Vector2(output.x / 200, output.y / 100), out var hit), Is.True);
            Assert.That(hit.Href, Is.EqualTo("object"));
            var pointer = PointerAtNormalizedPoint(textBox, new Vector2(output.x / 200, output.y / 100));
            var clicked = "";
            textBox.onLinkClicked.AddListener(value => clicked = value.Href);
            textBox.OnPointerEnter(pointer);
            Assert.That(textBox.GetComponentInChildren<TextBoxLinkHoverIndicator>(true)!.enabled, Is.True);
            textBox.OnPointerDown(pointer);
            textBox.OnPointerClick(pointer);
            Assert.That(clicked, Is.EqualTo("object"));
            textBox.OnPointerExit(pointer);
            var version = producer.OutputVersion;
            Assert.That(producer.maxScrollY, Is.GreaterThan(0), "Tall placeholder must expose inline overflow for scrolling.");
            textBox.ScrollToPercentY(1);
            yield return WaitForProducerAfter(producer, version, true);
            Assert.That(textBox.TryGetPlaceholderGeometrySnapshot(out var scrolled), Is.True);
            Assert.That(scrolled.Generation, Is.Not.EqualTo(first.Generation));
            Assert.That(scrolled.PaintPosition.y, Is.LessThan(first.PaintPosition.y));
            Assert.That(first.Geometry[0].Rect.size, Is.EqualTo(new Vector2(70, 20)));
            version = producer.OutputVersion;
            producer.paragraphStyle.writingMode = WritingMode.HorizontalTb;
            yield return WaitForProducerAfter(producer, version, true);
            Assert.That(textBox.TryGetPlaceholderGeometrySnapshot(out var horizontal), Is.True);
            Assert.That(horizontal.Generation, Is.Not.EqualTo(scrolled.Generation));
            Assert.That(horizontal.Geometry[0].Rect.size, Is.EqualTo(new Vector2(70, 20)));
            version = producer.OutputVersion;
            producer.content = "<img width=\"19\" height=\"27\" key=\"replacement\"/>天";
            producer.paragraphStyle.writingMode = WritingMode.VerticalRl;
            yield return WaitForProducerAfter(producer, version, false);
            Assert.That(textBox.TryGetPlaceholderGeometrySnapshot(out var changed), Is.True);
            Assert.That(changed.ContentVersion, Is.Not.EqualTo(first.ContentVersion));
            Assert.That(changed.Geometry, Has.Count.EqualTo(1));
            Assert.That(changed.Geometry[0].Key, Is.EqualTo("replacement"));
            Assert.That(changed.Geometry[0].StartUtf16, Is.Zero);
            version = producer.OutputVersion;
            producer.content = "天";
            yield return WaitForProducerAfter(producer, version, false);
            Assert.That(textBox.TryGetPlaceholderGeometrySnapshot(out _), Is.False);
        }

        [UnityTest]
        public IEnumerator P06_P10_P14_PlaceholderVisibilityFollowsEllipsisAndAutoMargins()
        {
            var textBox = CreateTextBox("天<img width=\"90\" height=\"90\" key=\"hidden\"/>地",
                out var producer, MarginAll(0));
            producer.paragraphStyle.singleLine = true;
            producer.textOverflow = TextOverflow.Ellipsis;
            yield return WaitForProducer(producer, true);
            Assert.That(textBox.TryGetPlaceholderGeometrySnapshot(out var hidden), Is.True);
            Assert.That(hidden.Geometry, Has.Count.EqualTo(1));
            Assert.That(hidden.Geometry[0].Visible, Is.False);
            Assert.That(hidden.Geometry[0].Rect, Is.EqualTo(default(Rect)));
            var version = producer.OutputVersion;
            producer.content = "<img width=\"50\" height=\"20\" key=\"centered\"/>";
            producer.paragraphStyle.singleLine = false;
            producer.margin = new Margin
            {
                Left = new MarginValue { Auto = true },
                Right = new MarginValue { Auto = true },
                Top = new MarginValue { Auto = true },
                Bottom = new MarginValue { Auto = true }
            };
            yield return WaitForProducerAfter(producer, version, true);
            Assert.That(textBox.TryGetPlaceholderGeometrySnapshot(out var centered), Is.True);
            Assert.That(centered.Geometry[0].Visible, Is.True);
            Assert.That(centered.PaintPosition + centered.Geometry[0].Rect.center * centered.FitScale,
                Is.EqualTo(new Vector2(100, 50)));
        }

        [UnityTest]
        public IEnumerator AF09_AF12_ScaledBlocksPlaceholdersPixelsAndHoverShareGeometry()
        {
            var textBox = CreateTextBox("<a href=\"scaled\"><p style=\"margin-top: 10px; text-indent: 10px\">" +
                "<u>天</u><img width=\"70\" height=\"20\" key=\"scaled\"/>地</p>" +
                "<p><sup>玄</sup>黃宇宙洪荒日月盈昃</p></a>", out var producer, MarginAll(0f));
            producer.enableAutoFontSize = true;
            producer.fontSizeMin = 6f;
            producer.fontSizeMax = 24f;
            producer.paragraphStyle.paragraphSpacing = 20f;
            producer.textStyle.lineHeight = 1.5f;
            producer.paragraphStyle.textAlign = TextAlign.Justify;
            yield return WaitForProducer(producer, true);
            Assert.That(textBox.Texture, Is.Not.Null);
            Assert.That(textBox.TryGetPlaceholderGeometrySnapshot(out var placeholders), Is.True);
            Assert.That(placeholders.FitScale, Is.InRange(0.2f, 0.8f));
            Assert.That(placeholders.Geometry[0].Rect.size, Is.EqualTo(new Vector2(70f, 20f)));
            Assert.That(placeholders.Geometry[0].Visible, Is.True);
            var point = placeholders.PaintPosition + placeholders.Geometry[0].Rect.center * placeholders.FitScale;
            var normalized = new Vector2(point.x / 200f, point.y / 100f);
            Assert.That(producer.TryHitLink(normalized, out var hit), Is.True);
            Assert.That(hit.Href, Is.EqualTo("scaled"));
            textBox.OnPointerEnter(PointerAtNormalizedPoint(textBox, normalized));
            Assert.That(textBox.GetComponentInChildren<TextBoxLinkHoverIndicator>(true)!.enabled, Is.True);
            Assert.That(producer.maxScrollX, Is.EqualTo(0f).Within(0.01f));
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var links, out _), Is.True);
            Assert.That(links.FitScale, Is.EqualTo(placeholders.FitScale));
            var ranges = links.Geometry[0].BlockRanges;
            Assert.That(ranges.Count, Is.EqualTo(2));
            Assert.That(ranges[0].FrameOffset.y * links.FitScale,
                Is.EqualTo(10f * links.FitScale).Within(0.01f));
            Assert.That((ranges[0].FrameOffset.x - ranges[1].FrameOffset.x) * links.FitScale,
                Is.EqualTo((links.Paragraphs[1].LayoutMetrics.PhysicalSize.x + 20f) * links.FitScale)
                    .Within(0.01f));
            using var target = new TextBoxRenderTarget();
            var settings = producer.CurrentSettings();
            var viewport = producer.CurrentViewport(new Vector2Int(200, 100));
            target.Rebuild(viewport, ColorSpace.Gamma, settings, true, null);
            yield return WaitForTarget(target, viewport, settings);
            Assert.That(target.TryGetLinkGeometrySnapshot(out var rendered, out _), Is.True);
            Assert.That(rendered.FitScale, Is.EqualTo(links.FitScale));
            var pixels = ReadOutput(target, viewport.VisibleOutputSizePixels);
            try
            {
                Assert.That(TryGetPaintedBounds(pixels, out var painted), Is.True);
                Assert.That(painted.Overlaps(rendered.ClipRect), Is.True);
                Assert.That(painted.width, Is.GreaterThan(20f));
            }
            finally
            {
                Object.Destroy(pixels);
            }
        }

        [UnityTest]
        public IEnumerator AF02_AF06_AF13_AF15_UpdatesRecomputeScaleAndEllipsisOverridesSearch()
        {
            var textBox = CreateTextBox("<a href=\"scale\">天</a>", out var producer, MarginAll(0f));
            producer.enableAutoFontSize = true;
            producer.fontSizeMin = 6f;
            producer.fontSizeMax = 60f;
            yield return WaitForProducer(producer, true);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var first, out _), Is.True);
            Assert.That(first.FitScale, Is.EqualTo(2f));
            var version = producer.OutputVersion;
            producer.content = "<a href=\"scale\">" + CjkWrapText + "</a>";
            yield return WaitForProducerAfter(producer, version, true);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var longer, out _), Is.True);
            Assert.That(longer.FitScale, Is.LessThan(1f));
            version = producer.OutputVersion;
            producer.fontSizeMin = 15f;
            producer.fontSizeMax = 15f;
            producer.textStyle.size = 40f;
            producer.GetComponent<RectTransform>().sizeDelta = new Vector2(180f, 120f);
            yield return WaitForProducerAfter(producer, version, true);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var changed, out _), Is.True);
            Assert.That(changed.FitScale, Is.EqualTo(0.375f));
            version = producer.OutputVersion;
            producer.paragraphStyle.writingMode = WritingMode.HorizontalTb;
            yield return WaitForProducerAfter(producer, version, true);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var horizontal, out _), Is.True);
            Assert.That(horizontal.FitScale, Is.EqualTo(0.375f));
            version = producer.OutputVersion;
            producer.paragraphStyle.writingMode = WritingMode.VerticalRl;
            producer.paragraphStyle.singleLine = true;
            producer.textOverflow = TextOverflow.Ellipsis;
            LogAssert.Expect(LogType.Exception, new Regex("auto font sizing defaulted"));
            yield return WaitForProducerAfter(producer, version, true);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var ellipsis, out _), Is.True);
            Assert.That(ellipsis.FitScale, Is.EqualTo(1f));
            Assert.That(producer.lastLayoutError, Is.Null);
        }

        private TextBox CreateTextBox(string content,
            out TextBoxRenderTextureProducer producer,
            Margin margin,
            Transform? parent = null)
        {
            var child = new GameObject("Vertical TextBox", typeof(RectTransform));
            child.transform.SetParent(parent != null ? parent : root.transform, false);
            child.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 100f);
            producer = child.AddComponent<TextBoxRenderTextureProducer>();
            producer.enableRichText = true;
            producer.textOverflow = TextOverflow.Clip;
            producer.wrapMode = TextBoxWrapMode.Wrap;
            producer.margin = margin;
            producer.content = content;
            producer.paragraphStyle.writingMode = WritingMode.VerticalRl;
            producer.paragraphStyle.textAlign = TextAlign.Start;
            producer.textStyle.size = 30f;
            var textBox = child.AddComponent<TextBox>();
            root.SetActive(true);
            return textBox;
        }

        private static IEnumerator WaitForProducer(TextBoxRenderTextureProducer producer,
            bool withLink)
        {
            for (var frame = 0; frame < 180; ++frame)
            {
                RenderTextureAtlas.Flush();
                UnitySkiaRenderTextureSurface.CollectCompletedEventsFromPump();
                producer.RebuildOutput(false);
                if (producer.HasOutput &&
                    (!withLink || producer.TryGetLinkGeometrySnapshot(out _, out _)))
                {
                    yield break;
                }
                yield return null;
            }

            Assert.Fail("Vertical TextBox output did not complete within 180 frames.");
        }

        private static IEnumerator WaitForProducerAfter(TextBoxRenderTextureProducer producer,
            long previousVersion,
            bool withLink)
        {
            for (var frame = 0; frame < 180; ++frame)
            {
                RenderTextureAtlas.Flush();
                UnitySkiaRenderTextureSurface.CollectCompletedEventsFromPump();
                producer.RebuildOutput(false);
                if (producer.HasOutput &&
                    producer.OutputVersion != previousVersion &&
                    (!withLink || producer.TryGetLinkGeometrySnapshot(out _, out _)))
                {
                    yield break;
                }
                yield return null;
            }

            Assert.Fail("Vertical TextBox output did not advance within 180 frames.");
        }

        private static readonly BindingFlags DeclaredInstance =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static Texture2D? AppliedLinkHoverCursor(TextBox textBox)
        {
            return (Texture2D?)typeof(TextBox).GetField("appliedLinkHoverCursor", DeclaredInstance)!
                .GetValue(textBox);
        }

        private static PointerEventData? HoverPointer(TextBox textBox)
        {
            return (PointerEventData?)typeof(TextBox).GetField("linkHoverPointer", DeclaredInstance)!
                .GetValue(textBox);
        }

        private static TextBoxLinkHoverIndicator HoverFirstLink(TextBox textBox)
        {
            Assert.That(textBox.TryGetContentSnapshot(out var content), Is.True);
            var rects = new List<Rect>();
            Assert.That(textBox.TryGetLinkLocalRects(content.ContentVersion, 0, rects), Is.True);
            Assert.That(rects, Is.Not.Empty);
            var pointer = new PointerEventData(null)
            {
                pointerId = -1,
                button = PointerEventData.InputButton.Left,
                eligibleForClick = true,
                position = RectTransformUtility.WorldToScreenPoint(null,
                    textBox.rectTransform.TransformPoint(rects[0].center)),
            };
            pointer.pointerCurrentRaycast = new RaycastResult { gameObject = textBox.gameObject };
            textBox.OnPointerEnter(pointer);
            var indicator = textBox.GetComponentInChildren<TextBoxLinkHoverIndicator>(true);
            Assert.That(indicator, Is.Not.Null);
            return indicator!;
        }

        private static bool LinkHoverIndicatorVertical(TextBoxLinkHoverIndicator indicator)
        {
            return (bool)typeof(TextBoxLinkHoverIndicator)
                .GetField("vertical", DeclaredInstance)!
                .GetValue(indicator);
        }

        private static PointerEventData PointerAtNormalizedPoint(TextBox textBox,
            Vector2 normalizedPoint)
        {
            var rect = textBox.rectTransform.rect;
            var localPoint = new Vector2(rect.xMin + normalizedPoint.x * rect.width,
                rect.yMax - normalizedPoint.y * rect.height);
            return new PointerEventData(null)
            {
                pointerId = -1,
                button = PointerEventData.InputButton.Left,
                eligibleForClick = true,
                position = RectTransformUtility.WorldToScreenPoint(null,
                    textBox.rectTransform.TransformPoint(localPoint)),
                pointerCurrentRaycast = new RaycastResult { gameObject = textBox.gameObject },
            };
        }

        private static int LinkPressCount(TextBox textBox)
        {
            var presses = (System.Collections.ICollection?)typeof(TextBox)
                .GetField("linkPresses", DeclaredInstance)!
                .GetValue(textBox);
            return presses?.Count ?? 0;
        }

        private static IEnumerator WaitForTarget(TextBoxRenderTarget target,
            TextBoxRenderViewport viewport,
            TextBoxRenderTargetSettings settings)
        {
            var withLink = settings.Content.Contains("<a");
            for (var frame = 0; frame < 180; ++frame)
            {
                RenderTextureAtlas.Flush();
                UnitySkiaRenderTextureSurface.CollectCompletedEventsFromPump();
                target.Rebuild(viewport, ColorSpace.Gamma, settings, false, null);
                if (target.HasOutput &&
                    (!withLink || target.TryGetLinkGeometrySnapshot(out _, out _)))
                {
                    yield break;
                }
                yield return null;
            }

            Assert.Fail("Vertical render target output did not complete within 180 frames.");
        }

        private static IEnumerator WaitForTargetAfter(TextBoxRenderTarget target,
            TextBoxRenderViewport viewport,
            TextBoxRenderTargetSettings settings,
            long previousVersion)
        {
            var withLink = settings.Content.Contains("<a");
            for (var frame = 0; frame < 180; ++frame)
            {
                RenderTextureAtlas.Flush();
                UnitySkiaRenderTextureSurface.CollectCompletedEventsFromPump();
                target.Rebuild(viewport, ColorSpace.Gamma, settings, false, null);
                if (target.HasOutput &&
                    target.OutputVersion != previousVersion &&
                    (!withLink || target.TryGetLinkGeometrySnapshot(out _, out _)))
                {
                    yield break;
                }
                yield return null;
            }

            Assert.Fail("Vertical render target output did not advance within 180 frames.");
        }

        private static bool TryGetVisibleLinkPoint(TextBoxLinkGeometrySnapshot snapshot,
            out Vector2 normalizedPoint)
        {
            normalizedPoint = default;
            var scale = snapshot.FitScale > 0f && float.IsFinite(snapshot.FitScale)
                ? snapshot.FitScale
                : 1f;
            foreach (var geometry in snapshot.Geometry)
            {
                foreach (var rect in geometry.Rects)
                {
                    var outputRect = new Rect(rect.x * scale + snapshot.PaintPosition.x,
                        rect.y * scale + snapshot.PaintPosition.y,
                        rect.width * scale,
                        rect.height * scale);
                    var visibleRect = Rect.MinMaxRect(
                        Mathf.Max(outputRect.xMin, snapshot.ClipRect.xMin),
                        Mathf.Max(outputRect.yMin, snapshot.ClipRect.yMin),
                        Mathf.Min(outputRect.xMax, snapshot.ClipRect.xMax),
                        Mathf.Min(outputRect.yMax, snapshot.ClipRect.yMax));
                    if (visibleRect.width < 1f || visibleRect.height < 1f)
                    {
                        continue;
                    }

                    normalizedPoint = new Vector2(visibleRect.center.x / snapshot.VisibleOutputSize.x,
                        visibleRect.center.y / snapshot.VisibleOutputSize.y);
                    return true;
                }
            }

            return false;
        }

        private static TextBoxRenderTargetSettings VerticalSettings(string content,
            Margin margin,
            TextOverflow overflow,
            TextBoxWrapMode wrap = TextBoxWrapMode.Wrap,
            float fontSize = 36f)
        {
            return new TextBoxRenderTargetSettings(content,
                margin,
                new ParagraphStyleSettings
                {
                    writingMode = WritingMode.VerticalRl,
                    textAlign = TextAlign.Start,
                },
                TextStyle(fontSize),
                wrap,
                overflow);
        }

        private static Margin MarginAll(float value)
        {
            var margin = new Margin();
            margin.Left = new MarginValue { Value = value };
            margin.Top = new MarginValue { Value = value };
            margin.Right = new MarginValue { Value = value };
            margin.Bottom = new MarginValue { Value = value };
            return margin;
        }

        private static Margin MarginSides(float left,
            float top,
            float right,
            float bottom,
            bool leftAuto = false,
            bool topAuto = false,
            bool rightAuto = false,
            bool bottomAuto = false)
        {
            return new Margin
            {
                Left = new MarginValue { Value = left, Auto = leftAuto },
                Top = new MarginValue { Value = top, Auto = topAuto },
                Right = new MarginValue { Value = right, Auto = rightAuto },
                Bottom = new MarginValue { Value = bottom, Auto = bottomAuto },
            };
        }

        private static Paragraph BuildVerticalParagraph(TextBoxRenderTargetSettings settings)
        {
            var parser = new RichTextParser.RichTextParser();
            parser.ParseText(settings.Content);
            var payload = parser.ConvertToSegments();
            using var paragraphStyle = settings.ParagraphStyle.ToParagraphStyle(false);
            using var textStyle = settings.TextStyle.ToTextStyle();
            var paragraphs = payload.ToParagraphs(paragraphStyle, textStyle);
            Assert.That(paragraphs, Has.Count.EqualTo(1));
            return paragraphs[0];
        }

        private static Texture2D ReadOutput(TextBoxRenderTarget target, Vector2Int size)
        {
            var temporary = RenderTexture.GetTemporary(size.x, size.y, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            try
            {
                var uv = target.OutputUvRect;
                Graphics.Blit(target.OutputTexture, temporary, uv.size, uv.position);
                RenderTexture.active = temporary;
                var readback = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false, true);
                readback.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                readback.Apply();
                return readback;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
            }
        }

        private static IEnumerator CaptureAutoVisualSample(TextBoxRenderTarget target,
            string outputDirectory,
            string name,
            TextBoxRenderViewport viewport,
            TextBoxRenderTargetSettings settings)
        {
            target.Rebuild(viewport, ColorSpace.Gamma, settings, true, null);
            yield return WaitForTarget(target, viewport, settings);
            Assert.That(target.LastLayoutError, Is.Null);
            Assert.That(target.TryGetLinkGeometrySnapshot(out var autoSnapshot, out _), Is.True);
            Assert.That(TryGetVisibleLinkPoint(autoSnapshot, out var autoPoint), Is.True,
                name + " must expose a visible link in Auto mode.");
            Assert.That(target.TryHitLink(autoPoint, out _), Is.True,
                name + " must hit its link at the Auto paint origin.");

            Rect autoBounds;
            var readback = ReadOutput(target, viewport.VisibleOutputSizePixels);
            try
            {
                var png = readback.EncodeToPNG();
                Assert.That(png, Is.Not.Empty);
                Assert.That(TryGetPaintedBounds(readback, out autoBounds), Is.True,
                    name + " must paint visible text.");
                File.WriteAllBytes(IOPath.Combine(outputDirectory, name + ".png"), png);
            }
            finally
            {
                Object.Destroy(readback);
            }

            var fixedSettings = WithoutAutoMargins(settings);
            target.Rebuild(viewport, ColorSpace.Gamma, fixedSettings, true, null);
            yield return WaitForTarget(target, viewport, fixedSettings);
            Assert.That(target.LastLayoutError, Is.Null);
            Assert.That(target.TryGetLinkGeometrySnapshot(out var fixedSnapshot, out _), Is.True);
            Assert.That(TryGetVisibleLinkPoint(fixedSnapshot, out var fixedPoint), Is.True,
                name + "-fixed must expose a visible link.");
            Assert.That(target.TryHitLink(fixedPoint, out _), Is.True,
                name + "-fixed must hit its link at the fixed paint origin.");

            Rect fixedBounds;
            readback = ReadOutput(target, viewport.VisibleOutputSizePixels);
            try
            {
                var png = readback.EncodeToPNG();
                Assert.That(png, Is.Not.Empty);
                Assert.That(TryGetPaintedBounds(readback, out fixedBounds), Is.True,
                    name + "-fixed must paint visible text.");
                File.WriteAllBytes(IOPath.Combine(outputDirectory, name + "-fixed.png"), png);
            }
            finally
            {
                Object.Destroy(readback);
            }

            Assert.That(autoSnapshot.Geometry, Has.Count.EqualTo(fixedSnapshot.Geometry.Count),
                name + " must preserve link range geometry when toggling Auto.");
            for (var index = 0; index < autoSnapshot.Geometry.Count; ++index)
            {
                Assert.That(autoSnapshot.Geometry[index].Occurrence,
                    Is.EqualTo(fixedSnapshot.Geometry[index].Occurrence));
                Assert.That(autoSnapshot.Geometry[index].Rects,
                    Is.EqualTo(fixedSnapshot.Geometry[index].Rects),
                    name + " must not change range-query rectangles when toggling Auto.");
            }

            Assert.That(autoBounds.width, Is.EqualTo(fixedBounds.width).Within(1f),
                name + " must keep the same painted width when toggling Auto.");
            Assert.That(autoBounds.height, Is.EqualTo(fixedBounds.height).Within(1f),
                name + " must keep the same painted height when toggling Auto.");
            Assert.That(autoBounds.xMin - fixedBounds.xMin,
                Is.EqualTo(autoSnapshot.PaintPosition.x - fixedSnapshot.PaintPosition.x)
                    .Within(1.5f),
                name + " texture X displacement must match the Auto paint origin.");
            Assert.That(autoBounds.yMin - fixedBounds.yMin,
                Is.EqualTo(autoSnapshot.PaintPosition.y - fixedSnapshot.PaintPosition.y)
                    .Within(1.5f),
                name + " texture Y displacement must match the Auto paint origin.");
        }

        private static TextBoxRenderTargetSettings WithoutAutoMargins(
            TextBoxRenderTargetSettings settings)
        {
            var source = settings.Margin;
            var margin = new Margin
            {
                Left = FixedMarginValue(source.Left),
                Top = FixedMarginValue(source.Top),
                Right = FixedMarginValue(source.Right),
                Bottom = FixedMarginValue(source.Bottom),
            };
            return new TextBoxRenderTargetSettings(settings.Content,
                margin,
                settings.ParagraphStyle,
                settings.TextStyle,
                settings.WrapMode,
                settings.TextOverflow,
                settings.AutoFontSize,
                settings.MinSize,
                settings.MaxSize,
                settings.Sizing,
                settings.MarginReferenceSize);
        }

        [UnityTest]
        public IEnumerator TC08_TateChuYokoLinkHitHoverAndRuntimeUpdate()
        {
            const string markup =
                "<a href=\"tcy-link\"><span style=\"text-combine-upright: all\">ABC</span></a>天地";
            var textBox = CreateTextBox(markup, out var producer, MarginAll(0));
            textBox.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 200);
            producer.textStyle.size = 30;
            yield return WaitForProducer(producer, true);
            Assert.That(producer.isVerticalLayout, Is.True);
            Assert.That(producer.lastLayoutError, Is.Null);
            Assert.That(producer.TryGetContentSnapshot(out var content), Is.True);
            Assert.That(content.PlainText, Is.EqualTo("ABC天地"));
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var geometry, out _), Is.True);
            var rects = geometry.Paragraphs[0].GetRectsForRange(0, 3);
            Assert.That(rects, Has.Count.EqualTo(1));
            Assert.That(rects[0].height, Is.EqualTo(30f).Within(1f));
            Assert.That(rects[0].width, Is.EqualTo(30f).Within(1f));
            Assert.That(TryGetVisibleLinkPoint(geometry, out var point), Is.True);
            Assert.That(producer.TryHitLink(point, out var hit), Is.True);
            Assert.That(hit.Href, Is.EqualTo("tcy-link"));
            var indicator = HoverFirstLink(textBox);
            Assert.That(indicator.enabled, Is.True);
            Assert.That(LinkHoverIndicatorVertical(indicator), Is.True);

            var version = producer.OutputVersion;
            producer.content =
                "<a href=\"tcy-updated\"><span style=\"text-combine-upright: all\">ABC</span></a>天地";
            yield return WaitForProducerAfter(producer, version, true);
            Assert.That(producer.lastLayoutError, Is.Null);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var updated, out _), Is.True);
            Assert.That(TryGetVisibleLinkPoint(updated, out var updatedPoint), Is.True);
            Assert.That(producer.TryHitLink(updatedPoint, out var updatedHit), Is.True);
            Assert.That(updatedHit.Href, Is.EqualTo("tcy-updated"));
        }

        [UnityTest]
        public IEnumerator TC21_GrowTateChuYokoLinkHitAndNaturalWidth()
        {
            const string markup =
                "<a href=\"tcy-grow\"><span style=\"text-combine-upright: all; " +
                "--milestro-text-combine-upright-sizing: grow\">ABC</span></a>天地";
            var textBox = CreateTextBox(markup, out var producer, MarginAll(0));
            textBox.GetComponent<RectTransform>().sizeDelta = new Vector2(240, 200);
            producer.textStyle.size = 30;
            yield return WaitForProducer(producer, true);
            Assert.That(producer.isVerticalLayout, Is.True);
            Assert.That(producer.lastLayoutError, Is.Null);
            Assert.That(producer.TryGetContentSnapshot(out var content), Is.True);
            Assert.That(content.PlainText, Is.EqualTo("ABC天地"));
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var geometry, out _), Is.True);
            var rects = geometry.Paragraphs[0].GetRectsForRange(0, 3);
            Assert.That(rects, Has.Count.EqualTo(1));
            Assert.That(rects[0].height, Is.EqualTo(30f).Within(1f));
            Assert.That(rects[0].width, Is.GreaterThan(35f));
            Assert.That(TryGetVisibleLinkPoint(geometry, out var point), Is.True);
            Assert.That(producer.TryHitLink(point, out var hit), Is.True);
            Assert.That(hit.Href, Is.EqualTo("tcy-grow"));
            var indicator = HoverFirstLink(textBox);
            Assert.That(indicator.enabled, Is.True);
            Assert.That(LinkHoverIndicatorVertical(indicator), Is.True);
        }

        private static MarginValue FixedMarginValue(MarginValue value)
        {
            return new MarginValue
            {
                Value = value.Value,
                Unit = value.Unit,
                Auto = false,
            };
        }

        private static bool TryGetPaintedBounds(Texture2D texture, out Rect bounds)
        {
            bounds = default;
            var pixels = texture.GetPixels32();
            var minX = texture.width;
            var minY = texture.height;
            var maxX = -1;
            var maxY = -1;
            for (var row = 0; row < texture.height; ++row)
            {
                var y = texture.height - 1 - row;
                for (var x = 0; x < texture.width; ++x)
                {
                    if (pixels[row * texture.width + x].a <= 16)
                    {
                        continue;
                    }

                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
            }

            if (maxX < minX || maxY < minY)
            {
                return false;
            }

            bounds = Rect.MinMaxRect(minX, minY, maxX + 1, maxY + 1);
            return true;
        }

        private static TextStyleSettings TextStyle(float size)
        {
            return new TextStyleSettings
            {
                size = size,
                locale = "ja",
            };
        }
    }

    public sealed class ScrollCaptureBehaviour : MonoBehaviour, IScrollHandler
    {
        public int Calls { get; private set; }
        public Vector2 LastDelta { get; private set; }

        public void OnScroll(PointerEventData eventData)
        {
            LastDelta = eventData.scrollDelta;
            ++Calls;
        }
    }
}
