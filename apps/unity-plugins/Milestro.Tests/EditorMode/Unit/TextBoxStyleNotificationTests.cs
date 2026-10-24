using System;
using Milestro.Components.Internal;
using Milestro.Model;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Milestro.Tests
{
    public class TextBoxStyleNotificationTests
    {
        private GameObject root = null!;
        private TextBoxRenderTextureProducer producer = null!;
        private CountingRenderTarget target = null!;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("TextBox style notification test", typeof(RectTransform));
            root.SetActive(false);
            producer = root.AddComponent<TextBoxRenderTextureProducer>();
            target = new CountingRenderTarget();
            producer.DisposeRenderTarget();
            producer.renderTargetFactory = () => target;
            root.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void TextStyleMutationMarksProducerPropertiesChanged()
        {
            var before = target.PropertiesChangedCount;

            producer.textStyle.size = 48f;

            Assert.AreEqual(before + 1, target.PropertiesChangedCount);
        }

        [Test]
        public void FontFamilyMutationMarksProducerPropertiesChanged()
        {
            var before = target.PropertiesChangedCount;

            producer.textStyle.fontFamilies.Add("sans-serif");

            Assert.AreEqual(before + 1, target.PropertiesChangedCount);
        }

        [Test]
        public void ParagraphStyleMutationMarksProducerPropertiesChanged()
        {
            var before = target.PropertiesChangedCount;

            producer.paragraphStyle.textAlign = TextAlign.Center;

            Assert.AreEqual(before + 1, target.PropertiesChangedCount);
        }

        [Test]
        public void ReplacedStyleObjectIsSubscribedAndOldOneIsDropped()
        {
            var oldStyle = producer.textStyle;
            var replacement = new TextStyleSettings();
            producer.textStyle = replacement;
            var before = target.PropertiesChangedCount;

            oldStyle.size = 100f;
            Assert.AreEqual(before, target.PropertiesChangedCount);

            replacement.size = 100f;
            Assert.AreEqual(before + 1, target.PropertiesChangedCount);
        }

        [Test]
        public void NoOpStyleAssignmentDoesNotNotify()
        {
            var before = target.PropertiesChangedCount;

            producer.textStyle.size = 36f;

            Assert.AreEqual(before, target.PropertiesChangedCount);
        }

        private sealed class CountingRenderTarget : ITextBoxRenderTarget
        {
            public Texture? OutputTexture => null;
            public Rect OutputUvRect => new Rect(0f, 0f, 1f, 1f);
            public int OutputWidth => 0;
            public int OutputHeight => 0;
            public bool HasOutput => false;
            public long OutputVersion => 0;
            public Vector2 ScrollOffset => Vector2.zero;
            public Vector2 ContentSize => Vector2.zero;
            public Vector2 ViewportSize => Vector2.zero;
            public Vector2 MaxScrollOffset => Vector2.zero;
            public TextBoxHorizontalScrollState HorizontalScrollState => default;
            internal int PropertiesChangedCount { get; private set; }

            public event Action<Milestro.Skia.UnitySkiaRenderTextureSurface.RenderSubmissionStatus>?
                RenderEventCompleted;

            public void MarkPropertiesChanged()
            {
                ++PropertiesChangedCount;
            }

            public void MarkPaintChanged()
            {
            }

            public bool TryHitLink(Vector2 normalizedViewportPoint, out TextBoxLinkHit hit)
            {
                hit = default;
                return false;
            }

            public bool TryGetContentSnapshot(out TextBoxContentSnapshot snapshot)
            {
                snapshot = default;
                return false;
            }

            public bool TryGetLinkGeometrySnapshot(out TextBoxLinkGeometrySnapshot snapshot, out long generation)
            {
                snapshot = default;
                generation = 0;
                return false;
            }

            public bool Rebuild(TextBoxRenderViewport viewport,
                ColorSpace colorSpace,
                TextBoxRenderTargetSettings settings,
                bool forceText,
                Object? logContext)
            {
                return true;
            }

            public void Dispose()
            {
            }
        }
    }
}
