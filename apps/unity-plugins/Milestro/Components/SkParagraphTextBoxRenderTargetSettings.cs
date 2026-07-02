using System.Collections.Generic;
using Milestro.Model;
using UnityEngine;

namespace Milestro.Components
{
    internal readonly struct SkParagraphTextBoxRenderTargetSettings
    {
        public SkParagraphTextBoxRenderTargetSettings(string content,
            RectOffset margin,
            List<string> fontFamilies,
            TextAlign textAlign,
            TextDirection textDirection,
            float size,
            Color textColor,
            string locale)
        {
            Content = content ?? "";
            Margin = margin ?? new RectOffset();
            FontFamilies = fontFamilies ?? new List<string>();
            TextAlign = textAlign;
            TextDirection = textDirection;
            Size = size;
            TextColor = textColor;
            Locale = locale ?? "";
        }

        public string Content { get; }
        public RectOffset Margin { get; }
        public List<string> FontFamilies { get; }
        public TextAlign TextAlign { get; }
        public TextDirection TextDirection { get; }
        public float Size { get; }
        public Color TextColor { get; }
        public string Locale { get; }
    }
}
