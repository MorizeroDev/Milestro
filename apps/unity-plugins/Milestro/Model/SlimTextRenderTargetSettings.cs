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
            bool fallbackToSystemFont,
            bool autoFontSize = false,
            float minSize = 0f,
            float maxSize = 0f,
            TextSizingResult? sizing = null)
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
            AutoFontSize = autoFontSize;
            MinSize = minSize;
            MaxSize = maxSize;
            Sizing = sizing;
        }

        public SlimTextRenderTargetSettings WithFontSize(float newFontSize)
        {
            return new SlimTextRenderTargetSettings(Text,
                FontFamily,
                FontWeight,
                newFontSize,
                TextColor,
                new RectOffset(RectOffsetLeft, RectOffsetRight, RectOffsetTop, RectOffsetBottom),
                HorizontalAlign,
                VerticalAlign,
                FallbackToSystemFont,
                AutoFontSize,
                MinSize,
                MaxSize,
                Sizing);
        }

        public SlimTextRenderTargetSettings WithSizing(TextSizingResult sizing)
        {
            return new SlimTextRenderTargetSettings(Text,
                FontFamily,
                FontWeight,
                FontSize,
                TextColor,
                new RectOffset(RectOffsetLeft, RectOffsetRight, RectOffsetTop, RectOffsetBottom),
                HorizontalAlign,
                VerticalAlign,
                FallbackToSystemFont,
                AutoFontSize,
                MinSize,
                MaxSize,
                sizing);
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
        public bool AutoFontSize { get; }
        public float MinSize { get; }
        public float MaxSize { get; }
        public TextSizingResult? Sizing { get; }
    }
}
