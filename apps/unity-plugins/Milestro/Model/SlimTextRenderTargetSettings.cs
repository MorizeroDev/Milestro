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
            RectOffset = NormalizeRectOffset(rectOffset);
            HorizontalAlign = horizontalAlign;
            VerticalAlign = verticalAlign;
            FallbackToSystemFont = fallbackToSystemFont;
        }

        public string Text { get; }
        public string FontFamily { get; }
        public int FontWeight { get; }
        public float FontSize { get; }
        public Color TextColor { get; }
        public RectOffset RectOffset { get; }
        public SlimTextHorizontalAlign HorizontalAlign { get; }
        public SlimTextVerticalAlign VerticalAlign { get; }
        public bool FallbackToSystemFont { get; }

        private static RectOffset NormalizeRectOffset(RectOffset rectOffset)
        {
            if (rectOffset == null)
            {
                return new RectOffset();
            }

            return new RectOffset(Mathf.Max(0, rectOffset.left),
                Mathf.Max(0, rectOffset.right),
                Mathf.Max(0, rectOffset.top),
                Mathf.Max(0, rectOffset.bottom));
        }
    }
}
