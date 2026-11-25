#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Milestro.Components.Internal;
using Milestro.Extensions;
using Milestro.Model;
using Milestro.ResourceManagement;
using Milestro.RichTextParser;
using Milestro.Skia.TextLayout;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Milestro.Tests
{
    public sealed class InlineImageTests
    {
        private sealed class Provider : IImageResourceProvider
        {
            internal TaskCompletionSource<ImageResource?>? Pending;
            internal ImageResource Resource;
            internal int Acquired;
            internal int Released;
            internal string? Source;
            public Task<ImageResource?> AcquireAsync(string source, CancellationToken cancellationToken)
            {
                ++Acquired;
                Source = source;
                return Pending?.Task ?? Task.FromResult<ImageResource?>(Resource);
            }
            public void Release(ImageResource resource) { ++Released; }
        }

        private Texture2D texture = null!;
        private Sprite sprite = null!;
        private IImageResourceProvider previousDefault = null!;

        [SetUp]
        public void SetUp()
        {
            texture = new Texture2D(8, 4);
            sprite = Sprite.Create(texture, new Rect(0, 0, 8, 4), Vector2.one * 0.5f);
            previousDefault = ImageResources.Providers.DefaultProvider;
        }

        [TearDown]
        public void TearDown()
        {
            ImageResources.Providers.DefaultProvider = previousDefault;
            ImageResources.Providers.Unregister("inline-test");
            UnityEngine.Object.DestroyImmediate(sprite);
            UnityEngine.Object.DestroyImmediate(texture);
        }

        private static ParagraphPayload Parse(string text)
        {
            var parser = new RichTextParser.RichTextParser();
            parser.ParseText(text);
            return parser.ConvertToSegments();
        }

        [Test]
        public async Task LeaseReleasesOriginalProviderExactlyOnceAfterReplacement()
        {
            var original = new Provider { Resource = new ImageResource(sprite, "original") };
            var replacement = new Provider { Resource = new ImageResource(sprite) };
            ImageResources.Providers.Register("inline-test", original);
            var lease = await ImageResources.AcquireAsync("INLINE-TEST://coin");
            ImageResources.Providers.Replace("inline-test", replacement);
            Assert.That(lease!.Provider, Is.SameAs(original));
            lease.Dispose();
            lease.Dispose();
            Assert.That(original.Released, Is.EqualTo(1));
            Assert.That(replacement.Released, Is.Zero);
            Assert.That(sprite, Is.Not.Null);
        }

        [Test]
        public async Task CancellationAfterAcquisitionReleasesLateResource()
        {
            var provider = new Provider { Pending = new TaskCompletionSource<ImageResource?>() };
            ImageResources.Providers.DefaultProvider = provider;
            using var cancellation = new CancellationTokenSource();
            var acquisition = ImageResources.AcquireAsync("/business/key", cancellation.Token);
            cancellation.Cancel();
            provider.Pending.SetResult(new ImageResource(sprite));
            try { await acquisition; Assert.Fail("Cancellation was ignored."); }
            catch (OperationCanceledException) { }
            Assert.That(provider.Source, Is.EqualTo("/business/key"));
            Assert.That(provider.Released, Is.EqualTo(1));
        }

        [Test]
        public void SessionReusesBindingsAcrossMeasurementsButSeparatesOccurrences()
        {
            var provider = new Provider { Resource = new ImageResource(sprite) };
            ImageResources.Providers.DefaultProvider = provider;
            using var session = new InlineImageSession();
            const string content = "<img src=\"coin\"/><img src=\"coin\"/>";
            var first = Parse(content);
            session.Bind(first, content, "xml");
            var second = Parse(content);
            session.Bind(second, content, "xml");
            Assert.That(provider.Acquired, Is.EqualTo(2));
            Assert.That(first.Placeholders[0].Presentation!.Binding,
                Is.SameAs(second.Placeholders[0].Presentation!.Binding));
            Assert.That(first.Placeholders[0].Presentation!.Binding,
                Is.Not.SameAs(first.Placeholders[1].Presentation!.Binding));
            session.Bind(Parse("plain"), "plain", "xml");
            Assert.That(provider.Released, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator DisposedSessionReleasesLateCompletion()
        {
            var provider = new Provider { Pending = new TaskCompletionSource<ImageResource?>() };
            ImageResources.Providers.DefaultProvider = provider;
            var session = new InlineImageSession();
            session.Bind(Parse("<img src=\"coin\"/>"), "image", "xml");
            session.Dispose();
            provider.Pending.SetResult(new ImageResource(sprite));
            yield return null;
            Assert.That(provider.Released, Is.EqualTo(1));
            Assert.That(session.Changed, Is.False);
        }

        [TestCase(WritingMode.HorizontalTb)]
        [TestCase(WritingMode.VerticalRl)]
        public void CssSizesAndMarginsParticipateInNativeLayout(WritingMode mode)
        {
            var payload = Parse("<placeholder width=\"9\" height=\"9\" style=\"width: 2em; height: 1em; margin: 1px 2px 3px 4px\"/>");
            using var paragraphStyle = new ParagraphStyle { WritingMode = mode };
            using var textStyle = new TextStyle { FontSize = 20 };
            using var paragraph = payload.ToParagraph(paragraphStyle, textStyle);
            paragraph.LayoutWithInlineExtent(200);
            var box = paragraph.GetRectsForPlaceholders()[0];
            Assert.That(box.width, Is.EqualTo(46).Within(0.01f));
            Assert.That(box.height, Is.EqualTo(24).Within(0.01f));
            Assert.That(payload.Placeholders[0].Presentation!.ContentRect, Is.EqualTo(new Rect(4, 1, 40, 20)));
        }

        [TestCase("<img src=\"coin\" width=\"40\" height=\"20\"/>")]
        [TestCase("<placeholder width=\"40\" height=\"20\"/>")]
        public void StandaloneInlineObjectsContributeToIntrinsicMeasurement(string content)
        {
            var settings = new TextBoxRenderTargetSettings(content, new Margin(),
                new ParagraphStyleSettings(), new TextStyleSettings { size = 20f },
                XmlRichTextParser.Id, TextBoxWrapMode.Wrap, TextOverflow.Clip);
            using var measurement = new TextBoxIntrinsicMeasurement(settings);

            var metrics = measurement.Measure(20f, float.PositiveInfinity);

            Assert.That(metrics.MaxContentWidth, Is.EqualTo(40f).Within(.01f));
            Assert.That(metrics.NaturalHeight, Is.GreaterThanOrEqualTo(20f));
        }

        [Test]
        public void AutoImageSizeUsesAspectRatioWithoutLoadingFromParser()
        {
            var provider = new Provider { Resource = new ImageResource(sprite) };
            ImageResources.Providers.DefaultProvider = provider;
            const string content = "<img src=\"coin\" style=\"height: 2em; opacity: .4; object-fit: cover; object-position: right bottom; visibility: hidden\"/>";
            var payload = Parse(content);
            Assert.That(provider.Acquired, Is.Zero);
            using var session = new InlineImageSession();
            session.Bind(payload, content, "xml");
            var style = payload.Placeholders[0].Presentation!;
            style.Resolve(10);
            Assert.That(style.ContentRect.size, Is.EqualTo(new Vector2(40, 20)));
            Assert.That(style.Visible, Is.False);
            Assert.That(style.Opacity, Is.EqualTo(.4f));
            Assert.That(style.Fit, Is.EqualTo(InlineObjectFit.Cover));
            Assert.That(style.Position, Is.EqualTo(Vector2.one));
        }

        [TestCase(InlineObjectFit.Contain, .5f, .5f, 0f, 5f, 20f, 15f, 0f, 1f)]
        [TestCase(InlineObjectFit.Contain, 1f, 1f, 0f, 0f, 20f, 10f, 0f, 1f)]
        [TestCase(InlineObjectFit.Cover, 1f, 1f, 0f, 0f, 20f, 20f, .5f, 1f)]
        [TestCase(InlineObjectFit.Fill, .5f, .5f, 0f, 0f, 20f, 20f, 0f, 1f)]
        [TestCase(InlineObjectFit.None, .5f, .5f, 6f, 8f, 14f, 12f, 0f, 1f)]
        [TestCase(InlineObjectFit.ScaleDown, .5f, .5f, 6f, 8f, 14f, 12f, 0f, 1f)]
        public void GraphicAppliesObjectFitPositionClippingAndOpacity(InlineObjectFit fit,
            float positionX, float positionY, float expectedLeft, float expectedBottom,
            float expectedRight, float expectedTop, float expectedUvLeft, float expectedUvRight)
        {
            var viewObject = new GameObject("Inline image graphic test", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(InlineImageGraphic));
            try
            {
                var graphic = viewObject.GetComponent<InlineImageGraphic>();
                var style = new InlineObjectStyle
                {
                    Fit = fit,
                    Position = new Vector2(positionX, positionY),
                    Opacity = .4f
                };
                var box = new Rect(0, 0, 20, 20);
                graphic.Present(sprite, box, box, style, 1f);
                using var vertices = new VertexHelper();
                var populate = typeof(InlineImageGraphic).GetMethod("OnPopulateMesh",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                Assert.That(populate, Is.Not.Null);
                populate!.Invoke(graphic, new object[] { vertices });
                var stream = new List<UIVertex>();
                vertices.GetUIVertexStream(stream);

                Assert.That(stream, Has.Count.EqualTo(6));
                var minimum = stream[0].position;
                var maximum = minimum;
                var uvMinimum = stream[0].uv0;
                var uvMaximum = uvMinimum;
                foreach (var vertex in stream)
                {
                    minimum = Vector3.Min(minimum, vertex.position);
                    maximum = Vector3.Max(maximum, vertex.position);
                    uvMinimum = Vector2.Min(uvMinimum, vertex.uv0);
                    uvMaximum = Vector2.Max(uvMaximum, vertex.uv0);
                }
                Assert.That(minimum.x, Is.EqualTo(expectedLeft).Within(.01f));
                Assert.That(minimum.y, Is.EqualTo(expectedBottom).Within(.01f));
                Assert.That(maximum.x, Is.EqualTo(expectedRight).Within(.01f));
                Assert.That(maximum.y, Is.EqualTo(expectedTop).Within(.01f));
                Assert.That(uvMinimum.x, Is.EqualTo(expectedUvLeft).Within(.001f));
                Assert.That(uvMaximum.x, Is.EqualTo(expectedUvRight).Within(.001f));
                Assert.That(stream[0].color.a / 255f, Is.EqualTo(.4f).Within(.01f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(viewObject);
            }
        }

        [TestCase("<img width=\"10\" height=\"10\"/>")]
        [TestCase("<placeholder src=\"coin\" width=\"10\" height=\"10\"/>")]
        [TestCase("<placeholder style=\"width:auto;height:1em\"/>")]
        public void TagResponsibilitiesAreStrict(string content)
        {
            Assert.That(() => Parse(content), Throws.Exception);
        }

        [Test]
        public void DataUriDecodesBinaryEscapesAndBase64()
        {
            Assert.That(DataUriImageResourceProvider.DecodeDataUri("data:image/png,%00%FF+"),
                Is.EqualTo(new byte[] { 0, 255, 43 }));
            Assert.That(DataUriImageResourceProvider.DecodeDataUri("DATA:image/png;base64,AP8%3D"),
                Is.EqualTo(new byte[] { 0, 255 }));
            Assert.That(() => DataUriImageResourceProvider.DecodeDataUri("data:image/png,%GG"), Throws.ArgumentException);
            Assert.That(() => DataUriImageResourceProvider.DecodeDataUri("data:text/plain,abc"), Throws.ArgumentException);
        }

        [Test]
        public void VisibilityInheritsAndCanBeOverriddenOnObject()
        {
            var payload = Parse("<span style=\"visibility:hidden\"><placeholder width=\"10\" height=\"10\"/>" +
                "<img src=\"coin\" style=\"visibility:visible\"/></span>");
            Assert.That(payload.Placeholders[0].Presentation!.Visible, Is.False);
            Assert.That(payload.Placeholders[1].Presentation!.Visible, Is.True);
        }

        [Test]
        public async Task SkiaDataUriPreservesPixelColorsAndVerticalOrientation()
        {
            var source = new Texture2D(1, 2, TextureFormat.RGBA32, false);
            try
            {
                source.SetPixel(0, 0, Color.red);
                source.SetPixel(0, 1, Color.blue);
                source.Apply();
                var uri = "data:image/png;base64," + Convert.ToBase64String(source.EncodeToPNG());
                using var lease = await ImageResources.AcquireAsync(uri);
                Assert.That(lease!.Resource.IntrinsicSize, Is.EqualTo(new Vector2(1, 2)));
                var output = lease.Resource.Sprite.texture;
                var target = RenderTexture.GetTemporary(1, 2, 0, RenderTextureFormat.ARGB32);
                var previous = RenderTexture.active;
                try
                {
                    Graphics.Blit(output, target);
                    RenderTexture.active = target;
                    source.ReadPixels(new Rect(0, 0, 1, 2), 0, 0);
                    source.Apply();
                    Assert.That(source.GetPixel(0, 0).r, Is.GreaterThan(.95f));
                    Assert.That(source.GetPixel(0, 1).b, Is.GreaterThan(.95f));
                }
                finally
                {
                    RenderTexture.active = previous;
                    RenderTexture.ReleaseTemporary(target);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task SvgDataUriUsesSvgRenderer(bool base64)
        {
            const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 12 6\"><rect width=\"12\" height=\"6\" fill=\"red\"/></svg>";
            var source = base64
                ? "data:image/svg+xml;base64," + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(svg))
                : "data:image/svg+xml," + Uri.EscapeDataString(svg);
            using var lease = await ImageResources.AcquireAsync(source);
            Assert.That(lease!.Resource.IntrinsicSize, Is.EqualTo(new Vector2(12, 6)));
            var target = RenderTexture.GetTemporary(12, 6);
            var previous = RenderTexture.active;
            var sample = new Texture2D(12, 6);
            try
            {
                Graphics.Blit(lease.Resource.Sprite.texture, target);
                RenderTexture.active = target;
                sample.ReadPixels(new Rect(0, 0, 12, 6), 0, 0);
                sample.Apply();
                Assert.That(sample.GetPixel(6, 3).r, Is.GreaterThan(.95f));
                Assert.That(sample.GetPixel(6, 3).g, Is.LessThan(.05f));
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(sample);
            }
        }

        [Test]
        public async Task FileProviderLoadsAndReleasesOwnedResources()
        {
            var path = System.IO.Path.Combine(Application.temporaryCachePath, "milestro-inline-image-" + Guid.NewGuid().ToString("N") + ".png");
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            try
            {
                var lease = await ImageResources.AcquireAsync(new Uri(path).AbsoluteUri);
                Assert.That(lease!.Resource.IntrinsicSize, Is.EqualTo(new Vector2(8, 4)));
                var loaded = lease.Resource.Sprite;
                var loadedTexture = loaded.texture;
                lease.Dispose();
                Assert.That(!loaded, Is.True);
                Assert.That(!loadedTexture, Is.True);
            }
            finally { System.IO.File.Delete(path); }
        }
    }
}
