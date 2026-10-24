using System.Collections;
using Milestro.Components;
using Milestro.Components.Internal;
using Milestro.Configuration;
using Milestro.Model;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Milestro.Tests
{
    public class SlimTextUpdatePlayModeTests
    {
        private RenderSurfaceConfiguration previousConfiguration;
        private bool previousProfilerEnabled;
        private GameObject canvasObject;

        [SetUp]
        public void SetUp()
        {
            previousConfiguration = MilestroConfiguration.Configuration.RenderSurface;
            previousProfilerEnabled = SlimTextPipelineProfiler.Enabled;
            MilestroConfiguration.Configuration.RenderSurface = new RenderSurfaceConfiguration
            {
                AtlasPageSize = 128
            };
        }

        [TearDown]
        public void TearDown()
        {
            SlimTextPipelineProfiler.Enabled = previousProfilerEnabled;
            MilestroConfiguration.Configuration.RenderSurface = previousConfiguration;
            if (canvasObject != null)
            {
                Object.Destroy(canvasObject);
            }
        }

        [UnityTest]
        public IEnumerator StaticCanvasSlimTextStopsRebuildingAfterInitialOutput()
        {
            canvasObject = new GameObject("Slim Text Update Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            var textObject = new GameObject("Canvas Slim Text", typeof(RectTransform), typeof(RawImage));
            textObject.transform.SetParent(canvasObject.transform, false);
            var rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(320f, 48f);
            var producer = textObject.AddComponent<SlimTextRenderTextureProducer>();
            var slimText = textObject.AddComponent<CanvasSlimText>();
            slimText.Configure("Static canvas slim text",
                20f,
                SlimTextHorizontalAlign.Left,
                Color.white);

            for (var frame = 0; frame < 180 && !producer.HasOutput; ++frame)
            {
                yield return null;
            }

            Assert.That(producer.HasOutput, Is.True);
            for (var frame = 0; frame < 12; ++frame)
            {
                yield return null;
            }

            SlimTextPipelineProfiler.Enabled = true;
            SlimTextPipelineProfiler.Reset();
            for (var frame = 0; frame < 24; ++frame)
            {
                yield return null;
            }

            Assert.That(SlimTextPipelineProfiler.ProducerRebuildCalls,
                Is.LessThanOrEqualTo(1),
                "A static Canvas Slim Text must not run the rebuild pipeline every frame.");
        }
    }
}
