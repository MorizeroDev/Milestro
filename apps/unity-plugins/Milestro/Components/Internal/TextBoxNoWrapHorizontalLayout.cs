using System;
using Milestro.Model;
using Milestro.Util;

namespace Milestro.Components.Internal
{
    internal readonly struct TextBoxNoWrapHorizontalLayout
    {
        private TextBoxNoWrapHorizontalLayout(float contentWidth,
            float logicalVisualLeft,
            float layoutAlignmentOffset,
            float initialScrollX,
            float maxScrollX)
        {
            ContentWidth = contentWidth;
            LogicalVisualLeft = logicalVisualLeft;
            LayoutAlignmentOffset = layoutAlignmentOffset;
            InitialScrollX = initialScrollX;
            MaxScrollX = maxScrollX;
        }

        internal float ContentWidth { get; }
        internal float LogicalVisualLeft { get; }
        internal float LayoutAlignmentOffset { get; }
        internal float InitialScrollX { get; }
        internal float MaxScrollX { get; }

        internal static TextBoxNoWrapHorizontalLayout Resolve(bool useWideLayout,
            TextAlign textAlign,
            TextDirection textDirection,
            float viewportWidth,
            float contentWidth,
            float layoutWidth)
        {
            if (!useWideLayout)
            {
                return default;
            }

            viewportWidth = SanitizeLength(viewportWidth);
            contentWidth = SanitizeLength(contentWidth);
            if (contentWidth <= 0f)
            {
                return default;
            }

            var logicalContainingWidth = Math.Max(viewportWidth, contentWidth);
            layoutWidth = Math.Max(logicalContainingWidth, SanitizeLength(layoutWidth));
            var alignmentFactor = AlignmentFactor(EffectiveAlign(textAlign, textDirection));
            var logicalVisualLeft = (logicalContainingWidth - contentWidth) * alignmentFactor;
            var layoutVisualLeft = (layoutWidth - contentWidth) * alignmentFactor;
            var maxScrollX = Math.Max(0f, contentWidth - viewportWidth);

            return new TextBoxNoWrapHorizontalLayout(contentWidth,
                logicalVisualLeft,
                Math.Max(0f, layoutVisualLeft - logicalVisualLeft),
                maxScrollX * alignmentFactor,
                maxScrollX);
        }

        internal static float ResolvePaintOffsetX(float layoutAlignmentOffset,
            float scrollX,
            float visualScrollOffsetX)
        {
            return -SanitizeOffset(layoutAlignmentOffset) - SanitizeOffset(scrollX) -
                   SanitizeSignedOffset(visualScrollOffsetX);
        }

        private static TextAlign EffectiveAlign(TextAlign textAlign, TextDirection textDirection)
        {
            switch (textAlign)
            {
                case TextAlign.Start:
                    return textDirection == TextDirection.Rtl ? TextAlign.Right : TextAlign.Left;
                case TextAlign.End:
                    return textDirection == TextDirection.Rtl ? TextAlign.Left : TextAlign.Right;
                default:
                    return textAlign;
            }
        }

        private static float AlignmentFactor(TextAlign textAlign)
        {
            switch (textAlign)
            {
                case TextAlign.Center:
                    return 0.5f;
                case TextAlign.Right:
                    return 1f;
                default:
                    return 0f;
            }
        }

        private static float SanitizeLength(float value)
        {
            return FloatUtil.IsFinite(value) ? Math.Max(0f, value) : 0f;
        }

        private static float SanitizeOffset(float value)
        {
            return FloatUtil.IsFinite(value) ? Math.Max(0f, value) : 0f;
        }

        private static float SanitizeSignedOffset(float value)
        {
            return FloatUtil.IsFinite(value) ? value : 0f;
        }
    }
}
