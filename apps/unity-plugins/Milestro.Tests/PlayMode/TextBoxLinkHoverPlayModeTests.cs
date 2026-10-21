using System.Collections;
using Milestro.Components.Internal;
using Milestro.Model;
using Milestro.Skia;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Milestro.Tests
{
    public class TextBoxLinkHoverPlayModeTests
    {
        [UnityTest]
        public IEnumerator PercentageMarginsKeepLinkGeometryUntilHeightChanges()
        {
            yield return AssertStableLinkGeometry("10%", 20f);
        }

        [UnityTest]
        public IEnumerator PixelMarginsKeepLinkGeometryWhenHeightChanges()
        {
            yield return AssertStableLinkGeometry("20px", 0f);
        }

        private static IEnumerator AssertStableLinkGeometry(string margin, float expectedMarginChange)
        {
            var settings = new TextBoxRenderTargetSettings(
                "<p style=\"margin-top: " + margin + "\"><a href=\"hover\">Hover link</a></p>",
                new Margin(),
                new ParagraphStyleSettings(),
                new TextStyleSettings { size = 20f },
                TextBoxWrapMode.Wrap,
                TextOverflow.Clip);
            using var target = new TextBoxRenderTarget();
            var size = new Vector2Int(300, 200);
            var viewport = TextBoxRenderViewport.Fixed(size, default, Vector2.zero, Vector2.zero);
            target.Rebuild(viewport, ColorSpace.Gamma, settings, true, null);
            yield return WaitForGeometry(target, viewport, settings);
            Assert.That(target.TryGetLinkGeometrySnapshot(out var original, out var generation), Is.True);
            var originalPoint = original.PaintPosition + original.Geometry[0].Rects[0].center;

            for (var frame = 0; frame < 5; ++frame)
            {
                target.Rebuild(viewport, ColorSpace.Gamma, settings, false, null);
                Assert.That(target.TryGetLinkGeometrySnapshot(out _, out var currentGeneration), Is.True,
                    "An unchanged layout must keep published link geometry available for hover.");
                Assert.That(currentGeneration, Is.EqualTo(generation));
                Assert.That(target.TryHitLink(originalPoint / size, out var hit), Is.True);
                Assert.That(hit.Href, Is.EqualTo("hover"));
                yield return null;
            }

            size.y = 400;
            viewport = TextBoxRenderViewport.Fixed(size, default, Vector2.zero, Vector2.zero);
            target.Rebuild(viewport, ColorSpace.Gamma, settings, false, null);
            yield return WaitForGeometry(target, viewport, settings);
            Assert.That(target.TryGetLinkGeometrySnapshot(out var resized, out var resizedGeneration), Is.True);
            Assert.That(resizedGeneration, Is.Not.EqualTo(generation));
            var resizedPoint = resized.PaintPosition + resized.Geometry[0].Rects[0].center;
            Assert.That(resizedPoint.y - originalPoint.y, Is.EqualTo(expectedMarginChange).Within(0.01f),
                "Margins must resolve against the new container height.");
            Assert.That(target.TryHitLink(resizedPoint / size, out _), Is.True);
        }

        private static IEnumerator WaitForGeometry(TextBoxRenderTarget target,
            TextBoxRenderViewport viewport,
            TextBoxRenderTargetSettings settings)
        {
            for (var frame = 0; frame < 120; ++frame)
            {
                RenderTextureAtlas.Flush();
                UnitySkiaRenderTextureSurface.CollectCompletedEventsFromPump();
                target.Rebuild(viewport, ColorSpace.Gamma, settings, false, null);
                if (target.HasOutput && target.TryGetLinkGeometrySnapshot(out _, out _))
                {
                    yield break;
                }
                yield return null;
            }

            Assert.Fail("The rendered link never retained its published hit geometry after an idle rebuild.");
        }
    }
}
