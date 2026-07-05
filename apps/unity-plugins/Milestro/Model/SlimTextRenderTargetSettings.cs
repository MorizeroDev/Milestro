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
            Vector2 padding,
            bool fallbackToSystemFont)
        {
            Text = text ?? "";
            FontFamily = fontFamily ?? "";
            FontWeight = fontWeight;
            FontSize = fontSize;
            TextColor = textColor;
            Padding = padding;
            FallbackToSystemFont = fallbackToSystemFont;
        }

        public string Text { get; }
        public string FontFamily { get; }
        public int FontWeight { get; }
        public float FontSize { get; }
        public Color TextColor { get; }
        public Vector2 Padding { get; }
        public bool FallbackToSystemFont { get; }
    }
}
