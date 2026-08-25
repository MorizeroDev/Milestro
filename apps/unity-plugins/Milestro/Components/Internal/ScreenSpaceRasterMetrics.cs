using System;
using Milestro.Util;
using UnityEngine;

namespace Milestro.Components.Internal
{
    internal readonly struct ScreenSpaceRasterMeasurement
    {
        internal ScreenSpaceRasterMeasurement(Vector2Int logicalSize,
            float desiredScale,
            Camera? eventCamera,
            int targetDisplay)
        {
            LogicalSize = logicalSize;
            DesiredScale = desiredScale;
            EventCamera = eventCamera;
            TargetDisplay = targetDisplay;
        }

        internal Vector2Int LogicalSize { get; }
        internal float DesiredScale { get; }
        internal Camera? EventCamera { get; }
        internal int TargetDisplay { get; }
    }

    internal static class ScreenSpaceRasterMetrics
    {
        private const float MinimumEdgeLength = 0.0001f;

        internal static bool TryMeasure(RectTransform rectTransform,
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

            var canvas = rectTransform.GetComponentInParent<Canvas>();
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
    }
}
