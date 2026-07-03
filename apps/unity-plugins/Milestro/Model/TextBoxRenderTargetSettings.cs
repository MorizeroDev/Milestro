using System.Collections.Generic;
using UnityEngine;

namespace Milestro.Model
{
    internal readonly struct TextBoxRenderTargetSettings
    {
        public TextBoxRenderTargetSettings(string content,
            RectOffset margin,
            List<string> fontFamilies,
            TextAlign textAlign,
            TextDirection textDirection,
            TextBoxWrapMode wrapMode,
            float size,
            int weight,
            Color textColor,
            string locale)
        {
            Content = content ?? "";
            Margin = margin ?? new RectOffset();
            FontFamilies = fontFamilies ?? new List<string>();
            TextAlign = textAlign;
            TextDirection = textDirection;
            WrapMode = wrapMode;
            Size = size;
            Weight = weight;
            TextColor = textColor;
            Locale = locale ?? "";
        }

        public string Content { get; }
        public RectOffset Margin { get; }
        public List<string> FontFamilies { get; }
        public TextAlign TextAlign { get; }
        public TextDirection TextDirection { get; }
        public TextBoxWrapMode WrapMode { get; }
        public float Size { get; }
        public int Weight { get; }
        public Color TextColor { get; }
        public string Locale { get; }
    }
}
