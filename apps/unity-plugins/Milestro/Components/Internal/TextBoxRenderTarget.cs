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
        private readonly List<Paragraph> paragraphs = new List<Paragraph>();
        private readonly List<int> blockStartsUtf16 = new List<int>();
        private readonly List<string> blockPlainTexts = new List<string>();
        private readonly List<MarginValue> blockMarginTopValues = new List<MarginValue>();
        private readonly List<MarginValue> blockMarginBottomValues = new List<MarginValue>();
        private readonly List<TextAlign> blockPhysicalAligns = new List<TextAlign>();
        private readonly List<float> blockHeights = new List<float>();
        private readonly List<float> blockMarginTops = new List<float>();
        private readonly List<float> blockMarginBottoms = new List<float>();
        private readonly List<float> blockTops = new List<float>();
        private readonly List<float> blockAlignmentOffsetsX = new List<float>();
        private readonly List<float> blockContentWidths = new List<float>();
        private readonly List<float> probeTops = new List<float>();
        private bool hasHeightDependentBlockMargins;
        private bool layoutChanged = true;
        private bool paintChanged = true;
        private int layoutWidth = 640;
        private int layoutHeight;
        private float paragraphHeight;
        private float paragraphContentWidth;
        private float paragraphVisualLeft;
        private float paragraphVisualWidth;
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
        private ParagraphStyle? cachedParagraphStyle;
        private TextStyle? cachedTextStyle;
        private bool stylesChanged = true;
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
        public bool HasPendingWork => layoutChanged ||
                                      paintChanged ||
                                      linkGeometryPublication.HasPending ||
                                      surface == null ||
                                      !surface.HasCompletedOutput;
        public long OutputVersion => outputVersion;
        public Vector2 ScrollOffset => scrollOffset;
        public Vector2 ContentSize => contentSize;
        public Vector2 ViewportSize => viewportSize;
        public Vector2 MaxScrollOffset => maxScrollOffset;
        public TextBoxHorizontalScrollState HorizontalScrollState => horizontalScrollState;

        public event Action<UnitySkiaRenderTextureSurface.RenderSubmissionStatus>? RenderEventCompleted;

        public void MarkPropertiesChanged()
        {
            stylesChanged = true;
            layoutChanged = true;
            paintChanged = true;
            linkGeometryPublication.InvalidatePublished();
            linkGeometryPublication.RequestRebuild();
        }

        public void MarkContentChanged()
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
                if (!snapshot.TryGetGeometry(annotation.Occurrence, out var geometry))
                {
                    continue;
                }

                foreach (var blockRange in geometry.BlockRanges)
                {
                    if (blockRange.BlockIndex < 0 || blockRange.BlockIndex >= snapshot.Paragraphs.Count)
                    {
                        continue;
                    }

                    var blockPoint = paragraphPoint - blockRange.FrameOffset;
                    if (!snapshot.Paragraphs[blockRange.BlockIndex].HitTestRange(blockRange.StartUtf16,
                            blockRange.EndUtf16,
                            blockPoint))
                    {
                        continue;
                    }

                    hit = new TextBoxLinkHit(annotation.Href,
                        annotation.Id,
                        annotation.Occurrence,
                        generation);
                    return true;
                }
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
                stylesChanged = true;
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

            var profiling = TextBoxPipelineProfiler.Enabled;
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

            var paragraphsRebuilt = false;
            if (forceText || paragraphs.Count == 0 || layoutChanged)
            {
                List<Paragraph> nextParagraphs;
                using (profiling ? TextBoxPipelineProfiler.BuildParagraphsMarker.Auto() : default)
                {
                    nextParagraphs = BuildParagraphs(settings, layoutSizePixels);
                }
                using (profiling ? TextBoxPipelineProfiler.RetireParagraphsMarker.Auto() : default)
                {
                    ReplaceParagraphs(nextParagraphs);
                }
                paragraphsRebuilt = true;
                needsDraw = true;
            }
            if (linkAnnotations.Count > 0)
            {
                linkGeometryPublication.RequireControlledSubmission();
            }

            using (profiling ? TextBoxPipelineProfiler.ResizeParagraphsMarker.Auto() : default)
            {
                needsDraw |= ResizeParagraphs(paragraphs, settings, layoutSizePixels, paragraphsRebuilt);
            }
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

            Vector2 paintPosition;
            Rect clipRect;
            UnitySkiaRenderCommandList commands;
            using (profiling ? TextBoxPipelineProfiler.BuildCommandsMarker.Auto() : default)
            {
                commands = BuildRenderCommands(settings,
                    viewport,
                    layoutSizePixels,
                    visibleOutputSizePixels,
                    logContext,
                    out paintPosition,
                    out clipRect);
            }
            var hasHitGeometry = paragraphs.Count > 0 &&
                                 linkAnnotations.Count > 0 &&
                                 clipRect.width > 0f &&
                                 clipRect.height > 0f;
            var hitGeometry = hasHitGeometry
                ? new TextBoxLinkGeometrySnapshot(contentVersion,
                    paragraphs.ToArray(),
                    linkAnnotations,
                    BuildLinkGeometry(paragraphs, linkAnnotations, viewport.VisualScrollOffset.x),
                    paintPosition,
                    clipRect,
                    visibleOutputSizePixels,
                    fitScale)
                : default;
            bool submitted;
            using (profiling ? TextBoxPipelineProfiler.SubmitMarker.Auto() : default)
            {
                submitted = TrySubmit(commands, hasHitGeometry, hitGeometry);
            }
            if (!submitted)
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
            RetireParagraphs();
            DisposeSurface();
            cachedParagraphStyle?.Dispose();
            cachedTextStyle?.Dispose();
            cachedParagraphStyle = null;
            cachedTextStyle = null;
            stylesChanged = true;
            layoutChanged = true;
            paintChanged = true;
            paragraphHeight = 0f;
            paragraphContentWidth = 0f;
            paragraphVisualLeft = 0f;
            paragraphVisualWidth = 0f;
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

        private List<Paragraph> BuildParagraphs(TextBoxRenderTargetSettings settings, Vector2Int layoutSizePixels)
        {
            var profiling = TextBoxPipelineProfiler.Enabled;
            var result = CreateParagraphs(settings, out var payload);
            string plainText;
            using (profiling ? TextBoxPipelineProfiler.PlainTextMarker.Auto() : default)
            {
                plainText = PlainText(payload);
            }
            linkAnnotations = payload.Links.Count == 0
                ? Array.Empty<RichTextParser.LinkAnnotation>()
                : new List<RichTextParser.LinkAnnotation>(payload.Links);
            using (profiling ? TextBoxPipelineProfiler.BlockMetadataMarker.Auto() : default)
            {
                CaptureBlockMetadata(payload, settings);
            }
            using (profiling ? TextBoxPipelineProfiler.ContentSnapshotMarker.Auto() : default)
            {
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
            }
            return result;
        }

        private void CaptureBlockMetadata(RichTextParser.ParagraphPayload payload,
            TextBoxRenderTargetSettings settings)
        {
            blockStartsUtf16.Clear();
            blockPlainTexts.Clear();
            blockMarginTopValues.Clear();
            blockMarginBottomValues.Clear();
            blockPhysicalAligns.Clear();
            hasHeightDependentBlockMargins = false;
            foreach (var block in payload.Blocks)
            {
                blockStartsUtf16.Add(block.StartUtf16);
                var plainText = new StringBuilder();
                foreach (var segment in block.Segments)
                {
                    plainText.Append(segment.Content ?? "");
                }
                blockPlainTexts.Add(plainText.ToString());
                blockMarginTopValues.Add(block.Style.MarginTop);
                blockMarginBottomValues.Add(block.Style.MarginBottom);
                hasHeightDependentBlockMargins |= IsHeightDependent(block.Style.MarginTop) ||
                                                  IsHeightDependent(block.Style.MarginBottom);

                var align = block.Style.TextAlign ??
                            payload.ParagraphStyle.TextAlign ??
                            settings.ParagraphStyle.textAlign;
                var direction = block.Style.TextDirection ??
                                payload.ParagraphStyle.TextDirection ??
                                settings.ParagraphStyle.textDirection;
                blockPhysicalAligns.Add(PhysicalAlign(align, direction));
            }
        }

        private static TextAlign PhysicalAlign(TextAlign align, TextDirection direction)
        {
            switch (align)
            {
                case TextAlign.Start:
                    return direction == TextDirection.Rtl ? TextAlign.Right : TextAlign.Left;
                case TextAlign.End:
                    return direction == TextDirection.Rtl ? TextAlign.Left : TextAlign.Right;
                case TextAlign.Justify:
                    return TextAlign.Left;
                default:
                    return align;
            }
        }

        private static bool IsHeightDependent(MarginValue value)
        {
            return !value.Auto &&
                   (value.Unit == MarginUnit.ContainerPercent || value.Unit == MarginUnit.Vh);
        }

        private IReadOnlyList<TextBoxLinkGeometry> BuildLinkGeometry(
            IReadOnlyList<Paragraph> source,
            IReadOnlyList<RichTextParser.LinkAnnotation> links,
            float visualScrollOffsetX)
        {
            var geometry = new List<TextBoxLinkGeometry>(links.Count);
            foreach (var link in links)
            {
                var rects = new List<Rect>();
                var blockRanges = new List<TextBoxLinkBlockRange>();
                for (var blockIndex = 0; blockIndex < source.Count; ++blockIndex)
                {
                    var blockStart = blockStartsUtf16[blockIndex];
                    var blockEnd = blockStart + blockPlainTexts[blockIndex].Length;
                    var localStart = Math.Max(link.StartUtf16, blockStart);
                    var localEnd = Math.Min(link.EndUtf16, blockEnd);
                    if (localStart >= localEnd)
                    {
                        continue;
                    }

                    var blockRects = source[blockIndex].GetRectsForRange(localStart - blockStart,
                        localEnd - blockStart);
                    var top = blockIndex < blockTops.Count ? blockTops[blockIndex] : 0f;
                    var paintOffsetX = TextBoxNoWrapHorizontalLayout.ResolvePaintOffsetX(
                        blockIndex < blockAlignmentOffsetsX.Count ? blockAlignmentOffsetsX[blockIndex] : 0f,
                        scrollOffset.x,
                        visualScrollOffsetX);
                    var offsetX = paintOffsetX / fitScale;
                    foreach (var rect in blockRects)
                    {
                        rects.Add(new Rect(rect.x + offsetX, rect.y + top, rect.width, rect.height));
                    }
                    blockRanges.Add(new TextBoxLinkBlockRange(blockIndex,
                        localStart - blockStart,
                        localEnd - blockStart,
                        new Vector2(offsetX, top)));
                }

                geometry.Add(new TextBoxLinkGeometry(link.Occurrence, rects, blockRanges));
            }

            return geometry;
        }

        private List<Paragraph> CreateParagraphs(TextBoxRenderTargetSettings settings,
            out RichTextParser.ParagraphPayload payload)
        {
            var profiling = TextBoxPipelineProfiler.Enabled;
            if (stylesChanged || cachedParagraphStyle == null || cachedTextStyle == null)
            {
                using (profiling ? TextBoxPipelineProfiler.CreateBaseStylesMarker.Auto() : default)
                {
                    cachedParagraphStyle?.Dispose();
                    cachedTextStyle?.Dispose();
                    cachedParagraphStyle = settings.ParagraphStyle.ToParagraphStyle(
                        settings.TextOverflow == TextOverflow.Ellipsis);
                    cachedTextStyle = settings.TextStyle.ToTextStyle();
                }
                stylesChanged = false;
            }
            var paragraphStyle = cachedParagraphStyle!;
            var textStyle = cachedTextStyle!;

            var parser = new RichTextParser.RichTextParser();
            using (profiling ? TextBoxPipelineProfiler.ParseRichTextMarker.Auto() : default)
            {
                parser.ParseText(settings.Content);
            }
            using (profiling ? TextBoxPipelineProfiler.ConvertRichTextMarker.Auto() : default)
            {
                payload = parser.ConvertToSegments();
            }
            using (profiling ? TextBoxPipelineProfiler.CreateNativeParagraphsMarker.Auto() : default)
            {
                return payload.ToParagraphs(paragraphStyle, textStyle);
            }
        }

        private static void ValidateMargin(Margin margin)
        {
            margin.Normalize();
        }

        private bool ResizeParagraphs(IReadOnlyList<Paragraph> targetParagraphs,
            TextBoxRenderTargetSettings settings,
            Vector2Int layoutSizePixels,
            bool force = false)
        {
            if (targetParagraphs.Count == 0)
            {
                fitScale = settings.Sizing.HasValue
                    ? settings.Sizing.Value.EffectiveFontSize / Mathf.Max(0.1f, settings.TextStyle.size)
                    : 1f;
                paragraphContentWidth = 0f;
                paragraphHeight = 0f;
                return false;
            }

            if (settings.Sizing.HasValue)
            {
                return ResizeResolvedParagraphs(targetParagraphs, settings, layoutSizePixels, force);
            }

            if (ShouldUseWideNoWrapLayout(settings))
            {
                return ResizeNoWrapParagraphs(targetParagraphs, settings, layoutSizePixels, force);
            }

            var viewportWidth = ContentViewportWidth(settings, layoutSizePixels);
            var alignmentChanged = ClearBlockAlignmentOffsets();

            if (!ShouldApplyAutoFontSize(settings))
            {
                fitScale = 1f;
                if (viewportWidth == layoutWidth && !force &&
                    (!hasHeightDependentBlockMargins || layoutSizePixels.y == layoutHeight))
                {
                    return alignmentChanged;
                }

                layoutWidth = viewportWidth;
                layoutHeight = layoutSizePixels.y;
                foreach (var targetParagraph in targetParagraphs)
                {
                    targetParagraph.Layout(layoutWidth);
                }
                paragraphContentWidth = layoutWidth;
                ResolveBlockMargins(settings, layoutSizePixels);
                var stackHeight = RestackBlocks(targetParagraphs, settings);
                ResolveWrapVisualBounds(targetParagraphs, layoutWidth);
                paragraphHeight = Mathf.Max(0f, stackHeight);
                return true;
            }

            return ResizeAutoFontSizeWrapParagraphs(targetParagraphs,
                settings,
                layoutSizePixels,
                viewportWidth,
                force,
                alignmentChanged);
        }

        private bool ResizeResolvedParagraphs(IReadOnlyList<Paragraph> targetParagraphs,
            TextBoxRenderTargetSettings settings,
            Vector2Int layoutSizePixels,
            bool force)
        {
            var sizing = settings.Sizing!.Value;
            var scale = sizing.EffectiveFontSize / Mathf.Max(0.1f, settings.TextStyle.size);
            var wraps = settings.WrapMode == TextBoxWrapMode.Wrap && !settings.ParagraphStyle.singleLine;
            if (!wraps && ShouldUseWideNoWrapLayout(settings))
            {
                return ResizeNoWrapParagraphs(targetParagraphs, settings, layoutSizePixels, force);
            }
            var contentWidth = wraps
                ? Mathf.Max(0.01f, (layoutSizePixels.x - sizing.Margin.FixedHorizontalSize) / Mathf.Max(0.001f, scale))
                : Mathf.Max(0.01f, (sizing.UsedWidth - sizing.Margin.FixedHorizontalSize) / Mathf.Max(0.001f, scale));
            var nextLayoutWidth = CeilToPositiveInt(contentWidth);
            var changed = force || layoutWidth != nextLayoutWidth || !Mathf.Approximately(fitScale, scale);
            layoutWidth = nextLayoutWidth;
            layoutHeight = layoutSizePixels.y;
            fitScale = scale;
            ClearBlockAlignmentOffsets();
            ResolveBlockMargins(settings, layoutSizePixels);
            blockHeights.Clear();
            paragraphContentWidth = 0f;
            for (var index = 0; index < targetParagraphs.Count; ++index)
            {
                var paragraph = targetParagraphs[index];
                paragraph.Layout(layoutWidth);
                blockHeights.Add(Mathf.Max(0f, paragraph.Height));
                paragraphContentWidth = Mathf.Max(paragraphContentWidth,
                    wraps ? paragraph.LongestLine : paragraph.MaxIntrinsicWidth);
            }
            var rawHeight = TextBoxBlockStack.Layout(blockHeights,
                blockMarginTops,
                blockMarginBottoms,
                settings.ParagraphStyle.paragraphSpacing,
                blockTops);
            paragraphHeight = rawHeight * scale;
            ResolveWrapVisualBounds(targetParagraphs, layoutWidth);
            return changed;
        }

        private bool ClearBlockAlignmentOffsets()
        {
            var hadOffset = false;
            for (var index = 0; index < blockAlignmentOffsetsX.Count; ++index)
            {
                hadOffset |= !Mathf.Approximately(blockAlignmentOffsetsX[index], 0f);
            }
            blockAlignmentOffsetsX.Clear();
            return hadOffset;
        }

        private void ResolveBlockMargins(TextBoxRenderTargetSettings settings, Vector2Int layoutSizePixels)
        {
            blockMarginTops.Clear();
            blockMarginBottoms.Clear();
            var referenceSize = settings.Sizing.HasValue ? settings.MarginReferenceSize :
                new Vector2(layoutSizePixels.x, layoutSizePixels.y);
            var scale = settings.Sizing.HasValue
                ? settings.Sizing.Value.EffectiveFontSize / Mathf.Max(0.1f, settings.TextStyle.size)
                : 1f;
            var context = new MarginResolveContext(referenceSize.x,
                referenceSize.y,
                settings.Sizing.HasValue
                    ? settings.Sizing.Value.EffectiveFontSize
                    : settings.TextStyle.size);
            for (var index = 0; index < blockMarginTopValues.Count; ++index)
            {
                blockMarginTops.Add(blockMarginTopValues[index].Resolve(context, MarginAxis.Vertical) / scale);
                blockMarginBottoms.Add(blockMarginBottomValues[index].Resolve(context, MarginAxis.Vertical) / scale);
            }
        }

        private float RestackBlocks(IReadOnlyList<Paragraph> targetParagraphs,
            TextBoxRenderTargetSettings settings)
        {
            blockHeights.Clear();
            for (var index = 0; index < targetParagraphs.Count; ++index)
            {
                blockHeights.Add(Mathf.Max(0f, targetParagraphs[index].Height));
            }

            return TextBoxBlockStack.Layout(blockHeights,
                blockMarginTops,
                blockMarginBottoms,
                settings.ParagraphStyle.paragraphSpacing,
                blockTops);
        }

        private void ResolveWrapVisualBounds(IReadOnlyList<Paragraph> targetParagraphs, float width)
        {
            var left = float.MaxValue;
            var right = float.MinValue;
            for (var index = 0; index < targetParagraphs.Count; ++index)
            {
                var visualWidth = Mathf.Clamp(targetParagraphs[index].LongestLine, 0f, width);
                var visualLeft = ResolveVisualLeft(blockPhysicalAligns[index], visualWidth, width);
                left = Math.Min(left, visualLeft);
                right = Math.Max(right, visualLeft + visualWidth);
            }

            if (left > right)
            {
                paragraphVisualLeft = 0f;
                paragraphVisualWidth = 0f;
                return;
            }

            paragraphVisualLeft = left;
            paragraphVisualWidth = right - left;
        }

        private bool ShouldApplyAutoFontSize(TextBoxRenderTargetSettings settings)
        {
            return settings.AutoFontSize && settings.TextOverflow != TextOverflow.Ellipsis;
        }

        private bool ResizeAutoFontSizeWrapParagraphs(IReadOnlyList<Paragraph> targetParagraphs,
            TextBoxRenderTargetSettings settings,
            Vector2Int layoutSizePixels,
            int viewportWidth,
            bool force,
            bool alignmentChanged)
        {
            var viewportHeight = ContentViewportHeight(settings, layoutSizePixels);
            ResolveBlockMargins(settings, layoutSizePixels);
            float scale;
            var finalVirtualWidth = Mathf.Max(1, viewportWidth);
            var probeWidth = -1;
            var probeSize = Vector2.zero;
            scale = TextAutoFontSizeScale.SearchFit(candidate =>
            {
                var candidateWidth = CeilToPositiveInt(viewportWidth / candidate);
                if (candidateWidth != probeWidth)
                {
                    probeWidth = candidateWidth;
                    foreach (var targetParagraph in targetParagraphs)
                    {
                        targetParagraph.Layout(candidateWidth);
                    }
                    probeSize = MeasureStackedContent(targetParagraphs, settings);
                }

                return probeSize;
            },
                viewportWidth,
                viewportHeight,
                settings.MinSize / settings.TextStyle.size,
                settings.MaxSize / settings.TextStyle.size);
            finalVirtualWidth = CeilToPositiveInt(viewportWidth / scale);
            if (probeWidth != finalVirtualWidth)
            {
                foreach (var targetParagraph in targetParagraphs)
                {
                    targetParagraph.Layout(finalVirtualWidth);
                }
            }

            var changed = alignmentChanged || force || finalVirtualWidth != layoutWidth;
            layoutWidth = finalVirtualWidth;
            var height = RestackBlocks(targetParagraphs, settings);
            var scaleChanged = !Mathf.Approximately(fitScale, scale);
            fitScale = scale;
            paragraphContentWidth = layoutWidth;

            var visualLeft = float.MaxValue;
            var visualRight = float.MinValue;
            for (var index = 0; index < targetParagraphs.Count; ++index)
            {
                var visualWidth = Mathf.Clamp(targetParagraphs[index].LongestLine * fitScale, 0f, viewportWidth);
                var left = ResolveVisualLeft(blockPhysicalAligns[index], visualWidth, viewportWidth);
                visualLeft = Math.Min(visualLeft, left);
                visualRight = Math.Max(visualRight, left + visualWidth);
            }
            if (visualLeft > visualRight)
            {
                paragraphVisualLeft = 0f;
                paragraphVisualWidth = 0f;
            }
            else
            {
                paragraphVisualLeft = visualLeft;
                paragraphVisualWidth = visualRight - visualLeft;
            }
            paragraphHeight = height * fitScale;
            return changed || scaleChanged;
        }

        private Vector2 MeasureStackedContent(IReadOnlyList<Paragraph> targetParagraphs,
            TextBoxRenderTargetSettings settings)
        {
            blockHeights.Clear();
            var width = 0f;
            foreach (var targetParagraph in targetParagraphs)
            {
                blockHeights.Add(Mathf.Max(0f, targetParagraph.Height));
                width = Math.Max(width, targetParagraph.LongestLine);
            }

            var total = TextBoxBlockStack.Layout(blockHeights,
                blockMarginTops,
                blockMarginBottoms,
                settings.ParagraphStyle.paragraphSpacing,
                probeTops);
            return new Vector2(width, total);
        }

        private bool ResizeNoWrapParagraphs(IReadOnlyList<Paragraph> targetParagraphs,
            TextBoxRenderTargetSettings settings,
            Vector2Int layoutSizePixels,
            bool force)
        {
            var viewportWidth = ContentViewportWidth(settings, layoutSizePixels);
            var profiling = TextBoxPipelineProfiler.Enabled;
            if (force)
            {
                blockContentWidths.Clear();
                for (var index = 0; index < targetParagraphs.Count; ++index)
                {
                    var plainText = index < blockPlainTexts.Count ? blockPlainTexts[index] : "";
                    using (profiling ? TextBoxPipelineProfiler.NoWrapProbeLayoutMarker.Auto() : default)
                    {
                        targetParagraphs[index].Layout(Paragraph.ResolveNoWrapProbeLayoutWidth(plainText,
                            settings.TextStyle.size,
                            viewportWidth));
                    }
                    using (profiling ? TextBoxPipelineProfiler.NoWrapMeasureMarker.Auto() : default)
                    {
                        blockContentWidths.Add(targetParagraphs[index].ResolveNoWrapContentWidth(plainText));
                    }
                }
            }

            var contentWidth = 0f;
            foreach (var width in blockContentWidths)
            {
                contentWidth = Math.Max(contentWidth, width);
            }

            ResolveBlockMargins(settings, layoutSizePixels);
            var unscaledHeight = 0f;
            var needsHeightProbe = !settings.Sizing.HasValue && ShouldApplyAutoFontSize(settings);
            if (needsHeightProbe)
            {
                unscaledHeight = RestackBlocks(targetParagraphs, settings);
            }

            paragraphContentWidth = contentWidth;
            var scale = settings.Sizing.HasValue
                ? settings.Sizing.Value.EffectiveFontSize / Mathf.Max(0.1f, settings.TextStyle.size)
                : 1f;
            if (needsHeightProbe)
            {
                scale = TextAutoFontSizeScale.Resolve(contentWidth,
                    unscaledHeight,
                    viewportWidth,
                    ContentViewportHeight(settings, layoutSizePixels),
                    settings.TextStyle.size,
                    settings.MinSize,
                    settings.MaxSize);
            }

            var virtualViewportWidth = settings.Sizing.HasValue ? viewportWidth / scale : viewportWidth;
            var newLayoutWidth = CeilToPositiveInt(Paragraph.ResolveNoWrapLayoutWidth(virtualViewportWidth, contentWidth));
            var widthChanged = newLayoutWidth != layoutWidth;
            if (widthChanged || force)
            {
                layoutWidth = newLayoutWidth;
                using (profiling ? TextBoxPipelineProfiler.NoWrapFinalLayoutMarker.Auto() : default)
                {
                    foreach (var targetParagraph in targetParagraphs)
                    {
                        targetParagraph.Layout(layoutWidth);
                    }
                }
            }
            using (profiling ? TextBoxPipelineProfiler.NoWrapHeightMarker.Auto() : default)
            {
                unscaledHeight = RestackBlocks(targetParagraphs, settings);
            }

            var scaleChanged = !Mathf.Approximately(fitScale, scale);
            fitScale = scale;

            var metricsChanged = false;
            var visualLeft = float.MaxValue;
            var visualRight = float.MinValue;
            for (var index = 0; index < targetParagraphs.Count; ++index)
            {
                var blockWidth = index < blockContentWidths.Count ? blockContentWidths[index] : 0f;
                var horizontalLayout = TextBoxNoWrapHorizontalLayout.Resolve(true,
                    blockPhysicalAligns[index],
                    settings.ParagraphStyle.textDirection,
                    viewportWidth,
                    blockWidth * fitScale,
                    layoutWidth * fitScale);
                while (blockAlignmentOffsetsX.Count <= index)
                {
                    blockAlignmentOffsetsX.Add(0f);
                }
                if (!Mathf.Approximately(blockAlignmentOffsetsX[index], horizontalLayout.LayoutAlignmentOffset))
                {
                    blockAlignmentOffsetsX[index] = horizontalLayout.LayoutAlignmentOffset;
                    metricsChanged = true;
                }

                var left = horizontalLayout.LogicalVisualLeft;
                var right = left + blockWidth * fitScale;
                visualLeft = Math.Min(visualLeft, left);
                visualRight = Math.Max(visualRight, right);
            }

            var nextVisualWidth = contentWidth * fitScale;
            var nextVisualLeft = visualLeft <= visualRight ? visualLeft : 0f;
            metricsChanged |= !Mathf.Approximately(paragraphVisualWidth, nextVisualWidth) ||
                              !Mathf.Approximately(paragraphVisualLeft, nextVisualLeft);
            paragraphVisualWidth = nextVisualWidth;
            paragraphVisualLeft = nextVisualLeft;
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
                settings.ParagraphStyle.textAlign,
                settings.ParagraphStyle.textDirection,
                nextViewportSize.x,
                paragraphs.Count > 0 ? paragraphContentWidth * fitScale : 0f,
                paragraphs.Count > 0 ? layoutWidth * fitScale : 0f);
            var nextContentWidth = useWideNoWrapLayout
                ? horizontalLayout.ContentWidth
                : layoutViewportWidth;
            var nextContentSize = new Vector2(nextContentWidth, paragraphs.Count > 0 ? paragraphHeight : 0f);
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
            if (settings.Sizing.HasValue)
            {
                return settings.Sizing.Value.Margin;
            }

            return settings.Margin.Resolve(new MarginResolveContext(containerSizePixels.x,
                containerSizePixels.y,
                settings.TextStyle.size));
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
            if (settings.ParagraphStyle.singleLine)
            {
                return settings.TextOverflow != TextOverflow.Ellipsis;
            }

            return settings.WrapMode == TextBoxWrapMode.NoWrap;
        }

        private static float ResolveVisualLeft(TextAlign physicalAlign,
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

            switch (physicalAlign)
            {
                case TextAlign.Center:
                    return (containingWidth - visualWidth) * 0.5f;
                case TextAlign.Right:
                    return containingWidth - visualWidth;
                default:
                    return 0f;
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
            if (paragraphs.Count > 0)
            {
                var margin = ResolveMargin(settings, layoutSizePixels);
                var layoutViewportSize = new Vector2(ContentViewportWidth(settings, layoutSizePixels),
                    ContentViewportHeight(settings, layoutSizePixels));
                var autoOffset = AutoContentOffset(settings, layoutViewportSize);
                var basePaintX = margin.Left + autoOffset.x;
                var basePaintY = margin.Top + autoOffset.y - scrollOffset.y - viewport.VisualScrollOffset.y;
                clipRect = ResolveClipRect(settings, viewport, margin, outputSizePixels);
                paintPosition = new Vector2(basePaintX, basePaintY);

                for (var index = 0; index < paragraphs.Count; ++index)
                {
                    var offsetX = TextBoxNoWrapHorizontalLayout.ResolvePaintOffsetX(
                        index < blockAlignmentOffsetsX.Count ? blockAlignmentOffsetsX[index] : 0f,
                        scrollOffset.x,
                        viewport.VisualScrollOffset.x);
                    var blockTop = index < blockTops.Count ? blockTops[index] : 0f;
                    var blockPaintPosition = new Vector2(basePaintX + offsetX,
                        basePaintY + blockTop * fitScale);

                    commands.DrawParagraph(paragraphs[index],
                        blockPaintPosition,
                        clipRect,
                        fitScale);
                }
            }
            else
            {
#if MILESTRO_RENDER_DEBUG_LOG
                Debug.LogWarning("No paragraphs built", logContext);
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
                return new Rect(0f,
                    0f,
                    outputSizePixels.x,
                    outputSizePixels.y);
            }

            return new Rect(0f,
                margin.Top,
                outputSizePixels.x,
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

        private void ReplaceParagraphs(List<Paragraph> nextParagraphs)
        {
            RetireParagraphs();
            paragraphs.AddRange(nextParagraphs);
        }

        private void RetireParagraphs()
        {
            foreach (var oldParagraph in paragraphs)
            {
                DisposeParagraphAfterPendingDraws(oldParagraph);
            }
            paragraphs.Clear();
        }

        private void DisposeParagraphAfterPendingDraws(Paragraph oldParagraph)
        {
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
