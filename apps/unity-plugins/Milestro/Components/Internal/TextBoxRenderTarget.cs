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
        private readonly List<MarginValue> blockMarginRightValues = new List<MarginValue>();
        private readonly List<MarginValue> blockMarginBottomValues = new List<MarginValue>();
        private readonly List<MarginValue> blockMarginLeftValues = new List<MarginValue>();
        private readonly List<TextAlign> blockPhysicalAligns = new List<TextAlign>();
        private readonly List<float> blockHeights = new List<float>();
        private readonly List<float> blockMarginTops = new List<float>();
        private readonly List<float> blockMarginRights = new List<float>();
        private readonly List<float> blockMarginBottoms = new List<float>();
        private readonly List<float> blockMarginLefts = new List<float>();
        private readonly List<float> blockLayoutWidths = new List<float>();
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
        private IReadOnlyList<RichTextParser.PlaceholderAnnotation> placeholderAnnotations =
            Array.Empty<RichTextParser.PlaceholderAnnotation>();
        private TextBoxContentSnapshot contentSnapshot;
        private bool hasContentSnapshot;
        private string contentSource = "";
        private long contentVersion;
        private bool hasContentSource;
        private TextBoxLinkGeometryPublication linkGeometryPublication;
        private ParagraphStyle? cachedParagraphStyle;
        private TextStyle? cachedTextStyle;
        private TextBoxRenderPreparation? cachedPreparation;
        private TextBoxRenderTargetSettings cachedRequestedSettings;
        private bool hasCachedRequestedSettings;
        private bool preparationDirty = true;
        private bool stylesChanged = true;
        private bool verticalLayoutActive;
        private bool hasVerticalLayoutMetrics;
        private TextBoxVerticalBlockLayout verticalLayoutMetrics;
        private Vector2[] verticalBlockMargins = Array.Empty<Vector2>();
        private Vector2[] verticalBlockSpacingMargins = Array.Empty<Vector2>();
        private WritingMode requestedWritingMode;
        private bool hasRequestedWritingMode;
        private ResolvedMargin verticalMargin;
        private bool verticalLeftAuto;
        private bool verticalTopAuto;
        private bool verticalRightAuto;
        private bool verticalBottomAuto;
        private float verticalContentViewportWidth;
        private float verticalContentViewportHeight;
        private Exception? lastLayoutError;
        private string? lastConfigurationDiagnosticKey;
        private bool configurationDegraded;
        private bool failedRebuildSuspended;
        private TextBoxRenderViewport failedViewport;
        private ColorSpace failedColorSpace;
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
        public bool IsVertical => verticalLayoutActive;
        public Exception? LastLayoutError => lastLayoutError;

        public event Action<UnitySkiaRenderTextureSurface.RenderSubmissionStatus>? RenderEventCompleted;

        public void MarkPropertiesChanged()
        {
            failedRebuildSuspended = false;
            preparationDirty = true;
            stylesChanged = true;
            layoutChanged = true;
            paintChanged = true;
            linkGeometryPublication.InvalidatePublished();
            linkGeometryPublication.RequestRebuild();
        }

        public void MarkContentChanged()
        {
            failedRebuildSuspended = false;
            preparationDirty = true;
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
            if (failedRebuildSuspended &&
                failedViewport.Matches(viewport) &&
                failedColorSpace == colorSpace)
            {
                return false;
            }

            failedRebuildSuspended = false;
            try
            {
                var shouldPrepare = preparationDirty ||
                                    forceText ||
                                    cachedPreparation == null ||
                                    !PreparationMatchesRequest(settings);
                if (shouldPrepare)
                {
                    cachedPreparation = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(settings);
                    cachedRequestedSettings = settings;
                    hasCachedRequestedSettings = true;
                    preparationDirty = false;
                    if (cachedPreparation.Diagnostic != null || configurationDegraded)
                    {
                        InvalidateFailedOutput();
                    }
                }

                var prepared = cachedPreparation!;
                var effectiveSettings = prepared.Settings;
                var writingMode = effectiveSettings.ParagraphStyle?.writingMode ?? WritingMode.HorizontalTb;
                if (writingMode != WritingMode.HorizontalTb && writingMode != WritingMode.VerticalRl)
                {
                    throw new NotSupportedException(
                        $"TextBox does not support writing mode value {(int)writingMode}.");
                }

                configurationDegraded = prepared.Diagnostic != null;

                if (hasRequestedWritingMode && requestedWritingMode != writingMode)
                {
                    ResetForWritingModeChange();
                }

                requestedWritingMode = writingMode;
                hasRequestedWritingMode = true;

                Exception? configurationDiagnostic = prepared.Diagnostic;
                var fallbackSettings = effectiveSettings;
                var usedLineHeightFallback = false;
                bool result;
                if (TextBoxVerticalLayoutSupport.IsVertical(effectiveSettings))
                {
                    result = RebuildVerticalWithFallback(viewport,
                        colorSpace,
                        effectiveSettings,
                        prepared.Payload,
                        forceText,
                        logContext,
                        ref configurationDiagnostic,
                        out fallbackSettings,
                        out usedLineHeightFallback);
                }
                else
                {
                    verticalLayoutActive = false;
                    hasVerticalLayoutMetrics = false;
                    result = RebuildHorizontal(viewport,
                        colorSpace,
                        effectiveSettings,
                        prepared.Payload,
                        forceText,
                        logContext);
                }

                if (usedLineHeightFallback)
                {
                    cachedPreparation = new TextBoxRenderPreparation(fallbackSettings,
                        prepared.Payload,
                        AsConfigurationDiagnostic(configurationDiagnostic),
                        prepared.UsesHorizontalFallback);
                    configurationDegraded = true;
                }

                lastLayoutError = null;
                ReportConfigurationDiagnostic(configurationDiagnostic, logContext);
                return result;
            }
            catch (Exception error)
            {
                InvalidateFailedOutput();
                lastLayoutError = error;
                failedRebuildSuspended = true;
                failedViewport = viewport;
                failedColorSpace = colorSpace;
                ReportConfigurationDiagnostic(error, logContext);
                return false;
            }
        }

        private bool RebuildVerticalWithFallback(TextBoxRenderViewport viewport,
            ColorSpace colorSpace,
            TextBoxRenderTargetSettings settings,
            RichTextParser.ParagraphPayload payload,
            bool forceText,
            UnityEngine.Object? logContext,
            ref Exception? diagnostic,
            out TextBoxRenderTargetSettings fallbackSettings,
            out bool usedFallback)
        {
            var lineHeight = settings.TextStyle.lineHeight;
            fallbackSettings = settings;
            usedFallback = false;
            var layoutSizePixels = NormalizeSize(viewport.LayoutSizePixels);
            var inlineExtent = ResolveVerticalInlineExtent(settings, layoutSizePixels);
            if (TextBoxVerticalLayoutSupport.HasUnrepresentableVerticalIndent(payload,
                    inlineExtent,
                    settings.TextStyle.size))
            {
                diagnostic = CombineDiagnostics(diagnostic,
                    new NotSupportedException(
                        "vertical text indent is not representable for the current content " +
                        "height; defaulted to 0"));
            }
            try
            {
                return RebuildVertical(viewport, colorSpace, settings, payload, forceText, logContext);
            }
            catch (Exception error) when (FloatUtil.IsFinite(lineHeight) && lineHeight > 0f)
            {
                InvalidateFailedOutput();
                var fallback = TextBoxVerticalLayoutSupport.WithNaturalLineHeight(settings);
                fallbackSettings = fallback;
                usedFallback = true;
                diagnostic = CombineDiagnostics(diagnostic,
                    new NotSupportedException(
                        $"lineHeight={lineHeight} could not produce a valid vertical layout; " +
                        $"defaulted to 0 (natural metrics). {error.Message}"));
                try
                {
                    return RebuildVertical(viewport, colorSpace, fallback, payload, forceText, logContext);
                }
                catch (Exception retryError)
                {
                    throw CombineDiagnostics(diagnostic, retryError);
                }
            }
        }

        private static NotSupportedException AsConfigurationDiagnostic(Exception? diagnostic)
        {
            if (diagnostic == null)
            {
                return new NotSupportedException(
                    "Canvas TextBox configuration was defaulted for rendering.");
            }

            return diagnostic as NotSupportedException ??
                   new NotSupportedException(diagnostic.Message);
        }

        private bool PreparationMatchesRequest(TextBoxRenderTargetSettings settings)
        {
            return hasCachedRequestedSettings &&
                   string.Equals(settings.Content,
                       cachedRequestedSettings.Content,
                       StringComparison.Ordinal) &&
                   string.Equals(settings.ParserId, cachedRequestedSettings.ParserId,
                       StringComparison.Ordinal) &&
                   TextBoxVerticalLayoutSupport.MarginsEqual(settings.Margin,
                       cachedRequestedSettings.Margin) &&
                   ReferenceEquals(settings.ParagraphStyle, cachedRequestedSettings.ParagraphStyle) &&
                   ReferenceEquals(settings.TextStyle, cachedRequestedSettings.TextStyle) &&
                   settings.WrapMode == cachedRequestedSettings.WrapMode &&
                   settings.TextOverflow == cachedRequestedSettings.TextOverflow &&
                   settings.AutoFontSize == cachedRequestedSettings.AutoFontSize &&
                   settings.MinSize.Equals(cachedRequestedSettings.MinSize) &&
                   settings.MaxSize.Equals(cachedRequestedSettings.MaxSize) &&
                   Nullable.Equals(settings.Sizing, cachedRequestedSettings.Sizing) &&
                   settings.MarginReferenceSize == cachedRequestedSettings.MarginReferenceSize &&
                   Nullable.Equals(settings.VerticalHeightMode,
                       cachedRequestedSettings.VerticalHeightMode) &&
                   Nullable.Equals(settings.VerticalMarginContext,
                       cachedRequestedSettings.VerticalMarginContext) &&
                   settings.SizingUsesVerticalLayout ==
                   cachedRequestedSettings.SizingUsesVerticalLayout;
        }

        private static NotSupportedException CombineDiagnostics(Exception? first, Exception next)
        {
            if (first == null)
            {
                return AsConfigurationDiagnostic(next);
            }

            return new NotSupportedException($"{first.Message} {next.Message}");
        }

        private void ReportConfigurationDiagnostic(Exception? diagnostic,
            UnityEngine.Object? logContext)
        {
#if UNITY_EDITOR
            if (diagnostic == null)
            {
                lastConfigurationDiagnosticKey = null;
                return;
            }

            var key = $"{diagnostic.GetType().FullName}:{diagnostic.Message}";
            if (string.Equals(lastConfigurationDiagnosticKey, key, StringComparison.Ordinal))
            {
                return;
            }

            lastConfigurationDiagnosticKey = key;
            if (diagnostic.StackTrace == null)
            {
                try
                {
                    throw diagnostic;
                }
                catch (Exception logged)
                {
                    Debug.LogException(logged, logContext);
                }

                return;
            }

            Debug.LogException(diagnostic, logContext);
#endif
        }

        private void ResetForWritingModeChange()
        {
            fitScale = 1f;
            DisposeSurface();
            RetireParagraphs();
            cachedParagraphStyle?.Dispose();
            cachedTextStyle?.Dispose();
            cachedParagraphStyle = null;
            cachedTextStyle = null;
            stylesChanged = true;
            layoutChanged = true;
            paintChanged = true;
            verticalLayoutActive = false;
            hasVerticalLayoutMetrics = false;
            verticalLayoutMetrics = default;
            hasRequestedWritingMode = false;
            verticalMargin = default;
            ResetVerticalAutoMargins();
            verticalContentViewportWidth = 0;
            verticalContentViewportHeight = 0;
            paragraphHeight = 0f;
            paragraphContentWidth = 0f;
            paragraphVisualLeft = 0f;
            paragraphVisualWidth = 0f;
            horizontalScrollState = default;
            scrollOffset = Vector2.zero;
            contentSize = Vector2.zero;
            viewportSize = Vector2.zero;
            maxScrollOffset = Vector2.zero;
            MarkOutputChanged();
        }

        public void InvalidateFailedOutput()
        {
            fitScale = 1f;
            RetireParagraphs();
            DisposeSurface();
            cachedParagraphStyle?.Dispose();
            cachedTextStyle?.Dispose();
            cachedParagraphStyle = null;
            cachedTextStyle = null;
            stylesChanged = true;
            layoutChanged = true;
            paintChanged = true;
            verticalLayoutActive = false;
            hasVerticalLayoutMetrics = false;
            verticalLayoutMetrics = default;
            verticalMargin = default;
            ResetVerticalAutoMargins();
            verticalContentViewportWidth = 0;
            verticalContentViewportHeight = 0;
            paragraphHeight = 0f;
            paragraphContentWidth = 0f;
            paragraphVisualLeft = 0f;
            paragraphVisualWidth = 0f;
            horizontalScrollState = default;
            scrollOffset = Vector2.zero;
            contentSize = Vector2.zero;
            viewportSize = Vector2.zero;
            maxScrollOffset = Vector2.zero;
            linkAnnotations = Array.Empty<RichTextParser.LinkAnnotation>();
            hasContentSnapshot = false;
            contentSnapshot = default;
            linkGeometryPublication.Reset();
            pendingCompletionToken = 0;
            pendingViewport = default;
            pendingColorSpace = default;
            MarkOutputChanged();
        }

        private bool RebuildHorizontal(TextBoxRenderViewport viewport,
            ColorSpace colorSpace,
            TextBoxRenderTargetSettings settings,
            RichTextParser.ParagraphPayload payload,
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
                    nextParagraphs = BuildParagraphs(settings, layoutSizePixels, payload);
                }
                using (profiling ? TextBoxPipelineProfiler.RetireParagraphsMarker.Auto() : default)
                {
                    ReplaceParagraphs(nextParagraphs);
                }
                paragraphsRebuilt = true;
                needsDraw = true;
            }
            if (linkAnnotations.Count > 0 || placeholderAnnotations.Count > 0)
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
                                 (linkAnnotations.Count > 0 || placeholderAnnotations.Count > 0) &&
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
                    fitScale,
                    BuildPlaceholderGeometry(paintPosition, clipRect, viewport.VisualScrollOffset.x))
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
            cachedPreparation = null;
            hasCachedRequestedSettings = false;
            cachedRequestedSettings = default;
            preparationDirty = true;
            configurationDegraded = false;
            layoutChanged = true;
            paintChanged = true;
            verticalLayoutActive = false;
            hasVerticalLayoutMetrics = false;
            verticalLayoutMetrics = default;
            hasRequestedWritingMode = false;
            verticalMargin = default;
            ResetVerticalAutoMargins();
            verticalContentViewportWidth = 0;
            verticalContentViewportHeight = 0;
            lastLayoutError = null;
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

        private bool RebuildVertical(TextBoxRenderViewport viewport,
            ColorSpace colorSpace,
            TextBoxRenderTargetSettings settings,
            RichTextParser.ParagraphPayload payload,
            bool forceText,
            UnityEngine.Object? logContext)
        {
            ValidateMargin(settings.Margin);
            verticalLayoutActive = true;
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
            var visibleOutputSizePixels = ClampVisibleOutputSize(
                NormalizeSize(viewport.VisibleOutputSizePixels), outputSizePixels);
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
                    nextParagraphs = BuildParagraphs(settings,
                        layoutSizePixels,
                        payload,
                        ResolveVerticalInlineExtent(settings, layoutSizePixels));
                }
                using (profiling ? TextBoxPipelineProfiler.RetireParagraphsMarker.Auto() : default)
                {
                    ReplaceParagraphs(nextParagraphs);
                }
                paragraphsRebuilt = true;
                needsDraw = true;
            }

            if (linkAnnotations.Count > 0 || placeholderAnnotations.Count > 0)
            {
                linkGeometryPublication.RequireControlledSubmission();
            }

            needsDraw |= ResolveVerticalParagraphs(paragraphs, settings, layoutSizePixels,
                paragraphsRebuilt);
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
                commands = BuildVerticalRenderCommands(settings,
                    viewport,
                    outputSizePixels,
                    logContext,
                    out paintPosition,
                    out clipRect);
            }
            var hasHitGeometry = paragraphs.Count > 0 &&
                                 (linkAnnotations.Count > 0 || placeholderAnnotations.Count > 0) &&
                                 clipRect.width > 0f &&
                                 clipRect.height > 0f;
            var hitGeometry = hasHitGeometry
                ? new TextBoxLinkGeometrySnapshot(contentVersion,
                    paragraphs.ToArray(),
                    linkAnnotations,
                    BuildVerticalLinkGeometry(paragraphs, linkAnnotations),
                    paintPosition,
                    clipRect,
                    visibleOutputSizePixels,
                    fitScale,
                    BuildPlaceholderGeometry(paintPosition, clipRect, viewport.VisualScrollOffset.x))
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

        private bool ResolveVerticalParagraphs(IReadOnlyList<Paragraph> targetParagraphs,
            TextBoxRenderTargetSettings settings,
            Vector2Int layoutSizePixels,
            bool force)
        {
            if (targetParagraphs.Count == 0)
            {
                hasVerticalLayoutMetrics = false;
                verticalLayoutMetrics = default;
                fitScale = 1f;
                paragraphContentWidth = 0f;
                paragraphHeight = 0f;
                ResetVerticalAutoMargins();
                return false;
            }

            var nextMargin = ResolveMargin(settings, layoutSizePixels);
            var contentWidth = ResolveVerticalContentWidth(settings, layoutSizePixels, nextMargin);
            var contentHeight = ResolveVerticalContentHeight(settings, layoutSizePixels, nextMargin);
            var marginChanged = !MarginsEqual(verticalMargin, nextMargin);
            var leftAuto = settings.Margin.Left.Auto;
            var topAuto = settings.Margin.Top.Auto;
            var rightAuto = settings.Margin.Right.Auto;
            var bottomAuto = settings.Margin.Bottom.Auto;
            var autoChanged = verticalLeftAuto != leftAuto ||
                              verticalTopAuto != topAuto ||
                              verticalRightAuto != rightAuto ||
                              verticalBottomAuto != bottomAuto;
            verticalMargin = nextMargin;
            verticalLeftAuto = leftAuto;
            verticalTopAuto = topAuto;
            verticalRightAuto = rightAuto;
            verticalBottomAuto = bottomAuto;
            var margins = TextBoxVerticalBlockLayout.ResolveMargins(blockMarginTopValues,
                blockMarginBottomValues, contentHeight, settings);
            var blockSpacingMargins = TextBoxVerticalBlockLayout.ResolveBlockMargins(
                blockMarginRightValues, blockMarginLeftValues, contentWidth, settings);
            var blockMarginsChanged = margins.Length != verticalBlockMargins.Length ||
                                      blockSpacingMargins.Length != verticalBlockSpacingMargins.Length;
            for (var index = 0; !blockMarginsChanged && index < margins.Length; ++index)
            {
                blockMarginsChanged = !margins[index].Equals(verticalBlockMargins[index]) ||
                                      !blockSpacingMargins[index].Equals(verticalBlockSpacingMargins[index]);
            }
            if (!force &&
                hasVerticalLayoutMetrics &&
                !blockMarginsChanged &&
                Mathf.Approximately(verticalContentViewportWidth, contentWidth) &&
                Mathf.Approximately(verticalContentViewportHeight, contentHeight))
            {
                return marginChanged || autoChanged;
            }

            var scale = settings.Sizing.HasValue
                ? settings.Sizing.Value.EffectiveFontSize / Mathf.Max(0.1f, settings.TextStyle.size)
                : 1f;
            if (!settings.Sizing.HasValue && ShouldApplyAutoFontSize(settings) &&
                settings.VerticalHeightMode != TextHeightMode.Auto)
            {
                var bounds = TextAutoFontSizeSettings.Default;
                bounds.fontSizeMin = settings.MinSize;
                bounds.fontSizeMax = settings.MaxSize;
                bounds.Normalize();
                scale = TextAutoFontSizeScale.SearchFit(candidate =>
                {
                    var virtualWidth = contentWidth / candidate;
                    var virtualHeight = contentHeight / candidate;
                    var measured = TextBoxVerticalBlockLayout.Layout(targetParagraphs,
                        UsesFiniteVerticalInlineExtent(settings)
                            ? virtualHeight
                            : float.PositiveInfinity,
                        settings.ParagraphStyle.paragraphSpacing, margins, blockSpacingMargins).PhysicalSize;
                    return measured.x <= virtualWidth && measured.y <= virtualHeight;
                },
                    bounds.fontSizeMin / settings.TextStyle.size,
                    bounds.fontSizeMax / settings.TextStyle.size);
            }
            var inlineExtent = UsesFiniteVerticalInlineExtent(settings)
                ? contentHeight / scale
                : float.PositiveInfinity;
            var metrics = TextBoxVerticalBlockLayout.Layout(targetParagraphs, inlineExtent,
                settings.ParagraphStyle.paragraphSpacing, margins, blockSpacingMargins);

            verticalBlockMargins = margins;
            verticalBlockSpacingMargins = blockSpacingMargins;
            verticalContentViewportWidth = contentWidth;
            verticalContentViewportHeight = contentHeight;
            verticalLayoutMetrics = metrics;
            hasVerticalLayoutMetrics = true;
            verticalLayoutActive = true;
            fitScale = scale;
            paragraphContentWidth = metrics.PhysicalSize.x * scale;
            paragraphHeight = metrics.PhysicalSize.y * scale;
            layoutWidth = CeilToPositiveInt(contentWidth);
            layoutHeight = CeilToPositiveInt(contentHeight);
            return true;
        }

        private static bool MarginsEqual(ResolvedMargin left, ResolvedMargin right)
        {
            return Mathf.Approximately(left.Left, right.Left) &&
                   Mathf.Approximately(left.Top, right.Top) &&
                   Mathf.Approximately(left.Right, right.Right) &&
                   Mathf.Approximately(left.Bottom, right.Bottom);
        }

        private UnitySkiaRenderCommandList BuildVerticalRenderCommands(
            TextBoxRenderTargetSettings settings,
            TextBoxRenderViewport viewport,
            Vector2Int outputSizePixels,
            UnityEngine.Object? logContext,
            out Vector2 paintPosition,
            out Rect clipRect)
        {
            var commands = new UnitySkiaRenderCommandList();
            paintPosition = Vector2.zero;
            clipRect = default;
            if (!hasVerticalLayoutMetrics || paragraphs.Count == 0)
            {
#if MILESTRO_RENDER_DEBUG_LOG
                Debug.LogWarning("No vertical paragraph built", logContext);
#endif
                return commands;
            }

            paintPosition = ResolveVerticalPaintOrigin(viewport);
            clipRect = ResolveVerticalClipRect(settings, viewport, outputSizePixels);
            for (var index = 0; index < paragraphs.Count; ++index)
            {
                commands.DrawParagraph(paragraphs[index],
                    paintPosition + verticalLayoutMetrics.FrameOffsets[index] * fitScale, clipRect, fitScale);
            }
            return commands;
        }

        private Vector2 ResolveVerticalPaintOrigin(TextBoxRenderViewport viewport)
        {
            var paragraphWidth = verticalLayoutMetrics.PhysicalSize.x * fitScale;
            return new Vector2(
                verticalMargin.Left +
                ResolveVerticalAutoOffsetX(verticalLeftAuto,
                    verticalRightAuto,
                    verticalContentViewportWidth,
                    paragraphWidth) +
                scrollOffset.x + viewport.VisualScrollOffset.x,
                verticalMargin.Top +
                ResolveVerticalAutoOffsetY(verticalTopAuto,
                    verticalBottomAuto,
                    verticalContentViewportHeight,
                    new Rect(verticalLayoutMetrics.ContentBounds.position * fitScale,
                        verticalLayoutMetrics.ContentBounds.size * fitScale)) -
                scrollOffset.y - viewport.VisualScrollOffset.y);
        }

        private static float ResolveVerticalAutoOffsetX(bool leftAuto,
            bool rightAuto,
            float viewportWidth,
            float paragraphWidth)
        {
            if (!FloatUtil.IsFinite(viewportWidth) || !FloatUtil.IsFinite(paragraphWidth))
            {
                return 0f;
            }

            var spare = viewportWidth - paragraphWidth;
            if (spare <= 0f)
            {
                return spare;
            }

            if (leftAuto && rightAuto)
            {
                return spare * 0.5f;
            }

            return rightAuto ? 0f : spare;
        }

        private static float ResolveVerticalAutoOffsetY(bool topAuto,
            bool bottomAuto,
            float viewportHeight,
            Rect contentBounds)
        {
            if ((!topAuto && !bottomAuto) ||
                !FloatUtil.IsFinite(viewportHeight) ||
                !FloatUtil.IsFinite(contentBounds.yMin) ||
                !FloatUtil.IsFinite(contentBounds.height) ||
                contentBounds.height <= 0f)
            {
                return 0f;
            }

            var spare = viewportHeight - contentBounds.height;
            if (spare <= 0f)
            {
                return 0f;
            }

            if (topAuto && bottomAuto)
            {
                return spare * 0.5f - contentBounds.yMin;
            }

            return topAuto ? spare - contentBounds.yMin : -contentBounds.yMin;
        }

        private void ResetVerticalAutoMargins()
        {
            verticalLeftAuto = false;
            verticalTopAuto = false;
            verticalRightAuto = false;
            verticalBottomAuto = false;
        }

        private Rect ResolveVerticalClipRect(TextBoxRenderTargetSettings settings,
            TextBoxRenderViewport viewport,
            Vector2Int outputSizePixels)
        {
            if (settings.TextOverflow == TextOverflow.Overflow || viewport.SliceOutput)
            {
                return new Rect(0f, 0f, outputSizePixels.x, outputSizePixels.y);
            }

            return new Rect(verticalMargin.Left,
                verticalMargin.Top,
                Mathf.Min(verticalContentViewportWidth, outputSizePixels.x - verticalMargin.Left),
                Mathf.Min(verticalContentViewportHeight, outputSizePixels.y - verticalMargin.Top));
        }

        private IReadOnlyList<TextBoxLinkGeometry> BuildVerticalLinkGeometry(
            IReadOnlyList<Paragraph> source,
            IReadOnlyList<RichTextParser.LinkAnnotation> links)
        {
            var geometry = new List<TextBoxLinkGeometry>(links.Count);
            foreach (var link in links)
            {
                var rects = new List<Rect>();
                var blockRanges = new List<TextBoxLinkBlockRange>();
                for (var index = 0; index < source.Count; ++index)
                {
                    var blockStart = blockStartsUtf16[index];
                    var blockEnd = blockStart + blockPlainTexts[index].Length;
                    var localStart = Math.Max(link.StartUtf16, blockStart);
                    var localEnd = Math.Min(link.EndUtf16, blockEnd);
                    if (localStart < localEnd)
                    {
                        var offset = verticalLayoutMetrics.FrameOffsets[index];
                        foreach (var rect in source[index].GetRectsForRange(localStart - blockStart,
                                     localEnd - blockStart))
                        {
                            rects.Add(new Rect(rect.position + offset, rect.size));
                        }
                        blockRanges.Add(new TextBoxLinkBlockRange(index,
                            localStart - blockStart,
                            localEnd - blockStart,
                            offset));
                    }
                }

                geometry.Add(new TextBoxLinkGeometry(link.Occurrence, rects, blockRanges));
            }

            return geometry;
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

        private List<Paragraph> BuildParagraphs(TextBoxRenderTargetSettings settings,
            Vector2Int layoutSizePixels,
            RichTextParser.ParagraphPayload payload,
            float verticalInlineExtent = float.PositiveInfinity)
        {
            var profiling = TextBoxPipelineProfiler.Enabled;
            var result = CreateParagraphs(settings, payload, verticalInlineExtent);
            string plainText;
            using (profiling ? TextBoxPipelineProfiler.PlainTextMarker.Auto() : default)
            {
                plainText = PlainText(payload);
            }
            linkAnnotations = payload.Links.Count == 0
                ? Array.Empty<RichTextParser.LinkAnnotation>()
                : new List<RichTextParser.LinkAnnotation>(payload.Links);
            placeholderAnnotations = new List<RichTextParser.PlaceholderAnnotation>(payload.Placeholders);
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
            blockMarginRightValues.Clear();
            blockMarginBottomValues.Clear();
            blockMarginLeftValues.Clear();
            blockPhysicalAligns.Clear();
            hasHeightDependentBlockMargins = false;
            foreach (var block in payload.Blocks)
            {
                blockStartsUtf16.Add(block.StartUtf16);
                blockPlainTexts.Add(payload.GetPlainText(block));
                blockMarginTopValues.Add(block.Style.MarginTop);
                blockMarginRightValues.Add(block.Style.MarginRight);
                blockMarginBottomValues.Add(block.Style.MarginBottom);
                blockMarginLeftValues.Add(block.Style.MarginLeft);
                hasHeightDependentBlockMargins |= IsHeightDependent(block.Style.MarginTop) ||
                                                  IsHeightDependent(block.Style.MarginBottom) ||
                                                  block.Style.MarginRight.Unit == MarginUnit.Vh ||
                                                  block.Style.MarginLeft.Unit == MarginUnit.Vh;

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
                    var blockLeft = blockIndex < blockMarginLefts.Count ? blockMarginLefts[blockIndex] : 0f;
                    var offsetX = blockLeft + paintOffsetX / fitScale;
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
            RichTextParser.ParagraphPayload payload,
            float verticalInlineExtent)
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

            TextBoxVerticalLayoutSupport.ValidatePayload(payload, settings);
            using (profiling ? TextBoxPipelineProfiler.CreateNativeParagraphsMarker.Auto() : default)
            {
                IDisposable? indentScope = null;
                if (TextBoxVerticalLayoutSupport.IsVertical(settings))
                {
                    indentScope = TextBoxVerticalLayoutSupport.UseRepresentableVerticalIndent(
                        payload,
                        verticalInlineExtent,
                        settings.TextStyle.size,
                        out _);
                }
                try
                {
                    return payload.ToParagraphs(paragraphStyle, textStyle);
                }
                finally
                {
                    indentScope?.Dispose();
                }
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
                ResolveBlockMargins(settings, layoutSizePixels);
                ResolveBlockLayoutWidths(layoutWidth);
                for (var index = 0; index < targetParagraphs.Count; ++index)
                {
                    targetParagraphs[index].Layout(blockLayoutWidths[index]);
                }
                paragraphContentWidth = layoutWidth;
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
            ResolveBlockLayoutWidths(nextLayoutWidth);
            blockHeights.Clear();
            paragraphContentWidth = 0f;
            for (var index = 0; index < targetParagraphs.Count; ++index)
            {
                var paragraph = targetParagraphs[index];
                paragraph.Layout(blockLayoutWidths[index]);
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
            blockMarginRights.Clear();
            blockMarginBottoms.Clear();
            blockMarginLefts.Clear();
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
                blockMarginRights.Add(blockMarginRightValues[index].Resolve(context, MarginAxis.Horizontal) / scale);
                blockMarginBottoms.Add(blockMarginBottomValues[index].Resolve(context, MarginAxis.Vertical) / scale);
                blockMarginLefts.Add(blockMarginLeftValues[index].Resolve(context, MarginAxis.Horizontal) / scale);
            }
        }

        private void ResolveBlockLayoutWidths(float containingWidth)
        {
            blockLayoutWidths.Clear();
            for (var index = 0; index < blockMarginLefts.Count; ++index)
            {
                blockLayoutWidths.Add(Math.Max(1f,
                    containingWidth - blockMarginLefts[index] - blockMarginRights[index]));
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
                var blockWidth = index < blockLayoutWidths.Count ? blockLayoutWidths[index] : width;
                var blockLeft = index < blockMarginLefts.Count ? blockMarginLefts[index] : 0f;
                var visualWidth = Mathf.Clamp(targetParagraphs[index].LongestLine, 0f, blockWidth);
                var visualLeft = blockLeft +
                                 ResolveVisualLeft(blockPhysicalAligns[index], visualWidth, blockWidth);
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
                    ResolveBlockLayoutWidths(candidateWidth);
                    for (var index = 0; index < targetParagraphs.Count; ++index)
                    {
                        targetParagraphs[index].Layout(blockLayoutWidths[index]);
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
                ResolveBlockLayoutWidths(finalVirtualWidth);
                for (var index = 0; index < targetParagraphs.Count; ++index)
                {
                    targetParagraphs[index].Layout(blockLayoutWidths[index]);
                }
            }
            else
            {
                ResolveBlockLayoutWidths(finalVirtualWidth);
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
                var blockWidth = blockLayoutWidths[index] * fitScale;
                var visualWidth = Mathf.Clamp(targetParagraphs[index].LongestLine * fitScale, 0f, blockWidth);
                var left = blockMarginLefts[index] * fitScale +
                           ResolveVisualLeft(blockPhysicalAligns[index], visualWidth, blockWidth);
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
            for (var index = 0; index < targetParagraphs.Count; ++index)
            {
                var targetParagraph = targetParagraphs[index];
                blockHeights.Add(Mathf.Max(0f, targetParagraph.Height));
                width = Math.Max(width, targetParagraph.LongestLine +
                                        blockMarginLefts[index] + blockMarginRights[index]);
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
            ResolveBlockMargins(settings, layoutSizePixels);
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
                            Math.Max(1f, viewportWidth - blockMarginLefts[index] - blockMarginRights[index])));
                    }
                    using (profiling ? TextBoxPipelineProfiler.NoWrapMeasureMarker.Auto() : default)
                    {
                        blockContentWidths.Add(targetParagraphs[index].ResolveNoWrapContentWidth(plainText));
                    }
                }
            }

            var contentWidth = 0f;
            for (var index = 0; index < blockContentWidths.Count; ++index)
            {
                contentWidth = Math.Max(contentWidth, blockContentWidths[index] +
                                                     blockMarginLefts[index] + blockMarginRights[index]);
            }

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
            layoutWidth = newLayoutWidth;
            ResolveBlockLayoutWidths(layoutWidth);
            if (widthChanged || force)
            {
                using (profiling ? TextBoxPipelineProfiler.NoWrapFinalLayoutMarker.Auto() : default)
                {
                    for (var index = 0; index < targetParagraphs.Count; ++index)
                    {
                        targetParagraphs[index].Layout(blockLayoutWidths[index]);
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
                    Math.Max(0f, viewportWidth -
                                      (blockMarginLefts[index] + blockMarginRights[index]) * fitScale),
                    blockWidth * fitScale,
                    blockLayoutWidths[index] * fitScale);
                while (blockAlignmentOffsetsX.Count <= index)
                {
                    blockAlignmentOffsetsX.Add(0f);
                }
                if (!Mathf.Approximately(blockAlignmentOffsetsX[index], horizontalLayout.LayoutAlignmentOffset))
                {
                    blockAlignmentOffsetsX[index] = horizontalLayout.LayoutAlignmentOffset;
                    metricsChanged = true;
                }

                var left = blockMarginLefts[index] * fitScale + horizontalLayout.LogicalVisualLeft;
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
            if (verticalLayoutActive)
            {
                var verticalMargin = ResolveMargin(settings, layoutSizePixels);
                var verticalViewportSize = new Vector2(
                    ResolveVerticalContentWidth(settings, layoutSizePixels, verticalMargin),
                    ResolveVerticalContentHeight(settings, outputSizePixels, verticalMargin));
                var verticalContentSize = hasVerticalLayoutMetrics
                    ? verticalLayoutMetrics.PhysicalSize
                    : Vector2.zero;
                if (placeholderAnnotations.Count > 0 && hasVerticalLayoutMetrics)
                {
                    for (var index = 0; index < paragraphs.Count; ++index)
                    {
                        foreach (var rect in paragraphs[index].GetRectsForPlaceholders())
                        {
                            verticalContentSize.y = Mathf.Max(verticalContentSize.y,
                                rect.yMax + verticalLayoutMetrics.FrameOffsets[index].y);
                        }
                    }
                }
                verticalContentSize *= fitScale;
                var verticalMaxScrollOffset = new Vector2(
                    Mathf.Max(0f, verticalContentSize.x - verticalViewportSize.x),
                    Mathf.Max(0f, verticalContentSize.y - verticalViewportSize.y));
                var verticalHorizontalScrollState = viewport.HorizontalScrollState.ResolveVertical(
                    verticalMaxScrollOffset.x,
                    viewport.HorizontalScrollRequest);
                var verticalScrollOffset = new Vector2(
                    verticalHorizontalScrollState.ScrollX,
                    Mathf.Clamp(FloatUtil.IsFinite(viewport.RequestedScrollY)
                            ? viewport.RequestedScrollY
                            : 0f,
                        0f,
                        verticalMaxScrollOffset.y));

                var verticalChanged = viewportSize != verticalViewportSize ||
                                      contentSize != verticalContentSize ||
                                      maxScrollOffset != verticalMaxScrollOffset ||
                                      scrollOffset != verticalScrollOffset;
                viewportSize = verticalViewportSize;
                contentSize = verticalContentSize;
                maxScrollOffset = verticalMaxScrollOffset;
                scrollOffset = verticalScrollOffset;
                horizontalScrollState = verticalHorizontalScrollState;
                return verticalChanged;
            }

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
            if (TextBoxVerticalLayoutSupport.IsVertical(settings))
            {
                if (settings.Sizing.HasValue)
                {
                    return settings.Sizing.Value.Margin;
                }

                var verticalContext = settings.VerticalMarginContext ??
                                      new MarginResolveContext(containerSizePixels.x,
                                          containerSizePixels.y,
                                          settings.TextStyle.size);
                return settings.Margin.ResolveVertical(verticalContext);
            }

            if (settings.Sizing.HasValue)
            {
                return settings.Sizing.Value.Margin;
            }

            return settings.Margin.Resolve(new MarginResolveContext(containerSizePixels.x,
                containerSizePixels.y,
                settings.TextStyle.size));
        }

        private float ResolveVerticalContentWidth(TextBoxRenderTargetSettings settings,
            Vector2Int layoutSizePixels,
            ResolvedMargin margin)
        {
            var allocatedWidth = settings.Sizing.HasValue
                ? settings.Sizing.Value.UsedWidth
                : layoutSizePixels.x;
            return Mathf.Max(0f, allocatedWidth - margin.FixedHorizontalSize);
        }

        private float ResolveVerticalContentHeight(TextBoxRenderTargetSettings settings,
            Vector2Int layoutSizePixels,
            ResolvedMargin margin)
        {
            var allocatedHeight = settings.Sizing.HasValue
                ? settings.Sizing.Value.UsedHeight
                : layoutSizePixels.y;
            return Mathf.Max(0f, allocatedHeight - margin.FixedVerticalSize);
        }

        internal float ResolveVerticalInlineExtent(TextBoxRenderTargetSettings settings,
            Vector2Int layoutSizePixels)
        {
            if (!UsesFiniteVerticalInlineExtent(settings))
            {
                return float.PositiveInfinity;
            }

            var margin = ResolveMargin(settings, layoutSizePixels);
            return ResolveVerticalContentHeight(settings, layoutSizePixels, margin);
        }

        private static bool UsesFiniteVerticalInlineExtent(TextBoxRenderTargetSettings settings)
        {
            return TextBoxVerticalBlockLayout.UsesFiniteInlineExtent(settings);
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
            foreach (var block in payload.Blocks)
            {
                builder.Append(payload.GetPlainText(block));
            }

            return builder.ToString();
        }

        private IReadOnlyList<TextBoxPlaceholderGeometry> BuildPlaceholderGeometry(
            Vector2 paintPosition, Rect clipRect, float visualScrollOffsetX)
        {
            var result = new List<TextBoxPlaceholderGeometry>(placeholderAnnotations.Count);
            foreach (var annotation in placeholderAnnotations)
            {
                var rect = default(Rect);
                var visible = false;
                for (var index = 0; index < paragraphs.Count; ++index)
                {
                    var start = blockStartsUtf16[index];
                    if (annotation.StartUtf16 < start || annotation.EndUtf16 > start + blockPlainTexts[index].Length)
                        continue;
                    var boxes = paragraphs[index].GetRectsForRange(annotation.StartUtf16 - start,
                        annotation.EndUtf16 - start);
                    if (boxes.Count == 0) break;
                    var offset = verticalLayoutActive
                        ? verticalLayoutMetrics.FrameOffsets[index]
                        : new Vector2((index < blockMarginLefts.Count ? blockMarginLefts[index] : 0f) +
                            TextBoxNoWrapHorizontalLayout.ResolvePaintOffsetX(
                                index < blockAlignmentOffsetsX.Count ? blockAlignmentOffsetsX[index] : 0f,
                                scrollOffset.x, visualScrollOffsetX) / fitScale,
                            index < blockTops.Count ? blockTops[index] : 0f);
                    rect = new Rect(boxes[0].position + offset, boxes[0].size);
                    var outputRect = new Rect(rect.position * fitScale + paintPosition, rect.size * fitScale);
                    visible = outputRect.Overlaps(clipRect);
                    break;
                }
                result.Add(new TextBoxPlaceholderGeometry(annotation.Key, annotation.StartUtf16, rect, visible));
            }
            return result.AsReadOnly();
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
                        viewport.VisualScrollOffset.x) +
                                   (index < blockMarginLefts.Count ? blockMarginLefts[index] : 0f) * fitScale;
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
