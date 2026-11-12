using System;
using Milestro.Model;
using Milestro.RichTextParser;
using Milestro.Skia;
using Milestro.Util;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using Canvas = UnityEngine.Canvas;

namespace Milestro.Components.Internal
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Milestro/Internal/Text Box Render Texture Producer")]
    public class TextBoxRenderTextureProducer : RenderTextureProducer, ITextIntrinsicMeasurement
    {
        [TextArea(3, 10)]
        [SerializeField]
        private string m_content = "";

        [SerializeField, InspectorName("Rich Text Parser")]
        [RichTextParserId]
        [Tooltip("Id of the parser used to render content as rich text. Empty renders content as plain text.")]
        private string? m_richTextParserId;

        [SerializeField]
        private Margin m_margin = new Margin();

        [SerializeField]
        private ParagraphStyleSettings m_paragraphStyle = new ParagraphStyleSettings();

        [SerializeField]
        private TextStyleSettings m_textStyle = new TextStyleSettings();

        [Tooltip("Wrap allows automatic line breaks at the actual assigned box width; NoWrap keeps each paragraph on one line. " +
                 "Also controls line breaking during Auto Font Size fitting. Width Mode selects the preferred box width; " +
                 "FitContent can become narrower than MaxContent only when wrapping is allowed. Single Line overrides wrapping.")]
        [SerializeField]
        private TextBoxWrapMode m_wrapMode = TextBoxWrapMode.Wrap;

        [SerializeField]
        private TextOverflow m_textOverflow = TextOverflow.Clip;

        [SerializeField]
        private TextAutoFontSizeSettings m_autoFontSize = TextAutoFontSizeSettings.Default;

        [SerializeField, HideInInspector]
        private bool m_screenSpaceRasterization = true;

        [NonSerialized] private RectTransform? rectTransformCache;
        [NonSerialized] private readonly Vector3[] worldCorners = new Vector3[4];
        [NonSerialized] private Canvas? canvasCache;
        [NonSerialized] private bool canvasLookupAttempted;
        [NonSerialized] private int canvasLookupRetryFrame;
        [NonSerialized] private ScreenSpaceRasterMetrics.RasterizationContext rasterContextCache;
        [NonSerialized] private float rasterDesiredScaleCache = 1f;
        [NonSerialized] private bool rasterContextValid;
        [NonSerialized] private ITextBoxRenderTarget? renderTarget;
        [NonSerialized] private TextBoxIntrinsicMeasurement? intrinsicMeasurement;
        [NonSerialized] private TextBoxIntrinsicMeasurement? horizontalFallbackMeasurement;
        [NonSerialized] private TextBoxVerticalIntrinsicMeasurement? verticalMeasurement;
        [NonSerialized] private TextSizingResult? lastSizing;
        [NonSerialized] private Exception? m_lastLayoutError;
        [NonSerialized] private bool m_hasObservedMargin;
        [NonSerialized] private MarginValue m_observedMarginLeft;
        [NonSerialized] private MarginValue m_observedMarginTop;
        [NonSerialized] private MarginValue m_observedMarginRight;
        [NonSerialized] private MarginValue m_observedMarginBottom;
        [NonSerialized] private bool m_hasObservedVerticalMarginContext;
        [NonSerialized] private MarginResolveContext m_observedVerticalMarginContext;
        [NonSerialized] private bool m_hasObservedRectSize;
        [NonSerialized] private Vector2 m_observedRectSize;

        public float effectiveFontSize => ResolveSizingAtLayoutWidth().EffectiveFontSize;

        public TextIntrinsicMetrics Measure(float fontSize, float contentWidth)
        {
            var rect = RectTransformComponent();
            var available = rect != null
                ? TextSizingBounds.ExternalAvailableSize(rect)
                : Vector2.zero;
            return Measurement(available).Measure(fontSize, contentWidth);
        }

        private TextBoxIntrinsicMeasurement Measurement(Vector2 available)
        {
            HookStyleModels();
            if (intrinsicMeasurement == null)
                intrinsicMeasurement = new TextBoxIntrinsicMeasurement(TextBoxRenderTargetSettingsBuilder.BuildFrom(this));
            intrinsicMeasurement.SetAvailableSize(available);
            return intrinsicMeasurement;
        }

        public TextSizingResult ResolveSizing()
        {
            return ResolveSizing(null);
        }

        internal TextSizingResult ResolveSizingAtLayoutWidth()
        {
            return ResolveSizing(RectTransformComponent()?.rect.width);
        }

        private TextSizingResult ResolveSizing(float? assignedWidth)
        {
            return ResolveSizing(assignedWidth, out _);
        }

        private TextSizingResult ResolveSizing(float? assignedWidth, out bool usesVerticalSizing)
        {
            var rect = RectTransformComponent();
            var layout = GetComponent<TextBoxLayoutElement>();
            var widthMode = layout != null && layout.isActiveAndEnabled ? layout.widthMode : TextWidthMode.Fixed;
            var heightMode = layout != null && layout.isActiveAndEnabled ? layout.heightMode : TextHeightMode.Fixed;
            var size = rect != null ? rect.rect.size : Vector2.zero;
            var available = rect != null ? TextSizingBounds.ExternalAvailableSize(rect) : Vector2.zero;
            usesVerticalSizing = false;
            if (IsVerticalRequested())
            {
                var marginContext = ResolveVerticalMarginContext(m_textStyle.size, rect);
                var requestedSettings = RequestedSettings().WithVerticalLayout(marginContext, heightMode);
                var preparation = TextBoxVerticalLayoutSupport.PrepareRenderConfiguration(
                    requestedSettings);
                var effective = preparation.Settings;
                var request = BuildSizingRequest(widthMode, heightMode, size, rect, effective);
                if (preparation.UsesHorizontalFallback)
                {
                    return ResolveHorizontalFallbackSizing(request, available, effective,
                        assignedWidth);
                }

                usesVerticalSizing = true;
                return ResolveVerticalSizing(request, size, heightMode, assignedWidth,
                    effective);
            }

            var settings = RequestedSettings();
            var horizontalRequest = BuildSizingRequest(widthMode, heightMode, size, rect, settings);
            return TextSizingResolver.Resolve(this, horizontalRequest,
                fontSize => m_margin.Resolve(new MarginResolveContext(available.x, available.y, fontSize)),
                assignedWidth);
        }

        private TextSizingRequest BuildSizingRequest(TextWidthMode widthMode,
            TextHeightMode heightMode,
            Vector2 allocatedSize,
            RectTransform? rect,
            TextBoxRenderTargetSettings settings)
        {
            var availableWidth = rect != null
                ? TextSizingBounds.AvailableWidth(rect)
                : float.PositiveInfinity;
            return new TextSizingRequest(widthMode, heightMode,
                allocatedSize.x, allocatedSize.y, availableWidth,
                settings.WrapMode == TextBoxWrapMode.Wrap &&
                !settings.ParagraphStyle.singleLine,
                settings.TextStyle.size,
                settings.AutoFontSize,
                settings.MinSize,
                settings.MaxSize,
                widthCap: widthMode == TextWidthMode.FitContent
                    ? availableWidth
                    : float.PositiveInfinity);
        }

        private TextSizingResult ResolveVerticalSizing(TextSizingRequest requested,
            Vector2 allocatedSize,
            TextHeightMode heightMode,
            float? assignedWidth,
            TextBoxRenderTargetSettings settings)
        {
            var margin = settings.Margin.ResolveVertical(
                settings.VerticalMarginContext ??
                new MarginResolveContext(0f, 0f, settings.TextStyle.size,
                    hasContainerWidth: false));
            if (heightMode != TextHeightMode.Fixed)
            {
                var unsized = new TextSizingRequest(TextWidthMode.Fixed,
                    TextHeightMode.Fixed,
                    allocatedSize.x,
                    allocatedSize.y,
                    float.PositiveInfinity,
                    requested.Wrap,
                    requested.DesignFontSize);
                return new TextSizingResult(unsized,
                    Mathf.Max(0f, allocatedSize.x),
                    Mathf.Max(0f, allocatedSize.y),
                    Mathf.Max(0.1f, requested.DesignFontSize),
                    margin,
                    default,
                    0f,
                    0f);
            }

            HookStyleModels();
            if (verticalMeasurement != null && !verticalMeasurement.MatchesMarginContext(settings))
            {
                verticalMeasurement.Dispose();
                verticalMeasurement = null;
            }
            verticalMeasurement ??= new TextBoxVerticalIntrinsicMeasurement(settings);
            return TextBoxVerticalSizingResolver.Resolve(verticalMeasurement,
                requested,
                margin,
                allocatedSize.y,
                assignedWidth);
        }

        private TextSizingResult ResolveHorizontalFallbackSizing(TextSizingRequest request,
            Vector2 available,
            TextBoxRenderTargetSettings settings,
            float? assignedWidth)
        {
            HookStyleModels();
            horizontalFallbackMeasurement ??= new TextBoxIntrinsicMeasurement(settings);
            horizontalFallbackMeasurement.SetAvailableSize(available);
            return TextSizingResolver.Resolve(horizontalFallbackMeasurement,
                request,
                fontSize => settings.Margin.Resolve(
                    new MarginResolveContext(available.x, available.y, fontSize)),
                assignedWidth);
        }

        private bool IsVerticalRequested()
        {
            return m_paragraphStyle != null &&
                   m_paragraphStyle.writingMode == WritingMode.VerticalRl;
        }

        private TextBoxRenderTargetSettings RequestedSettings()
        {
            return TextBoxRenderTargetSettingsBuilder.BuildFrom(this);
        }

        private MarginResolveContext ResolveVerticalMarginContext(float fontSize,
            RectTransform? rect)
        {
            var hasContainer = TextSizingBounds.TryGetContainingBlockWidth(rect!, out var cbWidth);
            var viewportWidth = 0f;
            var viewportHeight = 0f;
            var hasViewport = false;
            var canvas = CanvasComponent() ?? GetComponentInParent<Canvas>(true);
            var root = canvas != null ? canvas.rootCanvas : null;
            if (root != null)
            {
                var viewport = root.transform as RectTransform;
                if (viewport != null &&
                    FloatUtil.IsFinite(viewport.rect.width) &&
                    FloatUtil.IsFinite(viewport.rect.height))
                {
                    viewportWidth = Mathf.Max(0f, viewport.rect.width);
                    viewportHeight = Mathf.Max(0f, viewport.rect.height);
                    hasViewport = true;
                }
            }

            return new MarginResolveContext(cbWidth,
                0f,
                fontSize,
                viewportWidth,
                viewportHeight,
                hasContainer,
                hasViewport);
        }

        internal void InvalidateSizing()
        {
            MarkPropertiesChanged();
        }
        [NonSerialized] internal Func<ITextBoxRenderTarget>? renderTargetFactory;
        [NonSerialized] private ColorSpace? m_colorSpaceOverride;
        [NonSerialized] private float m_scrollX;
        [NonSerialized] private float m_scrollY;
        [NonSerialized] private TextBoxHorizontalScrollState m_horizontalScrollState;
        [NonSerialized] private Vector2 m_visualScrollOffset;
        [NonSerialized] private WritingMode m_observedWritingMode = WritingMode.HorizontalTb;
        [NonSerialized] private bool m_hasObservedWritingMode;
        [NonSerialized] private bool m_flowMode;
        [NonSerialized] private bool m_hasVisibleRange;
        [NonSerialized] private float m_visibleStartY;
        [NonSerialized] private float m_visibleEndY;
        [NonSerialized] private int m_visibleCapacityHeight;
#if MILESTRO_FLOW_TEXTBOX_DEBUG
        [NonSerialized] private bool m_flowDiagnosticsEnabled;
#endif
#if UNITY_EDITOR
        private const int MaxEditorSkippedRenderRetries = 2;
        [NonSerialized] private bool m_editorRebuildQueued;
        [NonSerialized] private int m_editorSkippedRenderRetries;
#endif

        public override Texture? OutputTexture => renderTarget?.OutputTexture;
        public override Rect OutputUvRect => renderTarget?.OutputUvRect ?? new Rect(0f, 0f, 1f, 1f);
        public override int OutputWidth => renderTarget?.OutputWidth ?? 0;
        public override int OutputHeight => renderTarget?.OutputHeight ?? 0;
        public override bool HasOutput => renderTarget?.HasOutput ?? false;
        public override long OutputVersion => renderTarget?.OutputVersion ?? 0;
        public float contentWidth => renderTarget?.ContentSize.x ?? 0f;
        public float contentHeight => renderTarget?.ContentSize.y ?? 0f;
        public float viewportWidth => renderTarget?.ViewportSize.x ?? 0f;
        public float viewportHeight => renderTarget?.ViewportSize.y ?? 0f;
        public float maxScrollX => renderTarget?.MaxScrollOffset.x ?? 0f;
        public float maxScrollY => renderTarget?.MaxScrollOffset.y ?? 0f;

        internal bool isVerticalLayout => renderTarget?.IsVertical ?? false;

        internal Exception? lastLayoutError => m_lastLayoutError;

        internal event Action? LayoutChanged;

        internal bool screenSpaceRasterization
        {
            get => m_screenSpaceRasterization;
            set
            {
                if (m_screenSpaceRasterization == value)
                {
                    return;
                }

                m_screenSpaceRasterization = value;
                MarkPaintChanged();
            }
        }

#if MILESTRO_FLOW_TEXTBOX_DEBUG
        internal bool flowDiagnosticsEnabled
        {
            get => m_flowDiagnosticsEnabled;
            set => m_flowDiagnosticsEnabled = value;
        }
#endif

        public string content
        {
            get => m_content;
            set
            {
                var nextValue = value ?? "";
                if (string.Equals(m_content, nextValue, StringComparison.Ordinal))
                {
                    return;
                }

                m_content = nextValue;
                MarkContentChanged();
            }
        }

        public string? richTextParserId
        {
            get => m_richTextParserId;
            set
            {
                var nextValue = string.IsNullOrEmpty(value) ? null : value;
                if (string.Equals(m_richTextParserId, nextValue, StringComparison.Ordinal))
                {
                    return;
                }

                m_richTextParserId = nextValue;
                MarkContentChanged();
            }
        }

        public ParagraphStyleSettings paragraphStyle
        {
            get => m_paragraphStyle;
            set
            {
                var nextValue = value ?? new ParagraphStyleSettings();
                if (ReferenceEquals(m_paragraphStyle, nextValue))
                {
                    return;
                }

                if (m_paragraphStyle != null)
                {
                    m_paragraphStyle.Changed -= MarkPropertiesChanged;
                }
                m_paragraphStyle = nextValue;
                HookStyleModels();
                MarkPropertiesChanged();
            }
        }

        public TextStyleSettings textStyle
        {
            get => m_textStyle;
            set
            {
                var nextValue = value ?? new TextStyleSettings();
                if (ReferenceEquals(m_textStyle, nextValue))
                {
                    return;
                }

                if (m_textStyle != null)
                {
                    m_textStyle.Changed -= MarkPropertiesChanged;
                }
                m_textStyle = nextValue;
                HookStyleModels();
                MarkPropertiesChanged();
            }
        }

        public TextBoxWrapMode wrapMode
        {
            get => m_wrapMode;
            set
            {
                m_wrapMode = value;
                MarkPropertiesChanged();
            }
        }

        public TextOverflow textOverflow
        {
            get => m_textOverflow;
            set
            {
                m_textOverflow = value;
                MarkPropertiesChanged();
            }
        }

        public Margin margin
        {
            get => m_margin;
            set
            {
                m_margin = value ?? new Margin();
                MarkPropertiesChanged();
            }
        }

        public bool enableAutoFontSize
        {
            get => m_autoFontSize.enabled;
            set
            {
                if (m_autoFontSize.enabled == value)
                {
                    return;
                }

                m_autoFontSize.enabled = value;
                MarkPropertiesChanged();
            }
        }

        public float fontSizeMin
        {
            get => m_autoFontSize.fontSizeMin;
            set
            {
                var nextValue = TextAutoFontSizeSettings.NormalizeBound(value);
                if (Mathf.Approximately(m_autoFontSize.fontSizeMin, nextValue))
                {
                    return;
                }

                m_autoFontSize.fontSizeMin = nextValue;
                m_autoFontSize.Normalize();
                MarkPropertiesChanged();
            }
        }

        public float fontSizeMax
        {
            get => m_autoFontSize.fontSizeMax;
            set
            {
                var nextValue = TextAutoFontSizeSettings.NormalizeBound(value);
                if (Mathf.Approximately(m_autoFontSize.fontSizeMax, nextValue))
                {
                    return;
                }

                m_autoFontSize.fontSizeMax = nextValue;
                m_autoFontSize.Normalize();
                MarkPropertiesChanged();
            }
        }

        public bool srgb
        {
            get => SurfaceColorSpace() == ColorSpace.Linear;
            set
            {
                m_colorSpaceOverride = value ? ColorSpace.Linear : ColorSpace.Gamma;
                MarkPropertiesChanged();
            }
        }

        public float scrollX
        {
            get => m_scrollX;
            set => SetScrollX(value);
        }

        public float scrollY
        {
            get => m_scrollY;
            set => SetScrollY(value);
        }

        internal Vector2 visualScrollOffset
        {
            get => m_visualScrollOffset;
            set
            {
                var next = new Vector2(FloatUtil.IsFinite(value.x) ? value.x : 0f,
                    FloatUtil.IsFinite(value.y) ? value.y : 0f);
                if (m_visualScrollOffset == next)
                {
                    return;
                }

                m_visualScrollOffset = next;
                MarkPaintChanged();
            }
        }

        public void ScrollByX(float delta)
        {
            if (TextBoxHorizontalScrollState.OffsetsEqual(delta, 0f))
            {
                return;
            }

            SetScrollX(m_scrollX + delta);
        }

        public void ScrollByY(float delta)
        {
            if (Mathf.Approximately(delta, 0f))
            {
                return;
            }

            SetScrollY(m_scrollY + delta);
        }

        internal bool flowMode
        {
            get => m_flowMode;
            set
            {
                if (m_flowMode == value)
                {
                    return;
                }

                m_flowMode = value;
                m_hasVisibleRange = false;
                m_visibleCapacityHeight = 0;
                MarkPaintChanged();
            }
        }

        internal bool SetVisibleRange(float localStartY, float localEndY, float visibleCapacityHeight)
        {
            if (!TextBoxFlowVisibleRange.TryNormalize(localStartY, localEndY, out var nextStartY, out var nextEndY))
            {
                DebugFlow($"set visible range clears raw=[{localStartY:F3},{localEndY:F3}]");
                return ClearVisibleRange();
            }

            var nextVisibleHeight = nextEndY - nextStartY;
            var nextCapacityHeight = TextBoxFlowVisibleRange.ResolveCapacityHeight(visibleCapacityHeight,
                nextVisibleHeight,
                0f);
            if (m_hasVisibleRange &&
                Mathf.Approximately(m_visibleStartY, nextStartY) &&
                Mathf.Approximately(m_visibleEndY, nextEndY) &&
                m_visibleCapacityHeight == nextCapacityHeight)
            {
                return false;
            }

            m_hasVisibleRange = true;
            m_visibleStartY = nextStartY;
            m_visibleEndY = nextEndY;
            m_visibleCapacityHeight = nextCapacityHeight;
            MarkPaintChanged();
            DebugFlow("set visible range " +
                      $"raw=[{localStartY:F3},{localEndY:F3}] " +
                      $"normalized=[{nextStartY:F3},{nextEndY:F3}] " +
                      $"capacity={m_visibleCapacityHeight}");
            return true;
        }

        internal bool ClearVisibleRange()
        {
            if (!m_hasVisibleRange)
            {
                return false;
            }

            m_hasVisibleRange = false;
            m_visibleStartY = 0f;
            m_visibleEndY = 0f;
            m_visibleCapacityHeight = 0;
            MarkPaintChanged();
            DebugFlow("clear visible range");
            return true;
        }

        protected virtual void OnEnable()
        {
            rectTransformCache = GetComponent<RectTransform>();
            CaptureObservedRectSize();
            if (GetComponent<Milestro.Components.WorldSpaceTextBox>() != null)
            {
                m_screenSpaceRasterization = false;
            }

            ResetRasterizationContext();
            RebuildResources(forceText: true);
        }

        protected virtual void OnDisable()
        {
            m_visualScrollOffset = Vector2.zero;
            DisposeRenderTarget();
            intrinsicMeasurement?.Dispose();
            intrinsicMeasurement = null;
            horizontalFallbackMeasurement?.Dispose();
            horizontalFallbackMeasurement = null;
            verticalMeasurement?.Dispose();
            verticalMeasurement = null;
            m_hasObservedVerticalMarginContext = false;
            m_hasObservedRectSize = false;
            ResetRasterizationContext();
        }

        private void OnTransformParentChanged()
        {
            canvasCache = null;
            canvasLookupAttempted = false;
            rasterContextValid = false;
        }

        private void Update()
        {
            RebuildResources(forceText: false);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (m_margin == null)
            {
                m_margin = new Margin();
            }
            if (m_paragraphStyle == null)
            {
                m_paragraphStyle = new ParagraphStyleSettings();
            }
            if (m_textStyle == null)
            {
                m_textStyle = new TextStyleSettings();
            }
            HookStyleModels();
            m_margin.Normalize();
            m_scrollX = FloatUtil.IsFinite(m_scrollX) ? Mathf.Max(0f, m_scrollX) : 0f;
            m_scrollY = FloatUtil.IsFinite(m_scrollY) ? Mathf.Max(0f, m_scrollY) : 0f;
            m_autoFontSize.Normalize();
            MarkPropertiesChanged();
            if (isActiveAndEnabled)
            {
                QueueEditorRebuild();
            }
        }
#endif

        private void OnRectTransformDimensionsChange()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            var rectTransform = transform as RectTransform;
            var nextSize = rectTransform != null ? rectTransform.rect.size : Vector2.zero;
            var hadSize = m_hasObservedRectSize;
            var heightChanged = !hadSize || !Mathf.Approximately(nextSize.y, m_observedRectSize.y);
            var widthChanged = !hadSize || !Mathf.Approximately(nextSize.x, m_observedRectSize.x);
            m_observedRectSize = nextSize;
            m_hasObservedRectSize = true;
            if (!heightChanged && widthChanged &&
                renderTarget != null && renderTarget.IsVertical)
            {
                RenderTarget.MarkPaintChanged();
            }
            else
            {
                MarkPropertiesChanged();
            }
#if UNITY_EDITOR
            QueueEditorRebuild();
#endif
        }

#if UNITY_EDITOR
        private void QueueEditorRebuild()
        {
            if (Application.isPlaying || m_editorRebuildQueued)
            {
                return;
            }

            m_editorRebuildQueued = true;
            EditorApplication.delayCall += RebuildResourcesFromEditorDelayCall;
        }

        private void RebuildResourcesFromEditorDelayCall()
        {
            m_editorRebuildQueued = false;
            if (!this || !isActiveAndEnabled)
            {
                return;
            }

            RebuildResources(forceText: false);
        }
#endif

        private void RebuildResources(bool forceText)
        {
            ObserveMarginChanges();
            ObserveVerticalLayoutChanges();
            if (!forceText &&
                renderTarget != null &&
                !renderTarget.HasPendingWork &&
                !RasterizationContextChanged())
            {
                return;
            }

            var profiling = TextBoxPipelineProfiler.Enabled;
            using var rebuildScope = profiling
                ? TextBoxPipelineProfiler.RebuildMarker.Auto()
                : default;
            try
            {
                TextBoxVerticalLayoutSupport.ValidateProducer(this);

                TextBoxRenderTargetSettings settings;
                using (profiling ? TextBoxPipelineProfiler.SettingsMarker.Auto() : default)
                {
                    settings = CurrentSettings();
                }

                Vector2Int currentSize;
                using (profiling ? TextBoxPipelineProfiler.SizeMarker.Auto() : default)
                {
                    currentSize = CurrentSize();
                }

                TextBoxRenderViewport viewport;
                using (profiling ? TextBoxPipelineProfiler.ViewportMarker.Auto() : default)
                {
                    viewport = CurrentViewportWithRasterization(currentSize);
                }

                if (m_screenSpaceRasterization && !rasterContextValid)
                {
#if UNITY_EDITOR
                    QueueEditorRebuild();
#endif
                    return;
                }

                bool rebuilt;
                var hadOutput = HasOutput;
                using (profiling ? TextBoxPipelineProfiler.TargetRebuildMarker.Auto() : default)
                {
                    rebuilt = RenderTarget.Rebuild(viewport,
                        SurfaceColorSpace(),
                        settings,
                        forceText,
                        this);
                }
                if (RenderTarget.LastLayoutError != null)
                {
                    m_lastLayoutError = RenderTarget.LastLayoutError;
                    if (hadOutput && !HasOutput)
                    {
                        NotifyOutputChanged();
                    }
                    return;
                }
                m_lastLayoutError = null;
                if (hadOutput && !HasOutput)
                {
                    NotifyOutputChanged();
                }
                DebugFlow("rebuild " +
                          $"forceText={forceText} rebuilt={rebuilt} currentSize={currentSize.x}x{currentSize.y} " +
                          $"hasRange={m_hasVisibleRange} range=[{m_visibleStartY:F3},{m_visibleEndY:F3}] " +
                          $"capacity={m_visibleCapacityHeight} " +
                          $"hasOutput={HasOutput} output={OutputWidth}x{OutputHeight} " +
                          $"viewport={viewportWidth:F3}x{viewportHeight:F3} content={contentWidth:F3}x{contentHeight:F3}");
                if (!rebuilt)
                {
#if UNITY_EDITOR
                    QueueEditorRebuild();
#endif
                }
                else
                {
                    ApplyHorizontalScrollState(RenderTarget.HorizontalScrollState);
                    if (!m_flowMode)
                    {
                        var nextScrollY = RenderTarget.ScrollOffset.y;
                        if (!Mathf.Approximately(m_scrollY, nextScrollY))
                        {
                            m_scrollY = nextScrollY;
                        }
                    }
                }
            }
            catch (Exception error)
            {
                m_lastLayoutError = error;
                if (renderTarget != null)
                {
                    renderTarget.InvalidateFailedOutput();
                }
                // Consumers must drop their previous Texture/UvRect even though
                // the rebuild exception interrupts the caller's normal apply
                // step. HasOutput is false at this point, so the observer clears
                // the stale graphic immediately.
                NotifyOutputChanged();
#if UNITY_EDITOR
                Debug.LogException(error, this);
#endif
            }
        }

        public void RebuildOutput(bool forceText)
        {
            RebuildResources(forceText);
        }

        internal bool TryHitLink(Vector2 normalizedViewportPoint, out TextBoxLinkHit hit)
        {
            if (renderTarget == null)
            {
                hit = default;
                return false;
            }

            return renderTarget.TryHitLink(normalizedViewportPoint, out hit);
        }

        internal bool TryGetContentSnapshot(out TextBoxContentSnapshot snapshot)
        {
            if (renderTarget == null)
            {
                snapshot = default;
                return false;
            }

            return renderTarget.TryGetContentSnapshot(out snapshot);
        }

        internal bool TryGetLinkGeometrySnapshot(out TextBoxLinkGeometrySnapshot snapshot, out long generation)
        {
            if (renderTarget == null)
            {
                snapshot = default;
                generation = 0;
                return false;
            }

            return renderTarget.TryGetLinkGeometrySnapshot(out snapshot, out generation);
        }

        private Vector2Int CurrentSize()
        {
            var rectTransform = RectTransformComponent();
            if (rectTransform == null)
            {
                return Vector2Int.one;
            }

            var rect = rectTransform.rect;
            return new Vector2Int(Mathf.Max(1, Mathf.CeilToInt(rect.width)),
                Mathf.Max(1, Mathf.CeilToInt(rect.height)));
        }

        private ColorSpace SurfaceColorSpace()
        {
            return m_colorSpaceOverride ?? UnitySkiaRenderTextureDescriptor.DefaultColorSpace;
        }

        internal TextBoxRenderTargetSettings CurrentSettings()
        {
            HookStyleModels();
            var settings = TextBoxRenderTargetSettingsBuilder.BuildFrom(this);
            var layout = GetComponent<TextBoxLayoutElement>();
            var requestedVertical = IsVerticalRequested();
            var marginContext = requestedVertical
                ? ResolveVerticalMarginContext(m_textStyle.size, RectTransformComponent())
                : (MarginResolveContext?)null;
            if (marginContext.HasValue)
            {
                settings = settings.WithVerticalLayout(marginContext.Value,
                    layout != null && layout.isActiveAndEnabled
                        ? layout.heightMode
                        : TextHeightMode.Fixed);
            }
            if (layout != null && layout.isActiveAndEnabled &&
                ((enableAutoFontSize && textOverflow != TextOverflow.Ellipsis) ||
                 layout.widthMode != TextWidthMode.Fixed ||
                 layout.heightMode != TextHeightMode.Fixed))
            {
                var sizing = ResolveSizing(RectTransformComponent()?.rect.width,
                    out var usesVerticalSizing);
                var available = TextSizingBounds.ExternalAvailableSize(RectTransformComponent()!);
                settings = settings.WithSizing(sizing,
                    available,
                    layout.heightMode,
                    marginContext,
                    usesVerticalSizing);
                if (!lastSizing.HasValue ||
                    !Mathf.Approximately(lastSizing.Value.PreferredWidth, sizing.PreferredWidth) ||
                    !Mathf.Approximately(lastSizing.Value.PreferredHeight, sizing.PreferredHeight))
                {
                    lastSizing = sizing;
                    NotifyLayoutChanged();
                }
            }
            return settings;
        }

        private void HookStyleModels()
        {
            if (m_paragraphStyle != null)
            {
                m_paragraphStyle.Changed -= MarkPropertiesChanged;
                m_paragraphStyle.Changed += MarkPropertiesChanged;
            }
            if (m_textStyle != null)
            {
                m_textStyle.Changed -= MarkPropertiesChanged;
                m_textStyle.Changed += MarkPropertiesChanged;
            }
        }

        private RectTransform? RectTransformComponent()
        {
            if (rectTransformCache == null)
            {
                rectTransformCache = GetComponent<RectTransform>();
            }

            return rectTransformCache;
        }

        private ResolvedMargin ResolveMargin(TextBoxRenderTargetSettings settings, Vector2Int containerSizePixels)
        {
            var referenceSize = settings.Sizing.HasValue ? settings.MarginReferenceSize :
                new Vector2(containerSizePixels.x, containerSizePixels.y);
            return settings.Margin.Resolve(new MarginResolveContext(referenceSize.x,
                referenceSize.y,
                settings.TextStyle.size));
        }

        private ITextBoxRenderTarget RenderTarget
        {
            get
            {
                if (renderTarget == null)
                {
                    renderTarget = renderTargetFactory?.Invoke() ?? new TextBoxRenderTarget();
                    renderTarget.RenderEventCompleted += OnRenderEventCompleted;
                }

                return renderTarget;
            }
        }

        internal void DisposeRenderTarget()
        {
            if (renderTarget == null)
            {
                return;
            }

            renderTarget.RenderEventCompleted -= OnRenderEventCompleted;
            renderTarget.Dispose();
            renderTarget = null;
        }

        private void OnRenderEventCompleted(UnitySkiaRenderTextureSurface.RenderSubmissionStatus status)
        {
            if (!this)
            {
                return;
            }

            if (status == UnitySkiaRenderTextureSurface.RenderSubmissionStatus.Drawn)
            {
#if UNITY_EDITOR
                m_editorSkippedRenderRetries = 0;
#endif
                NotifyOutputChanged();
                return;
            }

#if UNITY_EDITOR
            if (status == UnitySkiaRenderTextureSurface.RenderSubmissionStatus.Skipped &&
                !Application.isPlaying &&
                isActiveAndEnabled &&
                m_editorSkippedRenderRetries < MaxEditorSkippedRenderRetries)
            {
                ++m_editorSkippedRenderRetries;
                QueueEditorRebuild();
            }
#endif
        }

        private void MarkPropertiesChanged()
        {
#if UNITY_EDITOR
            m_editorSkippedRenderRetries = 0;
#endif
            var writingMode = m_paragraphStyle?.writingMode ?? WritingMode.HorizontalTb;
            if (m_hasObservedWritingMode && writingMode != m_observedWritingMode)
            {
                m_scrollX = 0f;
                m_scrollY = 0f;
                m_horizontalScrollState = default;
                m_visualScrollOffset = Vector2.zero;
            }
            m_observedWritingMode = writingMode;
            m_hasObservedWritingMode = true;
            intrinsicMeasurement?.Dispose();
            intrinsicMeasurement = null;
            horizontalFallbackMeasurement?.Dispose();
            horizontalFallbackMeasurement = null;
            verticalMeasurement?.Dispose();
            verticalMeasurement = null;
            lastSizing = null;
            m_hasObservedVerticalMarginContext = false;
            RenderTarget.MarkPropertiesChanged();
            NotifyLayoutChanged();
            GetComponent<TextBoxLayoutElement>()?.NotifyProducerConfigurationChanged();
            CaptureObservedMargin();
        }

        private void ObserveMarginChanges()
        {
            if (m_margin == null)
            {
                m_hasObservedMargin = false;
                return;
            }

            if (!m_hasObservedMargin)
            {
                CaptureObservedMargin();
                return;
            }

            if (MarginValuesEqual(m_margin.Left, m_observedMarginLeft) &&
                MarginValuesEqual(m_margin.Top, m_observedMarginTop) &&
                MarginValuesEqual(m_margin.Right, m_observedMarginRight) &&
                MarginValuesEqual(m_margin.Bottom, m_observedMarginBottom))
            {
                return;
            }

            MarkPropertiesChanged();
        }

        private void ObserveVerticalLayoutChanges()
        {
            if (!IsVerticalRequested())
            {
                m_hasObservedVerticalMarginContext = false;
                return;
            }

            var next = ResolveVerticalMarginContext(m_textStyle.size, RectTransformComponent());
            if (m_hasObservedVerticalMarginContext &&
                VerticalMarginContextsEqual(next, m_observedVerticalMarginContext))
            {
                return;
            }

            var changed = m_hasObservedVerticalMarginContext;
            m_observedVerticalMarginContext = next;
            m_hasObservedVerticalMarginContext = true;
            if (changed)
            {
                MarkPropertiesChanged();
            }
        }

        private void CaptureObservedRectSize()
        {
            var rectTransform = RectTransformComponent();
            if (rectTransform == null)
            {
                m_hasObservedRectSize = false;
                return;
            }

            m_observedRectSize = rectTransform.rect.size;
            m_hasObservedRectSize = true;
        }

        private static bool VerticalMarginContextsEqual(MarginResolveContext left,
            MarginResolveContext right)
        {
            return Mathf.Approximately(left.ContainerWidth, right.ContainerWidth) &&
                   Mathf.Approximately(left.ViewportWidth, right.ViewportWidth) &&
                   Mathf.Approximately(left.ViewportHeight, right.ViewportHeight) &&
                   Mathf.Approximately(left.FontSize, right.FontSize) &&
                   left.HasContainerWidth == right.HasContainerWidth &&
                   left.HasViewport == right.HasViewport;
        }

        private void CaptureObservedMargin()
        {
            if (m_margin == null)
            {
                m_hasObservedMargin = false;
                return;
            }

            m_observedMarginLeft = m_margin.Left;
            m_observedMarginTop = m_margin.Top;
            m_observedMarginRight = m_margin.Right;
            m_observedMarginBottom = m_margin.Bottom;
            m_hasObservedMargin = true;
        }

        private static bool MarginValuesEqual(MarginValue left, MarginValue right)
        {
            return BitConverter.SingleToInt32Bits(left.Value) ==
                   BitConverter.SingleToInt32Bits(right.Value) &&
                   left.Unit == right.Unit &&
                   left.Auto == right.Auto;
        }

        private void MarkContentChanged()
        {
#if UNITY_EDITOR
            m_editorSkippedRenderRetries = 0;
#endif
            intrinsicMeasurement?.Dispose();
            intrinsicMeasurement = null;
            horizontalFallbackMeasurement?.Dispose();
            horizontalFallbackMeasurement = null;
            verticalMeasurement?.Dispose();
            verticalMeasurement = null;
            lastSizing = null;
            m_hasObservedVerticalMarginContext = false;
            RenderTarget.MarkContentChanged();
            NotifyLayoutChanged();
        }

        private void SetScrollX(float value)
        {
            var nextScrollX = FloatUtil.IsFinite(value) ? Mathf.Max(0f, value) : 0f;
            m_horizontalScrollState = m_horizontalScrollState.WithUserRequest(nextScrollX);
            if (TextBoxHorizontalScrollState.OffsetsEqual(m_scrollX, nextScrollX))
            {
                return;
            }

            m_scrollX = nextScrollX;
#if UNITY_EDITOR
            m_editorSkippedRenderRetries = 0;
#endif
            RenderTarget.MarkPaintChanged();
        }

        internal TextBoxHorizontalScrollState horizontalScrollState => m_horizontalScrollState;

        internal void ApplyHorizontalScrollState(TextBoxHorizontalScrollState state)
        {
            m_horizontalScrollState = state;
            m_scrollX = state.ScrollX;
        }

        private void SetScrollY(float value)
        {
            var nextScrollY = FloatUtil.IsFinite(value) ? Mathf.Max(0f, value) : 0f;
            if (Mathf.Approximately(m_scrollY, nextScrollY))
            {
                return;
            }

            m_scrollY = nextScrollY;
#if UNITY_EDITOR
            m_editorSkippedRenderRetries = 0;
#endif
            RenderTarget.MarkPaintChanged();
        }

        private void NotifyLayoutChanged()
        {
            LayoutChanged?.Invoke();
        }

        internal TextBoxRenderViewport CurrentViewport(Vector2Int layoutSizePixels)
        {
            if (!m_flowMode)
            {
                return TextBoxRenderViewport.Fixed(layoutSizePixels,
                    m_horizontalScrollState,
                    new Vector2(m_scrollX, m_scrollY),
                    m_visualScrollOffset);
            }

            if (!m_hasVisibleRange)
            {
                DebugFlow($"current viewport invisible: no visible range layout={layoutSizePixels.x}x{layoutSizePixels.y}");
                return TextBoxRenderViewport.Invisible(layoutSizePixels, m_horizontalScrollState);
            }

            if (!TextBoxFlowVisibleRange.TryNormalize(m_visibleStartY,
                    m_visibleEndY,
                    layoutSizePixels.y,
                    out var visibleStartY,
                    out var visibleEndY))
            {
                DebugFlow("current viewport invisible: normalized empty " +
                          $"stored=[{m_visibleStartY:F3},{m_visibleEndY:F3}] " +
                          $"layout={layoutSizePixels.x}x{layoutSizePixels.y}");
                return TextBoxRenderViewport.Invisible(layoutSizePixels, m_horizontalScrollState);
            }

            var visibleHeight = visibleEndY - visibleStartY;
            var visibleSizePixels = new Vector2Int(layoutSizePixels.x,
                Mathf.Max(1, Mathf.RoundToInt(visibleHeight)));
            var capacityHeight = TextBoxFlowVisibleRange.ResolveCapacityHeight(m_visibleCapacityHeight,
                visibleHeight,
                layoutSizePixels.y);
            var outputSizePixels = new Vector2Int(layoutSizePixels.x, capacityHeight);
            DebugFlow("current viewport flow slice " +
                      $"stored=[{m_visibleStartY:F3},{m_visibleEndY:F3}] " +
                      $"normalized=[{visibleStartY:F3},{visibleEndY:F3}] " +
                      $"visibleHeight={visibleHeight:F3} layout={layoutSizePixels.x}x{layoutSizePixels.y} " +
                      $"visibleOutput={visibleSizePixels.x}x{visibleSizePixels.y} " +
                      $"output={outputSizePixels.x}x{outputSizePixels.y}");
            return TextBoxRenderViewport.FlowSlice(layoutSizePixels,
                outputSizePixels,
                visibleSizePixels,
                m_horizontalScrollState,
                visibleStartY);
        }

        internal TextBoxRenderViewport CurrentViewportWithRasterization(Vector2Int layoutSizePixels)
        {
            var viewport = CurrentViewport(layoutSizePixels);
            if (!m_screenSpaceRasterization)
            {
                return viewport;
            }

            var rectTransform = RectTransformComponent();
            if (rectTransform == null || !TryCaptureRasterizationContext(rectTransform, out var context))
            {
                rasterContextValid = false;
                return viewport.WithScreenSpaceRasterization(0f);
            }

            if (rasterContextValid && context.Matches(rasterContextCache))
            {
                return viewport.WithScreenSpaceRasterization(rasterDesiredScaleCache);
            }

            ScreenSpaceRasterMeasurement measurement;
            var profiling = TextBoxPipelineProfiler.Enabled;
            using (profiling ? TextBoxPipelineProfiler.RasterMeasureMarker.Auto() : default)
            {
                if (!ScreenSpaceRasterMetrics.TryMeasure(rectTransform,
                        context.RootCanvas,
                        worldCorners,
                        out measurement))
                {
                    rasterContextValid = false;
                    return viewport.WithScreenSpaceRasterization(0f);
                }
            }

            rasterContextCache = context;
            rasterDesiredScaleCache = measurement.DesiredScale;
            rasterContextValid = true;
            return viewport.WithScreenSpaceRasterization(measurement.DesiredScale);
        }

        private bool RasterizationContextChanged()
        {
            if (!m_screenSpaceRasterization)
            {
                return false;
            }

            var rectTransform = RectTransformComponent();
            if (rectTransform == null || !TryCaptureRasterizationContext(rectTransform, out var context))
            {
                return true;
            }

            return !rasterContextValid || !context.Matches(rasterContextCache);
        }

        private bool TryCaptureRasterizationContext(RectTransform rectTransform,
            out ScreenSpaceRasterMetrics.RasterizationContext context)
        {
            var canvas = CanvasComponent();
            return ScreenSpaceRasterMetrics.TryCapture(rectTransform, canvas, out context);
        }

        private Canvas? CanvasComponent()
        {
            if (canvasCache != null)
            {
                return canvasCache;
            }

            var frame = Time.frameCount;
            if (canvasLookupAttempted && frame < canvasLookupRetryFrame)
            {
                return null;
            }

            canvasLookupAttempted = true;
            canvasLookupRetryFrame = frame + 30;
            canvasCache = GetComponentInParent<Canvas>();
            return canvasCache;
        }

        private void ResetRasterizationContext()
        {
            canvasCache = null;
            canvasLookupAttempted = false;
            canvasLookupRetryFrame = 0;
            rasterContextValid = false;
        }

        private void MarkPaintChanged()
        {
#if UNITY_EDITOR
            m_editorSkippedRenderRetries = 0;
#endif
            RenderTarget.MarkPaintChanged();
        }

        [System.Diagnostics.Conditional("MILESTRO_FLOW_TEXTBOX_DEBUG")]
        private void DebugFlow(string message)
        {
#if MILESTRO_FLOW_TEXTBOX_DEBUG
            if (!m_flowDiagnosticsEnabled)
            {
                return;
            }

            Debug.Log($"[Milestro FlowTextBox][Producer:{name}] {message}", this);
#endif
        }

    }
}
