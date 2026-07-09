using System;
using System.Collections.Generic;
using Milestro.Model;
using Milestro.Skia;
using Milestro.Util;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Milestro.Components.Internal
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Milestro/Internal/Text Box Render Texture Producer")]
    public class TextBoxRenderTextureProducer : RenderTextureProducer
    {
        [TextArea(3, 10)]
        [SerializeField]
        private string m_content = "";

        [SerializeField]
        private Margin m_margin = new Margin();

        [SerializeField]
        private List<string> m_fontFamilies = new List<string>() { "Source Han Sans VF" };

        [SerializeField]
        private TextAlign m_textAlign = TextAlign.Start;

        [SerializeField]
        private TextDirection m_textDirection = TextDirection.Ltr;

        [SerializeField]
        private TextBoxWrapMode m_wrapMode = TextBoxWrapMode.Wrap;

        [SerializeField]
        private bool m_singleLine;

        [SerializeField]
        private TextOverflow m_textOverflow = TextOverflow.Clip;

        [SerializeField]
        private string m_ellipsisString = "\u2026";

        [SerializeField]
        private float m_size = 36;

        [SerializeField]
        [Range(0, 1000)]
        private int m_weight = FontWeight.Normal;

        [SerializeField]
        private Color m_textColor = Color.white;

        [SerializeField]
        private string m_locale = "zh-Hans";

        [NonSerialized] private RectTransform? rectTransformCache;
        [NonSerialized] private TextBoxRenderTarget? renderTarget;
        [NonSerialized] private ColorSpace? m_colorSpaceOverride;
        [NonSerialized] private float m_scrollX;
        [NonSerialized] private float m_scrollY;
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

        internal event Action? LayoutChanged;

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
                m_content = value ?? "";
                MarkPropertiesChanged();
            }
        }

        public List<string> fontFamilies
        {
            get => m_fontFamilies;
            set
            {
                m_fontFamilies = value ?? new List<string>();
                MarkPropertiesChanged();
            }
        }

        public TextAlign textAlign
        {
            get => m_textAlign;
            set
            {
                m_textAlign = value;
                MarkPropertiesChanged();
            }
        }

        public TextDirection textDirection
        {
            get => m_textDirection;
            set
            {
                m_textDirection = value;
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

        public bool singleLine
        {
            get => m_singleLine;
            set
            {
                m_singleLine = value;
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

        public string ellipsisString
        {
            get => m_ellipsisString;
            set
            {
                m_ellipsisString = value ?? "";
                MarkPropertiesChanged();
            }
        }

        public float size
        {
            get => m_size;
            set
            {
                m_size = value;
                MarkPropertiesChanged();
            }
        }

        public int weight
        {
            get => m_weight;
            set
            {
                m_weight = value;
                MarkPropertiesChanged();
            }
        }

        public Color textColor
        {
            get => m_textColor;
            set
            {
                m_textColor = value;
                MarkPropertiesChanged();
            }
        }

        public string locale
        {
            get => m_locale;
            set
            {
                m_locale = value ?? "";
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

        public void ScrollByX(float delta)
        {
            if (Mathf.Approximately(delta, 0f))
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
            RebuildResources(forceText: true);
        }

        protected virtual void OnDisable()
        {
            DisposeRenderTarget();
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
            m_margin.Normalize();
            m_scrollX = FloatUtil.IsFinite(m_scrollX) ? Mathf.Max(0f, m_scrollX) : 0f;
            m_scrollY = FloatUtil.IsFinite(m_scrollY) ? Mathf.Max(0f, m_scrollY) : 0f;
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

            MarkPropertiesChanged();
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
            var settings = CurrentSettings();
            var hadPreviousMeasurement = TryBuildLayoutMeasurement(settings, out var previousMeasurement);
            var currentSize = CurrentSize();

            var rebuilt = RenderTarget.Rebuild(CurrentViewport(currentSize),
                SurfaceColorSpace(),
                settings,
                forceText,
                this);
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

            if (!m_flowMode)
            {
                var nextScrollX = RenderTarget.ScrollOffset.x;
                if (!Mathf.Approximately(m_scrollX, nextScrollX))
                {
                    m_scrollX = nextScrollX;
                }

                var nextScrollY = RenderTarget.ScrollOffset.y;
                if (!Mathf.Approximately(m_scrollY, nextScrollY))
                {
                    m_scrollY = nextScrollY;
                }
            }

            var hasNextMeasurement = TryBuildLayoutMeasurement(settings, out var nextMeasurement);
            if (LayoutMeasurementChanged(hadPreviousMeasurement,
                    previousMeasurement,
                    hasNextMeasurement,
                    nextMeasurement))
            {
                NotifyLayoutChanged();
            }
        }

        public void RebuildOutput(bool forceText)
        {
            RebuildResources(forceText);
        }

        internal bool TryGetLayoutMeasurement(out TextBoxLayoutMeasurement measurement)
        {
            return TryBuildLayoutMeasurement(CurrentSettings(), out measurement);
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

        private TextBoxRenderTargetSettings CurrentSettings()
        {
            if (m_margin == null)
            {
                m_margin = new Margin();
            }
            return new TextBoxRenderTargetSettings(m_content,
                m_margin,
                m_fontFamilies,
                m_textAlign,
                m_textDirection,
                m_wrapMode,
                m_singleLine,
                m_textOverflow,
                m_ellipsisString,
                m_size,
                m_weight,
                m_textColor,
                m_locale);
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
            return settings.Margin.Resolve(new MarginResolveContext(containerSizePixels.x,
                containerSizePixels.y,
                settings.Size));
        }

        private TextBoxRenderTarget RenderTarget
        {
            get
            {
                if (renderTarget == null)
                {
                    renderTarget = new TextBoxRenderTarget();
                    renderTarget.RenderEventCompleted += OnRenderEventCompleted;
                }

                return renderTarget;
            }
        }

        private void DisposeRenderTarget()
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
            RenderTarget.MarkPropertiesChanged();
            NotifyLayoutChanged();
        }

        private void SetScrollX(float value)
        {
            var nextScrollX = FloatUtil.IsFinite(value) ? Mathf.Max(0f, value) : 0f;
            if (Mathf.Approximately(m_scrollX, nextScrollX))
            {
                return;
            }

            m_scrollX = nextScrollX;
#if UNITY_EDITOR
            m_editorSkippedRenderRetries = 0;
#endif
            RenderTarget.MarkPaintChanged();
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

        private bool TryBuildLayoutMeasurement(TextBoxRenderTargetSettings settings,
            out TextBoxLayoutMeasurement measurement)
        {
            if (renderTarget == null)
            {
                measurement = default;
                return false;
            }

            var margin = ResolveMargin(settings, CurrentSize());
            var preferredHeight = SanitizePreferredSize(contentHeight + margin.FixedVerticalSize);
            var hasPreferredWidth = ShouldUseContentPreferredWidth(settings);
            var preferredWidth = hasPreferredWidth
                ? SanitizePreferredSize(contentWidth + margin.FixedHorizontalSize)
                : SanitizePreferredSize(viewportWidth + margin.FixedHorizontalSize);

            measurement = new TextBoxLayoutMeasurement(preferredWidth,
                preferredHeight,
                hasPreferredWidth,
                contentWidth,
                contentHeight,
                viewportWidth,
                viewportHeight);
            return true;
        }

        private static bool LayoutMeasurementChanged(bool hadPreviousMeasurement,
            TextBoxLayoutMeasurement previousMeasurement,
            bool hasNextMeasurement,
            TextBoxLayoutMeasurement nextMeasurement)
        {
            if (hadPreviousMeasurement != hasNextMeasurement)
            {
                return true;
            }

            if (!hasNextMeasurement)
            {
                return false;
            }

            return previousMeasurement.HasContentPreferredWidth != nextMeasurement.HasContentPreferredWidth ||
                   !Approximately(previousMeasurement.PreferredWidth, nextMeasurement.PreferredWidth) ||
                   !Approximately(previousMeasurement.PreferredHeight, nextMeasurement.PreferredHeight);
        }

        private static bool Approximately(float a, float b)
        {
            return Mathf.Approximately(a, b);
        }

        private static bool ShouldUseContentPreferredWidth(TextBoxRenderTargetSettings settings)
        {
            if (settings.SingleLine)
            {
                return settings.TextOverflow != TextOverflow.Ellipsis;
            }

            return settings.WrapMode == TextBoxWrapMode.NoWrap;
        }

        private static float SanitizePreferredSize(float value)
        {
            if (!FloatUtil.IsFinite(value) || value < 0f)
            {
                return 0f;
            }

            return value;
        }

        private void NotifyLayoutChanged()
        {
            LayoutChanged?.Invoke();
        }

        private TextBoxRenderViewport CurrentViewport(Vector2Int layoutSizePixels)
        {
            if (!m_flowMode)
            {
                return TextBoxRenderViewport.Fixed(layoutSizePixels, new Vector2(m_scrollX, m_scrollY));
            }

            if (!m_hasVisibleRange)
            {
                DebugFlow($"current viewport invisible: no visible range layout={layoutSizePixels.x}x{layoutSizePixels.y}");
                return TextBoxRenderViewport.Invisible(layoutSizePixels);
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
                return TextBoxRenderViewport.Invisible(layoutSizePixels);
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
            return TextBoxRenderViewport.FlowSlice(layoutSizePixels, outputSizePixels, visibleSizePixels, visibleStartY);
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
