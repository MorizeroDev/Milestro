using Milestro.Components;
using Milestro.Components.Internal;
using NUnit.Framework;
using UnityEngine;

namespace Milestro.Tests
{
    public sealed class WorldSpaceSlimTextTests
    {
        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TextSetterOnInactiveObjectInitializesInternals()
        {
            root = new GameObject("Slim Text", typeof(RectTransform));
            root.SetActive(false);
            var text = root.AddComponent<WorldSpaceSlimText>();

            text.text = "hello";

            var producer = root.GetComponent<SlimTextRenderTextureProducer>();
            Assert.That(producer, Is.Not.Null);
            Assert.That(producer.text, Is.EqualTo("hello"));
            Assert.That(root.GetComponent<RenderTextureMeshPresenter>(), Is.Not.Null);
            Assert.That(root.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null);
        }

        [Test]
        public void NewComponentsUseEditorPlatformDefaultFontFamily()
        {
#if UNITY_EDITOR_WIN
            const string expectedFamily = "Microsoft YaHei UI";
#elif UNITY_EDITOR_OSX
            const string expectedFamily = "PingFang SC";
#elif UNITY_EDITOR_LINUX
            const string expectedFamily = "Noto Sans CJK SC";
#else
            const string expectedFamily = "system-ui";
#endif
            root = new GameObject("Slim Text", typeof(RectTransform));

            var producer = root.AddComponent<SlimTextRenderTextureProducer>();
            var text = root.AddComponent<WorldSpaceSlimText>();

            Assert.That(producer.fontFamily, Is.EqualTo(expectedFamily));
            Assert.That(text.fontFamily, Is.EqualTo(expectedFamily));
        }

        [Test]
        public void TextSetterReusesConfiguredInternals()
        {
            root = new GameObject("Slim Text", typeof(RectTransform));
            var text = root.AddComponent<WorldSpaceSlimText>();
            text.textureSizePixels = new Vector2Int(64, 16);
            var producer = root.GetComponent<SlimTextRenderTextureProducer>();
            var presenter = root.GetComponent<RenderTextureMeshPresenter>();
            var mesh = root.GetComponent<MeshFilter>().sharedMesh;

            text.text = "updated";

            Assert.That(producer.text, Is.EqualTo("updated"));
            Assert.That(root.GetComponent<SlimTextRenderTextureProducer>(), Is.SameAs(producer));
            Assert.That(root.GetComponent<RenderTextureMeshPresenter>(), Is.SameAs(presenter));
            Assert.That(root.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
        }

        [Test]
        public void TextSetterRestoresDisabledOwnedProducer()
        {
            root = new GameObject("Slim Text", typeof(RectTransform));
            var text = root.AddComponent<WorldSpaceSlimText>();
            var producer = root.GetComponent<SlimTextRenderTextureProducer>();
            producer.enabled = false;

            text.text = "updated";

            Assert.That(producer.enabled, Is.True);
            Assert.That(producer.text, Is.EqualTo("updated"));
        }

        [Test]
        public void TextSetterUpdatesMaxContentTextureSize()
        {
            root = new GameObject("Slim Text", typeof(RectTransform));
            var text = root.AddComponent<WorldSpaceSlimText>();
            text.widthMode = Milestro.Model.TextWidthMode.MaxContent;
            text.text = "A";
            var initialSize = ((RectTransform)root.transform).sizeDelta;

            text.text = "A much longer label";

            var updatedSize = ((RectTransform)root.transform).sizeDelta;
            Assert.That(updatedSize.x, Is.GreaterThan(initialSize.x));
        }
    }
}
