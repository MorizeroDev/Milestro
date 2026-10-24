using System.Collections;
using Milestro.Components.Internal;
using Milestro.Configuration;
using Milestro.Skia;
using Milestro.Skia.TextLayout;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Milestro.Tests
{
    public class RenderTextureAtlasPlayModeTests
    {
        private RenderSurfaceConfiguration previousConfiguration;

        [SetUp]
        public void SetUp()
        {
            previousConfiguration = MilestroConfiguration.Configuration.RenderSurface;
            MilestroConfiguration.Configuration.RenderSurface = new RenderSurfaceConfiguration { AtlasPageSize = 64 };
        }

        [TearDown]
        public void TearDown()
        {
            MilestroConfiguration.Configuration.RenderSurface = previousConfiguration;
        }

        [UnityTest]
        public IEnumerator SharesTextureSubmitsOnceAndPreservesUnchangedRegion()
        {
            using var first = new ManagedRenderTextureSurface();
            using var second = new ManagedRenderTextureSurface();
            using var red = MakeImage(Color.red);
            using var blue = MakeImage(Color.blue);
            Assert.That(first.EnsureExact(new Vector2Int(12, 12), ColorSpace.Gamma, out _), Is.True);
            Assert.That(second.EnsureExact(new Vector2Int(12, 12), ColorSpace.Gamma, out _), Is.True);
            Assert.That(first.Texture, Is.SameAs(second.Texture));
            Assert.That(first.DisplayUvRect, Is.Not.EqualTo(second.DisplayUvRect));
            var submissions = RenderTextureAtlas.SubmissionCount;
            Assert.That(first.TrySubmit(Draw(red)), Is.True);
            Assert.That(second.TrySubmit(Draw(blue)), Is.True);
            Assert.That(first.TrySubmit(Draw(blue)), Is.False);
            RenderTextureAtlas.Flush();
            Assert.That(RenderTextureAtlas.SubmissionCount - submissions, Is.EqualTo(1));
            yield return WaitForOutput(first, second);
            AssertSolid(first, Color.red);
            AssertSolid(second, Color.blue);
            var completed = false;
            first.RenderEventCompleted += status => completed = status == UnitySkiaRenderTextureSurface.RenderSubmissionStatus.Drawn;
            Assert.That(first.TrySubmit(new UnitySkiaRenderCommandList()), Is.True);
            RenderTextureAtlas.Flush();
            for (var frame = 0; frame < 120 && !completed; ++frame)
            {
                UnitySkiaRenderTextureSurface.CollectCompletedEventsFromPump();
                yield return null;
            }
            Assert.That(completed, Is.True);
            AssertSolid(first, Color.clear);
            AssertSolid(second, Color.blue);
            submissions = RenderTextureAtlas.SubmissionCount;
            RenderTextureAtlas.Flush();
            yield return null;
            Assert.That(RenderTextureAtlas.SubmissionCount, Is.EqualTo(submissions));
        }

        [UnityTest]
        public IEnumerator ScaledAtlasCropMatchesStandalonePixels()
        {
            using var atlas = new ManagedRenderTextureSurface();
            using var standalone = new ManagedRenderTextureSurface { AllowAtlas = false };
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.SetPixels(new[] { Color.red, Color.green, Color.blue, Color.white });
            texture.Apply();
            using var image = MilestroImage.MakeFromBytes(texture.EncodeToPNG());
            Object.Destroy(texture);
            Assert.That(atlas.TryEnsureScreenSpace(new Vector2Int(12, 12), 1.5f, ColorSpace.Gamma, null, out _), Is.True);
            Assert.That(standalone.TryEnsureScreenSpace(new Vector2Int(12, 12), 1.5f, ColorSpace.Gamma, null, out _), Is.True);
            Assert.That(atlas.TrySubmit(Draw(image)), Is.True);
            Assert.That(standalone.TrySubmit(Draw(image)), Is.True);
            RenderTextureAtlas.Flush();
            yield return WaitForOutput(atlas, standalone);
            var actual = ReadOutput(atlas);
            var expected = ReadOutput(standalone);
            try
            {
                var actualPixels = actual.GetPixels32();
                var expectedPixels = expected.GetPixels32();
                Assert.That(actualPixels.Length, Is.EqualTo(expectedPixels.Length));
                for (var index = 0; index < actualPixels.Length; ++index)
                {
                    Assert.That(actualPixels[index].r, Is.EqualTo(expectedPixels[index].r).Within(2), $"red pixel {index}");
                    Assert.That(actualPixels[index].g, Is.EqualTo(expectedPixels[index].g).Within(2), $"green pixel {index}");
                    Assert.That(actualPixels[index].b, Is.EqualTo(expectedPixels[index].b).Within(2), $"blue pixel {index}");
                    Assert.That(actualPixels[index].a, Is.EqualTo(expectedPixels[index].a).Within(2), $"alpha pixel {index}");
                }
            }
            finally
            {
                Object.Destroy(actual);
                Object.Destroy(expected);
            }
        }

        [UnityTest]
        public IEnumerator RetiredPendingRegionIsNotReusedUntilCompletion()
        {
            var first = new ManagedRenderTextureSurface();
            using var second = new ManagedRenderTextureSurface();
            using var blue = MakeImage(Color.blue);
            Assert.That(first.EnsureExact(new Vector2Int(12, 12), ColorSpace.Gamma, out _), Is.True);
            var previousUv = first.DisplayUvRect;
            Assert.That(first.TrySubmit(Draw(blue)), Is.True);
            first.Dispose();
            Assert.That(second.EnsureExact(new Vector2Int(12, 12), ColorSpace.Gamma, out _), Is.True);
            Assert.That(second.DisplayUvRect, Is.Not.EqualTo(previousUv));
            Assert.That(second.TrySubmit(Draw(blue)), Is.True);
            RenderTextureAtlas.Flush();
            yield return WaitForOutput(second);
            AssertSolid(second, Color.blue);
            using var replacement = new ManagedRenderTextureSurface();
            Assert.That(replacement.EnsureExact(new Vector2Int(12, 12), ColorSpace.Gamma, out _), Is.True);
            Assert.That(replacement.DisplayUvRect, Is.EqualTo(previousUv));
        }

        [UnityTest]
        public IEnumerator FullPagesAndDifferentColorSpacesRemainIsolated()
        {
            MilestroConfiguration.Configuration.RenderSurface.AtlasPageSize = 16;
            using var first = new ManagedRenderTextureSurface();
            using var second = new ManagedRenderTextureSurface();
            using var linear = new ManagedRenderTextureSurface();
            using var red = MakeImage(Color.red);
            using var blue = MakeImage(Color.blue);
            var pages = RenderTextureAtlas.PageCount;
            var submissions = RenderTextureAtlas.SubmissionCount;
            Assert.That(first.EnsureExact(new Vector2Int(12, 12), ColorSpace.Gamma, out _), Is.True);
            Assert.That(second.EnsureExact(new Vector2Int(12, 12), ColorSpace.Gamma, out _), Is.True);
            Assert.That(linear.EnsureExact(new Vector2Int(2, 2), ColorSpace.Linear, out _), Is.True);
            Assert.That(RenderTextureAtlas.PageCount - pages, Is.EqualTo(3));
            Assert.That(first.Texture, Is.Not.SameAs(second.Texture));
            Assert.That(first.Texture, Is.Not.SameAs(linear.Texture));
            Assert.That(first.TrySubmit(Draw(red)), Is.True);
            Assert.That(second.TrySubmit(Draw(blue)), Is.True);
            Assert.That(linear.TrySubmit(Draw(red)), Is.True);
            RenderTextureAtlas.Flush();
            Assert.That(RenderTextureAtlas.SubmissionCount - submissions, Is.EqualTo(3));
            yield return WaitForOutput(first, second, linear);
            AssertSolid(first, Color.red);
            AssertSolid(second, Color.blue);
            AssertSolid(linear, Color.red);
        }

        [UnityTest]
        public IEnumerator RetiringPendingPageDefersResourcesAndReleasesBudget()
        {
            var pages = RenderTextureAtlas.PageCount;
            var committedBytes = UnitySkiaRenderTextureSurface.BudgetLedger.Snapshot().CommittedBytes;
            var surface = new ManagedRenderTextureSurface();
            using var blue = MakeImage(Color.blue);
            var retired = new RetirementProbe();
            Assert.That(surface.EnsureExact(new Vector2Int(12, 12), ColorSpace.Gamma, out _), Is.True);
            Assert.That(surface.TrySubmit(Draw(blue)), Is.True);
            surface.DisposeResourceAfterPendingDraws(retired);
            surface.Dispose();
            Assert.That(retired.Disposed, Is.False);
            Assert.That(RenderTextureAtlas.PageCount, Is.EqualTo(pages + 1));
            RenderTextureAtlas.Flush();
            for (var frame = 0; frame < 120 && !retired.Disposed; ++frame)
            {
                UnitySkiaRenderTextureSurface.CollectCompletedEventsFromPump();
                yield return null;
            }
            Assert.That(retired.Disposed, Is.True);
            Assert.That(RenderTextureAtlas.PageCount, Is.EqualTo(pages));
            Assert.That(UnitySkiaRenderTextureSurface.BudgetLedger.Snapshot().CommittedBytes, Is.EqualTo(committedBytes));
        }

        [UnityTest]
        public IEnumerator ParagraphDrawCommandRetainsSourceUntilPendingAtlasSubmissionCompletes()
        {
            using var surface = new ManagedRenderTextureSurface();
            Assert.That(surface.EnsureExact(new Vector2Int(64, 64), ColorSpace.Gamma, out _), Is.True);
            using var paragraphStyle = new ParagraphStyle();
            using var textStyle = new TextStyle { FontSize = 24 };
            paragraphStyle.SetTextStyle(textStyle);
            using var builder = new ParagraphBuilder(paragraphStyle);
            builder.AddText("Retained paragraph");
            var paragraph = builder.Build();
            paragraph.Layout(60);
            var commands = new UnitySkiaRenderCommandList();
            commands.DrawParagraph(paragraph, Vector2.zero, new Rect(0, 0, 64, 64));
            Assert.That(surface.TrySubmit(commands), Is.True);

            paragraph.Dispose();
            RenderTextureAtlas.Flush();
            yield return WaitForOutput(surface);

            var readback = ReadOutput(surface);
            try
            {
                var hasVisiblePixel = false;
                foreach (var pixel in readback.GetPixels32())
                {
                    if (pixel.a > 0)
                    {
                        hasVisiblePixel = true;
                        break;
                    }
                }
                Assert.That(hasVisiblePixel, Is.True);
            }
            finally
            {
                Object.Destroy(readback);
            }
        }

        private sealed class RetirementProbe : System.IDisposable
        {
            internal bool Disposed { get; private set; }

            public void Dispose() => Disposed = true;
        }

        [UnityTest]
        public IEnumerator OversizedTargetsAndDisabledAtlasUseStandaloneTextures()
        {
            using var first = new ManagedRenderTextureSurface();
            using var second = new ManagedRenderTextureSurface();
            Assert.That(first.EnsureExact(new Vector2Int(65, 12), ColorSpace.Gamma, out _), Is.True);
            Assert.That(first.Texture.width, Is.EqualTo(65));
            MilestroConfiguration.Configuration.RenderSurface.EnableAtlas = false;
            Assert.That(second.EnsureExact(new Vector2Int(12, 12), ColorSpace.Gamma, out _), Is.True);
            Assert.That(second.Texture.width, Is.EqualTo(12));
            Assert.That(first.Texture, Is.Not.SameAs(second.Texture));
            yield return null;
        }

        private static IEnumerator WaitForOutput(params ManagedRenderTextureSurface[] surfaces)
        {
            for (var frame = 0; frame < 120; ++frame)
            {
                UnitySkiaRenderTextureSurface.CollectCompletedEventsFromPump();
                var ready = true;
                foreach (var surface in surfaces)
                {
                    ready &= surface.HasCompletedOutput;
                }
                if (ready)
                {
                    yield break;
                }
                yield return null;
            }
            Assert.Fail("Atlas submissions did not complete within 120 frames.");
        }

        private static MilestroImage MakeImage(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            var image = MilestroImage.MakeFromBytes(texture.EncodeToPNG());
            Object.Destroy(texture);
            return image;
        }

        private static UnitySkiaRenderCommandList Draw(MilestroImage image)
        {
            var commands = new UnitySkiaRenderCommandList();
            commands.DrawImage(image, new Rect(0, 0, 12, 12));
            return commands;
        }

        private static Texture2D ReadOutput(ManagedRenderTextureSurface surface)
        {
            var target = RenderTexture.GetTemporary(surface.Width, surface.Height, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            try
            {
                var uv = surface.DisplayUvRect;
                Graphics.Blit(surface.Texture, target, uv.size, uv.position);
                RenderTexture.active = target;
                var readback = new Texture2D(surface.Width, surface.Height, TextureFormat.RGBA32, false, true);
                readback.ReadPixels(new Rect(0, 0, surface.Width, surface.Height), 0, 0);
                readback.Apply();
                return readback;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        private static void AssertSolid(ManagedRenderTextureSurface surface, Color expected)
        {
            var readback = ReadOutput(surface);
            try
            {
                var actual = readback.GetPixel(surface.Width / 2, surface.Height / 2);
                Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.02f));
                Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.02f));
                Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.02f));
                Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.02f));
            }
            finally
            {
                Object.Destroy(readback);
            }
        }
    }
}
