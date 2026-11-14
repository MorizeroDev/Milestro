using System;
using Milestro.Components;
using Milestro.Components.Internal;
using Milestro.Configuration;
using Milestro.Model;
using Milestro.RichTextParser;
using Milestro.Skia.TextLayout;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Milestro.Tests
{
    public class TextBoxGlyphMeshTests
    {
        private RenderSurfaceConfiguration previousConfiguration;

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
            MilestroConfiguration.Configuration.RenderSurface = previousConfiguration;
        }

        [Test]
        public void MultilineTextUsesGlyphTextureAndKeepsStaticOutput()
        {
            var size = new Vector2Int(240, 100);
            var viewport = TextBoxRenderViewport.Fixed(size, default, Vector2.zero, Vector2.zero);
            var settings = Settings("First line\nSecond line");
            using var target = new TextBoxRenderTarget();

            Assert.That(target.Rebuild(viewport, ColorSpace.Gamma, settings, true, null), Is.True);
            Assert.That(target.LastLayoutError, Is.Null);
            Assert.That(target.HasOutput, Is.True);
            Assert.That(target.GlyphMeshOutput, Is.Not.Null);
            Assert.That(target.OutputTexture, Is.TypeOf<Texture2D>());
            Assert.That(target.OutputWidth, Is.EqualTo(size.x));
            Assert.That(target.OutputHeight, Is.EqualTo(size.y));
            var output = target.GlyphMeshOutput;
            var version = target.OutputVersion;

            Assert.That(target.Rebuild(viewport, ColorSpace.Gamma, settings, false, null), Is.True);
            Assert.That(target.GlyphMeshOutput, Is.SameAs(output));
            Assert.That(target.OutputVersion, Is.EqualTo(version));
            Assert.That(target.HasPendingWork, Is.False);
        }

        [Test]
        public void ScrollingPublishesGeometryFromTheSameParagraphLayout()
        {
            var size = new Vector2Int(240, 38);
            var settings = Settings("<a href=\"link\">First</a>\nSecond\nThird\nFourth", XmlRichTextParser.Id);
            var viewport = TextBoxRenderViewport.Fixed(size, default, Vector2.zero, Vector2.zero);
            using var target = new TextBoxRenderTarget();
            Assert.That(target.Rebuild(viewport, ColorSpace.Gamma, settings, true, null), Is.True);
            Assert.That(target.GlyphMeshOutput, Is.Not.Null);
            Assert.That(target.TryGetLinkGeometrySnapshot(out var before, out var generation), Is.True);
            var contentSize = target.ContentSize;
            var firstParagraph = before.Paragraphs[0];
            var point = before.PaintPosition + before.Geometry[0].Rects[0].center * before.FitScale;
            Assert.That(target.TryHitLink(point / size, out var hit), Is.True);
            Assert.That(hit.Href, Is.EqualTo("link"));

            Assert.That(target.Rebuild(viewport, ColorSpace.Gamma, settings, false, null), Is.True);
            Assert.That(target.TryGetLinkGeometrySnapshot(out _, out var stableGeneration), Is.True);
            Assert.That(stableGeneration, Is.EqualTo(generation));

            viewport = TextBoxRenderViewport.Fixed(size, target.HorizontalScrollState,
                new Vector2(0f, 5f), Vector2.zero);
            Assert.That(target.Rebuild(viewport, ColorSpace.Gamma, settings, false, null), Is.True);
            Assert.That(target.TryGetLinkGeometrySnapshot(out var after, out var nextGeneration), Is.True);
            Assert.That(after.Paragraphs[0], Is.SameAs(firstParagraph));
            Assert.That(nextGeneration, Is.Not.EqualTo(generation));
            Assert.That(target.ContentSize, Is.EqualTo(contentSize));
            Assert.That(after.PaintPosition.y, Is.EqualTo(before.PaintPosition.y - 5f).Within(0.01f));
            Assert.That(target.GlyphMeshOutput.ClipRect, Is.EqualTo(after.ClipRect));
            point = after.PaintPosition + after.Geometry[0].Rects[0].center * after.FitScale;
            Assert.That(target.TryHitLink(point / size, out hit), Is.True);
            Assert.That(hit.Href, Is.EqualTo("link"));
        }

        [Test]
        public void InvisibleAndFailedOutputReleaseMeshAndInteractionGeometry()
        {
            var size = new Vector2Int(240, 100);
            var settings = Settings("<a href=\"link\">visible</a>", XmlRichTextParser.Id);
            using var target = new TextBoxRenderTarget();
            var viewport = TextBoxRenderViewport.Fixed(size, default, Vector2.zero, Vector2.zero);
            Assert.That(target.Rebuild(viewport, ColorSpace.Gamma, settings, true, null), Is.True);
            Assert.That(target.GlyphMeshOutput, Is.Not.Null);

            Assert.That(target.Rebuild(TextBoxRenderViewport.Invisible(size, default),
                ColorSpace.Gamma, settings, false, null), Is.True);
            Assert.That(target.HasOutput, Is.False);
            Assert.That(target.GlyphMeshOutput, Is.Null);
            Assert.That(target.TryGetLinkGeometrySnapshot(out _, out _), Is.False);

            Assert.That(target.Rebuild(viewport, ColorSpace.Gamma, settings, false, null), Is.True);
            Assert.That(target.HasOutput, Is.True);
            target.InvalidateFailedOutput();
            Assert.That(target.HasOutput, Is.False);
            Assert.That(target.GlyphMeshOutput, Is.Null);
            Assert.That(target.TryGetLinkGeometrySnapshot(out _, out _), Is.False);
        }

        [TestCase(true, TextDecoration.NoDecoration)]
        [TestCase(false, TextDecoration.Underline)]
        public void ParagraphEffectsRejectTheWholeGlyphExport(bool background, TextDecoration decoration)
        {
            using var paragraphStyle = new ParagraphStyle();
            using var textStyle = new TextStyle { FontSize = 20f, Decoration = decoration };
            if (background)
            {
                textStyle.BackgroundColor = new Color32(255, 0, 0, 255);
            }

            using var builder = new ParagraphBuilder(paragraphStyle);
            builder.PushStyle(textStyle);
            builder.AddText("effect");
            using var paragraph = builder.Build();
            paragraph.Layout(200f);
            using var snapshot = paragraph.CreateGlyphLayoutSnapshot();
            Assert.That(snapshot, Is.Null);
        }

        [Test]
        public void ExportKeepsMultipleRunsAndOwnsFontsAfterParagraphDisposal()
        {
            using var paragraphStyle = new ParagraphStyle();
            using var firstStyle = new TextStyle { FontSize = 20f };
            using var secondStyle = new TextStyle { FontSize = 32f };
            using var builder = new ParagraphBuilder(paragraphStyle);
            builder.PushStyle(firstStyle);
            builder.AddText("office\n");
            builder.Pop();
            builder.PushStyle(secondStyle);
            builder.AddText("123");
            var paragraph = builder.Build();
            try
            {
                paragraph.Layout(240f);
                using var snapshot = paragraph.CreateGlyphLayoutSnapshot();
                Assert.That(snapshot, Is.Not.Null);
                Assert.That(snapshot.Glyphs, Is.Not.Empty);
                Assert.That(Array.Exists(snapshot.Glyphs, glyph => Mathf.Approximately(glyph.FontSize, 20f)), Is.True);
                Assert.That(Array.Exists(snapshot.Glyphs, glyph => Mathf.Approximately(glyph.FontSize, 32f)), Is.True);
                var firstBaseline = snapshot.Glyphs[0].Y;
                Assert.That(Array.Exists(snapshot.Glyphs, glyph => glyph.Y > firstBaseline), Is.True);

                paragraph.Layout(40f);
                paragraph.Dispose();
                Assert.That(snapshot.TryGetRasterBounds(0, 1f, out var bounds), Is.True);
                Assert.That(bounds.width, Is.GreaterThan(0));
                Assert.That(bounds.height, Is.GreaterThan(0));
                var pixels = snapshot.Rasterize(0, 1f, bounds.width * bounds.height);
                Assert.That(Array.Exists(pixels, pixel => pixel != 0), Is.True);
            }
            finally
            {
                paragraph.Dispose();
            }
        }

        [Test]
        public void TextBoxPublishesGlyphVerticesAndGraphicTint()
        {
            var root = new GameObject("Glyph Canvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var textObject = new GameObject("Glyph TextBox", typeof(RectTransform));
                textObject.transform.SetParent(root.transform, false);
                var rect = (RectTransform)textObject.transform;
                rect.sizeDelta = new Vector2(240f, 100f);
                var textBox = textObject.AddComponent<TextBox>();
                ((Graphic)textBox).color = Color.red;
                var producer = textObject.GetComponent<TextBoxRenderTextureProducer>();
                producer.textStyle.size = 20f;
                textBox.Text = "1234";
                producer.RebuildOutput(true);

                Assert.That(producer.GlyphMeshOutput, Is.Not.Null);
                textBox.Rebuild(CanvasUpdate.PreRender);
                var mesh = textBox.canvasRenderer.GetMesh();
                Assert.That(mesh.vertexCount, Is.EqualTo(16));
                Assert.That(textBox.Texture, Is.SameAs(producer.OutputTexture));
                Assert.That(textBox.material.shader.name, Is.EqualTo("Milestro/TextGlyphMesh"));
                foreach (var vertexColor in mesh.colors32)
                {
                    Assert.That(vertexColor, Is.EqualTo(new Color32(255, 0, 0, 255)));
                }

                textBox.enabled = false;
                Assert.That(textBox.Texture, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FlowTextBoxClipsGlyphVerticesToTheDisplayedSlice()
        {
            var root = new GameObject("Glyph Flow Canvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var textObject = new GameObject("Glyph Flow TextBox", typeof(RectTransform));
                textObject.transform.SetParent(root.transform, false);
                var rect = (RectTransform)textObject.transform;
                rect.sizeDelta = new Vector2(240f, 120f);
                var textBox = textObject.AddComponent<TextBox>();
                var producer = textObject.GetComponent<TextBoxRenderTextureProducer>();
                producer.textStyle.size = 20f;
                textBox.Text = "1111\n2222\n3333\n4444";
                textBox.SetFlowModeActive(true);
                textBox.SetFlowVisibleRange(true, 20f, 60f);
                producer.RebuildOutput(true);
                Assert.That(producer.GlyphMeshOutput, Is.Not.Null);

                textBox.Rebuild(CanvasUpdate.PreRender);
                var mesh = textBox.canvasRenderer.GetMesh();
                Assert.That(mesh.vertexCount, Is.GreaterThan(4));
                foreach (var vertex in mesh.vertices)
                {
                    Assert.That(vertex.y, Is.InRange(rect.rect.yMax - 60.01f, rect.rect.yMax - 19.99f));
                    Assert.That(vertex.x, Is.InRange(rect.rect.xMin - 0.01f, rect.rect.xMax + 0.01f));
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MeshConfigurationChangesRequestPreparation()
        {
            var size = new Vector2Int(240, 100);
            var viewport = TextBoxRenderViewport.Fixed(size, default, Vector2.zero, Vector2.zero);
            using var target = new TextBoxRenderTarget();
            Assert.That(target.Rebuild(viewport, ColorSpace.Gamma, Settings("1234"), true, null), Is.True);
            Assert.That(target.HasPendingWork, Is.False);
            MilestroConfiguration.Configuration.RenderSurface.MaxTextGlyphMeshGlyphs = 1;
            Assert.That(target.HasPendingWork, Is.True);
        }

        [Test]
        public void NegativeMarginsStillClipToTheOriginalOutputSurface()
        {
            var size = new Vector2Int(120, 40);
            var margin = Margin.FixedZero();
            margin.Top = new MarginValue { Value = -12f };
            margin.Bottom = new MarginValue { Value = -12f };
            var settings = new TextBoxRenderTargetSettings("1111\n2222\n3333", margin,
                new ParagraphStyleSettings(), new TextStyleSettings { size = 20f }, null,
                TextBoxWrapMode.Wrap, TextOverflow.Clip);
            using var target = new TextBoxRenderTarget();
            var viewport = TextBoxRenderViewport.Fixed(size, default, Vector2.zero, Vector2.zero);
            Assert.That(target.Rebuild(viewport, ColorSpace.Gamma, settings, true, null), Is.True);
            Assert.That(target.GlyphMeshOutput, Is.Not.Null);
            using var vertices = new VertexHelper();
            target.GlyphMeshOutput.Populate(vertices, new Rect(0f, 0f, size.x, size.y), Color.white);
            Assert.That(vertices.currentVertCount, Is.GreaterThan(0));
            var vertex = new UIVertex();
            for (var index = 0; index < vertices.currentVertCount; ++index)
            {
                vertices.PopulateUIVertex(ref vertex, index);
                Assert.That(vertex.position.x, Is.InRange(-0.01f, size.x + 0.01f));
                Assert.That(vertex.position.y, Is.InRange(-0.01f, size.y + 0.01f));
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TextBoxPresentationEffectsSwitchToTextureAndBack(bool gradient)
        {
            var root = new GameObject("Glyph Effects Canvas", typeof(RectTransform), typeof(Canvas));
            Material customMaterial = null;
            try
            {
                root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var textObject = new GameObject("Glyph Effects TextBox", typeof(RectTransform));
                textObject.transform.SetParent(root.transform, false);
                ((RectTransform)textObject.transform).sizeDelta = new Vector2(240f, 100f);
                var textBox = textObject.AddComponent<TextBox>();
                var producer = textObject.GetComponent<TextBoxRenderTextureProducer>();
                textBox.Text = "1234";
                producer.RebuildOutput(true);
                Assert.That(producer.GlyphMeshOutput, Is.Not.Null);

                if (gradient)
                {
                    textBox.enableColorGradient = true;
                }
                else
                {
                    customMaterial = new Material(Shader.Find("UI/Default"));
                    textBox.material = customMaterial;
                }
                producer.RebuildOutput(false);
                Assert.That(producer.GlyphMeshOutput, Is.Null);
                if (!gradient)
                {
                    Assert.That(textBox.material, Is.SameAs(customMaterial));
                }

                textBox.enableColorGradient = false;
                textBox.material = null;
                producer.RebuildOutput(false);
                Assert.That(producer.GlyphMeshOutput, Is.Not.Null);
                textBox.Rebuild(CanvasUpdate.PreRender);
                Assert.That(textBox.canvasRenderer.GetMesh().vertexCount, Is.EqualTo(16));
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (customMaterial != null)
                {
                    Object.DestroyImmediate(customMaterial);
                }
            }
        }

        [Test]
        public void TextBoxOpacityChangesKeepGroupCompositingOnTheTexturePath()
        {
            var root = new GameObject("Glyph Opacity Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            try
            {
                root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var textObject = new GameObject("Glyph Opacity TextBox", typeof(RectTransform));
                textObject.transform.SetParent(root.transform, false);
                ((RectTransform)textObject.transform).sizeDelta = new Vector2(240f, 100f);
                var textBox = textObject.AddComponent<TextBox>();
                var producer = textObject.GetComponent<TextBoxRenderTextureProducer>();
                textBox.Text = "1234";
                producer.RebuildOutput(true);
                Assert.That(producer.GlyphMeshOutput, Is.Not.Null);

                producer.textStyle.textColor = new Color(1f, 1f, 1f, 0.5f);
                producer.RebuildOutput(false);
                Assert.That(producer.GlyphMeshOutput, Is.Not.Null,
                    "Foreground opacity is applied during each original glyph draw and remains mesh-compatible.");
                producer.textStyle.textColor = Color.white;
                producer.RebuildOutput(false);

                ((Graphic)textBox).color = new Color(1f, 1f, 1f, 0.5f);
                Assert.That(textBox.SupportsGlyphMeshPresentation, Is.False,
                    "A translucent Graphic requires group compositing before publication.");
                producer.RebuildOutput(false);
                Assert.That(producer.GlyphMeshOutput, Is.Null,
                    $"Graphic alpha={((Graphic)textBox).color.a}, capability={textBox.SupportsGlyphMeshPresentation}, " +
                    $"layout error={producer.lastLayoutError}");
                ((Graphic)textBox).color = Color.white;
                producer.RebuildOutput(false);
                Assert.That(producer.GlyphMeshOutput, Is.Not.Null);

                textBox.canvasRenderer.SetAlpha(0.5f);
                producer.RebuildOutput(false);
                Assert.That(producer.GlyphMeshOutput, Is.Null);
                textBox.canvasRenderer.SetAlpha(1f);
                producer.RebuildOutput(false);
                Assert.That(producer.GlyphMeshOutput, Is.Not.Null);

                root.GetComponent<CanvasGroup>().alpha = 0.5f;
                Canvas.ForceUpdateCanvases();
                Assert.That(producer.GlyphMeshOutput, Is.Null);
                root.GetComponent<CanvasGroup>().alpha = 1f;
                Canvas.ForceUpdateCanvases();
                Assert.That(producer.GlyphMeshOutput, Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static TextBoxRenderTargetSettings Settings(string content, string parser = null)
        {
            return new TextBoxRenderTargetSettings(content, Margin.FixedZero(),
                new ParagraphStyleSettings(), new TextStyleSettings { size = 20f }, parser,
                TextBoxWrapMode.Wrap, TextOverflow.Clip);
        }
    }
}
