using System;
using System.Text;
using Milestro.Extensions;
using Milestro.Model;
using Milestro.Skia;
using Milestro.Skia.TextLayout;
using Milestro.Util;
using UnityEngine;

namespace Milestro.Components.Internal
{
    internal interface ITextBoxRenderTarget : IDisposable
    {
        Texture? OutputTexture { get; }
        Rect OutputUvRect { get; }
        int OutputWidth { get; }
        int OutputHeight { get; }
        bool HasOutput { get; }
        long OutputVersion { get; }
        Vector2 ScrollOffset { get; }
        Vector2 ContentSize { get; }
        Vector2 ViewportSize { get; }
        Vector2 MaxScrollOffset { get; }
        TextBoxHorizontalScrollState HorizontalScrollState { get; }

        event Action<UnitySkiaRenderTextureSurface.RenderSubmissionStatus>? RenderEventCompleted;

        void MarkPropertiesChanged();
        void MarkPaintChanged();
        bool Rebuild(TextBoxRenderViewport viewport,
            ColorSpace colorSpace,
            TextBoxRenderTargetSettings settings,
            bool forceText,
            UnityEngine.Object? logContext);
    }

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
            bool sliceOutput)
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
                false);
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
                true);
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
                true);
        }

        internal TextBoxHorizontalScrollState ResolveHorizontalScroll(TextBoxNoWrapHorizontalLayout layout)
        {
            return HorizontalScrollState.Resolve(layout, HorizontalScrollRequest);
        }
    }

    internal sealed class TextBoxRenderTarget : ITextBoxRenderTarget
    {
        private static readonly Rect DefaultUvRect = new Rect(0f, 0f, 1f, 1f);

        private UnityAutoRenderTextureSurface? surface;
        private Paragraph? paragraph;
        private bool layoutChanged = true;
        private bool paintChanged = true;
        private int layoutWidth = 640;
        private string paragraphPlainText = "";
        private float paragraphHeight;
        private float paragraphContentWidth;
        private float paragraphVisualLeft;
        private float paragraphVisualWidth;
        private float paragraphAlignmentOffset;
        private TextBoxHorizontalScrollState horizontalScrollState;
        private long outputVersion;
        private Vector2 scrollOffset;
        private Vector2 contentSize;
        private Vector2 viewportSize;
        private Vector2 maxScrollOffset;
        private Vector2Int outputVisibleSizePixels = Vector2Int.one;

        public Texture? OutputTexture => surface?.Texture;
        public Rect OutputUvRect => surface != null
            ? ResolveOutputUvRect(surface.DisplayUvRect,
                outputVisibleSizePixels,
                new Vector2Int(surface.Width, surface.Height))
            : DefaultUvRect;
        public int OutputWidth => surface?.Width ?? 0;
        public int OutputHeight => surface?.Height ?? 0;
        public bool HasOutput => surface?.Texture != null;
        public long OutputVersion => outputVersion;
        public Vector2 ScrollOffset => scrollOffset;
        public Vector2 ContentSize => contentSize;
        public Vector2 ViewportSize => viewportSize;
        public Vector2 MaxScrollOffset => maxScrollOffset;
        public TextBoxHorizontalScrollState HorizontalScrollState => horizontalScrollState;

        public event Action<UnitySkiaRenderTextureSurface.RenderSubmissionStatus>? RenderEventCompleted;

        public void MarkPropertiesChanged()
        {
            layoutChanged = true;
            paintChanged = true;
        }

        public void MarkPaintChanged()
        {
            paintChanged = true;
        }

        public bool Rebuild(TextBoxRenderViewport viewport,
            ColorSpace colorSpace,
            TextBoxRenderTargetSettings settings,
            bool forceText,
            UnityEngine.Object? logContext)
        {
            ValidateMargin(settings.Margin);
            var layoutSizePixels = NormalizeSize(viewport.LayoutSizePixels);
            var outputSizePixels = NormalizeSize(viewport.OutputSizePixels);
            var visibleOutputSizePixels = ClampVisibleOutputSize(NormalizeSize(viewport.VisibleOutputSizePixels),
                outputSizePixels);
            outputVisibleSizePixels = visibleOutputSizePixels;

            var needsDraw = false;
            if (viewport.DrawOutput)
            {
                needsDraw = EnsureSurface(outputSizePixels, colorSpace);
            }
            else
            {
                ClearSurfaceOutput();
            }

            if (forceText || paragraph == null || layoutChanged)
            {
                ReplaceParagraph(BuildParagraph(settings, layoutSizePixels));
                needsDraw = true;
            }

            needsDraw |= ResizeParagraph(paragraph, settings, layoutSizePixels);
            needsDraw |= UpdateScrollMetrics(settings,
                layoutSizePixels,
                visibleOutputSizePixels,
                viewport);
            needsDraw |= paintChanged;
            if (!viewport.DrawOutput)
            {
                layoutChanged = false;
                paintChanged = false;
                return true;
            }

            if (!needsDraw)
            {
                return true;
            }

            if (!TrySubmit(BuildRenderCommands(settings, viewport, layoutSizePixels, visibleOutputSizePixels, logContext)))
            {
                paintChanged = true;
                return false;
            }

            layoutChanged = false;
            paintChanged = false;
            return true;
        }

        public void Dispose()
        {
            RetireParagraph();
            DisposeSurface();
            paragraphPlainText = "";
            layoutChanged = true;
            paintChanged = true;
            paragraphHeight = 0f;
            paragraphContentWidth = 0f;
            paragraphVisualLeft = 0f;
            paragraphVisualWidth = 0f;
            paragraphAlignmentOffset = 0f;
            horizontalScrollState = default;
            scrollOffset = Vector2.zero;
            contentSize = Vector2.zero;
            viewportSize = Vector2.zero;
            maxScrollOffset = Vector2.zero;
            outputVisibleSizePixels = Vector2Int.one;
            MarkOutputChanged();
        }

        private bool EnsureSurface(Vector2Int sizePixels, ColorSpace colorSpace)
        {
            sizePixels = NormalizeSize(sizePixels);
            if (surface == null || surface.ColorSpace != colorSpace)
            {
                DisposeSurface();
                SetSurface(new UnityAutoRenderTextureSurface(sizePixels.x, sizePixels.y, colorSpace));
                MarkOutputChanged();
                return true;
            }

            if (surface.Width == sizePixels.x && surface.Height == sizePixels.y)
            {
                return false;
            }

            surface.Resize(sizePixels.x, sizePixels.y);
            MarkOutputChanged();
            return true;
        }

        private bool TrySubmit(UnitySkiaRenderCommandList commands)
        {
            if (surface == null)
            {
                throw new InvalidOperationException("Milestro TextBox render target has no surface.");
            }

            if (!surface.TrySubmit(commands))
            {
                return false;
            }

            return true;
        }

        private void SetSurface(UnityAutoRenderTextureSurface nextSurface)
        {
            surface = nextSurface;
            surface.RenderEventCompleted += HandleRenderEventCompleted;
        }

        private void DisposeSurface()
        {
            if (surface == null)
            {
                return;
            }

            surface.RenderEventCompleted -= HandleRenderEventCompleted;
            surface.Dispose();
            surface = null;
        }

        private void ClearSurfaceOutput()
        {
            if (surface == null)
            {
                return;
            }

            DisposeSurface();
            MarkOutputChanged();
        }

        private void HandleRenderEventCompleted(UnitySkiaRenderTextureSurface.RenderSubmissionStatus status)
        {
            if (status == UnitySkiaRenderTextureSurface.RenderSubmissionStatus.Drawn)
            {
                MarkOutputChanged();
            }
            else if (status == UnitySkiaRenderTextureSurface.RenderSubmissionStatus.Skipped)
            {
                paintChanged = true;
            }

            RenderEventCompleted?.Invoke(status);
        }

        private Paragraph BuildParagraph(TextBoxRenderTargetSettings settings, Vector2Int layoutSizePixels)
        {
            var result = CreateParagraph(settings, out var plainText);
            paragraphPlainText = plainText;
            ResizeParagraph(result, settings, layoutSizePixels, true);
            return result;
        }

        private Paragraph BuildPaintParagraph(TextBoxRenderTargetSettings settings)
        {
            var result = CreateParagraph(settings, out _);
            result.Layout(Math.Max(1, layoutWidth));
            return result;
        }

        private Paragraph CreateParagraph(TextBoxRenderTargetSettings settings,
            out string plainText)
        {
            using var paragraphStyle = new ParagraphStyle();
            paragraphStyle.TextAlign = settings.TextAlign;
            paragraphStyle.TextDirection = settings.TextDirection;
            if (settings.SingleLine)
            {
                paragraphStyle.MaxLines = 1;
                if (settings.TextOverflow == TextOverflow.Ellipsis)
                {
                    paragraphStyle.SetEllipsis(settings.EllipsisString);
                }
            }

            using var textStyle = new TextStyle();
            textStyle.SetFontFamilies(settings.FontFamilies);
            textStyle.FontSize = settings.Size;
            textStyle.Locale = settings.Locale;
            textStyle.Color = settings.TextColor;
            if (settings.Weight > FontWeight.Invisible && settings.Weight <= FontWeight.ExtraBlack)
            {
                textStyle.GetFontStyle(out int weight, out var width, out var slant);
                textStyle.SetFontStyle(settings.Weight, width, slant);
            }

            var parser = new RichTextParser.RichTextParser();
            parser.ParseText(settings.Content);
            var segments = parser.ConvertToSegments();
            plainText = PlainText(segments);
            return segments.ToParagraph(paragraphStyle, textStyle);
        }

        private static void ValidateMargin(Margin margin)
        {
            margin.Normalize();
        }

        private bool ResizeParagraph(Paragraph? targetParagraph,
            TextBoxRenderTargetSettings settings,
            Vector2Int layoutSizePixels,
            bool force = false)
        {
            if (targetParagraph == null)
            {
                return false;
            }

            if (ShouldUseWideNoWrapLayout(settings))
            {
                return ResizeNoWrapParagraph(targetParagraph, settings, layoutSizePixels, force);
            }

            var newLayoutWidth = ContentViewportWidth(settings, layoutSizePixels);
            var alignmentChanged = !Mathf.Approximately(paragraphAlignmentOffset, 0f);
            paragraphAlignmentOffset = 0f;
            if (newLayoutWidth == layoutWidth && !force)
            {
                return alignmentChanged;
            }

            layoutWidth = newLayoutWidth;
            targetParagraph.Layout(layoutWidth);
            paragraphContentWidth = layoutWidth;
            paragraphVisualWidth = Mathf.Clamp(targetParagraph.LongestLine, 0f, layoutWidth);
            paragraphVisualLeft = ResolveParagraphVisualLeft(settings, paragraphVisualWidth, layoutWidth);
            paragraphHeight = Mathf.Max(0f, targetParagraph.Height);
            return true;
        }

        private bool ResizeNoWrapParagraph(Paragraph targetParagraph,
            TextBoxRenderTargetSettings settings,
            Vector2Int layoutSizePixels,
            bool force)
        {
            var viewportWidth = ContentViewportWidth(settings, layoutSizePixels);
            if (force)
            {
                targetParagraph.Layout(Paragraph.ResolveNoWrapProbeLayoutWidth(paragraphPlainText,
                    settings.Size,
                    viewportWidth));
                paragraphContentWidth = targetParagraph.ResolveNoWrapContentWidth(paragraphPlainText);
            }

            var newLayoutWidth = CeilToPositiveInt(Paragraph.ResolveNoWrapLayoutWidth(viewportWidth, paragraphContentWidth));
            var widthChanged = newLayoutWidth != layoutWidth;
            if (widthChanged || force)
            {
                layoutWidth = newLayoutWidth;
                targetParagraph.Layout(layoutWidth);
            }

            var horizontalLayout = TextBoxNoWrapHorizontalLayout.Resolve(true,
                settings.TextAlign,
                settings.TextDirection,
                viewportWidth,
                paragraphContentWidth,
                layoutWidth);
            var metricsChanged = !Mathf.Approximately(paragraphVisualWidth, horizontalLayout.ContentWidth) ||
                                 !Mathf.Approximately(paragraphVisualLeft, horizontalLayout.LogicalVisualLeft) ||
                                 !Mathf.Approximately(paragraphAlignmentOffset,
                                     horizontalLayout.LayoutAlignmentOffset);
            paragraphVisualWidth = paragraphContentWidth;
            paragraphVisualLeft = horizontalLayout.LogicalVisualLeft;
            paragraphAlignmentOffset = horizontalLayout.LayoutAlignmentOffset;
            paragraphHeight = Mathf.Max(0f, targetParagraph.Height);
            return widthChanged || force || metricsChanged;
        }

        private bool UpdateScrollMetrics(TextBoxRenderTargetSettings settings,
            Vector2Int layoutSizePixels,
            Vector2Int outputSizePixels,
            TextBoxRenderViewport viewport)
        {
            var layoutViewportWidth = ContentViewportWidth(settings, layoutSizePixels);
            var nextViewportSize = new Vector2(layoutViewportWidth,
                ContentViewportHeight(settings, outputSizePixels));
            var useWideNoWrapLayout = ShouldUseWideNoWrapLayout(settings);
            var horizontalLayout = TextBoxNoWrapHorizontalLayout.Resolve(useWideNoWrapLayout,
                settings.TextAlign,
                settings.TextDirection,
                nextViewportSize.x,
                paragraph != null ? paragraphContentWidth : 0f,
                paragraph != null ? layoutWidth : 0f);
            var nextContentWidth = useWideNoWrapLayout
                ? horizontalLayout.ContentWidth
                : layoutViewportWidth;
            var nextContentSize = new Vector2(nextContentWidth, paragraph != null ? paragraphHeight : 0f);
            var nextMaxScrollOffset = new Vector2(
                Mathf.Max(0f, nextContentSize.x - nextViewportSize.x),
                Mathf.Max(0f, nextContentSize.y - nextViewportSize.y));
            var nextHorizontalScrollState = viewport.ResolveHorizontalScroll(horizontalLayout);
            var nextScrollOffset = new Vector2(
                nextHorizontalScrollState.ScrollX,
                Mathf.Clamp(FloatUtil.IsFinite(viewport.RequestedScrollY) ? viewport.RequestedScrollY : 0f,
                    0f,
                    nextMaxScrollOffset.y));

            var changed = viewportSize != nextViewportSize ||
                          contentSize != nextContentSize ||
                          maxScrollOffset != nextMaxScrollOffset ||
                          scrollOffset != nextScrollOffset;

            viewportSize = nextViewportSize;
            contentSize = nextContentSize;
            maxScrollOffset = nextMaxScrollOffset;
            scrollOffset = nextScrollOffset;
            horizontalScrollState = nextHorizontalScrollState;
            return changed;
        }

        private int ContentViewportWidth(TextBoxRenderTargetSettings settings, Vector2Int containerSizePixels)
        {
            var margin = ResolveMargin(settings, containerSizePixels);
            return CeilToPositiveInt(containerSizePixels.x - margin.FixedHorizontalSize);
        }

        private int ContentViewportHeight(TextBoxRenderTargetSettings settings, Vector2Int containerSizePixels)
        {
            var margin = ResolveMargin(settings, containerSizePixels);
            return CeilToPositiveInt(containerSizePixels.y - margin.FixedVerticalSize);
        }

        private ResolvedMargin ResolveMargin(TextBoxRenderTargetSettings settings, Vector2Int containerSizePixels)
        {
            return settings.Margin.Resolve(new MarginResolveContext(containerSizePixels.x,
                containerSizePixels.y,
                settings.Size));
        }

        private static int CeilToPositiveInt(float value)
        {
            if (!FloatUtil.IsFinite(value) || value <= 0f)
            {
                return 1;
            }

            return value >= int.MaxValue ? int.MaxValue : Mathf.CeilToInt(value);
        }

        private static bool ShouldUseWideNoWrapLayout(TextBoxRenderTargetSettings settings)
        {
            if (settings.SingleLine)
            {
                return settings.TextOverflow != TextOverflow.Ellipsis;
            }

            return settings.WrapMode == TextBoxWrapMode.NoWrap;
        }

        private static float ResolveParagraphVisualLeft(TextBoxRenderTargetSettings settings,
            float visualWidth,
            float containingWidth)
        {
            if (!FloatUtil.IsFinite(visualWidth) ||
                !FloatUtil.IsFinite(containingWidth) ||
                visualWidth <= 0f ||
                visualWidth >= containingWidth)
            {
                return 0f;
            }

            switch (EffectiveAlign(settings))
            {
                case TextAlign.Center:
                    return (containingWidth - visualWidth) * 0.5f;
                case TextAlign.Right:
                    return containingWidth - visualWidth;
                default:
                    return 0f;
            }
        }

        private static TextAlign EffectiveAlign(TextBoxRenderTargetSettings settings)
        {
            switch (settings.TextAlign)
            {
                case TextAlign.Start:
                    return settings.TextDirection == TextDirection.Rtl ? TextAlign.Right : TextAlign.Left;
                case TextAlign.End:
                    return settings.TextDirection == TextDirection.Rtl ? TextAlign.Left : TextAlign.Right;
                default:
                    return settings.TextAlign;
            }
        }

        private static string PlainText(RichTextParser.ParagraphPayload payload)
        {
            var builder = new StringBuilder();
            foreach (var segment in payload.Body)
            {
                builder.Append(segment.Content ?? "");
            }

            return builder.ToString();
        }

        private UnitySkiaRenderCommandList BuildRenderCommands(TextBoxRenderTargetSettings settings,
            TextBoxRenderViewport viewport,
            Vector2Int layoutSizePixels,
            Vector2Int outputSizePixels,
            UnityEngine.Object? logContext)
        {
            var commands = new UnitySkiaRenderCommandList();
            if (paragraph != null)
            {
                var margin = ResolveMargin(settings, layoutSizePixels);
                var layoutViewportSize = new Vector2(ContentViewportWidth(settings, layoutSizePixels),
                    ContentViewportHeight(settings, layoutSizePixels));
                var autoOffset = AutoContentOffset(settings, layoutViewportSize);
                var paintPosition = new Vector2(
                    margin.Left + autoOffset.x + TextBoxNoWrapHorizontalLayout.ResolvePaintOffsetX(
                        paragraphAlignmentOffset,
                        scrollOffset.x,
                        viewport.VisualScrollOffset.x),
                    margin.Top + autoOffset.y - scrollOffset.y - viewport.VisualScrollOffset.y);
                var clipRect = ResolveClipRect(settings, viewport, margin, outputSizePixels);
                commands.DrawParagraphSnapshot(() => BuildPaintParagraph(settings), paintPosition, clipRect);
            }
            else
            {
#if MILESTRO_RENDER_DEBUG_LOG
                Debug.LogWarning("No paragraph selected", logContext);
#endif
            }

            return commands;
        }

        private Rect ResolveClipRect(TextBoxRenderTargetSettings settings,
            TextBoxRenderViewport viewport,
            ResolvedMargin margin,
            Vector2Int outputSizePixels)
        {
            if (settings.TextOverflow == TextOverflow.Overflow)
            {
                return new Rect(0f, 0f, outputSizePixels.x, outputSizePixels.y);
            }

            if (viewport.SliceOutput)
            {
                return new Rect(margin.Left,
                    0f,
                    Mathf.Max(1f, outputSizePixels.x - margin.FixedHorizontalSize),
                    outputSizePixels.y);
            }

            return new Rect(margin.Left,
                margin.Top,
                viewportSize.x,
                viewportSize.y);
        }

        private Vector2 AutoContentOffset(TextBoxRenderTargetSettings settings, Vector2 layoutViewportSize)
        {
            return new Vector2(ResolveAutoMarginOffset(settings.Margin.Left.Auto,
                    settings.Margin.Right.Auto,
                    layoutViewportSize.x,
                    paragraphVisualLeft,
                    paragraphVisualWidth),
                ResolveAutoMarginOffset(settings.Margin.Top.Auto,
                    settings.Margin.Bottom.Auto,
                    layoutViewportSize.y,
                    0f,
                    paragraphHeight));
        }

        private static float ResolveAutoMarginOffset(bool startAuto,
            bool endAuto,
            float viewportLength,
            float contentStart,
            float contentLength)
        {
            if ((!startAuto && !endAuto) ||
                !FloatUtil.IsFinite(viewportLength) ||
                !FloatUtil.IsFinite(contentStart) ||
                !FloatUtil.IsFinite(contentLength) ||
                contentLength <= 0f ||
                contentLength >= viewportLength)
            {
                return 0f;
            }

            var spare = viewportLength - contentLength;
            if (startAuto && endAuto)
            {
                return spare * 0.5f - contentStart;
            }

            return startAuto ? spare - contentStart : -contentStart;
        }

        private void MarkOutputChanged()
        {
            unchecked
            {
                ++outputVersion;
            }
        }

        private void ReplaceParagraph(Paragraph nextParagraph)
        {
            var oldParagraph = paragraph;
            paragraph = nextParagraph;
            DisposeParagraphAfterPendingDraws(oldParagraph);
        }

        private void RetireParagraph()
        {
            var oldParagraph = paragraph;
            paragraph = null;
            DisposeParagraphAfterPendingDraws(oldParagraph);
        }

        private void DisposeParagraphAfterPendingDraws(Paragraph? oldParagraph)
        {
            if (oldParagraph == null)
            {
                return;
            }

            if (surface != null)
            {
                surface.DisposeResourceAfterPendingDraws(oldParagraph);
                return;
            }

            oldParagraph.Dispose();
        }

        private static Vector2Int NormalizeSize(Vector2Int sizePixels)
        {
            return new Vector2Int(Mathf.Max(1, sizePixels.x), Mathf.Max(1, sizePixels.y));
        }

        private static Vector2Int ClampVisibleOutputSize(Vector2Int visibleSizePixels,
            Vector2Int outputSizePixels)
        {
            return new Vector2Int(Mathf.Clamp(visibleSizePixels.x, 1, outputSizePixels.x),
                Mathf.Clamp(visibleSizePixels.y, 1, outputSizePixels.y));
        }

        private static Rect ResolveOutputUvRect(Rect displayUvRect,
            Vector2Int visibleSizePixels,
            Vector2Int outputSizePixels)
        {
            var visibleWidth = Mathf.Clamp(visibleSizePixels.x, 1, outputSizePixels.x);
            var visibleHeight = Mathf.Clamp(visibleSizePixels.y, 1, outputSizePixels.y);
            var widthScale = outputSizePixels.x > 0 ? Mathf.Clamp01((float)visibleWidth / outputSizePixels.x) : 1f;
            var heightScale = outputSizePixels.y > 0 ? Mathf.Clamp01((float)visibleHeight / outputSizePixels.y) : 1f;
            return new Rect(displayUvRect.x,
                displayUvRect.y + displayUvRect.height * (1f - heightScale),
                displayUvRect.width * widthScale,
                displayUvRect.height * heightScale);
        }
    }
}
