using System.Collections.Generic;
using Milestro.RichTextParser;
using UnityEngine;

namespace Milestro.Model
{
    internal readonly struct TextBoxRenderTargetSettings
    {
        public TextBoxRenderTargetSettings(string content,
            Margin margin,
            ParagraphStyleSettings paragraphStyle,
            TextStyleSettings textStyle,
            string? parserName,
            TextBoxWrapMode wrapMode,
            TextOverflow textOverflow,
            bool autoFontSize = false,
            float minSize = 0f,
            float maxSize = 0f,
            TextSizingResult? sizing = null, Vector2 marginReferenceSize = default,
            TextHeightMode? verticalHeightMode = null,
            MarginResolveContext? verticalMarginContext = null,
            bool sizingUsesVerticalLayout = false)
        {
            Content = content ?? "";
            ParserId = parserName;
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
            VerticalHeightMode = verticalHeightMode;
            VerticalMarginContext = verticalMarginContext;
            SizingUsesVerticalLayout = sizingUsesVerticalLayout;
        }

        public TextSizingResult? Sizing { get; }
        public Vector2 MarginReferenceSize { get; }
        public TextHeightMode? VerticalHeightMode { get; }
        public MarginResolveContext? VerticalMarginContext { get; }
        public bool SizingUsesVerticalLayout { get; }

        public TextBoxRenderTargetSettings WithSizing(TextSizingResult sizing,
            Vector2 referenceSize,
            TextHeightMode? verticalHeightMode = null,
            MarginResolveContext? verticalMarginContext = null,
            bool sizingUsesVerticalLayout = false)
        {
            return new TextBoxRenderTargetSettings(Content, Margin, ParagraphStyle, TextStyle,
                ParserId, WrapMode, TextOverflow, AutoFontSize, MinSize, MaxSize, sizing, referenceSize,
                verticalHeightMode, verticalMarginContext, sizingUsesVerticalLayout);
        }

        public TextBoxRenderTargetSettings WithVerticalLayout(MarginResolveContext marginContext,
            TextHeightMode heightMode)
        {
            return new TextBoxRenderTargetSettings(Content, Margin, ParagraphStyle, TextStyle,
                ParserId, WrapMode, TextOverflow, AutoFontSize, MinSize, MaxSize, Sizing,
                MarginReferenceSize, heightMode, marginContext, SizingUsesVerticalLayout);
        }

        public string Content { get; }
        public string? ParserId { get; }
        public Margin Margin { get; }
        public ParagraphStyleSettings ParagraphStyle { get; }
        public TextStyleSettings TextStyle { get; }
        public TextBoxWrapMode WrapMode { get; }
        public TextOverflow TextOverflow { get; }
        public bool AutoFontSize { get; }
        public float MinSize { get; }
        public float MaxSize { get; }

        internal ParagraphPayload ParseContent(bool allowViewportBlockMargins = false,
            ICollection<string>? issues = null)
        {
            if (ParserId == null || ParserId.Length == 0)
            {
                return ParagraphPayload.MakeText(Content);
            }

            if (!RichTextParserService.TryCreate(ParserId, out var parser) || parser == null)
            {
                issues?.Add($"unknown rich text parser id '{ParserId}'; content rendered as plain text");
                return ParagraphPayload.MakeText(Content);
            }

            return parser.Parse(Content,
                new RichTextParseContext(allowViewportBlockMargins, issues, TextStyle.locale));
        }
    }
}
