using System;
using System.Collections.Generic;
using Milestro.Model;
using Milestro.Skia.TextLayout;
using Milestro.Util;
using UnityEngine;

namespace Milestro.Components.Internal
{
    internal readonly struct TextBoxVerticalBlockLayout
    {
        private TextBoxVerticalBlockLayout(Vector2 physicalSize, Rect contentBounds,
            Vector2[] frameOffsets)
        {
            PhysicalSize = physicalSize;
            ContentBounds = contentBounds;
            FrameOffsets = frameOffsets;
        }

        internal Vector2 PhysicalSize { get; }
        internal Rect ContentBounds { get; }
        internal IReadOnlyList<Vector2> FrameOffsets { get; }

        internal static TextBoxVerticalBlockLayout Layout(IReadOnlyList<Paragraph> paragraphs,
            float inlineExtent, float paragraphSpacing)
        {
            return Layout(paragraphs, inlineExtent, paragraphSpacing, null);
        }

        internal static Vector2[] ResolveMargins(IReadOnlyList<MarginValue> tops,
            IReadOnlyList<MarginValue> bottoms, float contentHeight,
            TextBoxRenderTargetSettings settings)
        {
            var result = new Vector2[tops.Count];
            for (var index = 0; index < result.Length; ++index)
            {
                result[index] = new Vector2(ResolveMargin(tops[index], contentHeight, settings),
                    ResolveMargin(bottoms[index], contentHeight, settings));
            }
            return result;
        }

        internal static Vector2[] ResolveBlockMargins(IReadOnlyList<MarginValue> rights,
            IReadOnlyList<MarginValue> lefts, float contentWidth,
            TextBoxRenderTargetSettings settings)
        {
            var context = settings.VerticalMarginContext;
            var referenceWidth = context.HasValue && context.Value.HasContainerWidth
                ? context.Value.ContainerWidth
                : contentWidth;
            var result = new Vector2[rights.Count];
            for (var index = 0; index < result.Length; ++index)
            {
                result[index] = new Vector2(ResolveMargin(rights[index], referenceWidth, settings),
                    ResolveMargin(lefts[index], referenceWidth, settings));
            }
            return result;
        }

        private static float ResolveMargin(MarginValue margin, float contentHeight,
            TextBoxRenderTargetSettings settings)
        {
            if (!IsValidMargin(margin))
            {
                throw new NotSupportedException("Vertical block margins require finite nonnegative lengths.");
            }
            var context = settings.VerticalMarginContext;
            double reference = 1;
            switch (margin.Unit)
            {
                case MarginUnit.Em: reference = settings.TextStyle.size; break;
                case MarginUnit.ContainerPercent: reference = contentHeight / 100.0; break;
                case MarginUnit.Vw:
                case MarginUnit.Vh:
                    if (margin.Value != 0f && (!context.HasValue || !context.Value.HasViewport))
                    {
                        throw new InvalidOperationException("Vertical block viewport margin requires a root Canvas viewport.");
                    }
                    reference = !context.HasValue ? 0 :
                        (margin.Unit == MarginUnit.Vw ? context.Value.ViewportWidth : context.Value.ViewportHeight) / 100.0;
                    break;
            }
            return Finite(margin.Value * reference);
        }

        internal static bool IsValidMargin(MarginValue value)
        {
            return !value.Auto && FloatUtil.IsFinite(value.Value) && value.Value >= 0f &&
                   (value.Unit == MarginUnit.Px || value.Unit == MarginUnit.Em ||
                    value.Unit == MarginUnit.ContainerPercent || value.Unit == MarginUnit.Vw ||
                    value.Unit == MarginUnit.Vh);
        }

        internal static bool UsesFiniteInlineExtent(TextBoxRenderTargetSettings settings)
        {
            return settings.ParagraphStyle.singleLine
                ? settings.TextOverflow == TextOverflow.Ellipsis
                : settings.WrapMode == TextBoxWrapMode.Wrap;
        }

        internal static TextBoxVerticalBlockLayout Layout(IReadOnlyList<Paragraph> paragraphs,
            float inlineExtent, float paragraphSpacing, IReadOnlyList<Vector2>? margins)
        {
            return Layout(paragraphs, inlineExtent, paragraphSpacing, margins, null);
        }

        internal static TextBoxVerticalBlockLayout Layout(IReadOnlyList<Paragraph> paragraphs,
            float inlineExtent, float paragraphSpacing, IReadOnlyList<Vector2>? margins,
            IReadOnlyList<Vector2>? blockMargins)
        {
            var metrics = new ParagraphLayoutMetrics[paragraphs.Count];
            for (var index = 0; index < paragraphs.Count; ++index)
            {
                var margin = margins?[index] ?? Vector2.zero;
                var extent = float.IsPositiveInfinity(inlineExtent) ? inlineExtent :
                    Finite(Math.Max(0.0, (double)inlineExtent - margin.x - margin.y));
                paragraphs[index].LayoutWithInlineExtent(extent);
                metrics[index] = paragraphs[index].LayoutMetrics;
            }
            return Compose(metrics, paragraphSpacing, margins, blockMargins);
        }

        internal static TextBoxVerticalBlockLayout Compose(
            IReadOnlyList<ParagraphLayoutMetrics> metrics, float paragraphSpacing,
            IReadOnlyList<Vector2>? margins = null,
            IReadOnlyList<Vector2>? blockMargins = null)
        {
            if (!FloatUtil.IsFinite(paragraphSpacing) || paragraphSpacing < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(paragraphSpacing));
            }

            double width = metrics.Count > 0 ? BlockMargin(blockMargins, 0).x : 0f;
            var height = 0f;
            for (var index = 0; index < metrics.Count; ++index)
            {
                var current = metrics[index];
                if (current.WritingMode != WritingMode.VerticalRl ||
                    !FloatUtil.IsFinite(current.PhysicalSize.x) ||
                    !FloatUtil.IsFinite(current.PhysicalSize.y) ||
                    current.PhysicalSize.x < 0f || current.PhysicalSize.y < 0f)
                {
                    throw new InvalidOperationException("Invalid vertical block metrics.");
                }
                width += current.PhysicalSize.x;
                var blockMargin = BlockMargin(blockMargins, index);
                if (blockMargin.x < 0f || blockMargin.y < 0f)
                {
                    throw new InvalidOperationException("Invalid vertical block progression margins.");
                }
                if (index > 0)
                {
                    width += paragraphSpacing;
                }
                width += index + 1 < metrics.Count
                    ? Math.Max(blockMargin.y, BlockMargin(blockMargins, index + 1).x)
                    : blockMargin.y;
                var margin = margins?[index] ?? Vector2.zero;
                if (margin.x < 0f || margin.y < 0f)
                {
                    throw new InvalidOperationException("Invalid vertical block margins.");
                }
                height = Math.Max(height, Finite((double)margin.x + current.PhysicalSize.y + margin.y));
            }

            var physicalWidth = Finite(width);
            var offsets = new Vector2[metrics.Count];
            var bounds = default(Rect);
            var hasBounds = false;
            double left = metrics.Count > 0 ? BlockMargin(blockMargins, metrics.Count - 1).y : 0f;
            for (var index = metrics.Count - 1; index >= 0; --index)
            {
                var offset = new Vector2(Finite(left), margins?[index].x ?? 0f);
                offsets[index] = offset;
                var current = metrics[index].ContentBounds;
                Finite(current.xMin);
                Finite(current.xMax);
                Finite(current.yMin);
                Finite(current.yMax);
                if (current.width > 0f && current.height > 0f)
                {
                    var translated = Rect.MinMaxRect(Finite((double)current.xMin + offset.x),
                        Finite((double)current.yMin + offset.y), Finite((double)current.xMax + offset.x),
                        Finite((double)current.yMax + offset.y));
                    bounds = hasBounds
                        ? Rect.MinMaxRect(Math.Min(bounds.xMin, translated.xMin),
                            Math.Min(bounds.yMin, translated.yMin),
                            Math.Max(bounds.xMax, translated.xMax),
                            Math.Max(bounds.yMax, translated.yMax))
                        : translated;
                    Finite(bounds.width);
                    Finite(bounds.height);
                    hasBounds = true;
                }
                if (index > 0)
                {
                    left += (double)metrics[index].PhysicalSize.x + paragraphSpacing +
                            Math.Max(BlockMargin(blockMargins, index).x,
                                BlockMargin(blockMargins, index - 1).y);
                }
            }
            return new TextBoxVerticalBlockLayout(new Vector2(physicalWidth, height), bounds,
                offsets);
        }

        private static float Finite(double value)
        {
            if (!double.IsFinite(value) || value > float.MaxValue || value < -float.MaxValue)
            {
                throw new InvalidOperationException("Vertical block layout is not finite.");
            }
            return (float)value;
        }

        private static Vector2 BlockMargin(IReadOnlyList<Vector2>? margins, int index)
        {
            return margins?[index] ?? Vector2.zero;
        }
    }
}
