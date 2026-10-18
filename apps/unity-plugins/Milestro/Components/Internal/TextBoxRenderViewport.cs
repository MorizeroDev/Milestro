using System;
using UnityEngine;

namespace Milestro.Components.Internal
{
    internal readonly struct TextBoxRenderViewport
    {
        private TextBoxRenderViewport(Vector2Int layoutSizePixels,
            Vector2Int outputSizePixels,
            Vector2Int visibleOutputSizePixels,
            TextBoxHorizontalScrollState horizontalScrollState,
            TextBoxHorizontalScrollRequest horizontalScrollRequest,
            float requestedScrollY,
            Vector2 visualScrollOffset,
            bool drawOutput,
            bool sliceOutput,
            bool useScreenSpaceRasterization,
            float desiredRasterScale)
        {
            LayoutSizePixels = layoutSizePixels;
            OutputSizePixels = outputSizePixels;
            VisibleOutputSizePixels = visibleOutputSizePixels;
            HorizontalScrollState = horizontalScrollState;
            HorizontalScrollRequest = horizontalScrollRequest;
            RequestedScrollY = requestedScrollY;
            VisualScrollOffset = visualScrollOffset;
            DrawOutput = drawOutput;
            SliceOutput = sliceOutput;
            UseScreenSpaceRasterization = useScreenSpaceRasterization;
            DesiredRasterScale = desiredRasterScale;
        }

        public Vector2Int LayoutSizePixels { get; }
        public Vector2Int OutputSizePixels { get; }
        public Vector2Int VisibleOutputSizePixels { get; }
        public TextBoxHorizontalScrollState HorizontalScrollState { get; }
        public TextBoxHorizontalScrollRequest HorizontalScrollRequest { get; }
        public bool HasHorizontalScrollRequest => HorizontalScrollRequest.HasValue;
        public float RequestedScrollY { get; }
        public Vector2 VisualScrollOffset { get; }
        public bool DrawOutput { get; }
        public bool SliceOutput { get; }
        public bool UseScreenSpaceRasterization { get; }
        public float DesiredRasterScale { get; }

        public static TextBoxRenderViewport Fixed(Vector2Int sizePixels,
            TextBoxHorizontalScrollState horizontalScrollState,
            Vector2 scrollOffset,
            Vector2 visualScrollOffset)
        {
            return new TextBoxRenderViewport(sizePixels,
                sizePixels,
                sizePixels,
                horizontalScrollState,
                TextBoxHorizontalScrollRequest.FromValue(scrollOffset.x),
                scrollOffset.y,
                visualScrollOffset,
                true,
                false,
                false,
                1f);
        }

        public static TextBoxRenderViewport Invisible(Vector2Int layoutSizePixels,
            TextBoxHorizontalScrollState horizontalScrollState)
        {
            return new TextBoxRenderViewport(layoutSizePixels,
                Vector2Int.one,
                Vector2Int.one,
                horizontalScrollState,
                TextBoxHorizontalScrollRequest.None,
                0f,
                Vector2.zero,
                false,
                true,
                false,
                1f);
        }

        public static TextBoxRenderViewport FlowSlice(Vector2Int layoutSizePixels,
            Vector2Int outputSizePixels,
            Vector2Int visibleOutputSizePixels,
            TextBoxHorizontalScrollState horizontalScrollState,
            float localStartY)
        {
            return new TextBoxRenderViewport(layoutSizePixels,
                outputSizePixels,
                visibleOutputSizePixels,
                horizontalScrollState,
                TextBoxHorizontalScrollRequest.None,
                localStartY,
                Vector2.zero,
                true,
                true,
                false,
                1f);
        }

        internal TextBoxRenderViewport WithScreenSpaceRasterization(float desiredRasterScale)
        {
            return new TextBoxRenderViewport(LayoutSizePixels,
                OutputSizePixels,
                VisibleOutputSizePixels,
                HorizontalScrollState,
                HorizontalScrollRequest,
                RequestedScrollY,
                VisualScrollOffset,
                DrawOutput,
                SliceOutput,
                true,
                desiredRasterScale);
        }

        internal TextBoxHorizontalScrollState ResolveHorizontalScroll(TextBoxNoWrapHorizontalLayout layout)
        {
            return HorizontalScrollState.Resolve(layout, HorizontalScrollRequest);
        }

        internal bool Matches(TextBoxRenderViewport other)
        {
            return LayoutSizePixels == other.LayoutSizePixels &&
                   OutputSizePixels == other.OutputSizePixels &&
                   VisibleOutputSizePixels == other.VisibleOutputSizePixels &&
                   TextBoxHorizontalScrollState.OffsetsEqual(HorizontalScrollState.ScrollX,
                       other.HorizontalScrollState.ScrollX) &&
                   TextBoxHorizontalScrollState.OffsetsEqual(HorizontalScrollState.DefaultAnchor,
                       other.HorizontalScrollState.DefaultAnchor) &&
                   HorizontalScrollState.HasLayout == other.HorizontalScrollState.HasLayout &&
                   HorizontalScrollState.FollowsDefaultAnchor == other.HorizontalScrollState.FollowsDefaultAnchor &&
                   HorizontalScrollRequest.HasValue == other.HorizontalScrollRequest.HasValue &&
                   TextBoxHorizontalScrollState.OffsetsEqual(HorizontalScrollRequest.Value,
                       other.HorizontalScrollRequest.Value) &&
                   TextBoxHorizontalScrollState.OffsetsEqual(RequestedScrollY, other.RequestedScrollY) &&
                   TextBoxHorizontalScrollState.OffsetsEqual(VisualScrollOffset.x, other.VisualScrollOffset.x) &&
                   TextBoxHorizontalScrollState.OffsetsEqual(VisualScrollOffset.y, other.VisualScrollOffset.y) &&
                   DrawOutput == other.DrawOutput &&
                   SliceOutput == other.SliceOutput &&
                   UseScreenSpaceRasterization == other.UseScreenSpaceRasterization &&
                   BitConverter.SingleToInt32Bits(DesiredRasterScale) ==
                   BitConverter.SingleToInt32Bits(other.DesiredRasterScale);
        }
    }
}
