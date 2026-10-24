using System;
using Milestro.Model;
using Milestro.Skia;
using Milestro.Util;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.UI;
using Canvas = UnityEngine.Canvas;

namespace Milestro.Components.Internal
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Milestro/Internal/Slim Text Render Texture Producer")]
    public class SlimTextRenderTextureProducer : RenderTextureProducer, ILayoutElement
    {
        private const string DefaultFontFamily = "system-ui";

        [SerializeField] private string m_text = "";
        [SerializeField] private string m_fontFamily = DefaultFontFamily;
        [SerializeField, Range(0, 1000)] private int m_fontWeight = FontWeight.Normal;
        [SerializeField] private float m_fontSize = 24f;
        [SerializeField] private Color m_color = Color.white;
        [SerializeField] private RectOffset m_rectOffset = new RectOffset();
        [SerializeField] private SlimTextHorizontalAlign m_horizontalAlign = SlimTextHorizontalAlign.Left;
        [SerializeField] private SlimTextVerticalAlign m_verticalAlign = SlimTextVerticalAlign.Top;
        [SerializeField] private bool m_fallbackToSystemFont = true;
        [SerializeField] private TextAutoFontSizeSettings m_autoFontSize = TextAutoFontSizeSettings.Default;
        [Tooltip("Fixed uses the assigned width. SlimText is always single-line, so MaxContent and FitContent both request " +
                 "the natural text width including margins. Auto Font Size can shrink the text against fixed bounds or a FitContent parent constraint.")]
        [SerializeField] private TextWidthMode m_widthMode;
        [Tooltip("Fixed uses the assigned height. Auto requests the complete line height including margins.")]
        [SerializeField] private TextHeightMode m_heightMode;
        [SerializeField] private int m_layoutPriority = 1;
        [SerializeField, HideInInspector] private bool m_screenSpaceRasterization = true;

        [NonSerialized] private RectTransform? rectTransformCache;
        [NonSerialized] private Vector2Int cachedSize = Vector2Int.one;
        [NonSerialized] private bool sizeDirty = true;
        [NonSerialized] private readonly Vector3[] worldCorners = new Vector3[4];
        [NonSerialized] private SlimTextRenderTarget? renderTarget;
        [NonSerialized] private SlimTextIntrinsicMeasurement? intrinsicMeasurement;
        [NonSerialized] private TextSizingResult fixedSizingCache;
        [NonSerialized] private bool fixedSizingValid;
        [NonSerialized] private bool rebuildRequested = true;
        [NonSerialized] private Canvas? canvasCache;
        [NonSerialized] private bool canvasLookupAttempted;
        [NonSerialized] private int canvasLookupRetryFrame;
        [NonSerialized] private RasterizationContext rasterContextCache;
        [NonSerialized] private float rasterDesiredScaleCache = 1f;
        [NonSerialized] private bool rasterContextValid;
        [NonSerialized] private ColorSpace? m_colorSpaceOverride;
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
        public override long VisualVersion => renderTarget?.VisualVersion ?? 0;

        public float effectiveFontSize => ResolveSizing().EffectiveFontSize;

        private bool HasFixedLayout => m_widthMode == TextWidthMode.Fixed &&
                                       m_heightMode == TextHeightMode.Fixed &&
                                       !m_autoFontSize.enabled;

        public TextWidthMode widthMode
        {
            get => m_widthMode;
            set
            {
                var nextValue = Enum.IsDefined(typeof(TextWidthMode), value)
                    ? value
                    : TextWidthMode.Fixed;
                if (m_widthMode == nextValue) return;
                m_widthMode = nextValue;
                MarkPropertiesChanged();
            }
        }

        public TextHeightMode heightMode
        {
            get => m_heightMode;
            set
            {
                var nextValue = Enum.IsDefined(typeof(TextHeightMode), value)
                    ? value
                    : TextHeightMode.Fixed;
                if (m_heightMode == nextValue) return;
                m_heightMode = nextValue;
                MarkPropertiesChanged();
            }
        }

        public int layoutPriority
        {
            get => m_layoutPriority;
            set
            {
                if (m_layoutPriority == value)
                {
                    return;
                }

                m_layoutPriority = value;
                SetLayoutDirty();
            }
        }

        public float minWidth => m_widthMode == TextWidthMode.Fixed ? -1f : ResolveSizing().MinWidth;
        public float preferredWidth => m_widthMode == TextWidthMode.Fixed ? -1f : ResolveSizing().PreferredWidth;
        public float flexibleWidth => -1f;
        public float minHeight => -1f;
        public float preferredHeight => m_heightMode == TextHeightMode.Auto ? ResolveSizing().PreferredHeight : -1f;
        public float flexibleHeight => -1f;

        public void CalculateLayoutInputHorizontal()
        {
            rebuildRequested = true;
            ResolveSizing();
        }

        public void CalculateLayoutInputVertical()
        {
            rebuildRequested = true;
            ResolveSizing();
        }

        private void OnDidApplyAnimationProperties()
        {
            NormalizeSerializedValues();
            MarkPropertiesChanged();
        }

        public TextSizingResult ResolveSizing()
        {
            if (HasFixedLayout)
            {
                if (!fixedSizingValid)
                {
                    var fixedRectTransform = RectTransformComponent();
                    var fixedSizeValue = fixedRectTransform != null ? fixedRectTransform.rect.size : Vector2.one;
                    fixedSizingCache = ResolveSizing(fixedSizeValue, Vector2.zero);
                    fixedSizingValid = true;
                }

                return fixedSizingCache;
            }

            var rectTransform = RectTransformComponent();
            var fixedSize = rectTransform != null ? rectTransform.rect.size : Vector2.one;
            return ResolveSizing(fixedSize);
        }

        internal TextSizingResult ResolveSizing(Vector2 fixedSize)
        {
            if (HasFixedLayout)
            {
                return ResolveSizing(fixedSize, Vector2.zero);
            }

            var rectTransform = RectTransformComponent();
            var available = rectTransform != null
                ? TextSizingBounds.ExternalAvailableSize(rectTransform)
                : Vector2.zero;
            return ResolveSizing(fixedSize, available);
        }

        internal TextSizingResult ResolveSizing(Vector2 fixedSize, Vector2 available)
        {
            EnsureRectOffset();
            var rectTransform = RectTransformComponent();
            var hasParentBounds = rectTransform != null && rectTransform.parent is RectTransform;
            var availableWidth = hasParentBounds ? available.x : float.PositiveInfinity;
            var request = new TextSizingRequest(m_widthMode,
                m_heightMode,
                fixedSize.x,
                fixedSize.y,
                availableWidth,
                false,
                m_fontSize,
                m_autoFontSize.enabled,
                m_autoFontSize.fontSizeMin,
                m_autoFontSize.fontSizeMax,
                widthCap: m_widthMode == TextWidthMode.FitContent && hasParentBounds
                    ? available.x
                    : float.PositiveInfinity);
            return TextSizingResolver.Resolve(Measurement(), request, _ => new ResolvedMargin(
                m_rectOffset.left,
                m_rectOffset.top,
                m_rectOffset.right,
                m_rectOffset.bottom));
        }

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

        public string text
        {
            get => m_text;
            set
            {
                SetManagedStringText(value);
            }
        }

        public string fontFamily
        {
            get => m_fontFamily;
            set
            {
                var nextValue = value ?? "";
                if (m_fontFamily == nextValue)
                {
                    return;
                }

                m_fontFamily = nextValue;
                MarkPropertiesChanged();
            }
        }

        public int fontWeight
        {
            get => m_fontWeight;
            set
            {
                var nextValue = NormalizeFontWeight(value);
                if (m_fontWeight == nextValue)
                {
                    return;
                }

                m_fontWeight = nextValue;
                MarkPropertiesChanged();
            }
        }

        public float fontSize
        {
            get => m_fontSize;
            set
            {
                var nextValue = NormalizeFontSize(value);
                if (Mathf.Approximately(m_fontSize, nextValue))
                {
                    return;
                }

                m_fontSize = nextValue;
                MarkPropertiesChanged();
            }
        }

        public Color color
        {
            get => m_color;
            set
            {
                if (m_color == value)
                {
                    return;
                }

                m_color = value;
                MarkPaintChanged();
            }
        }

        public RectOffset rectOffset
        {
            get
            {
                EnsureRectOffset();
                return m_rectOffset;
            }
            set
            {
                var nextValue = CloneNormalizedRectOffset(value);
                if (RectOffsetsEqual(m_rectOffset, nextValue))
                {
                    return;
                }

                m_rectOffset = nextValue;
                MarkPropertiesChanged();
            }
        }

        public SlimTextHorizontalAlign horizontalAlign
        {
            get => m_horizontalAlign;
            set
            {
                if (m_horizontalAlign == value)
                {
                    return;
                }

                m_horizontalAlign = value;
                MarkPaintChanged();
            }
        }

        public SlimTextVerticalAlign verticalAlign
        {
            get => m_verticalAlign;
            set
            {
                if (m_verticalAlign == value)
                {
                    return;
                }

                m_verticalAlign = value;
                MarkPaintChanged();
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

        public bool fallbackToSystemFont
        {
            get => m_fallbackToSystemFont;
            set
            {
                if (m_fallbackToSystemFont == value)
                {
                    return;
                }

                m_fallbackToSystemFont = value;
                MarkPropertiesChanged();
            }
        }

        public bool srgb
        {
            get => SurfaceColorSpace() == ColorSpace.Linear;
            set
            {
                m_colorSpaceOverride = value ? ColorSpace.Linear : ColorSpace.Gamma;
                MarkPaintChanged();
            }
        }

        protected virtual void OnEnable()
        {
            sizeDirty = true;
            fixedSizingValid = false;
            rebuildRequested = true;
            rasterContextValid = false;
            canvasCache = null;
            canvasLookupAttempted = false;
            rectTransformCache = GetComponent<RectTransform>();
            if (GetComponent<Milestro.Components.WorldSpaceSlimText>() != null)
            {
                m_screenSpaceRasterization = false;
            }
            RebuildResources();
        }

        protected virtual void OnDisable()
        {
            // Keep the render target alive across disable/enable cycles; OnDestroy owns final release.
        }

        protected virtual void OnDestroy()
        {
            DisposeRenderTarget();
        }

        private void Update()
        {
            if (!rebuildRequested &&
                !RenderTarget.HasPendingWork &&
                !RasterizationContextChanged())
            {
                return;
            }

            RebuildResources();
        }

        private void OnTransformParentChanged()
        {
            fixedSizingValid = false;
            rebuildRequested = true;
            rasterContextValid = false;
            canvasCache = null;
            canvasLookupAttempted = false;
#if UNITY_EDITOR
            if (isActiveAndEnabled)
            {
                QueueEditorRebuild();
            }
#endif
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            NormalizeSerializedValues();
            MarkPropertiesChanged();
            if (isActiveAndEnabled)
            {
                QueueEditorRebuild();
            }
        }
#endif

        private void OnRectTransformDimensionsChange()
        {
            sizeDirty = true;
            fixedSizingValid = false;
            rebuildRequested = true;
            rasterContextValid = false;
            if (!isActiveAndEnabled)
            {
                return;
            }

            // This callback is commonly caused by ContentSizeFitter applying the current
            // preferred size. Invalidate rendering, but do not dirty layout from the
            // resulting RectTransform change or the fitter can request another pass.
            renderTarget?.MarkPropertiesChanged();
#if UNITY_EDITOR
            QueueEditorRebuild();
#endif
        }

        public void RebuildOutput(bool forceStyle)
        {
            if (forceStyle)
            {
                MarkPropertiesChanged();
            }

            RebuildResources();
        }

        public void SetManagedStringText(string? value)
        {
            var nextValue = value ?? "";
            var changed = m_text != nextValue;
            m_text = nextValue;
            if (RenderTarget.UseManagedStringText() || changed)
            {
                MarkTextChanged();
            }
        }

        internal void SyncTextWithoutModeSwitch(string? value)
        {
            var nextValue = value ?? "";
            if (m_text == nextValue)
            {
                return;
            }

            m_text = nextValue;
            MarkTextChanged();
        }

        public void PrepareTextUtf8NoAlloc(int capacity)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            NormalizeSerializedValues();
            var target = RenderTarget;
            target.EnsureNoAllocCapacity(capacity);
            var size = CurrentSize();
            ResolveRasterization(out var useScreenSpaceRasterization, out var desiredRasterScale);
            var rebuilt = target.Rebuild(size,
                SurfaceColorSpace(),
                CurrentSettings(),
                useScreenSpaceRasterization,
                desiredRasterScale,
                this);
            if (!rebuilt)
            {
#if UNITY_EDITOR
                QueueEditorRebuild();
#endif
            }
        }

        public void SetTextUtf8NoAlloc(byte[] buffer, int offset, int length)
        {
            NormalizeSerializedValues();
            var size = CurrentSize();
            ResolveRasterization(out var useScreenSpaceRasterization, out var desiredRasterScale);
            var rebuilt = RenderTarget.SetTextUtf8NoAlloc(size,
                SurfaceColorSpace(),
                CurrentSettings(),
                useScreenSpaceRasterization,
                desiredRasterScale,
                this,
                buffer,
                offset,
                length);
            if (!rebuilt)
            {
#if UNITY_EDITOR
                QueueEditorRebuild();
#endif
            }
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

            RebuildResources();
        }
#endif

        private void RebuildResources()
        {
            rebuildRequested = false;
            var profiling = SlimTextPipelineProfiler.Enabled;
            using var profilerScope = profiling
                ? SlimTextPipelineProfiler.ProducerUpdateMarker.Auto()
                : default;
            var phaseStart = profiling ? SlimTextPipelineProfiler.Timestamp : 0;
            if (profiling)
            {
                SlimTextPipelineProfiler.ProducerRebuildCalls++;
            }

            NormalizeSerializedValues();
            var size = CurrentSize();
            var rasterizationResolved = ResolveRasterization(out var useScreenSpaceRasterization,
                out var desiredRasterScale);
            var rebuilt = RenderTarget.Rebuild(size,
                SurfaceColorSpace(),
                CurrentSettings(),
                useScreenSpaceRasterization,
                desiredRasterScale,
                this);
            if (!rebuilt || useScreenSpaceRasterization && !rasterizationResolved)
            {
                rebuildRequested = true;
#if UNITY_EDITOR
                QueueEditorRebuild();
#endif
            }

            if (profiling)
            {
                SlimTextPipelineProfiler.ProducerRebuildMs += SlimTextPipelineProfiler.ElapsedMs(phaseStart);
            }
        }

        private Vector2Int CurrentSize()
        {
            if (!sizeDirty)
            {
                return cachedSize;
            }

            var rectTransform = RectTransformComponent();
            if (rectTransform == null)
            {
                return Vector2Int.one;
            }

            var rect = rectTransform.rect;
            cachedSize = new Vector2Int(Mathf.Max(1, Mathf.CeilToInt(rect.width)),
                Mathf.Max(1, Mathf.CeilToInt(rect.height)));
            sizeDirty = false;
            return cachedSize;
        }

        private bool ResolveRasterization(out bool useScreenSpaceRasterization, out float desiredRasterScale)
        {
            useScreenSpaceRasterization = m_screenSpaceRasterization;
            desiredRasterScale = 1f;
            if (!useScreenSpaceRasterization)
            {
                return true;
            }

            var rectTransform = RectTransformComponent();
            if (rectTransform == null || !TryCaptureRasterizationContext(rectTransform, out var context))
            {
                desiredRasterScale = 0f;
                rasterContextValid = false;
                return false;
            }

            if (rasterContextValid && context.Matches(rasterContextCache))
            {
                desiredRasterScale = rasterDesiredScaleCache;
                return true;
            }

            if (!ScreenSpaceRasterMetrics.TryMeasure(rectTransform,
                    context.RootCanvas,
                    worldCorners,
                    out var measurement))
            {
                desiredRasterScale = 0f;
                rasterContextValid = false;
                return false;
            }

            rasterContextCache = context;
            rasterDesiredScaleCache = measurement.DesiredScale;
            rasterContextValid = true;
            desiredRasterScale = measurement.DesiredScale;
            return true;
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
            out RasterizationContext context)
        {
            context = default;
            var canvas = CanvasComponent();
            if (canvas == null)
            {
                return false;
            }

            var rootCanvas = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
            var eventCamera = ScreenSpaceRasterMetrics.ResolveEventCamera(rootCanvas);
            var targetDisplay = ScreenSpaceRasterMetrics.ResolveTargetDisplay(rootCanvas, eventCamera);
            if (targetDisplay < 0)
            {
                return false;
            }

            context = new RasterizationContext(rootCanvas,
                eventCamera,
                rootCanvas.renderMode,
                targetDisplay,
                rectTransform.localToWorldMatrix,
                rootCanvas.transform.localToWorldMatrix,
                eventCamera != null ? eventCamera.transform.localToWorldMatrix : Matrix4x4.identity,
                eventCamera != null ? eventCamera.projectionMatrix : Matrix4x4.identity,
                rootCanvas.pixelRect,
                eventCamera != null ? eventCamera.pixelRect : default,
                rootCanvas.scaleFactor,
                Screen.width,
                Screen.height);
            return true;
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

        private ColorSpace SurfaceColorSpace()
        {
            return m_colorSpaceOverride ?? UnitySkiaRenderTextureDescriptor.DefaultColorSpace;
        }

        private SlimTextRenderTargetSettings CurrentSettings()
        {
            return new SlimTextRenderTargetSettings(m_text,
                m_fontFamily,
                m_fontWeight,
                m_fontSize,
                m_color,
                m_rectOffset,
                m_horizontalAlign,
                m_verticalAlign,
                m_fallbackToSystemFont,
                m_autoFontSize.enabled,
                m_autoFontSize.fontSizeMin,
                m_autoFontSize.fontSizeMax, ResolveSizing());
        }

        private SlimTextIntrinsicMeasurement Measurement()
        {
            if (intrinsicMeasurement == null)
            {
                intrinsicMeasurement = new SlimTextIntrinsicMeasurement(m_text,
                    m_fontFamily,
                    m_fontWeight,
                    m_fallbackToSystemFont);
            }

            return intrinsicMeasurement;
        }

        private RectTransform? RectTransformComponent()
        {
            if (rectTransformCache == null)
            {
                rectTransformCache = GetComponent<RectTransform>();
            }

            return rectTransformCache;
        }

        private SlimTextRenderTarget RenderTarget
        {
            get
            {
                if (renderTarget == null)
                {
                    renderTarget = new SlimTextRenderTarget();
                    renderTarget.RenderEventCompleted += OnRenderEventCompleted;
                }

                return renderTarget;
            }
        }

        private void DisposeRenderTarget()
        {
            intrinsicMeasurement?.Dispose();
            intrinsicMeasurement = null;
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
            fixedSizingValid = false;
            rebuildRequested = true;
#if UNITY_EDITOR
            m_editorSkippedRenderRetries = 0;
#endif
            intrinsicMeasurement?.Dispose();
            intrinsicMeasurement = null;
            SetLayoutDirty();
            RenderTarget.MarkPropertiesChanged();
#if UNITY_EDITOR
            if (isActiveAndEnabled)
            {
                QueueEditorRebuild();
            }
#endif
        }

        private void MarkTextChanged()
        {
            rebuildRequested = true;
#if UNITY_EDITOR
            m_editorSkippedRenderRetries = 0;
#endif
            intrinsicMeasurement?.Dispose();
            intrinsicMeasurement = null;
            if (m_widthMode != TextWidthMode.Fixed ||
                m_heightMode != TextHeightMode.Fixed ||
                m_autoFontSize.enabled)
            {
                SetLayoutDirty();
            }
            RenderTarget.MarkPropertiesChanged();
#if UNITY_EDITOR
            if (isActiveAndEnabled)
            {
                QueueEditorRebuild();
            }
#endif
        }

        private void MarkPaintChanged()
        {
            rebuildRequested = true;
#if UNITY_EDITOR
            m_editorSkippedRenderRetries = 0;
#endif
            RenderTarget.MarkPaintChanged();
#if UNITY_EDITOR
            if (isActiveAndEnabled)
            {
                QueueEditorRebuild();
            }
#endif
        }

        private void NormalizeSerializedValues()
        {
            if (m_text == null)
            {
                m_text = "";
            }

            if (m_fontFamily == null)
            {
                m_fontFamily = "";
            }

            m_fontWeight = NormalizeFontWeight(m_fontWeight);
            m_fontSize = NormalizeFontSize(m_fontSize);
            m_autoFontSize.Normalize();
            if ((uint)m_widthMode > (uint)TextWidthMode.FitContent)
            {
                m_widthMode = TextWidthMode.Fixed;
            }
            if ((uint)m_heightMode > (uint)TextHeightMode.Auto)
            {
                m_heightMode = TextHeightMode.Fixed;
            }
            NormalizeRectOffsetInPlace();
        }

        private void SetLayoutDirty()
        {
            var rectTransform = RectTransformComponent();
            if (isActiveAndEnabled && rectTransform != null)
            {
                LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
            }
        }

        private static int NormalizeFontWeight(int weight)
        {
            return Mathf.Clamp(weight, FontWeight.Invisible, FontWeight.ExtraBlack);
        }

        private static float NormalizeFontSize(float fontSize)
        {
            return FloatUtil.IsFinite(fontSize) ? Mathf.Max(1f, fontSize) : 1f;
        }

        private void EnsureRectOffset()
        {
            if (m_rectOffset == null)
            {
                m_rectOffset = new RectOffset();
            }
        }

        private void NormalizeRectOffsetInPlace()
        {
            EnsureRectOffset();
            m_rectOffset.left = Mathf.Max(0, m_rectOffset.left);
            m_rectOffset.right = Mathf.Max(0, m_rectOffset.right);
            m_rectOffset.top = Mathf.Max(0, m_rectOffset.top);
            m_rectOffset.bottom = Mathf.Max(0, m_rectOffset.bottom);
        }

        private static RectOffset CloneNormalizedRectOffset(RectOffset rectOffset)
        {
            if (rectOffset == null)
            {
                return new RectOffset();
            }

            return new RectOffset(Mathf.Max(0, rectOffset.left),
                Mathf.Max(0, rectOffset.right),
                Mathf.Max(0, rectOffset.top),
                Mathf.Max(0, rectOffset.bottom));
        }

        private static bool RectOffsetsEqual(RectOffset a, RectOffset b)
        {
            return a == b ||
                   a != null &&
                   b != null &&
                   a.left == b.left &&
                   a.right == b.right &&
                   a.top == b.top &&
                   a.bottom == b.bottom;
        }

        private readonly struct RasterizationContext
        {
            internal RasterizationContext(Canvas rootCanvas,
                Camera? eventCamera,
                RenderMode renderMode,
                int targetDisplay,
                Matrix4x4 rectToWorld,
                Matrix4x4 canvasToWorld,
                Matrix4x4 cameraToWorld,
                Matrix4x4 cameraProjection,
                Rect canvasPixelRect,
                Rect cameraPixelRect,
                float canvasScaleFactor,
                int screenWidth,
                int screenHeight)
            {
                RootCanvas = rootCanvas;
                EventCamera = eventCamera;
                RenderMode = renderMode;
                TargetDisplay = targetDisplay;
                RectToWorld = rectToWorld;
                CanvasToWorld = canvasToWorld;
                CameraToWorld = cameraToWorld;
                CameraProjection = cameraProjection;
                CanvasPixelRect = canvasPixelRect;
                CameraPixelRect = cameraPixelRect;
                CanvasScaleFactor = canvasScaleFactor;
                ScreenWidth = screenWidth;
                ScreenHeight = screenHeight;
            }

            internal Canvas RootCanvas { get; }
            internal Camera? EventCamera { get; }
            internal RenderMode RenderMode { get; }
            internal int TargetDisplay { get; }
            internal Matrix4x4 RectToWorld { get; }
            internal Matrix4x4 CanvasToWorld { get; }
            internal Matrix4x4 CameraToWorld { get; }
            internal Matrix4x4 CameraProjection { get; }
            internal Rect CanvasPixelRect { get; }
            internal Rect CameraPixelRect { get; }
            internal float CanvasScaleFactor { get; }
            internal int ScreenWidth { get; }
            internal int ScreenHeight { get; }

            internal bool Matches(in RasterizationContext other)
            {
                return RootCanvas == other.RootCanvas &&
                       EventCamera == other.EventCamera &&
                       RenderMode == other.RenderMode &&
                       TargetDisplay == other.TargetDisplay &&
                       RectToWorld == other.RectToWorld &&
                       CanvasToWorld == other.CanvasToWorld &&
                       CameraToWorld == other.CameraToWorld &&
                       CameraProjection == other.CameraProjection &&
                       CanvasPixelRect == other.CanvasPixelRect &&
                       CameraPixelRect == other.CameraPixelRect &&
                       CanvasScaleFactor == other.CanvasScaleFactor &&
                       ScreenWidth == other.ScreenWidth &&
                       ScreenHeight == other.ScreenHeight;
            }
        }
    }
}
