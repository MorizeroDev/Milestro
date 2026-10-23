using System;
using Milestro.Util;
using UnityEngine;

namespace Milestro.Components.Internal
{
    internal static class TextAutoFontSizeScale
    {
        public static float Resolve(float contentWidth,
            float contentHeight,
            float availableWidth,
            float availableHeight,
            float baseSize,
            float minSize,
            float maxSize)
        {
            if (!FloatUtil.IsFinite(contentWidth) ||
                !FloatUtil.IsFinite(contentHeight) ||
                !FloatUtil.IsFinite(availableWidth) ||
                !FloatUtil.IsFinite(availableHeight) ||
                !FloatUtil.IsFinite(baseSize) ||
                !FloatUtil.IsFinite(minSize) ||
                !FloatUtil.IsFinite(maxSize) ||
                baseSize <= 0f ||
                minSize > maxSize)
            {
                return 1f;
            }

            var fit = float.PositiveInfinity;
            if (contentWidth > 0f)
            {
                fit = availableWidth / contentWidth;
            }

            if (contentHeight > 0f)
            {
                var heightFit = availableHeight / contentHeight;
                fit = fit < heightFit ? fit : heightFit;
            }

            var minScale = minSize / baseSize;
            var maxScale = maxSize / baseSize;
            if (float.IsNaN(fit))
            {
                return 1f;
            }

            if (fit < minScale)
            {
                return minScale;
            }

            return fit > maxScale ? maxScale : fit;
        }

        private const int MaxSearchFitIterations = 32;
        private const float SearchFitScaleTolerance = 0.001f;

        public static float SearchFit(Func<float, Vector2> measure,
            float availableWidth,
            float availableHeight,
            float minScale,
            float maxScale)
        {
            if (measure == null ||
                !FloatUtil.IsFinite(availableWidth) ||
                !FloatUtil.IsFinite(availableHeight) ||
                !FloatUtil.IsFinite(minScale) ||
                !FloatUtil.IsFinite(maxScale) ||
                minScale <= 0f ||
                minScale > maxScale)
            {
                return 1f;
            }

            if (Fits(measure, maxScale, availableWidth, availableHeight))
            {
                return maxScale;
            }

            if (!Fits(measure, minScale, availableWidth, availableHeight))
            {
                return minScale;
            }

            var low = minScale;
            var high = maxScale;
            for (var iteration = 0;
                 iteration < MaxSearchFitIterations && high - low > SearchFitScaleTolerance;
                 ++iteration)
            {
                var mid = (low + high) * 0.5f;
                if (Fits(measure, mid, availableWidth, availableHeight))
                {
                    low = mid;
                }
                else
                {
                    high = mid;
                }
            }

            return low;
        }

        private static bool Fits(Func<float, Vector2> measure,
            float scale,
            float availableWidth,
            float availableHeight)
        {
            var measured = measure(scale);
            if (!FloatUtil.IsFinite(measured.x) ||
                !FloatUtil.IsFinite(measured.y) ||
                measured.x < 0f ||
                measured.y < 0f)
            {
                return false;
            }

            return measured.x * scale <= availableWidth &&
                   measured.y * scale <= availableHeight;
        }
    }
}
