using System;
using Milestro.Util;
using UnityEngine;

namespace Milestro.Components.Internal
{
    internal static class ScreenSpaceRasterMetrics
    {
        private const float MinimumEdgeLength = 0.0001f;

        internal static bool TryMeasure(RectTransform rectTransform,
            Vector3[] worldCorners,
            out ScreenSpaceRasterMeasurement measurement)
        {
            if (rectTransform == null)
            {
                measurement = default;
                return false;
            }

            var canvas = rectTransform.GetComponentInParent<Canvas>();
            return TryMeasure(rectTransform, canvas, worldCorners, out measurement);
        }

        internal static bool TryMeasure(RectTransform rectTransform,
            Canvas? canvas,
            Vector3[] worldCorners,
            out ScreenSpaceRasterMeasurement measurement)
        {
            measurement = default;
            if (rectTransform == null || worldCorners == null || worldCorners.Length < 4)
            {
                return false;
            }

            var rect = rectTransform.rect;
            if (!FloatUtil.IsFinite(rect.width) ||
                !FloatUtil.IsFinite(rect.height) ||
                rect.width <= 0f ||
                rect.height <= 0f ||
                rect.width > int.MaxValue ||
                rect.height > int.MaxValue)
            {
                return false;
            }

            if (canvas == null)
            {
                return false;
            }

            var rootCanvas = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
            var eventCamera = ResolveEventCamera(rootCanvas);
            var targetDisplay = ResolveTargetDisplay(rootCanvas, eventCamera);
            if (targetDisplay < 0)
            {
                return false;
            }

            rectTransform.GetWorldCorners(worldCorners);
            var bottomLeft = RectTransformUtility.WorldToScreenPoint(eventCamera, worldCorners[0]);
            var topLeft = RectTransformUtility.WorldToScreenPoint(eventCamera, worldCorners[1]);
            var topRight = RectTransformUtility.WorldToScreenPoint(eventCamera, worldCorners[2]);
            var bottomRight = RectTransformUtility.WorldToScreenPoint(eventCamera, worldCorners[3]);
            if (!TryMeasureProjectedCorners(new Vector2(rect.width, rect.height),
                    bottomLeft,
                    topLeft,
                    topRight,
                    bottomRight,
                    out var desiredScale))
            {
                return false;
            }

            var logicalWidth = Mathf.CeilToInt(rect.width);
            var logicalHeight = Mathf.CeilToInt(rect.height);
            if (logicalWidth <= 0 || logicalHeight <= 0)
            {
                return false;
            }

            measurement = new ScreenSpaceRasterMeasurement(new Vector2Int(logicalWidth, logicalHeight),
                desiredScale,
                eventCamera,
                targetDisplay);
            return true;
        }

        internal static bool TryMeasureProjectedCorners(Vector2 logicalSize,
            Vector2 bottomLeft,
            Vector2 topLeft,
            Vector2 topRight,
            Vector2 bottomRight,
            out float desiredScale)
        {
            desiredScale = 0f;
            if (!IsFinitePositive(logicalSize.x) ||
                !IsFinitePositive(logicalSize.y) ||
                !IsFinite(bottomLeft) ||
                !IsFinite(topLeft) ||
                !IsFinite(topRight) ||
                !IsFinite(bottomRight))
            {
                return false;
            }

            var bottomLength = Vector2.Distance(bottomLeft, bottomRight);
            var topLength = Vector2.Distance(topLeft, topRight);
            var leftLength = Vector2.Distance(bottomLeft, topLeft);
            var rightLength = Vector2.Distance(bottomRight, topRight);
            if (!IsFiniteEdge(bottomLength) ||
                !IsFiniteEdge(topLength) ||
                !IsFiniteEdge(leftLength) ||
                !IsFiniteEdge(rightLength))
            {
                return false;
            }

            var densityX = Mathf.Max(bottomLength, topLength) / logicalSize.x;
            var densityY = Mathf.Max(leftLength, rightLength) / logicalSize.y;
            desiredScale = Mathf.Max(densityX, densityY);
            return IsFinitePositive(desiredScale);
        }

        internal static Camera? ResolveEventCamera(Canvas canvas)
        {
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            if (canvas.worldCamera != null)
            {
                return canvas.worldCamera;
            }

            return canvas.renderMode == RenderMode.WorldSpace ? Camera.main : null;
        }

        internal static int ResolveTargetDisplay(Canvas canvas, Camera? eventCamera)
        {
            return eventCamera != null ? eventCamera.targetDisplay : canvas.targetDisplay;
        }

        internal static Vector2Int RasterizeVisibleSize(Vector2Int logicalSize,
            float effectiveScale,
            Vector2Int rasterBounds)
        {
            if (logicalSize.x <= 0 ||
                logicalSize.y <= 0 ||
                rasterBounds.x <= 0 ||
                rasterBounds.y <= 0 ||
                !IsFinitePositive(effectiveScale))
            {
                return Vector2Int.one;
            }

            var scaledWidth = Math.Ceiling(logicalSize.x * (double)effectiveScale);
            var scaledHeight = Math.Ceiling(logicalSize.y * (double)effectiveScale);
            if (scaledWidth <= 0d ||
                scaledHeight <= 0d ||
                double.IsNaN(scaledWidth) ||
                double.IsInfinity(scaledWidth) ||
                double.IsNaN(scaledHeight) ||
                double.IsInfinity(scaledHeight))
            {
                return Vector2Int.one;
            }

            var width = (int)Math.Min(scaledWidth, rasterBounds.x);
            var height = (int)Math.Min(scaledHeight, rasterBounds.y);
            return new Vector2Int(width, height);
        }

        private static bool IsFinite(Vector2 value)
        {
            return FloatUtil.IsFinite(value.x) && FloatUtil.IsFinite(value.y);
        }

        private static bool IsFinitePositive(float value)
        {
            return FloatUtil.IsFinite(value) && value > 0f;
        }

        private static bool IsFiniteEdge(float value)
        {
            return FloatUtil.IsFinite(value) && value > MinimumEdgeLength;
        }

        internal static bool TryCapture(RectTransform rectTransform,
            Canvas? canvas,
            out RasterizationContext context)
        {
            context = default;
            if (rectTransform == null || canvas == null)
            {
                return false;
            }

            var rootCanvas = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
            var eventCamera = ResolveEventCamera(rootCanvas);
            var targetDisplay = ResolveTargetDisplay(rootCanvas, eventCamera);
            if (targetDisplay < 0)
            {
                return false;
            }

            context = new RasterizationContext(rootCanvas,
                eventCamera,
                rootCanvas.renderMode,
                targetDisplay,
                rectTransform.localToWorldMatrix,
                rootCanvas.transform.localToWorldMatrix,
                eventCamera != null ? eventCamera.transform.localToWorldMatrix : Matrix4x4.identity,
                eventCamera != null ? eventCamera.projectionMatrix : Matrix4x4.identity,
                rootCanvas.pixelRect,
                eventCamera != null ? eventCamera.pixelRect : default,
                rootCanvas.scaleFactor,
                Screen.width,
                Screen.height);
            return true;
        }

        internal readonly struct RasterizationContext
        {
            internal RasterizationContext(Canvas rootCanvas,
                Camera? eventCamera,
                RenderMode renderMode,
                int targetDisplay,
                Matrix4x4 rectToWorld,
                Matrix4x4 canvasToWorld,
                Matrix4x4 cameraToWorld,
                Matrix4x4 cameraProjection,
                Rect canvasPixelRect,
                Rect cameraPixelRect,
                float canvasScaleFactor,
                int screenWidth,
                int screenHeight)
            {
                RootCanvas = rootCanvas;
                EventCamera = eventCamera;
                RenderMode = renderMode;
                TargetDisplay = targetDisplay;
                RectToWorld = rectToWorld;
                CanvasToWorld = canvasToWorld;
                CameraToWorld = cameraToWorld;
                CameraProjection = cameraProjection;
                CanvasPixelRect = canvasPixelRect;
                CameraPixelRect = cameraPixelRect;
                CanvasScaleFactor = canvasScaleFactor;
                ScreenWidth = screenWidth;
                ScreenHeight = screenHeight;
            }

            internal Canvas RootCanvas { get; }
            internal Camera? EventCamera { get; }
            internal RenderMode RenderMode { get; }
            internal int TargetDisplay { get; }
            internal Matrix4x4 RectToWorld { get; }
            internal Matrix4x4 CanvasToWorld { get; }
            internal Matrix4x4 CameraToWorld { get; }
            internal Matrix4x4 CameraProjection { get; }
            internal Rect CanvasPixelRect { get; }
            internal Rect CameraPixelRect { get; }
            internal float CanvasScaleFactor { get; }
            internal int ScreenWidth { get; }
            internal int ScreenHeight { get; }

            internal bool Matches(in RasterizationContext other)
            {
                return RootCanvas == other.RootCanvas &&
                       EventCamera == other.EventCamera &&
                       RenderMode == other.RenderMode &&
                       TargetDisplay == other.TargetDisplay &&
                       RectToWorld == other.RectToWorld &&
                       CanvasToWorld == other.CanvasToWorld &&
                       CameraToWorld == other.CameraToWorld &&
                       CameraProjection == other.CameraProjection &&
                       CanvasPixelRect == other.CanvasPixelRect &&
                       CameraPixelRect == other.CameraPixelRect &&
                       CanvasScaleFactor == other.CanvasScaleFactor &&
                       ScreenWidth == other.ScreenWidth &&
                       ScreenHeight == other.ScreenHeight;
            }
        }
    }
}
