using System.Reflection;
using Milestro.Components;
using Milestro.Components.Internal;
using Milestro.Configuration;
using Milestro.Model;
using Milestro.Skia;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Canvas = UnityEngine.Canvas;

namespace Milestro.Tests
{
    public sealed class SlimTextGlyphMeshTests
    {
        private RenderSurfaceConfiguration previousConfiguration;
        private GameObject root;

        [SetUp]
        public void SetUp()
        {
            previousConfiguration = MilestroConfiguration.Configuration.RenderSurface;
            MilestroConfiguration.Configuration.RenderSurface = new RenderSurfaceConfiguration
            {
                EnableTextGlyphMesh = true
            };
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
            MilestroConfiguration.Configuration.RenderSurface = previousConfiguration;
        }

        [Test]
        public void OptedInTargetBuildsMeshAndKeepsStaticOutput()
        {
            using var target = new SlimTextRenderTarget();
            Assert.That(Rebuild(target, "1234"), Is.True);
            Assert.That(target.HasOutput, Is.True);
            Assert.That(target.GlyphMeshOutput, Is.Not.Null);
            Assert.That(target.GlyphMeshOutput.Output.GlyphCount, Is.EqualTo(4));
            Assert.That(target.OutputTexture, Is.TypeOf<Texture2D>());
            Assert.That(target.OutputWidth, Is.EqualTo(240));
            Assert.That(target.OutputHeight, Is.EqualTo(60));
            var output = target.GlyphMeshOutput;
            var version = target.OutputVersion;

            Assert.That(Rebuild(target, "1234"), Is.True);

            Assert.That(target.GlyphMeshOutput, Is.SameAs(output));
            Assert.That(target.OutputVersion, Is.EqualTo(version));
            Assert.That(target.HasPendingWork, Is.False);
        }

        [Test]
        public void EmptyTextPublishesValidEmptyGeometry()
        {
            using var target = new SlimTextRenderTarget();
            Assert.That(Rebuild(target, ""), Is.True);
            Assert.That(target.HasOutput, Is.True);
            Assert.That(target.GlyphMeshOutput, Is.Not.Null);
            Assert.That(target.GlyphMeshOutput.Output.GlyphCount, Is.Zero);
            using var vertices = new VertexHelper();
            target.GlyphMeshOutput.Output.Populate(vertices, new Rect(0, 0, 240, 60), Color.white);
            Assert.That(vertices.currentVertCount, Is.Zero);
        }

        [Test]
        public void ColorOnlyUpdateReusesLayoutAndCachedGlyphs()
        {
            using var target = new SlimTextRenderTarget();
            Assert.That(Rebuild(target, "1234"), Is.True);
            var layoutField = typeof(SlimTextRenderTarget).GetField("glyphLayout",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(layoutField, Is.Not.Null);
            var layout = layoutField.GetValue(target);
            var texture = target.OutputTexture;
            var rasterized = TextGlyphAtlas.RasterizedGlyphs;
            var oldOutput = target.GlyphMeshOutput;
            target.MarkPaintChanged();

            Assert.That(Rebuild(target, "1234", Color.red), Is.True);

            Assert.That(layoutField.GetValue(target), Is.SameAs(layout));
            Assert.That(TextGlyphAtlas.RasterizedGlyphs, Is.EqualTo(rasterized));
            Assert.That(target.OutputTexture, Is.SameAs(texture));
            Assert.That(oldOutput.IsValid, Is.False);
            using var vertices = new VertexHelper();
            target.GlyphMeshOutput.Output.Populate(vertices, new Rect(0, 0, 240, 60), Color.white);
            var vertex = new UIVertex();
            vertices.PopulateUIVertex(ref vertex, 0);
            Assert.That(vertex.color, Is.EqualTo(new Color32(255, 0, 0, 255)));
        }

        [Test]
        public void LabelsSharePrewarmedGlyphTexture()
        {
            using var first = new SlimTextRenderTarget();
            using var second = new SlimTextRenderTarget();
            Assert.That(Rebuild(first, "0123456789"), Is.True);
            var rasterized = TextGlyphAtlas.RasterizedGlyphs;

            Assert.That(Rebuild(second, "9876543210"), Is.True);

            Assert.That(second.OutputTexture, Is.SameAs(first.OutputTexture));
            Assert.That(TextGlyphAtlas.RasterizedGlyphs, Is.EqualTo(rasterized));
        }

        [Test]
        public void ChangingPrewarmedDigitsReusesSnapshotWithoutRasterOrUpload()
        {
            using var target = new SlimTextRenderTarget();
            Assert.That(Rebuild(target, "0123456789"), Is.True);
            TextGlyphAtlas.Flush();
            var snapshotField = typeof(SlimTextRenderTarget).GetField("glyphTextSnapshot",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(snapshotField, Is.Not.Null);
            var snapshot = snapshotField.GetValue(target);
            var rasterized = TextGlyphAtlas.RasterizedGlyphs;
            var uploads = TextGlyphAtlas.Uploads;

            foreach (var content in new[] { "1234", "9876", "543210", "0" })
            {
                target.MarkPaintChanged();
                Assert.That(Rebuild(target, content), Is.True);
                TextGlyphAtlas.Flush();
                Assert.That(snapshotField.GetValue(target), Is.SameAs(snapshot));
                Assert.That(target.GlyphMeshOutput.Output.GlyphCount, Is.EqualTo(content.Length));
            }

            Assert.That(TextGlyphAtlas.RasterizedGlyphs, Is.EqualTo(rasterized));
            Assert.That(TextGlyphAtlas.Uploads, Is.EqualTo(uploads));
        }

        [Test]
        public void NoAllocModeReleasesMeshAndManagedModeCanRestoreIt()
        {
            using var target = new SlimTextRenderTarget();
            Assert.That(Rebuild(target, "1234"), Is.True);
            var previous = target.GlyphMeshOutput;

            target.EnsureNoAllocCapacity(32);

            Assert.That(target.UsesManagedStringText, Is.False);
            Assert.That(target.GlyphMeshOutput, Is.Null);
            Assert.That(previous.IsValid, Is.False);
            Assert.That(target.UseManagedStringText(), Is.True);
            Assert.That(Rebuild(target, "1234"), Is.True);
            Assert.That(target.GlyphMeshOutput.IsValid, Is.True);
        }

        [Test]
        public void DirectUtf8UpdateAlsoLeavesGlyphMeshMode()
        {
            using var target = new SlimTextRenderTarget();
            Assert.That(Rebuild(target, "1234"), Is.True);
            var previous = target.GlyphMeshOutput;
            var bytes = System.Text.Encoding.UTF8.GetBytes("5678");

            target.SetTextUtf8NoAlloc(new Vector2Int(240, 60), ColorSpace.Gamma,
                Settings("1234", Color.white), false, 1f, null, bytes, 0, bytes.Length);

            Assert.That(target.UsesManagedStringText, Is.False);
            Assert.That(target.GlyphMeshOutput, Is.Null);
            Assert.That(previous.IsValid, Is.False);
        }

        [Test]
        public void ComponentPublishesChildLayerAndTintAndCleansUp()
        {
            var text = CreateComponent();
            var producer = text.GetComponent<SlimTextRenderTextureProducer>();
            producer.RebuildOutput(false);
            var graphic = text.GetComponentInChildren<TextGlyphMeshGraphic>(true);
            Assert.That(graphic, Is.Not.Null);
            Assert.That(graphic.gameObject.layer, Is.EqualTo(text.gameObject.layer));
            Assert.That(text.GetComponent<RawImage>().enabled, Is.False);
            text.GetComponent<RawImage>().color = Color.green;
            Canvas.ForceUpdateCanvases();
            graphic.Rebuild(CanvasUpdate.PreRender);
            var mesh = graphic.canvasRenderer.GetMesh();
            Assert.That(mesh.vertexCount, Is.EqualTo(16));
            foreach (var vertexColor in mesh.colors32)
            {
                Assert.That(vertexColor, Is.EqualTo(new Color32(0, 255, 0, 255)));
            }

            text.enabled = false;
            Assert.That(graphic.enabled, Is.False);
            Assert.That(text.GetComponent<RawImage>().texture, Is.Null);
            text.enabled = true;
            Assert.That(graphic.enabled, Is.True);
            Object.DestroyImmediate(text);
            Assert.That(graphic == null, Is.True);
        }

        [Test]
        public void QueuedPreparationPublishesChildBeforeTheSameCanvasRender()
        {
            var text = CreateComponent();
            var producer = text.GetComponent<SlimTextRenderTextureProducer>();

            Canvas.ForceUpdateCanvases();

            var graphic = text.GetComponentInChildren<TextGlyphMeshGraphic>(true);
            Assert.That(producer.GlyphMeshOutput, Is.Not.Null);
            Assert.That(graphic, Is.Not.Null);
            Assert.That(graphic.enabled, Is.True);
            Assert.That(text.GetComponent<RawImage>().enabled, Is.False);
            Assert.That(graphic.canvasRenderer.GetMesh().vertexCount, Is.EqualTo(16));
        }

        [Test]
        public void GlyphPresentationRendersBeforeExistingChildOverlays()
        {
            var text = CreateComponent();
            var group = text.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.childControlWidth = true;
            group.childForceExpandWidth = false;
            var overlay = new GameObject("Existing Overlay", typeof(RectTransform), typeof(Image),
                typeof(LayoutElement));
            overlay.GetComponent<LayoutElement>().preferredWidth = 40f;
            overlay.transform.SetParent(text.transform, false);

            Canvas.ForceUpdateCanvases();

            var graphic = text.GetComponentInChildren<TextGlyphMeshGraphic>(true);
            Assert.That(graphic, Is.Not.Null);
            Assert.That(graphic.transform.GetSiblingIndex(), Is.LessThan(overlay.transform.GetSiblingIndex()));
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)text.transform);
            Assert.That(graphic.rectTransform.rect.width, Is.EqualTo(((RectTransform)text.transform).rect.width));
            Assert.That(((RectTransform)overlay.transform).rect.width, Is.EqualTo(40f));
        }

        [Test]
        public void DeferredChildReceivesHardMaskDuringItsFirstCanvasRender()
        {
            var text = CreateComponent();
            var mask = root.AddComponent<RectMask2D>();
            mask.PerformClipping();

            Canvas.ForceUpdateCanvases();

            var graphic = text.GetComponentInChildren<TextGlyphMeshGraphic>(true);
            Assert.That(graphic, Is.Not.Null);
            Assert.That(graphic.canvasRenderer.hasRectClipping, Is.True);
        }

        [Test]
        public void ChildPreservesMaskabilityAndCanvasRendererTint()
        {
            var text = CreateComponent();
            text.GetComponent<SlimTextRenderTextureProducer>().RebuildOutput(false);
            var image = text.GetComponent<RawImage>();
            var graphic = text.GetComponentInChildren<TextGlyphMeshGraphic>(true);
            Assert.That(graphic, Is.Not.Null);
            image.maskable = false;
            var rendererColor = new Color(0.4f, 0.5f, 0.6f, 1f);
            image.canvasRenderer.SetColor(rendererColor);
            text.gameObject.layer = 0;

            Canvas.ForceUpdateCanvases();

            Assert.That(graphic.maskable, Is.False);
            Assert.That(graphic.canvasRenderer.GetColor(), Is.EqualTo(rendererColor));
            Assert.That(graphic.gameObject.layer, Is.EqualTo(0));
        }

        [Test]
        public void PresentationOpacityRejectsMeshAndRestoresAtRenderDeadline()
        {
            var text = CreateComponent();
            var producer = text.GetComponent<SlimTextRenderTextureProducer>();
            var image = text.GetComponent<RawImage>();
            producer.RebuildOutput(false);
            Assert.That(producer.GlyphMeshOutput, Is.Not.Null);
            image.color = new Color(1f, 1f, 1f, 0.5f);
            Assert.That(text.SupportsGlyphMesh, Is.False);
            image.color = Color.white;
            image.canvasRenderer.SetAlpha(0.5f);
            Assert.That(text.SupportsGlyphMesh, Is.False);
            image.canvasRenderer.SetAlpha(1f);
            var group = root.AddComponent<CanvasGroup>();
            group.alpha = 0.5f;

            Canvas.ForceUpdateCanvases();

            Assert.That(text.SupportsGlyphMesh, Is.False);
            Assert.That(producer.GlyphMeshOutput, Is.Null);
            group.alpha = 1f;
            Canvas.ForceUpdateCanvases();
            Assert.That(text.SupportsGlyphMesh, Is.True);
            Assert.That(producer.GlyphMeshOutput, Is.Not.Null);
        }

        [Test]
        public void SoftAncestorMaskRejectsMeshOnlyWhenMaskingApplies()
        {
            var text = CreateComponent();
            var image = text.GetComponent<RawImage>();
            var mask = root.AddComponent<RectMask2D>();
            Assert.That(text.SupportsGlyphMesh, Is.True);

            mask.softness = new Vector2Int(4, 0);
            Assert.That(text.SupportsGlyphMesh, Is.False);
            image.maskable = false;
            Assert.That(text.SupportsGlyphMesh, Is.True);
            image.maskable = true;
            mask.enabled = false;
            Assert.That(text.SupportsGlyphMesh, Is.True);
            mask.enabled = true;
            mask.softness = Vector2Int.zero;
            Assert.That(text.SupportsGlyphMesh, Is.True);
        }

        [Test]
        public void ComponentCapabilityTracksRaycastMaskAndCustomMaterial()
        {
            var text = CreateComponent();
            var image = text.GetComponent<RawImage>();
            Assert.That(text.SupportsGlyphMesh, Is.True);
            image.raycastTarget = true;
            Assert.That(text.SupportsGlyphMesh, Is.False);
            image.raycastTarget = false;
            var mask = text.gameObject.AddComponent<Mask>();
            Assert.That(text.SupportsGlyphMesh, Is.False);
            Object.DestroyImmediate(mask);
            Assert.That(text.SupportsGlyphMesh, Is.True);
            var material = new Material(image.defaultMaterial);
            try
            {
                image.material = material;
                Assert.That(text.SupportsGlyphMesh, Is.False);
                image.material = null;
                Assert.That(text.SupportsGlyphMesh, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        private CanvasSlimText CreateComponent()
        {
            root = new GameObject("Slim Glyph Canvas", typeof(RectTransform), typeof(Canvas));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var child = new GameObject("Slim Glyph Text", typeof(RectTransform), typeof(RawImage));
            child.SetActive(false);
            child.layer = 5;
            child.transform.SetParent(root.transform, false);
            ((RectTransform)child.transform).sizeDelta = new Vector2(240f, 60f);
            var text = child.AddComponent<CanvasSlimText>();
            text.Configure("1234", 20f, SlimTextHorizontalAlign.Left, Color.white);
            child.SetActive(true);
            return text;
        }

        private static bool Rebuild(SlimTextRenderTarget target, string content, Color? color = null)
        {
            return target.Rebuild(new Vector2Int(240, 60), ColorSpace.Gamma,
                Settings(content, color ?? Color.white), false, 1f, null, allowGlyphMesh: true);
        }

        private static SlimTextRenderTargetSettings Settings(string content, Color color)
        {
            return new SlimTextRenderTargetSettings(content,
                SlimTextRenderTextureProducer.DefaultFontFamily, FontWeight.Normal, 20f,
                color, new RectOffset(), SlimTextHorizontalAlign.Left,
                SlimTextVerticalAlign.Top, true);
        }
    }
}
