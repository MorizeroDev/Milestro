using System;
using System.Collections.Generic;
using System.Text;
using Milestro.Extensions;
using Milestro.Model;
using Milestro.Skia;
using Milestro.Skia.TextLayout;
using Milestro.Util;
using UnityEngine;

namespace Milestro.Components.Internal
{
    internal sealed class TextBoxRenderTarget : ITextBoxRenderTarget
    {
        private static readonly Rect DefaultUvRect = new Rect(0f, 0f, 1f, 1f);

        private ManagedRenderTextureSurface? surface;
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
        private float fitScale = 1f;
        private Vector2 contentSize;
        private Vector2 viewportSize;
        private Vector2 maxScrollOffset;
        private Vector2Int outputVisibleSizePixels = Vector2Int.one;
        private IReadOnlyList<RichTextParser.LinkAnnotation> linkAnnotations =
            Array.Empty<RichTextParser.LinkAnnotation>();
        private TextBoxContentSnapshot contentSnapshot;
        private bool hasContentSnapshot;
        private string contentSource = "";
        private long contentVersion;
        private bool hasContentSource;
        private TextBoxLinkGeometryPublication linkGeometryPublication;
        private long pendingCompletionToken;
        private TextBoxRenderViewport pendingViewport;
        private ColorSpace pendingColorSpace;

        public Texture? OutputTexture => surface?.Texture;
        public Rect OutputUvRect => surface != null
            ? ResolveOutputUvRect(surface.DisplayUvRect,
                ScreenSpaceRasterMetrics.RasterizeVisibleSize(outputVisibleSizePixels,
                    surface.EffectiveRasterScale,
                    new Vector2Int(surface.Width, surface.Height)),
                new Vector2Int(surface.Width, surface.Height))
            : DefaultUvRect;
        public int OutputWidth => surface?.Width ?? 0;
        public int OutputHeight => surface?.Height ?? 0;
        public bool HasOutput => surface?.HasCompletedOutput ?? false;
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
            linkGeometryPublication.InvalidatePublished();
            linkGeometryPublication.RequestRebuild();
        }

        public void MarkPaintChanged()
        {
            paintChanged = true;
            linkGeometryPublication.InvalidatePublished();
            linkGeometryPublication.RequestRebuild();
        }

        public bool TryHitLink(Vector2 normalizedViewportPoint, out TextBoxLinkHit hit)
        {
            hit = default;
            if (!linkGeometryPublication.TryGetPublished(out var snapshot, out var generation) ||
                !FloatUtil.IsFinite(normalizedViewportPoint.x) ||
                !FloatUtil.IsFinite(normalizedViewportPoint.y) ||
                normalizedViewportPoint.x < 0f ||
                normalizedViewportPoint.x > 1f ||
                normalizedViewportPoint.y < 0f ||
                normalizedViewportPoint.y > 1f)
            {
                return false;
            }

            var outputPoint = new Vector2(normalizedViewportPoint.x * snapshot.VisibleOutputSize.x,
                normalizedViewportPoint.y * snapshot.VisibleOutputSize.y);
            if (!snapshot.ClipRect.Contains(outputPoint))
            {
                return false;
            }

            var fitScale = snapshot.FitScale > 0f && float.IsFinite(snapshot.FitScale) ? snapshot.FitScale : 1f;
            var paragraphPoint = (outputPoint - snapshot.PaintPosition) / fitScale;
            foreach (var annotation in snapshot.Links)
            {
                if (!snapshot.Paragraph.HitTestRange(annotation.StartUtf16, annotation.EndUtf16, paragraphPoint))
                {
                    continue;
                }

                hit = new TextBoxLinkHit(annotation.Href,
                    annotation.Id,
                    annotation.Occurrence,
                    generation);
                return true;
            }

            return false;
        }

        public bool TryGetContentSnapshot(out TextBoxContentSnapshot snapshot)
        {
            snapshot = contentSnapshot;
            return hasContentSnapshot;
        }

        public bool TryGetLinkGeometrySnapshot(out TextBoxLinkGeometrySnapshot snapshot, out long generation)
        {
            return linkGeometryPublication.TryGetPublished(out snapshot, out generation);
        }

        public bool Rebuild(TextBoxRenderViewport viewport,
            ColorSpace colorSpace,
            TextBoxRenderTargetSettings settings,
            bool forceText,
            UnityEngine.Object? logContext)
        {
            ValidateMargin(settings.Margin);
            if (forceText)
            {
                layoutChanged = true;
                paintChanged = true;
                linkGeometryPublication.InvalidatePublished();
            }

            if (linkGeometryPublication.HasPending)
            {
                if (forceText || layoutChanged || paintChanged ||
                    !pendingViewport.Matches(viewport) || pendingColorSpace != colorSpace)
                {
                    linkGeometryPublication.RequestRebuild();
                }
                return false;
            }

            var layoutSizePixels = NormalizeSize(viewport.LayoutSizePixels);
            var outputSizePixels = NormalizeSize(viewport.OutputSizePixels);
            var visibleOutputSizePixels = ClampVisibleOutputSize(NormalizeSize(viewport.VisibleOutputSizePixels),
                outputSizePixels);
            outputVisibleSizePixels = visibleOutputSizePixels;

            var needsDraw = false;
            var surfaceReady = true;
            if (viewport.DrawOutput)
            {
                surfaceReady = EnsureSurface(outputSizePixels,
                        colorSpace,
                        viewport.UseScreenSpaceRasterization,
                        viewport.DesiredRasterScale,
                        logContext,
                        out var surfaceChanged);
                if (!surfaceReady)
                {
                    needsDraw = true;
                }
                else
                {
                    needsDraw = surfaceChanged;
                }
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
            if (linkAnnotations.Count > 0)
            {
                linkGeometryPublication.RequireControlledSubmission();
            }

            needsDraw |= ResizeParagraph(paragraph, settings, layoutSizePixels);
            needsDraw |= UpdateScrollMetrics(settings,
                layoutSizePixels,
                visibleOutputSizePixels,
                viewport);
            needsDraw |= paintChanged;
            if (!viewport.DrawOutput)
            {
                linkGeometryPublication.InvalidatePublished();
                layoutChanged = false;
                paintChanged = false;
                return true;
            }

            if (!surfaceReady || surface == null || !surface.HasSurface)
            {
                layoutChanged = false;
                paintChanged = true;
                linkGeometryPublication.InvalidatePublished();
                return false;
            }

            if (!needsDraw)
            {
                return true;
            }

            if (linkGeometryPublication.RequiresControlledSubmission &&
                !linkGeometryPublication.CanBeginControlledSubmission)
            {
                layoutChanged = false;
                paintChanged = true;
                return false;
            }

            var commands = BuildRenderCommands(settings,
                viewport,
                layoutSizePixels,
                visibleOutputSizePixels,
                logContext,
                out var paintPosition,
                out var clipRect);
            var hasHitGeometry = paragraph != null &&
                                 linkAnnotations.Count > 0 &&
                                 clipRect.width > 0f &&
                                 clipRect.height > 0f;
            var hitGeometry = hasHitGeometry
                ? new TextBoxLinkGeometrySnapshot(contentVersion,
                    paragraph!,
                    linkAnnotations,
                    BuildLinkGeometry(paragraph!, linkAnnotations),
                    paintPosition,
                    clipRect,
                    visibleOutputSizePixels,
                    fitScale)
                : default;
            if (!TrySubmit(commands, hasHitGeometry, hitGeometry))
            {
                paintChanged = true;
                return false;
            }

            pendingViewport = viewport;
            pendingColorSpace = colorSpace;
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
            linkAnnotations = Array.Empty<RichTextParser.LinkAnnotation>();
            linkGeometryPublication.Reset();
            pendingCompletionToken = 0;
            pendingViewport = default;
            pendingColorSpace = default;
            MarkOutputChanged();
        }

        private bool EnsureSurface(Vector2Int sizePixels,
            ColorSpace colorSpace,
            bool useScreenSpaceRasterization,
            float desiredRasterScale,
            UnityEngine.Object? logContext,
            out bool changed)
        {
            sizePixels = NormalizeSize(sizePixels);
            var currentSurface = surface;
            if (currentSurface == null)
            {
                currentSurface = new ManagedRenderTextureSurface();
                SetSurface(currentSurface);
            }

            var ready = useScreenSpaceRasterization
                ? currentSurface.TryEnsureScreenSpace(sizePixels,
                    desiredRasterScale,
                    colorSpace,
                    logContext,
                    out changed)
                : currentSurface.EnsureExact(sizePixels, colorSpace, out changed);
            if (changed)
            {
                MarkOutputChanged();
            }

            return ready;
        }

        private bool TrySubmit(UnitySkiaRenderCommandList commands,
            bool hasHitGeometry,
            TextBoxLinkGeometrySnapshot hitGeometry)
        {
            if (surface == null)
            {
                throw new InvalidOperationException("Milestro TextBox render target has no surface.");
            }

            if (!linkGeometryPublication.RequiresControlledSubmission && !hasHitGeometry)
            {
                if (!surface.TrySubmit(commands))
                {
                    return false;
                }

                linkGeometryPublication.TrackOrdinarySubmission();
                return true;
            }

            if (!linkGeometryPublication.TryBegin(hasHitGeometry, hitGeometry, out var token))
            {
                throw new InvalidOperationException("Milestro TextBox render target already has a pending submission.");
            }

            pendingCompletionToken = token;
            try
            {
                if (surface.TrySubmit(commands))
                {
                    return true;
                }
            }
            catch
            {
                linkGeometryPublication.Cancel(token, out _);
                pendingCompletionToken = 0;
                throw;
            }

            linkGeometryPublication.Cancel(token, out _);
            pendingCompletionToken = 0;
            return false;
        }

        private void SetSurface(ManagedRenderTextureSurface nextSurface)
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
            linkGeometryPublication.Reset();
            pendingCompletionToken = 0;
            pendingViewport = default;
            pendingColorSpace = default;
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
            if (!linkGeometryPublication.HasPending)
            {
                if (!linkGeometryPublication.CompleteOrdinarySubmission())
                {
                    return;
                }

                HandleCompletedOutput(status);
                RenderEventCompleted?.Invoke(status);
                return;
            }

            var token = pendingCompletionToken;
            if (!linkGeometryPublication.Complete(token, status, out var needsRebuild))
            {
                return;
            }

            pendingCompletionToken = 0;
            pendingViewport = default;
            pendingColorSpace = default;
            paintChanged |= needsRebuild;

            HandleCompletedOutput(status);
            RenderEventCompleted?.Invoke(status);
        }

        private void HandleCompletedOutput(UnitySkiaRenderTextureSurface.RenderSubmissionStatus status)
        {
            if (status == UnitySkiaRenderTextureSurface.RenderSubmissionStatus.Drawn)
            {
                MarkOutputChanged();
            }
            else
            {
                paintChanged = true;
            }
        }

        private Paragraph BuildParagraph(TextBoxRenderTargetSettings settings, Vector2Int layoutSizePixels)
        {
            var result = CreateParagraph(settings, out var payload);
            var plainText = PlainText(payload);
            paragraphPlainText = plainText;
            linkAnnotations = payload.Links.Count == 0
                ? Array.Empty<RichTextParser.LinkAnnotation>()
                : new List<RichTextParser.LinkAnnotation>(payload.Links);
            if (!hasContentSource || !string.Equals(contentSource, settings.Content, StringComparison.Ordinal))
            {
                unchecked
                {
                    ++contentVersion;
                    if (contentVersion == 0)
                    {
                        ++contentVersion;
                    }
                }
                contentSource = settings.Content;
                hasContentSource = true;
            }
            contentSnapshot = new TextBoxContentSnapshot(contentVersion, plainText, linkAnnotations);
            hasContentSnapshot = true;
            ResizeParagraph(result, settings, layoutSizePixels, true);
            return result;
        }

        private static IReadOnlyList<TextBoxLinkGeometry> BuildLinkGeometry(
            Paragraph source,
            IReadOnlyList<RichTextParser.LinkAnnotation> links)
        {
            var geometry = new List<TextBoxLinkGeometry>(links.Count);
            foreach (var link in links)
            {
                geometry.Add(new TextBoxLinkGeometry(link.Occurrence,
                    source.GetRectsForRange(link.StartUtf16, link.EndUtf16)));
            }

            return geometry;
        }

        private Paragraph BuildPaintParagraph(TextBoxRenderTargetSettings settings)
        {
            var result = CreateParagraph(settings, out _);
            result.Layout(Math.Max(1, layoutWidth));
            return result;
        }

        private Paragraph CreateParagraph(TextBoxRenderTargetSettings settings,
            out RichTextParser.ParagraphPayload payload)
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
            payload = parser.ConvertToSegments();
            return payload.ToParagraph(paragraphStyle, textStyle);
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

            var viewportWidth = ContentViewportWidth(settings, layoutSizePixels);
            var alignmentChanged = !Mathf.Approximately(paragraphAlignmentOffset, 0f);
            paragraphAlignmentOffset = 0f;

            if (!ShouldApplyAutoSize(settings))
            {
                fitScale = 1f;
                if (viewportWidth == layoutWidth && !force)
                {
                    return alignmentChanged;
                }

                layoutWidth = viewportWidth;
                targetParagraph.Layout(layoutWidth);
                paragraphContentWidth = layoutWidth;
                paragraphVisualWidth = Mathf.Clamp(targetParagraph.LongestLine, 0f, layoutWidth);
                paragraphVisualLeft = ResolveParagraphVisualLeft(settings, paragraphVisualWidth, layoutWidth);
                paragraphHeight = Mathf.Max(0f, targetParagraph.Height);
                return true;
            }

            return ResizeAutoSizeWrapParagraph(targetParagraph,
                settings,
                layoutSizePixels,
                viewportWidth,
                force,
                alignmentChanged);
        }

        private bool ShouldApplyAutoSize(TextBoxRenderTargetSettings settings)
        {
            return settings.AutoSize && settings.TextOverflow != TextOverflow.Ellipsis;
        }

        private bool ResizeAutoSizeWrapParagraph(Paragraph targetParagraph,
            TextBoxRenderTargetSettings settings,
            Vector2Int layoutSizePixels,
            int viewportWidth,
            bool force,
            bool alignmentChanged)
        {
            var viewportHeight = ContentViewportHeight(settings, layoutSizePixels);
            float scale;
            var finalVirtualWidth = Mathf.Max(1, viewportWidth);
            if (settings.AutoSizeWrapMode == TextAutoSizeWrapMode.Wrap)
            {
                var probeWidth = -1;
                var probeSize = Vector2.zero;
                scale = TextAutoSizeScale.SearchFit(candidate =>
                    {
                        var candidateWidth = CeilToPositiveInt(viewportWidth / candidate);
                        if (candidateWidth != probeWidth)
                        {
                            probeWidth = candidateWidth;
                            targetParagraph.Layout(candidateWidth);
                            probeSize = new Vector2(targetParagraph.LongestLine,
                                Mathf.Max(0f, targetParagraph.Height));
                        }

                        return probeSize;
                    },
                    viewportWidth,
                    viewportHeight,
                    settings.MinSize / settings.Size,
                    settings.MaxSize / settings.Size);
                finalVirtualWidth = CeilToPositiveInt(viewportWidth / scale);
                if (probeWidth != finalVirtualWidth)
                {
                    targetParagraph.Layout(finalVirtualWidth);
                }
            }
            else
            {
                if (finalVirtualWidth != layoutWidth || force)
                {
                    targetParagraph.Layout(finalVirtualWidth);
                }

                scale = TextAutoSizeScale.Resolve(targetParagraph.LongestLine,
                    Mathf.Max(0f, targetParagraph.Height),
                    viewportWidth,
                    viewportHeight,
                    settings.Size,
                    settings.MinSize,
                    settings.MaxSize);
            }

            var changed = alignmentChanged || force || finalVirtualWidth != layoutWidth;
            layoutWidth = finalVirtualWidth;
            var height = Mathf.Max(0f, targetParagraph.Height);
            var scaleChanged = !Mathf.Approximately(fitScale, scale);
            fitScale = scale;
            paragraphContentWidth = layoutWidth;
            paragraphVisualWidth = Mathf.Clamp(targetParagraph.LongestLine * fitScale, 0f, viewportWidth);
            paragraphVisualLeft = ResolveParagraphVisualLeft(settings, paragraphVisualWidth, viewportWidth);
            paragraphHeight = height * fitScale;
            return changed || scaleChanged;
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

            var unscaledHeight = Mathf.Max(0f, targetParagraph.Height);
            var scale = 1f;
            if (ShouldApplyAutoSize(settings))
            {
                scale = TextAutoSizeScale.Resolve(paragraphContentWidth,
                    unscaledHeight,
                    viewportWidth,
                    ContentViewportHeight(settings, layoutSizePixels),
                    settings.Size,
                    settings.MinSize,
                    settings.MaxSize);
            }

            var newLayoutWidth = CeilToPositiveInt(Paragraph.ResolveNoWrapLayoutWidth(viewportWidth, paragraphContentWidth));
            var widthChanged = newLayoutWidth != layoutWidth;
            if (widthChanged || force)
            {
                layoutWidth = newLayoutWidth;
                targetParagraph.Layout(layoutWidth);
            }

            var scaleChanged = !Mathf.Approximately(fitScale, scale);
            fitScale = scale;

            var horizontalLayout = TextBoxNoWrapHorizontalLayout.Resolve(true,
                settings.TextAlign,
                settings.TextDirection,
                viewportWidth,
                paragraphContentWidth * fitScale,
                layoutWidth * fitScale);
            var metricsChanged = !Mathf.Approximately(paragraphVisualWidth, horizontalLayout.ContentWidth) ||
                                 !Mathf.Approximately(paragraphVisualLeft, horizontalLayout.LogicalVisualLeft) ||
                                 !Mathf.Approximately(paragraphAlignmentOffset,
                                     horizontalLayout.LayoutAlignmentOffset);
            paragraphVisualWidth = paragraphContentWidth * fitScale;
            paragraphVisualLeft = horizontalLayout.LogicalVisualLeft;
            paragraphAlignmentOffset = horizontalLayout.LayoutAlignmentOffset;
            paragraphHeight = unscaledHeight * fitScale;
            return widthChanged || force || metricsChanged || scaleChanged;
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
                paragraph != null ? paragraphContentWidth * fitScale : 0f,
                paragraph != null ? layoutWidth * fitScale : 0f);
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
            UnityEngine.Object? logContext,
            out Vector2 paintPosition,
            out Rect clipRect)
        {
            var commands = new UnitySkiaRenderCommandList();
            paintPosition = Vector2.zero;
            clipRect = default;
            if (paragraph != null)
            {
                var margin = ResolveMargin(settings, layoutSizePixels);
                var layoutViewportSize = new Vector2(ContentViewportWidth(settings, layoutSizePixels),
                    ContentViewportHeight(settings, layoutSizePixels));
                var autoOffset = AutoContentOffset(settings, layoutViewportSize);
                paintPosition = new Vector2(
                    margin.Left + autoOffset.x + TextBoxNoWrapHorizontalLayout.ResolvePaintOffsetX(
                        paragraphAlignmentOffset,
                        scrollOffset.x,
                        viewport.VisualScrollOffset.x),
                    margin.Top + autoOffset.y - scrollOffset.y - viewport.VisualScrollOffset.y);
                clipRect = ResolveClipRect(settings, viewport, margin, outputSizePixels);
                commands.DrawParagraphSnapshot(() => BuildPaintParagraph(settings), paintPosition, clipRect, fitScale);
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
