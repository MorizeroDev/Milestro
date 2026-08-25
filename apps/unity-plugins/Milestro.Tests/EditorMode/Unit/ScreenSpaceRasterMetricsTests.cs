using Milestro.Components.Internal;
using Milestro.Configuration;
using Milestro.Skia;
using NUnit.Framework;
using UnityEngine;
using UnityCanvas = UnityEngine.Canvas;

namespace Milestro.Tests
{
    public class ScreenSpaceRasterMetricsTests
    {
        [TestCase(1f)]
        [TestCase(1.25f)]
        [TestCase(1.5f)]
        [TestCase(2f)]
        public void FourCornerProjectionResolvesExpectedUniformScale(float scale)
        {
            Assert.That(ScreenSpaceRasterMetrics.TryMeasureProjectedCorners(new Vector2(100f, 40f),
                    Vector2.zero,
                    new Vector2(0f, 40f * scale),
                    new Vector2(100f * scale, 40f * scale),
                    new Vector2(100f * scale, 0f),
                    out var desiredScale),
                Is.True);
            Assert.That(desiredScale, Is.EqualTo(scale).Within(0.0001f));
        }

        [Test]
        public void RotatedProjectionUsesTransformedEdgesInsteadOfAxisAlignedBounds()
        {
            var xEdge = new Vector2(173.20508f, 100f);
            var yEdge = new Vector2(-25f, 43.30127f);
            var bottomLeft = new Vector2(300f, 200f);
            var topLeft = bottomLeft + yEdge;
            var bottomRight = bottomLeft + xEdge;
            var topRight = bottomRight + yEdge;

            Assert.That(ScreenSpaceRasterMetrics.TryMeasureProjectedCorners(new Vector2(100f, 50f),
                    bottomLeft,
                    topLeft,
                    topRight,
                    bottomRight,
                    out var desiredScale),
                Is.True);
            Assert.That(desiredScale, Is.EqualTo(2f).Within(0.0001f));

            var aabbWidthDensity = (topRight.x - topLeft.x) / 100f;
            Assert.That(aabbWidthDensity, Is.Not.EqualTo(desiredScale).Within(0.01f));
        }

        [Test]
        public void DegenerateAndNonfiniteProjectionFailClosed()
        {
            Assert.That(ScreenSpaceRasterMetrics.TryMeasureProjectedCorners(new Vector2(100f, 50f),
                    Vector2.zero,
                    Vector2.zero,
                    new Vector2(100f, 0f),
                    new Vector2(100f, 0f),
                    out _),
                Is.False);
            Assert.That(ScreenSpaceRasterMetrics.TryMeasureProjectedCorners(new Vector2(100f, 50f),
                    Vector2.zero,
                    new Vector2(0f, 50f),
                    new Vector2(float.NaN, 50f),
                    new Vector2(100f, 0f),
                    out _),
                Is.False);
        }

        [Test]
        public void CanvasContextUsesCanvasEventCameraAndTargetDisplay()
        {
            var canvasObject = new GameObject("ScreenSpaceRasterCanvas", typeof(RectTransform), typeof(UnityCanvas));
            var cameraObject = new GameObject("ScreenSpaceRasterCamera", typeof(Camera));
            try
            {
                var canvas = canvasObject.GetComponent<UnityCanvas>();
                var camera = cameraObject.GetComponent<Camera>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.targetDisplay = 2;
                Assert.That(ScreenSpaceRasterMetrics.ResolveEventCamera(canvas), Is.Null);
                Assert.That(ScreenSpaceRasterMetrics.ResolveTargetDisplay(canvas, null), Is.EqualTo(2));

                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                camera.targetDisplay = 3;
                Assert.That(ScreenSpaceRasterMetrics.ResolveEventCamera(canvas), Is.SameAs(camera));
                Assert.That(ScreenSpaceRasterMetrics.ResolveTargetDisplay(canvas, camera), Is.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(canvasObject);
            }
        }

        [Test]
        public void RasterViewportKeepsLogicalCoordinatesAndScalesOnlyVisiblePixels()
        {
            var viewport = TextBoxRenderViewport.Fixed(new Vector2Int(320, 80),
                    default,
                    new Vector2(17f, 9f),
                    new Vector2(3f, 2f))
                .WithScreenSpaceRasterization(1.5f);

            Assert.That(viewport.LayoutSizePixels, Is.EqualTo(new Vector2Int(320, 80)));
            Assert.That(viewport.OutputSizePixels, Is.EqualTo(new Vector2Int(320, 80)));
            Assert.That(viewport.VisibleOutputSizePixels, Is.EqualTo(new Vector2Int(320, 80)));
            Assert.That(viewport.RequestedScrollY, Is.EqualTo(9f));
            Assert.That(viewport.VisualScrollOffset, Is.EqualTo(new Vector2(3f, 2f)));
            Assert.That(viewport.DesiredRasterScale, Is.EqualTo(1.5f));
            Assert.That(ScreenSpaceRasterMetrics.RasterizeVisibleSize(new Vector2Int(320, 80),
                    1.5f,
                    new Vector2Int(480, 120)),
                Is.EqualTo(new Vector2Int(480, 120)));
        }

        [Test]
        public void QuantizedScaleRemainsStableAcrossThresholdJitter()
        {
            var configuration = new RenderSurfaceConfiguration();
            Assert.That(RenderSurfacePolicy.TryQuantizeDesiredScale(1.25f,
                    configuration.ScaleQuantum,
                    configuration.ScaleHysteresis,
                    configuration.MaxScreenSpaceRasterScale,
                    0f,
                    out var stableScale),
                Is.True);
            Assert.That(stableScale, Is.EqualTo(1.25f));

            foreach (var jitter in new[] { 1.249f, 1.251f, 1.299f, 1.201f })
            {
                Assert.That(RenderSurfacePolicy.TryQuantizeDesiredScale(jitter,
                        configuration.ScaleQuantum,
                        configuration.ScaleHysteresis,
                        configuration.MaxScreenSpaceRasterScale,
                        stableScale,
                        out var nextScale),
                    Is.True);
                Assert.That(nextScale, Is.EqualTo(stableScale));
            }
        }
    }
}
