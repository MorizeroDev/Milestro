using System.Collections.Generic;
using UnityEngine;

namespace Milestro.Model
{
    internal readonly struct TextBoxRenderTargetSettings
    {
        public TextBoxRenderTargetSettings(string content,
            Margin margin,
            List<string> fontFamilies,
            TextAlign textAlign,
            TextDirection textDirection,
            TextBoxWrapMode wrapMode,
            bool singleLine,
            TextOverflow textOverflow,
            string ellipsisString,
            float size,
            int weight,
            Color textColor,
            string locale,
            bool autoSize = false,
            float minSize = 0f,
            float maxSize = 0f,
            TextAutoSizeWrapMode autoSizeWrapMode = TextAutoSizeWrapMode.NoWrap)
        {
            Content = content ?? "";
            Margin = margin ?? Margin.FixedZero();
            Margin.Normalize();
            FontFamilies = fontFamilies ?? new List<string>();
            TextAlign = textAlign;
            TextDirection = textDirection;
            WrapMode = wrapMode;
            SingleLine = singleLine;
            TextOverflow = textOverflow;
            EllipsisString = ellipsisString ?? "";
            Size = size;
            Weight = weight;
            TextColor = textColor;
            Locale = locale ?? "";
            AutoSize = autoSize;
            MinSize = minSize;
            MaxSize = maxSize;
            AutoSizeWrapMode = autoSizeWrapMode;
        }

        public string Content { get; }
        public Margin Margin { get; }
        public List<string> FontFamilies { get; }
        public TextAlign TextAlign { get; }
        public TextDirection TextDirection { get; }
        public TextBoxWrapMode WrapMode { get; }
        public bool SingleLine { get; }
        public TextOverflow TextOverflow { get; }
        public string EllipsisString { get; }
        public float Size { get; }
        public int Weight { get; }
        public Color TextColor { get; }
        public string Locale { get; }
        public bool AutoSize { get; }
        public float MinSize { get; }
        public float MaxSize { get; }
        public TextAutoSizeWrapMode AutoSizeWrapMode { get; }
    }
}
