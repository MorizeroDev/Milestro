#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Milestro.Components.Internal;
using Milestro.ResourceManagement;
using Milestro.RichTextParser;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEditor;

namespace Milestro.Tests
{
    public sealed class InlineSpriteTests
    {
        private sealed class CustomBorrow : InlineResourceBorrow
        {
            internal int Returned;

            internal CustomBorrow(ImageResource resource) : base(resource) { }

            protected override void ReturnCore()
            {
                Assert.That(IsDisposed, Is.True);
                ++Returned;
                Dispose();
                throw new InvalidOperationException("custom return failed");
            }
        }

        [Test]
        public void CustomBorrowUsesBaseClassReturnGuarantees()
        {
            var resource = new ImageResource(sprite, suggestedHeight: InlineResourceLength.Em(2));
            var borrow = new CustomBorrow(resource);
            Assert.That(borrow.Resource, Is.EqualTo(resource));
            Assert.That(borrow.IsDisposed, Is.False);
            Assert.Throws<InvalidOperationException>(() => ((IDisposable)borrow).Dispose());
            borrow.Dispose();
            Assert.That(borrow.Returned, Is.EqualTo(1));
            Assert.That(sprite.rect.width, Is.EqualTo(8));
        }

        [Test]
        public void FactoryRequiresReturnCallback()
        {
            Assert.Throws<ArgumentNullException>(() => InlineResourceBorrow.Create(new ImageResource(sprite), null!));
        }

        private sealed class Provider : ISpriteResourceProvider, IImageResourceProvider
        {
            internal ImageResource? Resource;
            internal TaskCompletionSource<ImageResource?>? Pending;
            internal readonly List<SpriteRequest> Requests = new List<SpriteRequest>();
            internal int Released;
            internal object? ReturnedToken;
            internal object? Token;

            public async Task<InlineResourceBorrow?> BorrowAsync(SpriteRequest request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                return await BorrowAsync(new ImageRequest(""), cancellationToken);
            }

            public async Task<InlineResourceBorrow?> BorrowAsync(ImageRequest request, CancellationToken cancellationToken)
            {
                var resource = await (Pending?.Task ?? Task.FromResult(Resource));
                var token = Token;
                return resource.HasValue ? InlineResourceBorrow.Create(resource.Value, () =>
                {
                    ++Released;
                    ReturnedToken = token;
                }) : null;
            }

        }

        private Texture2D texture = null!;
        private Sprite sprite = null!;
        private ISpriteResourceProvider previousSpriteProvider = null!;
        private IImageResourceProvider previousImageProvider = null!;

        [SetUp]
        public void SetUp()
        {
            texture = new Texture2D(8, 4);
            sprite = Sprite.Create(texture, new Rect(0, 0, 8, 4), new Vector2(.25f, .75f), 32);
            previousSpriteProvider = SpriteResources.Provider;
            previousImageProvider = ImageResources.Providers.DefaultProvider;
        }

        [TearDown]
        public void TearDown()
        {
            SpriteResources.Provider = previousSpriteProvider;
            ImageResources.Providers.DefaultProvider = previousImageProvider;
            UnityEngine.Object.DestroyImmediate(sprite);
            UnityEngine.Object.DestroyImmediate(texture);
        }

        private static ParagraphPayload Parse(string source)
        {
            var parser = new RichTextParser.RichTextParser();
            parser.ParseText(source);
            return parser.ConvertToSegments();
        }

        [Test]
        public void ProviderReceivesAttributesAndStructuredContentWithoutParsingChildrenAsText()
        {
            var provider = new Provider { Resource = new ImageResource(sprite) };
            SpriteResources.Provider = provider;
            const string content = "before<sprite pack=\"a&amp;b\" width=\"24\" style=\"height: 1em\">\n" +
                "<item rarity=\"rare\">sword &amp; shield</item><!--keep--><![CDATA[<literal>]]>\n</sprite>after";
            var payload = Parse(content);
            Assert.That(provider.Requests, Is.Empty);
            Assert.That(payload.Placeholders, Has.Count.EqualTo(1));
            Assert.That(payload.Placeholders[0].StartUtf16, Is.EqualTo(6));
            Assert.That(payload.Blocks[0].LengthUtf16, Is.EqualTo(12));
            using var session = new InlineImageSession();
            session.Bind(payload, content, "xml");
            var request = provider.Requests[0];
            Assert.That(request.Attributes["pack"], Is.EqualTo("a&b"));
            Assert.That(request.Attributes["width"], Is.EqualTo("24"));
            Assert.That(request.Attributes["style"], Is.EqualTo("height: 1em"));
            var document = new XmlDocument { PreserveWhitespace = true };
            document.LoadXml("<root>" + request.Content + "</root>");
            Assert.That(document.SelectSingleNode("/root/item")!.InnerText, Is.EqualTo("sword & shield"));
            Assert.That(document.SelectSingleNode("/root/item/@rarity")!.Value, Is.EqualTo("rare"));
            Assert.That(request.Content, Does.Contain("<!--keep-->"));
            Assert.That(request.Content, Does.Contain("<![CDATA[<literal>]]>"));
            Assert.That(request.Content, Does.StartWith("\n"));
        }

        [TestCase("<sprite/>")]
        [TestCase("<sprite></sprite>")]
        public void EmptySpritesNeedNoPrescribedSelector(string content)
        {
            var request = Parse(content).Placeholders[0].SpriteRequest!;
            Assert.That(request.Attributes, Is.Empty);
            Assert.That(request.Content, Is.Empty);
        }

        [Test]
        public async Task BorrowReturnsToOriginalOwnerExactlyOnceWithoutDestroyingSprite()
        {
            var token = new object();
            var owner = new Provider { Resource = new ImageResource(sprite), Token = token };
            SpriteResources.Provider = owner;
            var lease = await SpriteResources.BorrowAsync(new SpriteRequest(new Dictionary<string, string>(), ""));
            var replacement = new Provider();
            SpriteResources.Provider = replacement;
            lease!.Dispose();
            lease.Dispose();
            Assert.That(owner.Released, Is.EqualTo(1));
            Assert.That(owner.ReturnedToken, Is.SameAs(token));
            Assert.That(replacement.Released, Is.Zero);
            Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(8, 4)));
        }

        [Test]
        public void MeasurementsReuseBorrowButOccurrencesAndContentChangesDoNot()
        {
            var provider = new Provider { Resource = new ImageResource(sprite) };
            SpriteResources.Provider = provider;
            const string content = "<sprite>coin</sprite><sprite>coin</sprite>";
            using var session = new InlineImageSession();
            session.Bind(Parse(content), content, "xml");
            session.Bind(Parse(content), content, "xml");
            Assert.That(provider.Requests, Has.Count.EqualTo(2));
            const string replacement = "<sprite>gem</sprite>";
            session.Bind(Parse(replacement), replacement, "xml");
            Assert.That(provider.Released, Is.EqualTo(2));
            Assert.That(provider.Requests[2].Content, Is.EqualTo("gem"));
            session.Dispose();
            Assert.That(provider.Released, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator LateCompletionIsReturnedWithoutUpdatingDisposedSession()
        {
            var provider = new Provider { Pending = new TaskCompletionSource<ImageResource?>() };
            SpriteResources.Provider = provider;
            var session = new InlineImageSession();
            session.Bind(Parse("<sprite/>"), "<sprite/>", "xml");
            session.Dispose();
            provider.Pending.SetResult(new ImageResource(sprite));
            yield return null;
            Assert.That(provider.Released, Is.EqualTo(1));
            Assert.That(session.Changed, Is.False);
        }

        [UnityTest]
        public IEnumerator AsyncSizeBecomesAvailableAndRequestsRelayout()
        {
            var provider = new Provider { Pending = new TaskCompletionSource<ImageResource?>() };
            SpriteResources.Provider = provider;
            using var session = new InlineImageSession();
            var payload = Parse("<sprite/>");
            session.Bind(payload, "<sprite/>", "xml");
            var style = payload.Placeholders[0].Presentation!;
            style.Resolve(10);
            Assert.That(style.ContentRect.size, Is.EqualTo(new Vector2(10, 10)));
            provider.Pending.SetResult(new ImageResource(sprite, suggestedHeight: InlineResourceLength.Em(2)));
            yield return null;
            Assert.That(session.Changed, Is.True);
            style.Resolve(10);
            Assert.That(style.ContentRect.size, Is.EqualTo(new Vector2(40, 20)));
        }

        [TestCase("sprite", "", 40, 20)]
        [TestCase("img", "", 40, 20)]
        [TestCase("sprite", "width=\"30\"", 30, 15)]
        [TestCase("img", "height=\"8\"", 16, 8)]
        [TestCase("sprite", "width=\"30\" height=\"7\"", 30, 7)]
        [TestCase("img", "width=\"30\" style=\"width: auto; height: 3em\"", 60, 30)]
        public void TagDimensionsOverrideProviderSuggestions(string tag, string attributes, float width, float height)
        {
            var provider = new Provider { Resource = new ImageResource(sprite, suggestedHeight: InlineResourceLength.Em(2)) };
            SpriteResources.Provider = provider;
            ImageResources.Providers.DefaultProvider = provider;
            var content = $"<{tag} src=\"coin\" {attributes}/>";
            using var session = new InlineImageSession();
            var payload = Parse(content);
            session.Bind(payload, content, "xml");
            var style = payload.Placeholders[0].Presentation!;
            style.Resolve(10);
            Assert.That(style.ContentRect.size, Is.EqualTo(new Vector2(width, height)));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ProviderDimensionsCanBePixelsOrEmAndCanOverrideAspect(bool em)
        {
            var provider = new Provider
            {
                Resource = new ImageResource(sprite, suggestedWidth: InlineResourceLength.Pixels(13),
                    suggestedHeight: em ? InlineResourceLength.Em(2) : InlineResourceLength.Pixels(7))
            };
            SpriteResources.Provider = provider;
            using var session = new InlineImageSession();
            var payload = Parse("<sprite/>");
            session.Bind(payload, "<sprite/>", "xml");
            var style = payload.Placeholders[0].Presentation!;
            style.Resolve(10);
            Assert.That(style.ContentRect.size, Is.EqualTo(new Vector2(13, em ? 20 : 7)));
            style.Resolve(20);
            Assert.That(style.ContentRect.size, Is.EqualTo(new Vector2(13, em ? 40 : 7)));
        }

        [Test]
        public void InvalidSuggestedDimensionsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => InlineResourceLength.Em(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => InlineResourceLength.Pixels(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ImageResource(sprite, suggestedHeight: default(InlineResourceLength)));
        }

        [Test]
        public async Task InvalidHandleIsReturnedToOwner()
        {
            var provider = new Provider { Resource = default(ImageResource) };
            SpriteResources.Provider = provider;
            try
            {
                await SpriteResources.BorrowAsync(new SpriteRequest(new Dictionary<string, string>(), ""));
                Assert.Fail("Invalid Sprite was accepted.");
            }
            catch (InvalidOperationException) { }
            Assert.That(provider.Released, Is.EqualTo(1));
        }

        [Test]
        public void MissingResourceIsNotRetriedOnMeasurement()
        {
            var provider = new Provider();
            SpriteResources.Provider = provider;
            using var session = new InlineImageSession();
            session.Bind(Parse("<sprite/>"), "<sprite/>", "xml");
            session.Bind(Parse("<sprite/>"), "<sprite/>", "xml");
            Assert.That(provider.Requests, Has.Count.EqualTo(1));
            session.Dispose();
            session.Bind(Parse("<sprite/>"), "<sprite/>", "xml");
            Assert.That(provider.Requests, Has.Count.EqualTo(2));
            Assert.That(provider.Released, Is.Zero);
        }

        [Test]
        public void OneProviderHandlesBothRequestShapesAndReturnsIndependentBorrows()
        {
            var provider = new Provider { Resource = new ImageResource(sprite) };
            SpriteResources.Provider = provider;
            ImageResources.Providers.DefaultProvider = provider;
            const string content = "<img src=\"coin\"/><sprite><item id=\"coin\"/></sprite>";
            var payload = Parse(content);
            using var session = new InlineImageSession();
            session.Bind(payload, content, "xml");
            var imageBorrow = payload.Placeholders[0].Presentation!.Binding!.Borrow!;
            var spriteBorrow = payload.Placeholders[1].Presentation!.Binding!.Borrow!;
            Assert.That(imageBorrow, Is.Not.SameAs(spriteBorrow));
            Assert.That(imageBorrow.Resource.Sprite, Is.SameAs(spriteBorrow.Resource.Sprite));
            Assert.That(provider.Requests, Has.Count.EqualTo(1));
            session.Dispose();
            imageBorrow.Dispose();
            spriteBorrow.Dispose();
            Assert.That(provider.Released, Is.EqualTo(2));
            Assert.That(sprite.rect.width, Is.EqualTo(8));
        }

        [Test]
        public void TmproSpriteBindsThroughSharedProviderAndReturnsOnContentChange()
        {
            var provider = new Provider { Resource = new ImageResource(sprite, suggestedHeight: InlineResourceLength.Em(2)) };
            SpriteResources.Provider = provider;
            const string content = "<sprite=\"items\" name=coin>";
            var parser = new Milestro.RichText.TMPro.TMProRichTextParser();
            var payload = parser.Parse(content, new RichTextParseContext());
            using var session = new InlineImageSession();
            session.Bind(payload, content, "tmpro");
            session.Bind(parser.Parse(content, new RichTextParseContext()), content, "tmpro");
            Assert.That(provider.Requests, Has.Count.EqualTo(1));
            Assert.That(provider.Requests[0].Attributes["asset"], Is.EqualTo("items"));
            var presentation = payload.Placeholders[0].Presentation!;
            presentation.Resolve(10);
            Assert.That(presentation.ContentRect.size, Is.EqualTo(new Vector2(40, 20)));
            session.Bind(Parse("plain"), "plain", "xml");
            Assert.That(provider.Released, Is.EqualTo(1));
        }

        [Test]
        public void ReturnIsIdempotentEvenWithReentrancyOrCallbackFailure()
        {
            var returned = 0;
            InlineResourceBorrow? borrow = null;
            borrow = InlineResourceBorrow.Create(new ImageResource(sprite), () =>
            {
                ++returned;
                borrow!.Dispose();
                throw new InvalidOperationException("return failed");
            });
            Assert.Throws<InvalidOperationException>(() => borrow.Dispose());
            Assert.That(borrow.IsDisposed, Is.True);
            borrow.Dispose();
            Assert.That(returned, Is.EqualTo(1));
        }

        [Test]
        public void AlreadyReturnedBorrowIsRejected()
        {
            var borrow = InlineResourceBorrow.Create(new ImageResource(sprite), () => { });
            borrow.Dispose();
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await InlineResourceBorrow.AcceptAsync(Task.FromResult<InlineResourceBorrow?>(borrow), CancellationToken.None));
        }

        [TestCase("img", false, 20, 10)]
        [TestCase("sprite", false, 20, 10)]
        [TestCase("img", true, 24, 12)]
        [TestCase("sprite", true, 24, 12)]
        public void DefaultAndWidthOnlySuggestionsUseIntrinsicRatio(string tag, bool suggestWidth, float width, float height)
        {
            var provider = new Provider { Resource = new ImageResource(sprite,
                suggestedWidth: suggestWidth ? InlineResourceLength.Pixels(24) : (InlineResourceLength?)null) };
            SpriteResources.Provider = provider;
            ImageResources.Providers.DefaultProvider = provider;
            var content = $"<{tag} src=\"coin\"/>";
            var payload = Parse(content);
            using var session = new InlineImageSession();
            session.Bind(payload, content, "xml");
            var style = payload.Placeholders[0].Presentation!;
            style.Resolve(10);
            Assert.That(style.ContentRect.size, Is.EqualTo(new Vector2(width, height)));
        }

        [Test]
        public void GraphicUsesSpriteMeshAndInterpolatesUvsAtClipEdges()
        {
            var folder = "Assets/SpriteMeshTest" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            var generated = new Texture2D(64, 64);
            var view = new GameObject("Sprite mesh test", typeof(RectTransform), typeof(CanvasRenderer), typeof(InlineImageGraphic));
            try
            {
                var pixels = new Color32[64 * 64];
                for (var row = 0; row < 64; ++row)
                    for (var column = 0; column < 64; ++column)
                        pixels[row * 64 + column] = column + row < 64 ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                generated.SetPixels32(pixels);
                generated.Apply();
                var path = folder + "/triangle.png";
                File.WriteAllBytes(path, generated.EncodeToPNG());
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.isReadable = true;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 32;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.Tight;
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(.25f, .75f);
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
                var resource = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                var graphic = view.GetComponent<InlineImageGraphic>();
                graphic.Present(resource, new Rect(0, 0, 80, 40), new Rect(20, 0, 40, 40),
                    new InlineObjectStyle { Fit = InlineObjectFit.Fill }, 1);
                Assert.That(graphic.mainTexture, Is.SameAs(resource.texture));
                using var helper = new VertexHelper();
                typeof(InlineImageGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)!
                    .Invoke(graphic, new object[] { helper });
                var vertices = new List<UIVertex>();
                helper.GetUIVertexStream(vertices);
                Assert.That(vertices, Is.Not.Empty);
                foreach (var vertex in vertices)
                {
                    Assert.That(vertex.position.x, Is.InRange(19.999f, 60.001f));
                    Assert.That(vertex.position.y, Is.InRange(-.001f, 32f));
                    Assert.That(vertex.position.x / 80 + vertex.position.y / 40, Is.LessThanOrEqualTo(1.05f));
                    Assert.That(vertex.uv0.x, Is.EqualTo(vertex.position.x / 80).Within(.001f));
                    Assert.That(vertex.uv0.y, Is.EqualTo(vertex.position.y / 40).Within(.001f));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(view);
                UnityEngine.Object.DestroyImmediate(generated);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void SubTextureSpritePreservesUvRegionAndPivotWhileClipping()
        {
            var region = Sprite.Create(texture, new Rect(2, 1, 4, 2), new Vector2(.25f, .75f), 32);
            var view = new GameObject("Sprite region test", typeof(RectTransform), typeof(CanvasRenderer), typeof(InlineImageGraphic));
            try
            {
                var graphic = view.GetComponent<InlineImageGraphic>();
                graphic.Present(region, new Rect(0, 0, 80, 40), new Rect(20, 10, 40, 20),
                    new InlineObjectStyle { Fit = InlineObjectFit.Fill }, 1);
                using var helper = new VertexHelper();
                typeof(InlineImageGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)!
                    .Invoke(graphic, new object[] { helper });
                var vertices = new List<UIVertex>();
                helper.GetUIVertexStream(vertices);
                Assert.That(vertices, Is.Not.Empty);
                foreach (var vertex in vertices)
                {
                    Assert.That(vertex.position.x, Is.InRange(19.999f, 60.001f));
                    Assert.That(vertex.position.y, Is.InRange(9.999f, 30.001f));
                    Assert.That(vertex.uv0.x, Is.EqualTo(.25f + vertex.position.x / 160).Within(.001f));
                    Assert.That(vertex.uv0.y, Is.EqualTo(.25f + vertex.position.y / 80).Within(.001f));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(view);
                UnityEngine.Object.DestroyImmediate(region);
            }
        }
    }
}
