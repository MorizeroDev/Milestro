using System;
using Milestro.Model;
using Milestro.Skia;
using Milestro.Util;
using UnityEngine;
using SkFont = Milestro.Skia.Font;

namespace Milestro.Components.Internal
{
    internal sealed class SlimTextIntrinsicMeasurement : ITextIntrinsicMeasurement, IDisposable
    {
        private readonly string text;
        private readonly string fontFamily;
        private readonly int fontWeight;
        private readonly bool fallbackToSystemFont;
        private SkFont? font;
        private float fontSize;

        public SlimTextIntrinsicMeasurement(string text, string fontFamily, int fontWeight,
            bool fallbackToSystemFont)
        {
            this.text = text ?? "";
            this.fontFamily = fontFamily ?? "";
            this.fontWeight = fontWeight;
            this.fallbackToSystemFont = fallbackToSystemFont;
        }

        public TextIntrinsicMetrics Measure(float requestedFontSize, float contentWidth)
        {
            if (text.Length == 0) return default;
            var nextFontSize = Mathf.Max(0.1f, requestedFontSize);
            if (font == null || !Mathf.Approximately(fontSize, nextFontSize))
            {
                font?.Dispose();
                font = FontRegistry.ResolveFont(fontFamily, fontWeight, nextFontSize, fallbackToSystemFont);
                fontSize = nextFontSize;
            }

            var measured = font.MeasureText(text);
            var bounds = measured.Bounds;
            var width = FloatUtil.IsFinite(bounds.xMin) && FloatUtil.IsFinite(bounds.xMax) &&
                        FloatUtil.IsFinite(measured.AdvanceX)
                ? Mathf.Max(0f, Mathf.Max(bounds.xMax, measured.AdvanceX) - bounds.xMin)
                : 0f;
            var height = FloatUtil.IsFinite(bounds.yMin) && FloatUtil.IsFinite(bounds.yMax)
                ? Mathf.Max(0f, bounds.yMax - bounds.yMin)
                : 0f;
            return new TextIntrinsicMetrics(width, width, height, width, height);
        }

        public void Dispose()
        {
            font?.Dispose();
            font = null;
        }
    }
}
