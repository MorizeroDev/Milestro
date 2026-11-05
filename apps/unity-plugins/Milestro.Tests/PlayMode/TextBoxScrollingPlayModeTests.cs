using System.Collections;
using Milestro.Components;
using Milestro.Components.Internal;
using Milestro.Configuration;
using Milestro.Model;
using Milestro.RichTextParser;
using Milestro.Skia;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Canvas = UnityEngine.Canvas;

namespace Milestro.Tests
{
    public class TextBoxScrollingPlayModeTests
    {
        private GameObject root;
        private RenderSurfaceConfiguration previousConfiguration;
        private float previousCaptureDeltaTime;
        private const string Lines = "first\nsecond\nthird";
        private const string LinkedLines = "<a href=\"test\">" + Lines + "</a>";

        [SetUp]
        public void SetUp()
        {
            previousConfiguration = MilestroConfiguration.Configuration.RenderSurface;
            previousCaptureDeltaTime = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            MilestroConfiguration.Configuration.RenderSurface = new RenderSurfaceConfiguration();
            root = new GameObject("TextBox scrolling test", typeof(RectTransform), typeof(Canvas));
            root.SetActive(false);
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            MilestroConfiguration.Configuration.RenderSurface = previousConfiguration;
            Time.captureDeltaTime = previousCaptureDeltaTime;
        }

        [UnityTest]
        public IEnumerator PlainTextScrollRequestSurvivesPendingDraw()
        {
            yield return AssertScrollRequestSurvivesPendingDraw(Lines, false);
        }

        [UnityTest]
        public IEnumerator LinkedTextScrollRequestSurvivesPendingDraw()
        {
            yield return AssertScrollRequestSurvivesPendingDraw(LinkedLines, true);
        }

        [UnityTest]
        public IEnumerator LinkedParagraphsScrollRequestSurvivesPendingDraw()
        {
            yield return AssertScrollRequestSurvivesPendingDraw(
                "<p style=\"margin-bottom: 20px\"><b>Heading</b></p><p>" + LinkedLines + "</p>",
                true);
        }

        [UnityTest]
        public IEnumerator LinkedTextHorizontalScrollRequestSurvivesPendingDraw()
        {
            var textBox = CreateTextBox("<a href=\"test\">first second third fourth fifth sixth</a>",
                out var producer,
                TextBoxWrapMode.NoWrap);
            var maxScrollX = producer.maxScrollX;
            Assert.That(maxScrollX, Is.GreaterThan(0f));

            textBox.ScrollToPercentX(1f);
            Assert.That(producer.scrollX, Is.EqualTo(maxScrollX).Within(0.01f));
            yield return WaitForOutput(producer, true);
            Assert.That(producer.scrollX, Is.EqualTo(maxScrollX).Within(0.01f));
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var snapshot, out _), Is.True);
            Assert.That(snapshot.Geometry[0].BlockRanges[0].FrameOffset.x,
                Is.EqualTo(-maxScrollX).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator LatestLinkedTextScrollRequestWinsWhileDrawIsPending()
        {
            var textBox = CreateTextBox(LinkedLines, out var producer);
            textBox.ScrollToPercentY(0.25f);
            textBox.ScrollToPercentY(0.75f);

            Assert.That(textBox.GetScrollPercentY(), Is.EqualTo(0.75f).Within(0.001f));
            Assert.That(textBox.TryGetScrollState(out var state), Is.True);
            Assert.That(state.ScrollY, Is.EqualTo(producer.maxScrollY * 0.75f).Within(0.01f));
            yield return WaitForOutput(producer, true);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var snapshot, out _), Is.True);
            Assert.That(snapshot.PaintPosition.y, Is.EqualTo(-producer.maxScrollY * 0.75f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator LinkedTextWheelTweenReachesRequestedOffset()
        {
            var textBox = CreateTextBox(LinkedLines, out var producer);
            var eventObject = new GameObject("EventSystem", typeof(EventSystem));
            eventObject.transform.SetParent(root.transform, false);
            var scrollEvent = new PointerEventData(eventObject.GetComponent<EventSystem>())
            {
                scrollDelta = new Vector2(0f, -1f)
            };

            textBox.OnScroll(scrollEvent);
            var expectedScrollY = Mathf.Min(48f, producer.maxScrollY);
            for (var frame = 0; frame < 60; ++frame)
            {
                yield return null;
            }

            Assert.That(producer.scrollY, Is.EqualTo(expectedScrollY).Within(0.01f));
            yield return WaitForOutput(producer, true);
            Assert.That(producer.TryGetLinkGeometrySnapshot(out var snapshot, out _), Is.True);
            Assert.That(snapshot.PaintPosition.y, Is.EqualTo(-expectedScrollY).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator ElementGeometryProvidesStableContentAndScrolledCanvasRects()
        {
            var textBox = CreateTextBox("<p>zero</p><p><span id=\"target\">one</span></p>" +
                                        "<p>two</p><p>three</p><p>four</p><p>five</p>",
                out var producer);
            yield return WaitForOutput(producer, true);

            Assert.That(textBox.TryGetElementGeometryById("target", out var initial), Is.True);
            Assert.That(initial.ContentRect.width, Is.GreaterThan(0f));
            Assert.That(initial.ContentRect.height, Is.GreaterThan(0f));
            Assert.That(textBox.TryGetElementGeometryById("missing", out _), Is.False);
            Assert.That(textBox.TryGetElementGeometryById("", out _), Is.False);
            Assert.That(textBox.TryGetScrollState(out var state), Is.True);

            var targetPercent = initial.ContentRect.y / (state.ContentHeight - state.ViewportHeight);
            textBox.ScrollToPercentY(targetPercent);
            Assert.That(producer.scrollY, Is.EqualTo(initial.ContentRect.y).Within(0.1f));

            textBox.ScrollToPercentY(1f);
            yield return WaitForOutput(producer, true);
            Assert.That(textBox.TryGetElementGeometryById("target", out var scrolled), Is.True);
            Assert.That(scrolled.ContentRect.x, Is.EqualTo(initial.ContentRect.x).Within(0.01f));
            Assert.That(scrolled.ContentRect.y, Is.EqualTo(initial.ContentRect.y).Within(0.01f));
            Assert.That(scrolled.CanvasRect.y - initial.CanvasRect.y,
                Is.EqualTo(producer.maxScrollY).Within(0.1f));
        }

        [UnityTest]
        public IEnumerator EmptyParagraphIdProvidesScrollableAnchorPoint()
        {
            var textBox = CreateTextBox("<p>zero</p><p id=\"anchor\"></p><p>two</p>" +
                                        "<p>three</p><p>four</p><p>five</p>",
                out var producer);
            yield return WaitForOutput(producer, true);

            Assert.That(textBox.TryGetElementGeometryById("anchor", out var anchor), Is.True);
            Assert.That(anchor.ContentRect.size, Is.EqualTo(Vector2.zero));
            Assert.That(anchor.CanvasRect.size, Is.EqualTo(Vector2.zero));
            Assert.That(anchor.ContentRect.y, Is.GreaterThan(0f));
            Assert.That(textBox.TryGetScrollState(out var state), Is.True);

            textBox.ScrollToPercentY(anchor.ContentRect.y / (state.ContentHeight - state.ViewportHeight));
            Assert.That(producer.scrollY, Is.EqualTo(anchor.ContentRect.y).Within(0.1f));
        }

        [UnityTest]
        public IEnumerator VerticalElementGeometryFollowsHorizontalScroll()
        {
            var textBox = CreateTextBox("<p>天地玄黃宇宙洪荒日月盈昃辰宿列張</p><p id=\"anchor\"></p>" +
                                        "<p>寒來暑往秋收冬藏閏餘成歲律呂調陽</p>",
                out var producer,
                verticalLayout: true);
            yield return WaitForOutput(producer, true);
            Assert.That(producer.maxScrollX, Is.GreaterThan(0f));
            Assert.That(textBox.TryGetElementGeometryById("anchor", out var initial), Is.True);
            Assert.That(initial.ContentRect.size, Is.EqualTo(Vector2.zero));

            textBox.ScrollToPercentX(1f);
            yield return WaitForOutput(producer, true);
            Assert.That(textBox.TryGetElementGeometryById("anchor", out var scrolled), Is.True);
            Assert.That(scrolled.ContentRect.x, Is.EqualTo(initial.ContentRect.x).Within(0.01f));
            Assert.That(scrolled.CanvasRect.x - initial.CanvasRect.x,
                Is.EqualTo(producer.maxScrollX).Within(0.1f));
        }

        private TextBox CreateTextBox(string content,
            out TextBoxRenderTextureProducer producer,
            TextBoxWrapMode wrapMode = TextBoxWrapMode.Wrap,
            bool verticalLayout = false)
        {
            var child = new GameObject("TextBox", typeof(RectTransform));
            child.transform.SetParent(root.transform, false);
            child.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 100f);
            producer = child.AddComponent<TextBoxRenderTextureProducer>();
            producer.richTextParserId = XmlRichTextParser.Id;
            producer.textOverflow = TextOverflow.Overflow;
            producer.wrapMode = wrapMode;
            if (verticalLayout)
            {
                producer.paragraphStyle.writingMode = WritingMode.VerticalRl;
            }
            producer.content = content;
            var textBox = child.AddComponent<TextBox>();
            root.SetActive(true);
            return textBox;
        }

        private IEnumerator AssertScrollRequestSurvivesPendingDraw(string content, bool withLink)
        {
            var textBox = CreateTextBox(content, out var producer);
            var maxScrollY = producer.maxScrollY;
            Assert.That(maxScrollY, Is.GreaterThan(0f));
            textBox.ScrollToPercentY(1f);
            Assert.That(producer.maxScrollY, Is.EqualTo(maxScrollY));
            Assert.That(producer.scrollY, Is.EqualTo(maxScrollY).Within(0.01f),
                "A pending draw must not discard the scrollbar request.");
            yield return WaitForOutput(producer, withLink);
            Assert.That(producer.scrollY, Is.EqualTo(maxScrollY).Within(0.01f));
            if (withLink)
            {
                Assert.That(producer.TryGetLinkGeometrySnapshot(out var snapshot, out _), Is.True);
                Assert.That(snapshot.PaintPosition.y, Is.EqualTo(-maxScrollY).Within(0.01f));
            }
        }

        private static IEnumerator WaitForOutput(TextBoxRenderTextureProducer producer, bool withLink)
        {
            for (var frame = 0; frame < 120; ++frame)
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

            Assert.Fail("Scrolled output did not complete within 120 frames.");
        }
    }
}
