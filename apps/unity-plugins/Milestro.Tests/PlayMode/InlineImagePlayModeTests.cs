#nullable enable
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Milestro.Components;
using Milestro.Components.Internal;
using Milestro.Configuration;
using Milestro.Model;
using Milestro.ResourceManagement;
using Milestro.RichTextParser;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Milestro.Tests
{
    public sealed class InlineImagePlayModeTests
    {
        private sealed class Provider : IImageResourceProvider
        {
            internal Sprite Sprite = null!;
            internal int Acquired;
            internal int Released;
            public Task<ImageResource?> AcquireAsync(string source, CancellationToken cancellationToken)
            {
                ++Acquired;
                return Task.FromResult<ImageResource?>(new ImageResource(Sprite));
            }
            public void Release(ImageResource resource) { ++Released; }
        }

        [UnityTest]
        public IEnumerator OverlayTracksLayoutAndReleasesContentBindings()
        {
            var texture = new Texture2D(8, 4);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 8, 4), Vector2.one * .5f);
            var provider = new Provider { Sprite = sprite };
            var previous = ImageResources.Providers.DefaultProvider;
            ImageResources.Providers.DefaultProvider = provider;
            var root = new GameObject("Inline image test", typeof(Canvas));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var child = new GameObject("Text", typeof(RectTransform));
            child.SetActive(false);
            child.transform.SetParent(root.transform, false);
            child.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 100);
            var producer = child.AddComponent<TextBoxRenderTextureProducer>();
            var textBox = child.AddComponent<TextBox>();
            producer.richTextParserId = XmlRichTextParser.Id;
            producer.content = "<img src=\"coin\" width=\"40\" height=\"20\"/><placeholder width=\"10\" height=\"10\"/>";
            try
            {
                child.SetActive(true);
                for (var frame = 0; frame < 10; ++frame) yield return null;
                Assert.That(textBox.TryGetPlaceholderGeometrySnapshot(out var snapshot), Is.True);
                Assert.That(snapshot.Geometry.Count, Is.EqualTo(2));
                Assert.That(snapshot.Geometry[0].Source, Is.EqualTo("coin"));
                var views = child.GetComponentsInChildren<InlineImageGraphic>();
                Assert.That(views.Length, Is.EqualTo(1));
                Assert.That(views[0].mainTexture, Is.SameAs(texture));
                Assert.That(views[0].raycastTarget, Is.False);
                Assert.That(provider.Acquired, Is.EqualTo(1));

                producer.paragraphStyle.writingMode = WritingMode.VerticalRl;
                for (var frame = 0; frame < 4; ++frame) yield return null;
                Assert.That(provider.Acquired, Is.EqualTo(1));
                Assert.That(child.GetComponentsInChildren<InlineImageGraphic>().Length, Is.EqualTo(1));

                textBox.enabled = false;
                yield return null;
                Assert.That(child.GetComponentsInChildren<InlineImageGraphic>().Length, Is.Zero);
                textBox.enabled = true;
                yield return null;
                Assert.That(provider.Acquired, Is.EqualTo(1));

                producer.content = "<placeholder width=\"10\" height=\"10\"/>";
                for (var frame = 0; frame < 3; ++frame) yield return null;
                Assert.That(provider.Released, Is.EqualTo(1));
                Assert.That(child.GetComponentsInChildren<InlineImageGraphic>().Length, Is.Zero);
            }
            finally
            {
                ImageResources.Providers.DefaultProvider = previous;
                Object.Destroy(root);
                Object.Destroy(sprite);
                Object.Destroy(texture);
            }
        }

        [UnityTest]
        public IEnumerator OverlayStaysVisibleWhileScrolling()
        {
            var texture = new Texture2D(8, 4);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 8, 4), Vector2.one * .5f);
            var provider = new Provider { Sprite = sprite };
            var previous = ImageResources.Providers.DefaultProvider;
            ImageResources.Providers.DefaultProvider = provider;
            var root = new GameObject("Inline image scroll test", typeof(Canvas));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var child = new GameObject("Text", typeof(RectTransform));
            child.SetActive(false);
            child.transform.SetParent(root.transform, false);
            child.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 60);
            var producer = child.AddComponent<TextBoxRenderTextureProducer>();
            var textBox = child.AddComponent<TextBox>();
            producer.richTextParserId = XmlRichTextParser.Id;
            producer.content = "<img src=\"coin\" width=\"40\" height=\"20\"/>" +
                               string.Concat(Enumerable.Repeat("<p>scrollable line of text</p>", 12));
            try
            {
                child.SetActive(true);
                for (var frame = 0; frame < 10; ++frame) yield return null;
                Assert.That(child.GetComponentsInChildren<InlineImageGraphic>().Length, Is.EqualTo(1));
                Assert.That(producer.maxScrollY, Is.GreaterThan(24f));

                for (var step = 1; step <= 8; ++step)
                {
                    producer.scrollY = step * 2f;
                    yield return null;
                    Assert.That(child.GetComponentsInChildren<InlineImageGraphic>().Length, Is.EqualTo(1),
                        $"Inline image must stay visible while scrolling (step {step}).");
                }
            }
            finally
            {
                ImageResources.Providers.DefaultProvider = previous;
                Object.Destroy(root);
                Object.Destroy(sprite);
                Object.Destroy(texture);
            }
        }

        [DefaultExecutionOrder(10000)]
        private sealed class LateUpdateInvalidator : MonoBehaviour
        {
            internal Action? Invalidated;

            private void LateUpdate()
            {
                Invalidated?.Invoke();
            }
        }

        [DefaultExecutionOrder(1100)]
        private sealed class InlineImageSyncProbe : MonoBehaviour
        {
            private static readonly FieldInfo FlowActiveField = typeof(TextBox)
                .GetField("flowModeActive", BindingFlags.Instance | BindingFlags.NonPublic)!;
            private static readonly FieldInfo ImageRectField = typeof(InlineImageGraphic)
                .GetField("imageRect", BindingFlags.Instance | BindingFlags.NonPublic)!;

            internal TextBox Target = null!;
            internal TextBoxRenderTextureProducer Producer = null!;
            internal int Samples;
            internal float LastDelta;
            internal Rect LastExpected;
            internal Rect LastActual;

            internal bool FlowModeActive =>
                Target != null && (bool)FlowActiveField.GetValue(Target)!;

            private void LateUpdate()
            {
                if (Target == null || Producer == null || !Target.isActiveAndEnabled)
                {
                    return;
                }

                if (!Producer.TryGetInlineImageGeometrySnapshot(out var snapshot))
                {
                    return;
                }

                var found = false;
                var placeholder = default(TextBoxPlaceholderGeometry);
                foreach (var item in snapshot.Placeholders)
                {
                    if (!item.Visible || item.Source != "coin")
                    {
                        continue;
                    }

                    placeholder = item;
                    found = true;
                    break;
                }

                if (!found)
                {
                    return;
                }

                InlineImageGraphic? view = null;
                foreach (var candidate in Target.GetComponentsInChildren<InlineImageGraphic>(true))
                {
                    if (candidate.gameObject.activeInHierarchy)
                    {
                        view = candidate;
                        break;
                    }
                }

                if (view == null)
                {
                    return;
                }

                if (!TryExpectedRect(Target, placeholder, snapshot, out var expected))
                {
                    return;
                }

                LastActual = (Rect)ImageRectField.GetValue(view)!;
                LastExpected = expected;
                LastDelta = Mathf.Max(
                    Mathf.Abs(LastActual.xMin - LastExpected.xMin),
                    Mathf.Abs(LastActual.yMin - LastExpected.yMin),
                    Mathf.Abs(LastActual.xMax - LastExpected.xMax),
                    Mathf.Abs(LastActual.yMax - LastExpected.yMax));
                ++Samples;
            }

            private static bool TryExpectedRect(TextBox textBox,
                in TextBoxPlaceholderGeometry placeholder,
                in TextBoxLinkGeometrySnapshot snapshot,
                out Rect expected)
            {
                expected = default;
                var scale = snapshot.FitScale > 0f && float.IsFinite(snapshot.FitScale)
                    ? snapshot.FitScale
                    : 1f;
                var outputRect = new Rect(placeholder.Rect.x * scale + snapshot.PaintPosition.x,
                    placeholder.Rect.y * scale + snapshot.PaintPosition.y,
                    placeholder.Rect.width * scale,
                    placeholder.Rect.height * scale);
                var rect = textBox.GetPixelAdjustedRect();
                using var vertices = new VertexHelper();
                typeof(TextBox).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly)!
                    .Invoke(textBox, new object[] { vertices });
                if (vertices.currentVertCount == 0)
                {
                    return false;
                }
                var top = float.NegativeInfinity;
                var bottom = float.PositiveInfinity;
                var vertex = default(UIVertex);
                for (var index = 0; index < vertices.currentVertCount; ++index)
                {
                    vertices.PopulateUIVertex(ref vertex, index);
                    top = Mathf.Max(top, vertex.position.y);
                    bottom = Mathf.Min(bottom, vertex.position.y);
                }
                var width = Mathf.Max(1f, snapshot.VisibleOutputSize.x);
                var height = Mathf.Max(1f, snapshot.VisibleOutputSize.y);
                var visibleHeight = Mathf.Max(0.0001f, top - bottom);
                expected = Rect.MinMaxRect(
                    rect.xMin + outputRect.xMin / width * rect.width,
                    top - outputRect.yMax / height * visibleHeight,
                    rect.xMin + outputRect.xMax / width * rect.width,
                    top - outputRect.yMin / height * visibleHeight);
                return true;
            }
        }

        [UnityTest]
        public IEnumerator OverlayFollowsFlowVisibleRangeWhileScrolling()
        {
            var texture = new Texture2D(8, 4);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 8, 4), Vector2.one * .5f);
            var provider = new Provider { Sprite = sprite };
            var previous = ImageResources.Providers.DefaultProvider;
            ImageResources.Providers.DefaultProvider = provider;
            var root = new GameObject("Inline image flow sync test", typeof(Canvas));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportObject.transform.SetParent(root.transform, false);
            var viewport = (RectTransform)viewportObject.transform;
            viewport.sizeDelta = new Vector2(200f, 100.25f);
            var child = new GameObject("Text", typeof(RectTransform));
            child.SetActive(false);
            child.transform.SetParent(viewport, false);
            var contentRect = (RectTransform)child.transform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, 100f);
            var producer = child.AddComponent<TextBoxRenderTextureProducer>();
            var textBox = child.AddComponent<TextBox>();
            var layout = child.AddComponent<TextBoxLayoutElement>();
            layout.widthMode = TextWidthMode.Fixed;
            layout.heightMode = TextHeightMode.Auto;
            var viewportField = typeof(TextBoxLayoutElement)
                .GetField("m_viewport", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(viewportField, Is.Not.Null);
            viewportField!.SetValue(layout, viewport);
            var fitter = child.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            producer.richTextParserId = XmlRichTextParser.Id;
            producer.textOverflow = TextOverflow.Overflow;
            producer.content = "<p><img src=\"coin\" width=\"40\" height=\"20\" style=\"object-fit: fill\"/> image line</p>" +
                               "<p>line</p><p>line</p><p>line</p>" +
                               string.Concat(Enumerable.Repeat("<p>scrollable line of text</p>", 30));
            try
            {
                child.SetActive(true);
                for (var frame = 0; frame < 10; ++frame) yield return null;
                Assert.That(child.GetComponentsInChildren<InlineImageGraphic>(true).Length, Is.EqualTo(1));

                var probe = root.AddComponent<InlineImageSyncProbe>();
                probe.Target = textBox;
                probe.Producer = producer;
                for (var frame = 0; frame < 3; ++frame) yield return null;
                Assert.That(probe.FlowModeActive, Is.True, "test setup must use flow mode");
                Assert.That(probe.Samples, Is.GreaterThan(0), "probe must observe the inline image");

                var previousSamples = probe.Samples;
                for (var step = 1; step <= 12; ++step)
                {
                    contentRect.anchoredPosition = new Vector2(0f, step * 1.25f);
                    yield return null;
                    Assert.That(probe.Samples, Is.GreaterThan(previousSamples),
                        $"Probe must observe the inline image while scrolling (step {step}).");
                    Assert.That(probe.LastDelta, Is.LessThan(0.1f),
                        $"Inline image must use the current flow visible range (step {step}, " +
                        $"expected {probe.LastExpected}, actual {probe.LastActual}).");
                    previousSamples = probe.Samples;
                }
            }
            finally
            {
                ImageResources.Providers.DefaultProvider = previous;
                Object.Destroy(root);
                Object.Destroy(sprite);
                Object.Destroy(texture);
            }
        }

        [UnityTest]
        public IEnumerator OverlaySurvivesTransientLinkGeometryInvalidation()
        {
            var texture = new Texture2D(8, 4);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 8, 4), Vector2.one * .5f);
            var provider = new Provider { Sprite = sprite };
            var previous = ImageResources.Providers.DefaultProvider;
            ImageResources.Providers.DefaultProvider = provider;
            var root = new GameObject("Inline image transient geometry test", typeof(Canvas));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var child = new GameObject("Text", typeof(RectTransform));
            child.SetActive(false);
            child.transform.SetParent(root.transform, false);
            child.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 100);
            var producer = child.AddComponent<TextBoxRenderTextureProducer>();
            child.AddComponent<TextBox>();
            producer.richTextParserId = XmlRichTextParser.Id;
            producer.textOverflow = TextOverflow.Overflow;
            producer.content = "<p><img src=\"coin\" width=\"40\" height=\"20\"/> image line</p>" +
                               string.Concat(Enumerable.Repeat("<p>scrollable line of text</p>", 12));
            try
            {
                child.SetActive(true);
                for (var frame = 0; frame < 10; ++frame) yield return null;
                Assert.That(child.GetComponentsInChildren<InlineImageGraphic>(true).Length, Is.EqualTo(1));

                var invalidator = root.AddComponent<LateUpdateInvalidator>();
                invalidator.Invalidated = CreateInvalidator(producer);
                producer.scrollY = 1f;
                for (var frame = 0; frame < 10; ++frame) yield return null;
                for (var frame = 0; frame < 12; ++frame)
                {
                    yield return null;
                    Assert.That(producer.TryGetLinkGeometrySnapshot(out _, out _), Is.False,
                        "test setup must keep the published geometry unavailable");
                    var views = child.GetComponentsInChildren<InlineImageGraphic>(true);
                    Assert.That(views.Length, Is.EqualTo(1));
                    Assert.That(views[0].gameObject.activeInHierarchy, Is.True,
                        $"Inline image must remain presented when link geometry is transiently invalidated (frame {frame}).");
                }
            }
            finally
            {
                ImageResources.Providers.DefaultProvider = previous;
                Object.Destroy(root);
                Object.Destroy(sprite);
                Object.Destroy(texture);
            }
        }

        [UnityTest]
        public IEnumerator InlineImageStaysAlignedWithTextDuringScrollRectInertia()
        {
            var texture = new Texture2D(32, 32);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 32, 32), Vector2.one * .5f);
            var provider = new Provider { Sprite = sprite };
            var previous = ImageResources.Providers.DefaultProvider;
            ImageResources.Providers.DefaultProvider = provider;
            var root = new GameObject("Inline image inertia alignment test", typeof(Canvas));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scrollObject = new GameObject("Scroll", typeof(RectTransform));
            scrollObject.transform.SetParent(root.transform, false);
            ((RectTransform)scrollObject.transform).sizeDelta = new Vector2(600f, 200f);
            var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportObject.transform.SetParent(scrollObject.transform, false);
            var viewport = (RectTransform)viewportObject.transform;
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            var child = new GameObject("Text", typeof(RectTransform));
            child.SetActive(false);
            child.transform.SetParent(viewport, false);
            var contentRect = (RectTransform)child.transform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, 200f);
            var producer = child.AddComponent<TextBoxRenderTextureProducer>();
            var textBox = child.AddComponent<TextBox>();
            var layout = child.AddComponent<TextBoxLayoutElement>();
            layout.widthMode = TextWidthMode.Fixed;
            layout.heightMode = TextHeightMode.Auto;
            typeof(TextBoxLayoutElement).GetField("m_viewport", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(layout, viewport);
            var fitter = child.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scrollRect = scrollObject.AddComponent<MilestroScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.inertia = true;
            producer.richTextParserId = XmlRichTextParser.Id;
            producer.textOverflow = TextOverflow.Overflow;
            producer.content = "<p><img src=\"coin\" width=\"32\" height=\"32\" style=\"object-fit: fill\"/>" +
                               " <span id=\"line\">Inline SVG Data URI sword</span></p>" +
                               string.Concat(Enumerable.Repeat("<p>scrollable line of text</p>", 40));
            try
            {
                child.SetActive(true);
                for (var frame = 0; frame < 12; ++frame) yield return null;
                var probe = root.AddComponent<InlineImageSyncProbe>();
                probe.Target = textBox;
                probe.Producer = producer;
                for (var frame = 0; frame < 3; ++frame) yield return null;
                Assert.That(probe.FlowModeActive, Is.True, "test setup must use flow mode");
                Assert.That(probe.Samples, Is.GreaterThan(0), "probe must observe the inline image");
                var previousSamples = probe.Samples;
                var start = contentRect.anchoredPosition;
                scrollRect.velocity = new Vector2(0f, 100f);
                var moved = false;
                var scrollingSamples = 0;
                for (var step = 1; step <= 20; ++step)
                {
                    yield return null;
                    moved |= contentRect.anchoredPosition != start;
                    if (probe.Samples == previousSamples)
                    {
                        continue;
                    }

                    ++scrollingSamples;
                    Assert.That(probe.LastDelta, Is.LessThan(0.1f),
                        $"Inline image must stay aligned with the text mesh while the published geometry lags " +
                        $"(step {step}, expected {probe.LastExpected}, actual {probe.LastActual}).");
                    previousSamples = probe.Samples;
                }

                Assert.That(moved, Is.True, "ScrollRect inertia must move the content in LateUpdate.");
                Assert.That(scrollingSamples, Is.GreaterThanOrEqualTo(5),
                    "Alignment must be observed repeatedly during inertia.");
            }
            finally
            {
                ImageResources.Providers.DefaultProvider = previous;
                Object.Destroy(root);
                Object.Destroy(sprite);
                Object.Destroy(texture);
            }
        }

        [UnityTest]
        public IEnumerator RenderedImageAndTextStayAlignedDuringContinuousScroll()
        {
            return AssertRenderedScrollAlignment(true, true);
        }

        [UnityTest]
        public IEnumerator RenderedImageAndTextStayAlignedWithoutAtlas()
        {
            return AssertRenderedScrollAlignment(false, true);
        }

        [UnityTest]
        public IEnumerator RenderedImageAndTextStayAlignedDuringInternalScroll()
        {
            return AssertRenderedScrollAlignment(true, false);
        }

        private static IEnumerator AssertRenderedScrollAlignment(bool atlas, bool flow)
        {
            var previousConfiguration = MilestroConfiguration.Configuration.RenderSurface;
            MilestroConfiguration.Configuration.RenderSurface = new RenderSurfaceConfiguration { EnableAtlas = atlas };
            var texture = new Texture2D(16, 16);
            texture.SetPixels(Enumerable.Repeat(Color.magenta, 256).ToArray());
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), Vector2.one * .5f);
            var previous = ImageResources.Providers.DefaultProvider;
            ImageResources.Providers.DefaultProvider = new Provider { Sprite = sprite };
            var target = new RenderTexture(320, 240, 24);
            target.Create();
            var cameraObject = new GameObject("Inline image pixel camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 120f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.targetTexture = target;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            var root = new GameObject("Inline image pixel canvas", typeof(Canvas));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10f;
            var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportObject.transform.SetParent(root.transform, false);
            var viewport = (RectTransform)viewportObject.transform;
            viewport.sizeDelta = new Vector2(280f, 200f);
            var child = new GameObject("Text", typeof(RectTransform));
            child.SetActive(false);
            child.transform.SetParent(viewport, false);
            var contentRect = (RectTransform)child.transform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, 200f);
            var producer = child.AddComponent<TextBoxRenderTextureProducer>();
            child.AddComponent<TextBox>();
            if (flow)
            {
                var layout = child.AddComponent<TextBoxLayoutElement>();
                layout.widthMode = TextWidthMode.Fixed;
                layout.heightMode = TextHeightMode.Auto;
                typeof(TextBoxLayoutElement).GetField("m_viewport", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(layout, viewport);
                var fitter = child.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            producer.richTextParserId = XmlRichTextParser.Id;
            producer.textOverflow = TextOverflow.Overflow;
            producer.textStyle.textColor = Color.green;
            producer.content = "<p><placeholder width=\"1\" height=\"60\"/></p>" +
                               "<p>MMMM<img src=\"coin\" width=\"20\" height=\"20\" style=\"object-fit: fill\"/></p>" +
                               "<p><placeholder width=\"1\" height=\"600\"/></p>";
            var readback = new Texture2D(320, 240, TextureFormat.RGBA32, false);
            try
            {
                child.SetActive(true);
                for (var frame = 0; frame < 20; ++frame) yield return null;
                var samples = 0;
                var renderedOffset = 0f;
                var sampler = root.AddComponent<LateUpdateInvalidator>();
                sampler.Invalidated = () =>
                {
                    renderedOffset = ReadRenderedOffset(camera, target, readback);
                    ++samples;
                };
                yield return null;
                Assert.That(samples, Is.GreaterThan(0));
                var baseline = renderedOffset;
                for (var step = 1; step <= 24; ++step)
                {
                    var offsetY = (step <= 12 ? step : 24 - step) * 3f;
                    if (flow) contentRect.anchoredPosition = new Vector2(0f, offsetY);
                    else producer.scrollY = offsetY;
                    var previousSamples = samples;
                    yield return null;
                    Assert.That(samples, Is.GreaterThan(previousSamples));
                    Assert.That(renderedOffset, Is.EqualTo(baseline).Within(1.1f),
                        $"Rendered image must move with rendered text on scroll frame {step}.");
                }
            }
            finally
            {
                ImageResources.Providers.DefaultProvider = previous;
                MilestroConfiguration.Configuration.RenderSurface = previousConfiguration;
                Object.Destroy(root);
                Object.Destroy(cameraObject);
                Object.Destroy(readback);
                target.Release();
                Object.Destroy(target);
                Object.Destroy(sprite);
                Object.Destroy(texture);
            }
        }

        private static float ReadRenderedOffset(Camera camera, RenderTexture target, Texture2D readback)
        {
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                readback.Apply();
            }
            finally
            {
                RenderTexture.active = previous;
            }

            var textSum = 0f;
            var imageSum = 0f;
            var textCount = 0;
            var imageCount = 0;
            var pixels = readback.GetPixels32();
            for (var index = 0; index < pixels.Length; ++index)
            {
                var pixel = pixels[index];
                if (pixel.g > 128 && pixel.r < 64 && pixel.b < 64)
                {
                    textSum += index / target.width;
                    ++textCount;
                }
                if (pixel.r > 128 && pixel.b > 128 && pixel.g < 64)
                {
                    imageSum += index / target.width;
                    ++imageCount;
                }
            }
            Assert.That(textCount, Is.GreaterThan(10), "Rendered text must be visible.");
            Assert.That(imageCount, Is.GreaterThan(10), "Rendered image must be visible.");
            return imageSum / imageCount - textSum / textCount;
        }

        private static Action CreateInvalidator(TextBoxRenderTextureProducer producer)
        {
            var targetField = typeof(TextBoxRenderTextureProducer)
                .GetField("renderTarget", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(targetField, Is.Not.Null);
            var target = targetField!.GetValue(producer);
            Assert.That(target, Is.Not.Null);
            var publicationField = target!.GetType()
                .GetField("linkGeometryPublication", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(publicationField, Is.Not.Null);
            var publication = publicationField!.GetValue(target);
            Assert.That(publication, Is.Not.Null);
            var invalidate = publication!.GetType()
                .GetMethod("InvalidatePublished", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(invalidate, Is.Not.Null);
            return () =>
            {
                var boxed = publicationField.GetValue(target)!;
                invalidate!.Invoke(boxed, null);
                publicationField.SetValue(target, boxed);
            };
        }
    }
}
