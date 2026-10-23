using UnityEngine;

namespace Milestro.Model
{
    internal readonly struct TextBoxRenderTargetSettings
    {
        public TextBoxRenderTargetSettings(string content,
            Margin margin,
            ParagraphStyleSettings paragraphStyle,
            TextStyleSettings textStyle,
            TextBoxWrapMode wrapMode,
            TextOverflow textOverflow,
            bool autoFontSize = false,
            float minSize = 0f,
            float maxSize = 0f,
            TextSizingResult? sizing = null, Vector2 marginReferenceSize = default)
        {
            Content = content ?? "";
            Margin = margin ?? Margin.FixedZero();
            Margin.Normalize();
            ParagraphStyle = paragraphStyle ?? new ParagraphStyleSettings();
            TextStyle = textStyle ?? new TextStyleSettings();
            WrapMode = wrapMode;
            TextOverflow = textOverflow;
            AutoFontSize = autoFontSize;
            MinSize = minSize;
            MaxSize = maxSize;
            Sizing = sizing;
            MarginReferenceSize = marginReferenceSize;
        }

        public TextSizingResult? Sizing { get; }
        public Vector2 MarginReferenceSize { get; }

        public TextBoxRenderTargetSettings WithSizing(TextSizingResult sizing, Vector2 referenceSize)
        {
            return new TextBoxRenderTargetSettings(Content, Margin, ParagraphStyle, TextStyle,
                WrapMode, TextOverflow, AutoFontSize, MinSize, MaxSize, sizing, referenceSize);
        }

        public string Content { get; }
        public Margin Margin { get; }
        public ParagraphStyleSettings ParagraphStyle { get; }
        public TextStyleSettings TextStyle { get; }
        public TextBoxWrapMode WrapMode { get; }
        public TextOverflow TextOverflow { get; }
        public bool AutoFontSize { get; }
        public float MinSize { get; }
        public float MaxSize { get; }
    }
}
