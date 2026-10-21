using System.Collections;
using Milestro.Components.Internal;
using Milestro.Configuration;
using Milestro.Model;
using Milestro.Skia;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Milestro.Tests
{
    public class TextBoxTextIndentPlayModeTests
    {
        private RenderSurfaceConfiguration previousConfiguration;

        [SetUp]
        public void SetUp()
        {
            previousConfiguration = MilestroConfiguration.Configuration.RenderSurface;
        }

        [TearDown]
        public void TearDown()
        {
            MilestroConfiguration.Configuration.RenderSurface = previousConfiguration;
        }

        [UnityTest]
        public IEnumerator PixelIndentPaintsAndHitsLinkInsideMargin()
        {
            yield return AssertIndentPaintsAndHitsLink("-20px", TextOverflow.Clip, false);
        }

        [UnityTest]
        public IEnumerator EmIndentPaintsAndHitsLinkInsideMarginInAtlas()
        {
            yield return AssertIndentPaintsAndHitsLink("-1em", TextOverflow.Ellipsis, true);
        }

        [UnityTest]
        public IEnumerator PercentageIndentPaintsAndHitsLinkInsideMargin()
        {
            yield return AssertIndentPaintsAndHitsLink("-10%", TextOverflow.Clip, true);
        }

        private static IEnumerator AssertIndentPaintsAndHitsLink(string indent, TextOverflow overflow, bool atlas)
        {
            MilestroConfiguration.Configuration.RenderSurface = new RenderSurfaceConfiguration { EnableAtlas = atlas };
            var margin = new Margin();
            margin.Left = new MarginValue { Value = 20f };
            margin.Right = new MarginValue { Value = 20f };
            margin.Top = new MarginValue { Value = 20f };
            margin.Bottom = new MarginValue { Value = 20f };
            var settings = new TextBoxRenderTargetSettings(
                "normal<p style=\"text-indent: " + indent + "\"><a href=\"indent\">test</a></p>normal",
                margin,
                new ParagraphStyleSettings(),
                new TextStyleSettings { size = 20f },
                TextBoxWrapMode.Wrap,
                overflow);
            var size = new Vector2Int(200, 200);
            var viewport = TextBoxRenderViewport.Fixed(size, default, Vector2.zero, Vector2.zero);
            using var target = new TextBoxRenderTarget();
            target.Rebuild(viewport, ColorSpace.Gamma, settings, true, null);
            for (var frame = 0; frame < 120; ++frame)
            {
                RenderTextureAtlas.Flush();
                UnitySkiaRenderTextureSurface.CollectCompletedEventsFromPump();
                target.Rebuild(viewport, ColorSpace.Gamma, settings, false, null);
                if (target.HasOutput && target.TryGetLinkGeometrySnapshot(out _, out _)) break;
                yield return null;
            }

            Assert.That(target.HasOutput, Is.True);
            Assert.That(target.TryGetLinkGeometrySnapshot(out var snapshot, out _), Is.True);
            var linkRect = snapshot.Geometry[0].Rects[0];
            var linkPoint = snapshot.PaintPosition + new Vector2(linkRect.xMin + 5f, linkRect.center.y);
            Assert.That(linkPoint.x, Is.InRange(0f, 19f));
            Assert.That(target.TryHitLink(new Vector2(linkPoint.x / size.x, linkPoint.y / size.y), out var hit), Is.True);
            Assert.That(hit.Href, Is.EqualTo("indent"));
            Assert.That(target.TryHitLink(new Vector2(-0.01f, linkPoint.y / size.y), out _), Is.False);
            Assert.That(target.ViewportSize, Is.EqualTo(new Vector2(160f, 160f)));

            var readback = ReadOutput(target, size);
            try
            {
                var pixels = readback.GetPixels32();
                var paintedPixels = 0;
                for (var row = 0; row < size.y; ++row)
                {
                    for (var column = 0; column < 20; ++column)
                    {
                        if (pixels[row * size.x + column].a > 16) ++paintedPixels;
                    }
                }
                Assert.That(paintedPixels, Is.GreaterThan(0), "Indented glyphs must remain visible inside the margin.");
            }
            finally
            {
                Object.Destroy(readback);
            }
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
    }
}
