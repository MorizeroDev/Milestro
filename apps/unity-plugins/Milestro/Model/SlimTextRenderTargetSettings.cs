using UnityEngine;

namespace Milestro.Model
{
    internal readonly struct SlimTextRenderTargetSettings
    {
        public SlimTextRenderTargetSettings(string text,
            string fontFamily,
            int fontWeight,
            float fontSize,
            Color textColor,
            RectOffset rectOffset,
            SlimTextHorizontalAlign horizontalAlign,
            SlimTextVerticalAlign verticalAlign,
            bool fallbackToSystemFont)
        {
            Text = text ?? "";
            FontFamily = fontFamily ?? "";
            FontWeight = fontWeight;
            FontSize = fontSize;
            TextColor = textColor;
            RectOffsetLeft = rectOffset != null ? Mathf.Max(0, rectOffset.left) : 0;
            RectOffsetRight = rectOffset != null ? Mathf.Max(0, rectOffset.right) : 0;
            RectOffsetTop = rectOffset != null ? Mathf.Max(0, rectOffset.top) : 0;
            RectOffsetBottom = rectOffset != null ? Mathf.Max(0, rectOffset.bottom) : 0;
            HorizontalAlign = horizontalAlign;
            VerticalAlign = verticalAlign;
            FallbackToSystemFont = fallbackToSystemFont;
        }

        public string Text { get; }
        public string FontFamily { get; }
        public int FontWeight { get; }
        public float FontSize { get; }
        public Color TextColor { get; }
        public int RectOffsetLeft { get; }
        public int RectOffsetRight { get; }
        public int RectOffsetTop { get; }
        public int RectOffsetBottom { get; }
        public SlimTextHorizontalAlign HorizontalAlign { get; }
        public SlimTextVerticalAlign VerticalAlign { get; }
        public bool FallbackToSystemFont { get; }
    }
}
