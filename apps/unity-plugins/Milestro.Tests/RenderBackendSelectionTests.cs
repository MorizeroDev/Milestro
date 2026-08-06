using System.Reflection;
using Milestro.Components;
using Milestro.Components.Internal;
using Milestro.Skia;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Milestro.Tests
{
    public class RenderBackendSelectionTests
    {
        [Test]
        public void ComponentBackendPropertiesPreserveExplicitVulkanAndNormalizeInvalidValues()
        {
            var textInputObject = new GameObject("TextInput backend", typeof(RectTransform));
            var slimTextObject = new GameObject("SlimText backend", typeof(RectTransform));
            var textBoxObject = new GameObject("TextBox backend", typeof(RectTransform));
            var worldSpaceObject = new GameObject("WorldSpaceSlimText backend", typeof(RectTransform));
            textInputObject.SetActive(false);
            slimTextObject.SetActive(false);
            textBoxObject.SetActive(false);
            worldSpaceObject.SetActive(false);

            try
            {
                var textInput = textInputObject.AddComponent<TextInput>();
                var slimText = slimTextObject.AddComponent<SlimTextRenderTextureProducer>();
                var textBox = textBoxObject.AddComponent<TextBoxRenderTextureProducer>();
                var worldSpace = worldSpaceObject.AddComponent<WorldSpaceSlimText>();

                textInput.backend = UnitySkiaGraphicsBackend.Vulkan;
                slimText.backend = UnitySkiaGraphicsBackend.Vulkan;
                textBox.backend = UnitySkiaGraphicsBackend.Vulkan;
                worldSpace.backend = UnitySkiaGraphicsBackend.Vulkan;

                Assert.That(textInput.backend, Is.EqualTo(UnitySkiaGraphicsBackend.Vulkan));
                Assert.That(slimText.backend, Is.EqualTo(UnitySkiaGraphicsBackend.Vulkan));
                Assert.That(textBox.backend, Is.EqualTo(UnitySkiaGraphicsBackend.Vulkan));
                Assert.That(worldSpace.backend, Is.EqualTo(UnitySkiaGraphicsBackend.Vulkan));

                var invalid = (UnitySkiaGraphicsBackend)int.MaxValue;
                textInput.backend = invalid;
                slimText.backend = invalid;
                textBox.backend = invalid;
                worldSpace.backend = invalid;

                Assert.That(textInput.backend, Is.EqualTo(UnitySkiaGraphicsBackend.Auto));
                Assert.That(slimText.backend, Is.EqualTo(UnitySkiaGraphicsBackend.Auto));
                Assert.That(textBox.backend, Is.EqualTo(UnitySkiaGraphicsBackend.Auto));
                Assert.That(worldSpace.backend, Is.EqualTo(UnitySkiaGraphicsBackend.Auto));
            }
            finally
            {
                Object.DestroyImmediate(textInputObject);
                Object.DestroyImmediate(slimTextObject);
                Object.DestroyImmediate(textBoxObject);
                Object.DestroyImmediate(worldSpaceObject);
            }
        }

        [Test]
        public void WorldSpaceSlimTextForwardsBackendToOwnedProducer()
        {
            var owner = new GameObject("WorldSpaceSlimText backend forwarding", typeof(RectTransform));
            owner.SetActive(false);

            try
            {
                var worldSpace = owner.AddComponent<WorldSpaceSlimText>();
                worldSpace.backend = UnitySkiaGraphicsBackend.Vulkan;

                var producer = owner.GetComponent<SlimTextRenderTextureProducer>();
                Assert.That(producer, Is.Not.Null);
                Assert.That(producer.backend, Is.EqualTo(UnitySkiaGraphicsBackend.Vulkan));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void RenderTextureGraphicDoesNotDrawWhitePlaceholderWithoutOutput()
        {
            var owner = new GameObject("TextBox without output", typeof(RectTransform), typeof(CanvasRenderer));
            owner.SetActive(false);

            try
            {
                var graphic = owner.AddComponent<TextInput>();
                using var vertices = new VertexHelper();

                typeof(RenderTextureGraphic)
                    .GetMethod("OnPopulateMesh",
                        BindingFlags.Instance | BindingFlags.NonPublic,
                        null,
                        new[] { typeof(VertexHelper) },
                        null)!
                    .Invoke(graphic, new object[] { vertices });

                Assert.That(vertices.currentVertCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void SlimTextOnValidateReplacesTargetWhenSerializedBackendChanges()
        {
            var owner = new GameObject("SlimText inspector backend", typeof(RectTransform));
            owner.SetActive(false);

            try
            {
                var producer = owner.AddComponent<SlimTextRenderTextureProducer>();
                var original = new SlimTextRenderTarget(UnitySkiaGraphicsBackend.Auto);
                SetField(producer, "renderTarget", original);
                SetField(producer, "m_backend", UnitySkiaGraphicsBackend.Vulkan);

                InvokeOnValidate(producer);

                var replacement = GetField<SlimTextRenderTarget>(producer, "renderTarget");
                Assert.That(replacement, Is.Not.SameAs(original));
                Assert.That(replacement.Backend, Is.EqualTo(UnitySkiaGraphicsBackend.Vulkan));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void TextBoxOnValidateReplacesTargetWhenSerializedBackendChanges()
        {
            var owner = new GameObject("TextBox inspector backend", typeof(RectTransform));
            owner.SetActive(false);

            try
            {
                var producer = owner.AddComponent<TextBoxRenderTextureProducer>();
                var original = new TextBoxRenderTarget(UnitySkiaGraphicsBackend.Auto);
                SetField(producer, "renderTarget", original);
                SetField(producer, "renderTargetBackend", UnitySkiaGraphicsBackend.Auto);
                SetField(producer, "m_backend", UnitySkiaGraphicsBackend.Vulkan);

                InvokeOnValidate(producer);

                var replacement = GetField<TextBoxRenderTarget>(producer, "renderTarget");
                Assert.That(replacement, Is.Not.SameAs(original));
                Assert.That(GetField<UnitySkiaGraphicsBackend>(producer, "renderTargetBackend"),
                    Is.EqualTo(UnitySkiaGraphicsBackend.Vulkan));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [TestCase((int)UnitySkiaRenderTextureSurface.RenderSubmissionStatus.Skipped)]
        [TestCase((int)UnitySkiaRenderTextureSurface.RenderSubmissionStatus.Failed)]
        public void TextInputRetriesNonDrawnRenderCompletion(int statusValue)
        {
            var status = (UnitySkiaRenderTextureSurface.RenderSubmissionStatus)statusValue;
            var owner = new GameObject("TextInput render retry", typeof(RectTransform));
            owner.SetActive(false);

            try
            {
                var input = owner.AddComponent<TextInput>();
                SetField(input, "paintDirty", false);

                InvokeCompletion(input, "OnRenderEventCompleted", status);

                Assert.That(GetField<bool>(input, "paintDirty"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [TestCase((int)UnitySkiaRenderTextureSurface.RenderSubmissionStatus.Skipped)]
        [TestCase((int)UnitySkiaRenderTextureSurface.RenderSubmissionStatus.Failed)]
        public void SlimTextRetriesNonDrawnRenderCompletion(int statusValue)
        {
            var status = (UnitySkiaRenderTextureSurface.RenderSubmissionStatus)statusValue;
            var target = new SlimTextRenderTarget(UnitySkiaGraphicsBackend.Vulkan);
            try
            {
                SetField(target, "paintChanged", false);
                SetField(target, "noAllocMode", true);
                SetField(target, "noAllocTextChanged", false);

                InvokeCompletion(target, "HandleRenderEventCompleted", status);

                Assert.That(GetField<bool>(target, "paintChanged"), Is.True);
                Assert.That(GetField<bool>(target, "noAllocTextChanged"), Is.True);
            }
            finally
            {
                target.Dispose();
            }
        }

        [TestCase((int)UnitySkiaRenderTextureSurface.RenderSubmissionStatus.Skipped)]
        [TestCase((int)UnitySkiaRenderTextureSurface.RenderSubmissionStatus.Failed)]
        public void TextBoxRetriesNonDrawnRenderCompletion(int statusValue)
        {
            var status = (UnitySkiaRenderTextureSurface.RenderSubmissionStatus)statusValue;
            var owner = new GameObject("TextBox render retry", typeof(RectTransform));
            owner.SetActive(false);

            try
            {
                var producer = owner.AddComponent<TextBoxRenderTextureProducer>();
                var target = new TextBoxRenderTarget(UnitySkiaGraphicsBackend.Vulkan);
                SetField(target, "paintChanged", false);
                SetField(producer, "renderTarget", target);
                SetField(producer, "renderTargetBackend", UnitySkiaGraphicsBackend.Vulkan);

                InvokeCompletion(producer, "OnRenderEventCompleted", status);

                Assert.That(GetField<bool>(target, "paintChanged"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        private static void InvokeOnValidate(object target)
        {
            target.GetType()
                .GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(target, null);
        }

        private static void InvokeCompletion(object target,
            string methodName,
            UnitySkiaRenderTextureSurface.RenderSubmissionStatus status)
        {
            target.GetType()
                .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(target, new object[] { status });
        }

        private static T GetField<T>(object target, string fieldName)
        {
            return (T)target.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(target)!;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(target, value);
        }
    }
}
