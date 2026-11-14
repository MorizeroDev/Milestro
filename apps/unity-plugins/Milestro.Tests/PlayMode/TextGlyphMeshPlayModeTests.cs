using System;
using System.Collections;
using Milestro.Components;
using Milestro.Components.Internal;
using Milestro.Configuration;
using Milestro.Model;
using Milestro.Skia;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Canvas = UnityEngine.Canvas;
using Object = UnityEngine.Object;

namespace Milestro.Tests
{
    public class TextGlyphMeshPlayModeTests
    {
        [UnityTest]
        public IEnumerator SlimDigitsRenderThroughTheSharedShaderAndReuseWarmedGlyphs()
        {
            yield return RenderPrewarmedDigits(false);
        }

        [UnityTest]
        public IEnumerator ParagraphDigitsRenderThroughTheSharedShaderAndReuseWarmedGlyphs()
        {
            yield return RenderPrewarmedDigits(true);
        }

        private static IEnumerator RenderPrewarmedDigits(bool paragraph)
        {
            var previousConfiguration = MilestroConfiguration.Configuration.RenderSurface;
            MilestroConfiguration.Configuration.RenderSurface = new RenderSurfaceConfiguration
            {
                EnableTextGlyphMesh = true
            };
            var cameraObject = new GameObject("Glyph Readback Camera", typeof(Camera));
            var canvasObject = new GameObject("Glyph Readback Canvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.layer = 5;
            var target = new RenderTexture(256, 128, 24, RenderTextureFormat.ARGB32);
            try
            {
                target.Create();
                var camera = cameraObject.GetComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.cullingMask = 1 << 5;
                camera.targetTexture = target;
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10f;
                var textObject = new GameObject("Glyph Readback Text", typeof(RectTransform));
                textObject.layer = 5;
                textObject.transform.SetParent(canvasObject.transform, false);
                ((RectTransform)textObject.transform).sizeDelta = new Vector2(240f, 80f);
                RenderTextureProducer producer;
                Action<string> setContent;
                Func<bool> hasGlyphMesh;
                if (paragraph)
                {
                    var textBox = textObject.AddComponent<TextBox>();
                    var paragraphProducer = textObject.GetComponent<TextBoxRenderTextureProducer>();
                    paragraphProducer.textStyle.size = 32f;
                    paragraphProducer.textStyle.textColor = Color.red;
                    textBox.Text = "0123456789";
                    producer = paragraphProducer;
                    setContent = content => textBox.Text = content;
                    hasGlyphMesh = () => paragraphProducer.GlyphMeshOutput != null;
                }
                else
                {
                    textObject.AddComponent<RawImage>();
                    var slimText = textObject.AddComponent<CanvasSlimText>();
                    slimText.Configure("0123456789", 32f, SlimTextHorizontalAlign.Left, Color.red);
                    var slimProducer = textObject.GetComponent<SlimTextRenderTextureProducer>();
                    producer = slimProducer;
                    setContent = content => slimText.Text = content;
                    hasGlyphMesh = () => slimProducer.GlyphMeshOutput != null;
                }

                yield return null;
                Canvas.ForceUpdateCanvases();
                TextGlyphAtlas.Flush();
                Assert.That(hasGlyphMesh(), Is.True);
                Assert.That(producer.OutputTexture, Is.TypeOf<Texture2D>());
                camera.Render();
                AssertRedGlyphPixels(target);
                var rasterized = TextGlyphAtlas.RasterizedGlyphs;
                var uploads = TextGlyphAtlas.Uploads;

                setContent("9876543210");
                Canvas.ForceUpdateCanvases();
                TextGlyphAtlas.Flush();
                Assert.That(hasGlyphMesh(), Is.True);
                camera.Render();
                AssertRedGlyphPixels(target);
                Assert.That(TextGlyphAtlas.RasterizedGlyphs, Is.EqualTo(rasterized));
                Assert.That(TextGlyphAtlas.Uploads, Is.EqualTo(uploads));
            }
            finally
            {
                Object.Destroy(canvasObject);
                Object.Destroy(cameraObject);
                target.Release();
                Object.Destroy(target);
                MilestroConfiguration.Configuration.RenderSurface = previousConfiguration;
            }
        }

        private static void AssertRedGlyphPixels(RenderTexture target)
        {
            var previous = RenderTexture.active;
            var readback = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                readback.Apply();
                var count = 0;
                foreach (var pixel in readback.GetPixels32())
                {
                    if (pixel.r > 32 && pixel.g < 8 && pixel.b < 8)
                    {
                        ++count;
                    }
                }
                Assert.That(count, Is.GreaterThan(100), "The UI shader must render visible tinted glyph pixels.");
            }
            finally
            {
                RenderTexture.active = previous;
                Object.Destroy(readback);
            }
        }
    }
}
