using Milestro.Components;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Milestro.Tests
{
    public sealed class RenderTextureGraphicTests
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
        public void TextBoxWithoutOutputDoesNotBuildFallbackQuad()
        {
            root = new GameObject("Test Canvas", typeof(RectTransform), typeof(Canvas));
            var textObject = new GameObject("Text Box", typeof(RectTransform));
            textObject.transform.SetParent(root.transform, false);
            ((RectTransform)textObject.transform).sizeDelta = new Vector2(400, 100);
            var textBox = textObject.AddComponent<TextBox>();
            textBox.Texture = null;

            textBox.Rebuild(CanvasUpdate.PreRender);

            Assert.That(textBox.canvasRenderer.GetMesh().vertexCount, Is.Zero);
        }
    }
}
