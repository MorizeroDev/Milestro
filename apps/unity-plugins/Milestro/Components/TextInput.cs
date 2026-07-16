using System;
using System.Collections.Generic;
using System.Text;
using Milestro.Components.Internal;
using Milestro.Configuration;
using Milestro.Input;
using Milestro.Model;
using Milestro.Skia;
using Milestro.Skia.TextLayout;
using Milestro.Util;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

namespace Milestro.Components
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("Milestro/Text Input")]
    public class TextInput : RenderTextureGraphic,
        IPointerDownHandler,
        IPointerClickHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IScrollHandler,
        ISelectHandler,
        IDeselectHandler,
        ITextBoxScrollTarget
    {
        private const char ReplacementCharacter = '\ufffd';
        private const float DefaultScrollWheelStepPixels = 48f;
        private const float DefaultKeyboardScrollInterlockTimeoutSeconds = 0.03f;
        private const float DefaultScrollTweenDurationSeconds = 0.14f;

        [TextArea(1, 8)]
        [SerializeField]
        [FormerlySerializedAs("text")]
        private string m_text = "";

        [SerializeField]
        [FormerlySerializedAs("margin")]
        private Margin m_margin = new Margin();

        [SerializeField]
        [FormerlySerializedAs("fontFamilies")]
        private List<string> m_fontFamilies = new List<string>() { "system-ui" };

        [SerializeField]
        [FormerlySerializedAs("size")]
        private float m_size = 36;

        [SerializeField]
        [FormerlySerializedAs("textColor")]
        private Color m_textColor = Color.white;

        [SerializeField]
        [FormerlySerializedAs("caretColor")]
        private Color m_caretColor = Color.white;

        [SerializeField]
        private Color m_selectionColor = new Color(0.2f, 0.49f, 1.0f, 0.4f);

        [SerializeField]
        [FormerlySerializedAs("caretWidth")]
        private float m_caretWidth = 2;

        [SerializeField]
        private TextInputAlignment m_textAlignment = TextInputAlignment.Left;

        [SerializeField]
        private TextInputLineMode m_lineMode = TextInputLineMode.SingleLine;

        [SerializeField]
        private TextInputWrapMode m_wrapMode = TextInputWrapMode.NoWrap;

        [SerializeField]
        private TextOverflow m_textOverflow = TextOverflow.Clip;

        [SerializeField]
        private string m_ellipsisString = "\u2026";

        [SerializeField]
        private bool m_readOnly;

        [SerializeField]
        private bool m_maskInput;

        [SerializeField]
        private char m_maskChar = '*';

        [SerializeField]
        private bool m_allowCopy = true;

        [SerializeField]
        private bool m_selectionEnabled = true;

        [SerializeField]
        private bool m_smoothScroll = true;

        [SerializeField]
        [Min(0f)]
        private float m_scrollTweenDurationSeconds = DefaultScrollTweenDurationSeconds;

        [SerializeField]
        private ScrollElasticSettings? m_scrollElastic = new ScrollElasticSettings();

        public ScrollElasticSettings scrollElastic
        {
            get => ScrollElasticSettings.Resolve(ref m_scrollElastic);
            set
            {
                m_scrollElastic = value;
                ScrollElasticSettings.Resolve(ref m_scrollElastic);
            }
        }

        [SerializeField]
        [FormerlySerializedAs("blinkInterval")]
        private float m_blinkInterval = 0.5f;

        [SerializeField]
        [FormerlySerializedAs("locale")]
        private string m_locale = "zh-Hans";

        [NonSerialized] private RectTransform rectTransformCache;
        [NonSerialized] private UnityAutoRenderTextureSurface? surface;
        [NonSerialized] private InputBox? inputBox;
        [NonSerialized] private HybridInputDispatcher.HybridInputSinkRegistration? inputRegistration;
        [NonSerialized] private InputSink? inputSink;
        [NonSerialized] private readonly TextInputFrameState inputFrameState = new TextInputFrameState();
        [NonSerialized] private ColorSpace? m_colorSpaceOverride;
        [NonSerialized] private bool styleDirty = true;
        [NonSerialized] private bool layoutDirty = true;
        [NonSerialized] private bool paintDirty = true;
        [NonSerialized] private bool caretVisible = true;
        [NonSerialized] private float nextBlinkTime;
        [NonSerialized] private bool compositionActive;
        [NonSerialized] private string lastCompositionText = "";
        [NonSerialized] private char pendingHighSurrogate;
        [NonSerialized] private float pendingHighSurrogateTime;
        [NonSerialized] private char pendingGuiHighSurrogate;
        [NonSerialized] private float pendingGuiHighSurrogateTime;
        [NonSerialized] private readonly ScrollAxisLock scrollAxisLock = new ScrollAxisLock();
        [NonSerialized] private readonly ScrollTween scrollTweenX = new ScrollTween();
        [NonSerialized] private readonly ScrollTween scrollTweenY = new ScrollTween();
        [NonSerialized] private readonly ScrollElasticAxis scrollElasticX = new ScrollElasticAxis();
        [NonSerialized] private readonly ScrollElasticAxis scrollElasticY = new ScrollElasticAxis();
        [NonSerialized]
        private readonly List<MonoBehaviour> parentScrollHandlerScratch = new List<MonoBehaviour>(8);
        [NonSerialized]
        private readonly ScrollElasticReleasePolicy scrollElasticReleaseX =
            new ScrollElasticReleasePolicy();
        [NonSerialized]
        private readonly ScrollElasticReleasePolicy scrollElasticReleaseY =
            new ScrollElasticReleasePolicy();
        [NonSerialized] private readonly StringBuilder queuedGuiCommittedInput = new StringBuilder();
        [NonSerialized] private float nextLeftRepeatTime;
        [NonSerialized] private float nextRightRepeatTime;
        [NonSerialized] private float nextUpRepeatTime;
        [NonSerialized] private float nextDownRepeatTime;
        [NonSerialized] private float nextHomeRepeatTime;
        [NonSerialized] private float nextEndRepeatTime;
        [NonSerialized] private float nextBackspaceRepeatTime;
        [NonSerialized] private float nextDeleteRepeatTime;
        [NonSerialized] private bool pointerSelecting;
        [NonSerialized] private bool keyboardScrollInterlockActive;
        [NonSerialized] private float keyboardScrollInterlockUntilTime = -1f;
#if UNITY_EDITOR
        private const int MaxEditorSkippedRenderRetries = 2;
        [NonSerialized] private bool m_editorRebuildQueued;
        [NonSerialized] private int m_editorSkippedRenderRetries;
#endif

        internal event Action<string>? InternalValueChanged;
        internal event Action<string>? InternalEndEdit;
        internal event Action? InternalFocusGained;
        internal event Action? InternalFocusLost;

        internal bool IsDispatcherFocused => inputRegistration?.IsFocused == true;

        public string Text
        {
            get => m_text;
            set => SetText(value, notify: true);
        }

        public void SetTextWithoutNotify(string value)
        {
            SetText(value, notify: false);
        }

        private void SetText(string value, bool notify)
        {
            var managedCanonical = CanonicalizeTextForLineMode(value, m_lineMode);
            if (string.Equals(m_text, managedCanonical, StringComparison.Ordinal) &&
                (inputBox == null || string.Equals(inputBox.Text, managedCanonical, StringComparison.Ordinal)))
            {
                return;
            }

            var canonical = managedCanonical;
            if (inputBox != null)
            {
                inputBox.Text = managedCanonical;
                canonical = inputBox.Text;
            }

            if (string.Equals(m_text, canonical, StringComparison.Ordinal))
            {
                return;
            }

            CancelScrollTweens();
            compositionActive = false;
            lastCompositionText = "";
            ResetSurrogateInputState();
            ResetKeyRepeatState();
            CommitCanonicalText(canonical, notify, sessionBound: false);
        }

        private bool CommitCanonicalText(string canonical, bool notify, bool sessionBound)
        {
            canonical ??= string.Empty;
            if (string.Equals(m_text, canonical, StringComparison.Ordinal))
            {
                return false;
            }

            m_text = canonical;
            paintDirty = true;
            if (notify)
            {
                inputSink ??= new InputSink(this);
                HybridInputRuntime.NotifyValueChanged(inputSink, canonical, sessionBound);
            }
            return true;
        }

        public Margin margin
        {
            get => m_margin;
            set => m_margin = value;
        }

        public List<string> fontFamilies
        {
            get => m_fontFamilies;
            set => m_fontFamilies = value;
        }

        public float size
        {
            get => m_size;
            set => m_size = value;
        }

        public Color textColor
        {
            get => m_textColor;
            set => m_textColor = value;
        }

        public Color caretColor
        {
            get => m_caretColor;
            set => m_caretColor = value;
        }

        public Color selectionColor
        {
            get => m_selectionColor;
            set
            {
                if (m_selectionColor == value)
                {
                    return;
                }

                m_selectionColor = value;
                inputBox?.SetSelectionColor(m_selectionColor);
                paintDirty = true;
            }
        }

        public float caretWidth
        {
            get => m_caretWidth;
            set => m_caretWidth = value;
        }

        public TextInputAlignment textAlignment
        {
            get => m_textAlignment;
            set
            {
                if (m_textAlignment == value)
                {
                    return;
                }

                m_textAlignment = value;
                CancelScrollTweens();
                styleDirty = true;
                layoutDirty = true;
                paintDirty = true;
            }
        }

        public TextInputLineMode lineMode
        {
            get => m_lineMode;
            set
            {
                if (m_lineMode == value)
                {
                    return;
                }

                m_lineMode = value;
                CoerceWrapModeForLineMode();
                SetText(m_text, notify: true);
                compositionActive = false;
                lastCompositionText = "";
                ResetSurrogateInputState();
                ResetKeyRepeatState();
                CancelScrollTweens();
                inputBox?.SetSoftWrap(EffectiveSoftWrap());
                styleDirty = true;
                layoutDirty = true;
                paintDirty = true;
            }
        }

        public TextInputWrapMode wrapMode
        {
            get => m_wrapMode;
            set
            {
                if (m_wrapMode == value)
                {
                    return;
                }

                m_wrapMode = m_lineMode == TextInputLineMode.SingleLine ? TextInputWrapMode.NoWrap : value;
                CancelScrollTweens();
                inputBox?.SetSoftWrap(EffectiveSoftWrap());
                layoutDirty = true;
                paintDirty = true;
            }
        }

        public TextOverflow textOverflow
        {
            get => m_textOverflow;
            set
            {
                if (m_textOverflow == value)
                {
                    return;
                }

                m_textOverflow = value;
                inputBox?.SetTextOverflow(m_textOverflow);
                layoutDirty = true;
                paintDirty = true;
            }
        }

        public string ellipsisString
        {
            get => m_ellipsisString;
            set
            {
                var next = value ?? "";
                if (m_ellipsisString == next)
                {
                    return;
                }

                m_ellipsisString = next;
                inputBox?.SetEllipsis(m_ellipsisString);
                layoutDirty = true;
                paintDirty = true;
            }
        }

        public bool readOnly
        {
            get => m_readOnly;
            set
            {
                if (m_readOnly == value)
                {
                    return;
                }

                m_readOnly = value;
                if (m_readOnly)
                {
                    ClearPendingTextInput();
                    inputBox?.ClearComposition();
                    inputBox?.BreakUndoGroup();
                }
                ApplyImeCompositionMode();
                paintDirty = true;
            }
        }

        public bool maskInput
        {
            get => m_maskInput;
            set
            {
                if (m_maskInput == value)
                {
                    return;
                }

                m_maskInput = value;
                CancelScrollTweens();
                inputBox?.SetMaskInput(m_maskInput);
                layoutDirty = true;
                paintDirty = true;
            }
        }

        public bool isPassword
        {
            get => m_maskInput;
            set => maskInput = value;
        }

        public char maskChar
        {
            get => m_maskChar;
            set
            {
                if (m_maskChar == value)
                {
                    return;
                }

                m_maskChar = value == '\0' ? '*' : value;
                CancelScrollTweens();
                inputBox?.SetMaskChar(m_maskChar);
                layoutDirty = true;
                paintDirty = true;
            }
        }

        public bool allowCopy
        {
            get => m_allowCopy;
            set => m_allowCopy = value;
        }

        public bool selectionEnabled
        {
            get => m_selectionEnabled;
            set
            {
                if (m_selectionEnabled == value)
                {
                    return;
                }

                m_selectionEnabled = value;
                ClearSelectionIfDisabled();
                paintDirty = true;
            }
        }

        public bool smoothScroll
        {
            get => m_smoothScroll;
            set => m_smoothScroll = value;
        }

        public float scrollTweenDurationSeconds
        {
            get => m_scrollTweenDurationSeconds;
            set => m_scrollTweenDurationSeconds = value;
        }

        public float blinkInterval
        {
            get => m_blinkInterval;
            set => m_blinkInterval = value;
        }

        public string locale
        {
            get => m_locale;
            set => m_locale = value;
        }

        public bool srgb
        {
            get => SurfaceColorSpace() == ColorSpace.Linear;
            set
            {
                m_colorSpaceOverride = value ? ColorSpace.Linear : ColorSpace.Gamma;
                paintDirty = true;
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            ElasticSettings();
            rectTransformCache = GetComponent<RectTransform>();
            inputRegistration?.Dispose();
            inputSink ??= new InputSink(this);
            inputRegistration = HybridInputRuntime.RegisterSink(inputSink);
            styleDirty = true;
            layoutDirty = true;
            paintDirty = true;
            ResetBlink();
            RebuildResources();
            if (EventSystem.current?.currentSelectedGameObject == gameObject)
            {
                AcquireDispatcherFocus();
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ReleaseInputFocus();
            inputRegistration?.Dispose();
            inputRegistration = null;
            Texture = null;
            RetireInputBox();
            DisposeSurface();
            CancelScrollTweens();
            scrollAxisLock.Reset();
            ResetKeyboardScrollInterlock();
            styleDirty = true;
            layoutDirty = true;
            paintDirty = true;
        }

        private void Update()
        {
            if (IsDispatcherFocused)
            {
                if (CanReadInput())
                {
                    UpdateCaretBlink();
                }
            }

            if (inputBox != null && !styleDirty && !layoutDirty)
            {
                TickScrollTweens(inputBox);
                TickElastic(inputBox);
            }

            RebuildResources();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pointerSelecting = false;
            HitTestPointer(eventData, false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (pointerSelecting)
            {
                return;
            }

            HitTestPointer(eventData, false);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            pointerSelecting = m_selectionEnabled;
            HitTestPointer(eventData, m_selectionEnabled);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!m_selectionEnabled)
            {
                return;
            }

            pointerSelecting = true;
            HitTestPointer(eventData, true);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            pointerSelecting = false;
            ReleaseElasticImmediately();
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (eventData == null)
            {
                return;
            }

            if (TrySuppressKeyboardInterlockedScroll(eventData))
            {
                return;
            }

            var scrollInput = HybridInputRuntime.ResolveScrollInput(eventData);
            var axis = scrollAxisLock.Resolve(scrollInput.Delta,
                ScrollEventUtil.IsHorizontalScrollModifierDown(),
                out var contentOffsetDelta,
                out var lockedScrollDelta);
            if (axis == ScrollAxis.None)
            {
                ScrollEventUtil.PassScrollToParent(transform, eventData, lockedScrollDelta);
                return;
            }

            RebuildResources();
            if (inputBox == null)
            {
                ScrollEventUtil.PassScrollToParent(transform, eventData, lockedScrollDelta);
                return;
            }

            var stepPixels = ScrollWheelStepPixels();
            var shouldTweenPointerScroll = ShouldTweenPointerScroll(scrollInput.Delta);
            var elasticSettings = ElasticSettings();
            var allowElastic = scrollInput.Metadata.Capability != HybridScrollCapability.Unsupported &&
                               elasticSettings.Enabled &&
                               !ScrollEventUtil.HasActiveParentScrollHandler(transform, parentScrollHandlerScratch);
            if (!allowElastic)
            {
                SettleElastic();
            }
            if (SettleUnavailableElasticAxes(inputBox))
            {
                RebuildResources();
            }
            var handledX = (axis == ScrollAxis.Horizontal || axis == ScrollAxis.Free) &&
                           TryScrollX(inputBox,
                               contentOffsetDelta.x,
                               stepPixels,
                               shouldTweenPointerScroll,
                               allowElastic,
                               elasticSettings);
            var handledY = (axis == ScrollAxis.Vertical || axis == ScrollAxis.Free) &&
                           TryScrollY(inputBox,
                               contentOffsetDelta.y,
                               stepPixels,
                               shouldTweenPointerScroll,
                               allowElastic,
                               elasticSettings);
            if (!handledX && !handledY)
            {
                ScrollEventUtil.PassScrollToParent(transform, eventData, lockedScrollDelta);
                return;
            }

            if (!shouldTweenPointerScroll)
            {
                paintDirty = true;
                RebuildResources();
            }

            if (axis == ScrollAxis.Free)
            {
                ScrollEventUtil.PassScrollToParent(transform,
                    eventData,
                    new Vector2(handledX ? 0f : lockedScrollDelta.x, handledY ? 0f : lockedScrollDelta.y));
            }

            ObserveElasticRelease(axis, scrollInput.Metadata, elasticSettings.ReleaseDelaySeconds);
            eventData.Use();
        }

        public Vector2 GetScrollPercent()
        {
            if (!TryGetScrollMetrics(out var metrics))
            {
                return Vector2.zero;
            }

            return new Vector2(ResolveScrollPercentX(metrics), ResolveScrollPercentY(metrics));
        }

        public float GetScrollPercentX()
        {
            return TryGetScrollMetrics(out var metrics) ? ResolveScrollPercentX(metrics) : 0f;
        }

        public float GetScrollPercentY()
        {
            return TryGetScrollMetrics(out var metrics) ? ResolveScrollPercentY(metrics) : 0f;
        }

        public void ScrollToPercent(Vector2 percent, bool animated = false)
        {
            SettleElastic();
            if (!TryGetScrollEditor(out var editor, out var metrics))
            {
                return;
            }

            var shouldAnimate = animated && ShouldTweenScroll();
            var changed = SetScrollPercentX(editor, metrics, percent.x, shouldAnimate);
            changed |= SetScrollPercentY(editor, metrics, percent.y, shouldAnimate);
            if (changed)
            {
                paintDirty = true;
                RebuildResources();
            }
        }

        public void ScrollToPercentX(float percent, bool animated = false)
        {
            SettleElastic();
            if (!TryGetScrollEditor(out var editor, out var metrics) ||
                !SetScrollPercentX(editor, metrics, percent, animated && ShouldTweenScroll()))
            {
                return;
            }

            paintDirty = true;
            RebuildResources();
        }

        public void ScrollToPercentY(float percent, bool animated = false)
        {
            SettleElastic();
            if (!TryGetScrollEditor(out var editor, out var metrics) ||
                !SetScrollPercentY(editor, metrics, percent, animated && ShouldTweenScroll()))
            {
                return;
            }

            paintDirty = true;
            RebuildResources();
        }

        public bool TryGetScrollState(out TextBoxScrollState state)
        {
            if (!TryGetScrollMetrics(out var metrics))
            {
                state = default;
                return false;
            }

            state = new TextBoxScrollState(metrics.ScrollX,
                metrics.ScrollY,
                metrics.ViewportWidth,
                metrics.ViewportHeight,
                metrics.ContentWidth,
                Mathf.Max(metrics.ViewportHeight, metrics.Height));
            return true;
        }

        private bool TryScrollX(InputBox editor,
            float contentOffsetDelta,
            float stepPixels,
            bool tweenScroll,
            bool allowElastic,
            ScrollElasticSettings elasticSettings)
        {
            if (Mathf.Approximately(contentOffsetDelta, 0f))
            {
                return false;
            }

            var metrics = editor.GetMetrics();
            scrollTweenX.CancelIfExternallyMoved(metrics.ScrollX);
            var deltaPixels = contentOffsetDelta * stepPixels;
            var maxScrollX = MaxScrollX(metrics);
            var effectiveScrollX = tweenScroll && scrollTweenX.IsActive()
                ? metrics.ScrollX + scrollTweenX.PendingDeltaFrom(metrics.ScrollX)
                : metrics.ScrollX;
            if (allowElastic && (scrollElasticX.IsActive ||
                                 effectiveScrollX + deltaPixels < 0f ||
                                 effectiveScrollX + deltaPixels > maxScrollX))
            {
                if (!scrollElasticX.Apply(effectiveScrollX,
                        maxScrollX,
                        deltaPixels,
                        elasticSettings,
                        out var nextEffectiveScrollX))
                {
                    return false;
                }

                ApplyElasticLogicalOffset(scrollTweenX,
                    editor,
                    metrics.ScrollX,
                    effectiveScrollX,
                    nextEffectiveScrollX,
                    maxScrollX,
                    tweenScroll,
                    horizontal: true);
                MarkElasticPresentationChanged();
                return true;
            }

            if (tweenScroll)
            {
                return scrollTweenX.ScrollBy(metrics.ScrollX, deltaPixels, maxScrollX) ||
                       scrollTweenX.IsActive();
            }

            scrollTweenX.Cancel();
            return editor.ScrollByX(deltaPixels);
        }

        private bool TryScrollY(InputBox editor,
            float contentOffsetDelta,
            float stepPixels,
            bool tweenScroll,
            bool allowElastic,
            ScrollElasticSettings elasticSettings)
        {
            if (Mathf.Approximately(contentOffsetDelta, 0f))
            {
                return false;
            }

            var metrics = editor.GetMetrics();
            scrollTweenY.CancelIfExternallyMoved(metrics.ScrollY);
            var deltaPixels = contentOffsetDelta * stepPixels;
            var maxScrollY = MaxScrollY(metrics);
            var effectiveScrollY = tweenScroll && scrollTweenY.IsActive()
                ? metrics.ScrollY + scrollTweenY.PendingDeltaFrom(metrics.ScrollY)
                : metrics.ScrollY;
            if (allowElastic && (scrollElasticY.IsActive ||
                                 effectiveScrollY + deltaPixels < 0f ||
                                 effectiveScrollY + deltaPixels > maxScrollY))
            {
                if (!scrollElasticY.Apply(effectiveScrollY,
                        maxScrollY,
                        deltaPixels,
                        elasticSettings,
                        out var nextEffectiveScrollY))
                {
                    return false;
                }

                ApplyElasticLogicalOffset(scrollTweenY,
                    editor,
                    metrics.ScrollY,
                    effectiveScrollY,
                    nextEffectiveScrollY,
                    maxScrollY,
                    tweenScroll,
                    horizontal: false);
                MarkElasticPresentationChanged();
                return true;
            }

            if (tweenScroll)
            {
                return scrollTweenY.ScrollBy(metrics.ScrollY, deltaPixels, maxScrollY) ||
                       scrollTweenY.IsActive();
            }

            scrollTweenY.Cancel();
            return editor.ScrollByY(deltaPixels);
        }

        private bool TryGetScrollMetrics(out InputBoxMetrics metrics)
        {
            return TryGetScrollEditor(out _, out metrics);
        }

        private bool TryGetScrollEditor(out InputBox editor, out InputBoxMetrics metrics)
        {
            if (!isActiveAndEnabled)
            {
                editor = null!;
                metrics = default;
                return false;
            }

            if (rectTransformCache == null)
            {
                rectTransformCache = GetComponent<RectTransform>();
            }

            RebuildResources();
            if (inputBox == null)
            {
                editor = null!;
                metrics = default;
                return false;
            }

            editor = inputBox;
            metrics = editor.GetMetrics();
            return true;
        }

        private static float ResolveScrollPercentX(InputBoxMetrics metrics)
        {
            return FloatUtil.ScrollOffsetToPercent(metrics.ScrollX, MaxScrollX(metrics));
        }

        private static float ResolveScrollPercentY(InputBoxMetrics metrics)
        {
            return FloatUtil.ScrollOffsetToPercent(metrics.ScrollY, MaxScrollY(metrics));
        }

        private bool SetScrollPercentX(InputBox editor, InputBoxMetrics metrics, float percent, bool animated)
        {
            var nextScrollX = FloatUtil.PercentToScrollOffset(percent, MaxScrollX(metrics));
            if (!scrollTweenX.ScrollTo(metrics.ScrollX,
                    nextScrollX,
                    MaxScrollX(metrics),
                    out nextScrollX,
                    animated))
            {
                return false;
            }

            return editor.ScrollByX(nextScrollX - metrics.ScrollX);
        }

        private bool SetScrollPercentY(InputBox editor, InputBoxMetrics metrics, float percent, bool animated)
        {
            var nextScrollY = FloatUtil.PercentToScrollOffset(percent, MaxScrollY(metrics));
            if (!scrollTweenY.ScrollTo(metrics.ScrollY,
                    nextScrollY,
                    MaxScrollY(metrics),
                    out nextScrollY,
                    animated))
            {
                return false;
            }

            return editor.ScrollByY(nextScrollY - metrics.ScrollY);
        }

        private void TickScrollTweens(InputBox editor)
        {
            if (!scrollTweenX.IsActive() && !scrollTweenY.IsActive())
            {
                return;
            }

            var metrics = editor.GetMetrics();
            var changed = false;
            if (TickScrollTween(scrollTweenX, metrics.ScrollX, MaxScrollX(metrics), out var nextScrollX))
            {
                changed |= editor.ScrollByX(nextScrollX - metrics.ScrollX);
            }

            if (TickScrollTween(scrollTweenY, metrics.ScrollY, MaxScrollY(metrics), out var nextScrollY))
            {
                changed |= editor.ScrollByY(nextScrollY - metrics.ScrollY);
            }

            if (changed)
            {
                paintDirty = true;
            }
        }

        private bool TickScrollTween(ScrollTween tween, float currentValue, float maxValue, out float nextValue)
        {
            return tween.Tick(currentValue,
                maxValue,
                Time.deltaTime,
                out nextValue);
        }

        private bool ShouldTweenScroll()
        {
            return Application.isPlaying && m_smoothScroll && ScrollTweenDurationSeconds() > 0f;
        }

        private bool ShouldTweenPointerScroll(Vector2 scrollDelta)
        {
            return ShouldTweenScroll() &&
                   !ScrollEventUtil.ShouldBypassTweenForPointerScroll(scrollDelta);
        }

        private void CancelScrollTweens()
        {
            scrollTweenX.Cancel();
            scrollTweenY.Cancel();
            SettleElastic();
        }

        private static void ApplyElasticLogicalOffset(ScrollTween tween,
            InputBox editor,
            float currentOffset,
            float effectiveOffset,
            float nextEffectiveOffset,
            float maxOffset,
            bool tweenScroll,
            bool horizontal)
        {
            if (tweenScroll)
            {
                tween.ScrollBy(currentOffset, nextEffectiveOffset - effectiveOffset, maxOffset);
                return;
            }

            tween.Cancel();
            if (horizontal)
            {
                editor.ScrollByX(nextEffectiveOffset - currentOffset);
            }
            else
            {
                editor.ScrollByY(nextEffectiveOffset - currentOffset);
            }
        }

        private void ObserveElasticRelease(ScrollAxis axis,
            HybridScrollMetadata metadata,
            float releaseDelaySeconds)
        {
            var eventTime = Time.unscaledTimeAsDouble;
            if (axis == ScrollAxis.Horizontal || axis == ScrollAxis.Free)
            {
                ObserveElasticRelease(scrollElasticX,
                    scrollElasticReleaseX,
                    metadata,
                    releaseDelaySeconds,
                    eventTime);
            }
            if (axis == ScrollAxis.Vertical || axis == ScrollAxis.Free)
            {
                ObserveElasticRelease(scrollElasticY,
                    scrollElasticReleaseY,
                    metadata,
                    releaseDelaySeconds,
                    eventTime);
            }
        }

        private static void ObserveElasticRelease(ScrollElasticAxis elasticAxis,
            ScrollElasticReleasePolicy releasePolicy,
            HybridScrollMetadata metadata,
            float releaseDelaySeconds,
            double eventTime)
        {
            if (elasticAxis.IsActive)
            {
                releasePolicy.Observe(metadata, eventTime, releaseDelaySeconds);
            }
            else
            {
                releasePolicy.Cancel();
            }
        }

        private void TickElastic(InputBox editor)
        {
            if (!HasElasticState())
            {
                return;
            }

            var settings = ElasticSettings();
            if (HybridInputRuntime.Diagnostics.ScrollCapability == HybridScrollCapability.Unsupported ||
                !settings.Enabled ||
                ScrollEventUtil.HasActiveParentScrollHandler(transform, parentScrollHandlerScratch))
            {
                SettleElastic();
                return;
            }

            SettleUnavailableElasticAxes(editor);

            if (scrollElasticReleaseX.TryBeginReturn(Time.unscaledTimeAsDouble))
            {
                scrollElasticX.BeginReturn(settings);
            }
            if (scrollElasticReleaseY.TryBeginReturn(Time.unscaledTimeAsDouble))
            {
                scrollElasticY.BeginReturn(settings);
            }

            var changed = scrollElasticX.TickReturn(Time.unscaledDeltaTime, settings);
            changed |= scrollElasticY.TickReturn(Time.unscaledDeltaTime, settings);
            if (changed)
            {
                MarkElasticPresentationChanged();
            }
        }

        private void ReleaseElasticImmediately()
        {
            var now = Time.unscaledTimeAsDouble;
            if (scrollElasticX.IsActive)
            {
                scrollElasticReleaseX.ReleaseImmediately(now);
            }
            if (scrollElasticY.IsActive)
            {
                scrollElasticReleaseY.ReleaseImmediately(now);
            }
        }

        private void SettleElastic()
        {
            scrollElasticReleaseX.Cancel();
            scrollElasticReleaseY.Cancel();
            var changed = scrollElasticX.Settle();
            changed |= scrollElasticY.Settle();
            if (changed)
            {
                MarkElasticPresentationChanged();
            }
        }

        private ScrollElasticSettings ElasticSettings()
        {
            return scrollElastic;
        }

        private bool HasElasticState()
        {
            return scrollElasticX.IsActive ||
                   scrollElasticY.IsActive ||
                   scrollElasticReleaseX.IsPending ||
                   scrollElasticReleaseY.IsPending;
        }

        private bool SettleUnavailableElasticAxes(InputBox editor)
        {
            var metrics = editor.GetMetrics();
            var changed = SettleElasticAxesForRange(MaxScrollX(metrics), MaxScrollY(metrics));
            if (changed)
            {
                MarkElasticPresentationChanged();
            }
            return changed;
        }

        internal bool SettleElasticAxesForRange(float maxScrollX, float maxScrollY)
        {
            var changed = scrollElasticX.SettleIfUnavailable(maxScrollX, scrollElasticReleaseX);
            changed |= scrollElasticY.SettleIfUnavailable(maxScrollY, scrollElasticReleaseY);
            return changed;
        }

        private void MarkElasticPresentationChanged()
        {
            paintDirty = true;
            if (IsDispatcherFocused && inputRegistration != null)
            {
                UpdateCompositionCursorPosition();
            }
        }

        internal static Vector2 ResolveElasticContentPresentationDelta(Vector2 elasticOffset)
        {
            return -elasticOffset;
        }

        internal static Vector2 ResolveElasticLocalPresentationDelta(Vector2 elasticOffset)
        {
            var contentDelta = ResolveElasticContentPresentationDelta(elasticOffset);
            return new Vector2(contentDelta.x, -contentDelta.y);
        }

        internal static Vector2 ResolveElasticPresentationScreenPoint(RectTransform rectTransform,
            Vector2 logicalLocalPoint,
            Vector2 elasticOffset,
            Camera? camera)
        {
            var presentedLocalPoint = logicalLocalPoint + ResolveElasticLocalPresentationDelta(elasticOffset);
            return RectTransformUtility.WorldToScreenPoint(camera,
                rectTransform.TransformPoint(presentedLocalPoint));
        }

        private float ScrollTweenDurationSeconds()
        {
            return FloatUtil.IsFinite(m_scrollTweenDurationSeconds)
                ? Mathf.Max(0f, m_scrollTweenDurationSeconds)
                : DefaultScrollTweenDurationSeconds;
        }

        private static float ScrollWheelStepPixels()
        {
            var stepPixels = MilestroConfiguration.Configuration.TextInput.ScrollWheelStepPixels;
            return FloatUtil.IsFinite(stepPixels) ? Mathf.Max(1f, stepPixels) : DefaultScrollWheelStepPixels;
        }

        private static float MaxScrollX(InputBoxMetrics metrics)
        {
            return MaxScroll(metrics.ContentWidth, metrics.ViewportWidth);
        }

        private static float MaxScrollY(InputBoxMetrics metrics)
        {
            return MaxScroll(metrics.Height, metrics.ViewportHeight);
        }

        private static float MaxScroll(float contentSize, float viewportSize)
        {
            if (!FloatUtil.IsFinite(contentSize) || !FloatUtil.IsFinite(viewportSize))
            {
                return 0f;
            }

            return Mathf.Max(0f, contentSize - viewportSize);
        }

        private void HitTestPointer(PointerEventData eventData, bool extendSelection)
        {
            CancelScrollTweens();
            RebuildResources();
            var eventSystem = EventSystem.current;
            var wasAlreadySelected = eventSystem != null && eventSystem.currentSelectedGameObject == gameObject;
            eventSystem?.SetSelectedGameObject(gameObject, eventData);
            if (wasAlreadySelected)
            {
                AcquireDispatcherFocus();
            }

            if (inputBox == null ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransformCache,
                    eventData.position,
                    eventData.pressEventCamera,
                    out var localPoint))
            {
                return;
            }

            var contentPoint = ToContentPoint(localPoint);
            var effectiveExtendSelection = extendSelection && m_selectionEnabled;
            var changed = inputBox.HitTest(contentPoint, effectiveExtendSelection);
            if (effectiveExtendSelection && TryAutoScrollForDrag(contentPoint))
            {
                changed |= inputBox.HitTest(contentPoint, true);
            }
            compositionActive = false;
            lastCompositionText = "";
            ResetSurrogateInputState();
            ResetKeyRepeatState();
            ResetBlink();
            paintDirty = true;
        }

        private bool TryAutoScrollForDrag(Vector2 contentPoint)
        {
            if (inputBox == null)
            {
                return false;
            }

            var contentHeight = ContentSize().y;
            var deltaY = 0f;
            if (contentPoint.y < 0f)
            {
                deltaY = contentPoint.y;
            }
            else if (contentPoint.y > contentHeight)
            {
                deltaY = contentPoint.y - contentHeight;
            }

            if (Mathf.Approximately(deltaY, 0f))
            {
                return false;
            }

            return inputBox.ScrollByY(deltaY);
        }

        private void OnGUI()
        {
            // Legacy Input.inputString can replacement-encode supplementary-plane text;
            // IMGUI text events can still expose the original surrogate halves.
            if (!IsDispatcherFocused || m_readOnly || !CanReadInput())
            {
                return;
            }

            var currentEvent = Event.current;
            if (currentEvent == null || currentEvent.type != EventType.KeyDown)
            {
                return;
            }

            QueueSupplementaryGuiCharacter(currentEvent.character);
        }

        public void OnSelect(BaseEventData eventData)
        {
            AcquireDispatcherFocus();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            ReleaseInputFocus();
        }

        private void OnInputFrame(HybridInputFrame frame)
        {
            if (!IsDispatcherFocused)
            {
                return;
            }
            if (!CanReadInput())
            {
                return;
            }

            ReadKeyboardInput(frame);
            RebuildResources();
        }

        private void OnInputReset(HybridInputResetReason reason)
        {
            var releaseFocus = reason != HybridInputResetReason.ApplicationFocusLost &&
                               reason != HybridInputResetReason.DeviceChanged;
            ResetLocalInputState(releaseFocus);
            if (isActiveAndEnabled)
            {
                RebuildResources();
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            ElasticSettings().Validate();
            CoerceWrapModeForLineMode();
            SetText(m_text, notify: false);
            if (m_readOnly)
            {
                ClearPendingTextInput();
                inputBox?.ClearComposition();
                inputBox?.BreakUndoGroup();
            }
            if (m_maskChar == '\0')
            {
                m_maskChar = '*';
            }
            ValidateMargin();
            ApplyInputBoxAutoMargin();
            inputBox?.SetMaskInput(m_maskInput);
            inputBox?.SetMaskChar(m_maskChar);
            inputBox?.SetTextOverflow(m_textOverflow);
            inputBox?.SetEllipsis(m_ellipsisString);
            ClearSelectionIfDisabled();
            ApplyImeCompositionMode();
            m_scrollTweenDurationSeconds = FloatUtil.IsFinite(m_scrollTweenDurationSeconds)
                ? Mathf.Max(0f, m_scrollTweenDurationSeconds)
                : DefaultScrollTweenDurationSeconds;
            if (surface != null) UvRect = surface.DisplayUvRect;
            m_editorSkippedRenderRetries = 0;
            styleDirty = true;
            layoutDirty = true;
            paintDirty = true;
        }
#endif

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            if (isActiveAndEnabled)
            {
                layoutDirty = true;
                paintDirty = true;
#if UNITY_EDITOR
                m_editorSkippedRenderRetries = 0;
#endif
                CancelScrollTweens();
                scrollAxisLock.Reset();
                SetVerticesDirty();
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

        private void OnDispatcherFocusGained()
        {
            inputFrameState.Reset();
            inputBox?.SetFocused(true);
            ApplyImeCompositionMode();
            ResetBlink();
            paintDirty = true;
            InternalFocusGained?.Invoke();
        }

        private void OnDispatcherEndEdit(string finalText)
        {
            InternalEndEdit?.Invoke(finalText);
        }

        private void OnDispatcherFocusLost()
        {
            InternalFocusLost?.Invoke();
        }

        private void OnDispatcherValueChanged(string value)
        {
            InternalValueChanged?.Invoke(value);
        }

        private void ReleaseInputFocus()
        {
            if (IsDispatcherFocused && inputRegistration != null)
            {
                inputRegistration.ReleaseFocus();
                return;
            }

            ResetLocalInputState(releaseFocus: true);
        }

        private bool AcquireDispatcherFocus()
        {
            if (inputRegistration != null)
            {
                return inputRegistration.AcquireFocus();
            }

            inputSink ??= new InputSink(this);
            inputRegistration = HybridInputRuntime.RegisterSink(inputSink);
            return inputRegistration.AcquireFocus();
        }

        private void ResetLocalInputState(bool releaseFocus)
        {
            compositionActive = false;
            lastCompositionText = "";
            inputFrameState.Reset();
            ResetSurrogateInputState();
            ResetKeyRepeatState();
            if (releaseFocus)
            {
                caretVisible = false;
            }
            if (inputBox != null)
            {
                inputBox.BreakUndoGroup();
                inputBox.ClearComposition();
                if (releaseFocus)
                {
                    inputBox.ClearSelection();
                    inputBox.SetCaretVisible(false);
                    inputBox.SetFocused(false);
                }
            }
            paintDirty = true;
        }

        private bool CanReadInput()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null || !eventSystem.isActiveAndEnabled ||
                eventSystem.currentSelectedGameObject != gameObject)
            {
                return false;
            }

#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                var focusedWindow = EditorWindow.focusedWindow;
                if (focusedWindow == null || focusedWindow.GetType().Name != "GameView")
                {
                    return false;
                }
            }
#endif

            return true;
        }

        private void ResetBlink()
        {
            caretVisible = true;
            nextBlinkTime = Time.unscaledTime + Mathf.Max(0.05f, m_blinkInterval);
        }

        private void UpdateCaretBlink()
        {
            if (Time.unscaledTime < nextBlinkTime)
            {
                return;
            }

            caretVisible = !caretVisible;
            nextBlinkTime = Time.unscaledTime + Mathf.Max(0.05f, m_blinkInterval);
            paintDirty = true;
        }

        private void ReadKeyboardInput(HybridInputFrame frame)
        {
            var providerInput = inputFrameState.Apply(frame);
            if (m_readOnly)
            {
                ClearPendingTextInput();
                var readOnlyChanged = ClearActiveComposition();
                readOnlyChanged |= ReadEditingKeys(frame, false, false);
                ApplyInputChange(readOnlyChanged, sessionBound: true);
                return;
            }

            var committedGroupChanged = false;
            var hadCompositionBeforeInput = compositionActive ||
                                            !string.IsNullOrEmpty(inputFrameState.CompositionText);
            var committedText = ReadCommittedText(frame, providerInput);
            var committedTextHadLineBreak = ContainsLineBreak(committedText);
            if (committedText.Length > 0 && inputBox != null)
            {
                if (compositionActive)
                {
                    committedGroupChanged |= inputBox.CommitComposition(committedText);
                    compositionActive = false;
                    lastCompositionText = "";
                }
                else
                {
                    inputBox.InsertText(committedText);
                    committedGroupChanged = true;
                }

                if (!string.IsNullOrEmpty(inputFrameState.CompositionText))
                {
                    committedGroupChanged |= UpdateComposition(inputFrameState.CompositionText);
                }
            }
            else
            {
                committedGroupChanged |= UpdateComposition(inputFrameState.CompositionText);
            }

            ApplyInputChange(committedGroupChanged, sessionBound: true);
            var editingGroupChanged = ReadEditingKeys(frame,
                hadCompositionBeforeInput,
                committedTextHadLineBreak);
            ApplyInputChange(editingGroupChanged, sessionBound: true);
        }

        private bool ReadEditingKeys(HybridInputFrame frame,
            bool suppressForComposition,
            bool suppressEnterInsertion)
        {
            if (suppressForComposition || compositionActive || inputBox == null)
            {
                ResetKeyRepeatState();
                return false;
            }

            if (TryHandleUndoRedoShortcut(frame, out var historyChanged))
            {
                return historyChanged;
            }

            if (TryHandleClipboardShortcut(frame, out var clipboardChanged))
            {
                return clipboardChanged;
            }

            var changed = false;
            if (frame.WasKeyPressed(KeyCode.Return) || frame.WasKeyPressed(KeyCode.KeypadEnter))
            {
                ResetSurrogateInputState();
                if (!m_readOnly && m_lineMode == TextInputLineMode.MultiLine && !suppressEnterInsertion)
                {
                    inputBox.InsertText("\n");
                    changed = true;
                }
            }

            if (InputBoxShortcutUtil.IsSelectAllDown(frame))
            {
                ResetSurrogateInputState();
                if (m_selectionEnabled)
                {
                    changed |= inputBox.SelectAll();
                }
            }

            var extendSelection = m_selectionEnabled && InputBoxShortcutUtil.IsSelectionExtendDown(frame);
            if (ShouldProcessRepeatingKey(frame, KeyCode.LeftArrow, ref nextLeftRepeatTime))
            {
                ResetSurrogateInputState();
                changed |= MoveLeftArrow(frame, inputBox, extendSelection);
            }
            if (ShouldProcessRepeatingKey(frame, KeyCode.RightArrow, ref nextRightRepeatTime))
            {
                ResetSurrogateInputState();
                changed |= MoveRightArrow(frame, inputBox, extendSelection);
            }
            if (ShouldProcessRepeatingKey(frame, KeyCode.UpArrow, ref nextUpRepeatTime))
            {
                ResetSurrogateInputState();
                changed |= MoveUpArrow(frame, inputBox, extendSelection);
            }
            if (ShouldProcessRepeatingKey(frame, KeyCode.DownArrow, ref nextDownRepeatTime))
            {
                ResetSurrogateInputState();
                changed |= MoveDownArrow(frame, inputBox, extendSelection);
            }
            if (ShouldProcessRepeatingKey(frame, KeyCode.Home, ref nextHomeRepeatTime))
            {
                ResetSurrogateInputState();
                changed |= InputBoxShortcutUtil.IsDocumentBoundaryModifierDown(frame)
                    ? inputBox.MoveDocumentStart(extendSelection)
                    : inputBox.MoveLineStart(extendSelection);
            }
            if (ShouldProcessRepeatingKey(frame, KeyCode.End, ref nextEndRepeatTime))
            {
                ResetSurrogateInputState();
                changed |= InputBoxShortcutUtil.IsDocumentBoundaryModifierDown(frame)
                    ? inputBox.MoveDocumentEnd(extendSelection)
                    : inputBox.MoveLineEnd(extendSelection);
            }
            if (ShouldProcessRepeatingKey(frame, KeyCode.Backspace, ref nextBackspaceRepeatTime))
            {
                ResetSurrogateInputState();
                if (!m_readOnly)
                {
                    changed |= inputBox.DeleteBackward();
                }
            }
            if (ShouldProcessRepeatingKey(frame, KeyCode.Delete, ref nextDeleteRepeatTime))
            {
                ResetSurrogateInputState();
                if (!m_readOnly)
                {
                    changed |= inputBox.DeleteForward();
                }
            }
            return changed;
        }

        private static bool MoveLeftArrow(HybridInputFrame frame, InputBox editor, bool extendSelection)
        {
            return InputBoxShortcutUtil.IsMacBoundaryArrowModifierDown(frame)
                ? editor.MoveLineStart(extendSelection)
                : editor.MovePrevious(extendSelection);
        }

        private static bool MoveRightArrow(HybridInputFrame frame, InputBox editor, bool extendSelection)
        {
            return InputBoxShortcutUtil.IsMacBoundaryArrowModifierDown(frame)
                ? editor.MoveLineEnd(extendSelection)
                : editor.MoveNext(extendSelection);
        }

        private static bool MoveUpArrow(HybridInputFrame frame, InputBox editor, bool extendSelection)
        {
            return InputBoxShortcutUtil.IsMacBoundaryArrowModifierDown(frame)
                ? editor.MoveDocumentStart(extendSelection)
                : editor.MoveUp(extendSelection);
        }

        private static bool MoveDownArrow(HybridInputFrame frame, InputBox editor, bool extendSelection)
        {
            return InputBoxShortcutUtil.IsMacBoundaryArrowModifierDown(frame)
                ? editor.MoveDocumentEnd(extendSelection)
                : editor.MoveDown(extendSelection);
        }

        private bool TryHandleUndoRedoShortcut(HybridInputFrame frame, out bool changed)
        {
            changed = false;
            if (inputBox == null)
            {
                return false;
            }

            if (InputBoxShortcutUtil.IsUndoDown(frame))
            {
                ResetSurrogateInputState();
                ResetKeyRepeatState();
                if (m_readOnly)
                {
                    return true;
                }
                changed = inputBox.Undo();
                return true;
            }

            if (InputBoxShortcutUtil.IsRedoDown(frame))
            {
                ResetSurrogateInputState();
                ResetKeyRepeatState();
                if (m_readOnly)
                {
                    return true;
                }
                changed = inputBox.Redo();
                return true;
            }

            return false;
        }

        private bool TryHandleClipboardShortcut(HybridInputFrame frame, out bool changed)
        {
            changed = false;
            if (inputBox == null)
            {
                return false;
            }

            if (InputBoxShortcutUtil.IsCopyDown(frame))
            {
                ResetSurrogateInputState();
                ResetKeyRepeatState();
                if (!m_allowCopy)
                {
                    return true;
                }

                var selectedText = inputBox.SelectedText;
                if (selectedText.Length > 0)
                {
                    GUIUtility.systemCopyBuffer = selectedText;
                }
                return true;
            }

            if (InputBoxShortcutUtil.IsCutDown(frame))
            {
                ResetSurrogateInputState();
                ResetKeyRepeatState();
                if (!m_allowCopy)
                {
                    return true;
                }

                if (!inputBox.Selection.HasSelection)
                {
                    return true;
                }

                var selectedText = inputBox.SelectedText;
                GUIUtility.systemCopyBuffer = selectedText;
                if (m_readOnly)
                {
                    return true;
                }

                inputBox.BreakUndoGroup();
                changed = inputBox.DeleteForward();
                inputBox.BreakUndoGroup();
                return true;
            }

            if (InputBoxShortcutUtil.IsPasteDown(frame))
            {
                ResetSurrogateInputState();
                ResetKeyRepeatState();
                if (m_readOnly)
                {
                    return true;
                }

                var clipboardText = CanonicalizeTextForLineMode(GUIUtility.systemCopyBuffer, m_lineMode);
                if (clipboardText.Length == 0)
                {
                    return true;
                }

                inputBox.BreakUndoGroup();
                inputBox.InsertText(clipboardText);
                inputBox.BreakUndoGroup();
                changed = true;
                return true;
            }

            return false;
        }

        private bool ClearActiveComposition()
        {
            compositionActive = false;
            lastCompositionText = "";
            return inputBox != null && inputBox.ClearComposition();
        }

        private void ClearPendingTextInput()
        {
            compositionActive = false;
            lastCompositionText = "";
            inputFrameState.Reset();
            ResetSurrogateInputState();
        }

        private void ClearSelectionIfDisabled()
        {
            if (m_selectionEnabled || inputBox == null)
            {
                return;
            }

            inputBox.ClearSelection();
        }

        private void ApplyImeCompositionMode()
        {
            if (!IsDispatcherFocused)
            {
                return;
            }

            inputRegistration?.SetImeEnabled(!m_readOnly);
        }

        private bool ShouldProcessRepeatingKey(HybridInputFrame frame, KeyCode key, ref float nextRepeatTime)
        {
            var now = Time.unscaledTime;
            if (frame.WasKeyPressed(key))
            {
                nextRepeatTime = now + MilestroConfiguration.Configuration.TextInput.KeyRepeatInitialDelay;
                return true;
            }

            if (!frame.IsKeyPressed(key))
            {
                nextRepeatTime = 0;
                return false;
            }
#if ENABLE_IN
#endif

            if (nextRepeatTime <= 0)
            {
                nextRepeatTime = now + MilestroConfiguration.Configuration.TextInput.KeyRepeatInitialDelay;
                return false;
            }

            if (now < nextRepeatTime)
            {
                return false;
            }

            nextRepeatTime = now + MilestroConfiguration.Configuration.TextInput.KeyRepeatInterval;
            return true;
        }

        private void ResetKeyRepeatState()
        {
            nextLeftRepeatTime = 0;
            nextRightRepeatTime = 0;
            nextUpRepeatTime = 0;
            nextDownRepeatTime = 0;
            nextHomeRepeatTime = 0;
            nextEndRepeatTime = 0;
            nextBackspaceRepeatTime = 0;
            nextDeleteRepeatTime = 0;
        }

        private void ApplyInputChange(bool changed, bool sessionBound)
        {
            if (inputBox != null)
            {
                UpdateCompositionCursorPosition();
            }

            var textChanged = inputBox != null && CommitCanonicalText(inputBox.Text, notify: true, sessionBound);
            if ((changed || textChanged) && inputBox != null)
            {
                inputBox.EnsureCaretVisible();
                CancelScrollTweens();
                BeginKeyboardScrollInterlock();
                ResetBlink();
                paintDirty = true;
            }
        }

        private bool TrySuppressKeyboardInterlockedScroll(PointerEventData eventData)
        {
            if (!keyboardScrollInterlockActive)
            {
                return false;
            }

            var now = Time.unscaledTime;
            if (!IsHorizontalCaretNavigationDown() && now > keyboardScrollInterlockUntilTime)
            {
                ResetKeyboardScrollInterlock();
                return false;
            }

            if (!HasHorizontalContentScroll(eventData.scrollDelta, ScrollEventUtil.IsHorizontalScrollModifierDown()))
            {
                return false;
            }

            scrollAxisLock.Reset();
            eventData.Use();
            return true;
        }

        private void BeginKeyboardScrollInterlock()
        {
            keyboardScrollInterlockActive = true;
            keyboardScrollInterlockUntilTime = Time.unscaledTime + KeyboardScrollInterlockTimeoutSeconds();
            scrollAxisLock.Reset();
        }

        private void ResetKeyboardScrollInterlock()
        {
            keyboardScrollInterlockActive = false;
            keyboardScrollInterlockUntilTime = -1f;
        }

        private static bool HasHorizontalContentScroll(Vector2 scrollDelta, bool forceHorizontal)
        {
            var hasHorizontalDelta = FloatUtil.IsFinite(scrollDelta.x) && !Mathf.Approximately(scrollDelta.x, 0f);
            var hasVerticalDelta = FloatUtil.IsFinite(scrollDelta.y) && !Mathf.Approximately(scrollDelta.y, 0f);
            return hasHorizontalDelta || (forceHorizontal && hasVerticalDelta);
        }

        private static float KeyboardScrollInterlockTimeoutSeconds()
        {
            var timeoutSeconds = MilestroConfiguration.Configuration.TextInput.KeyboardScrollInterlockTimeoutSeconds;
            return FloatUtil.IsFinite(timeoutSeconds) ? Mathf.Max(0f, timeoutSeconds) : DefaultKeyboardScrollInterlockTimeoutSeconds;
        }

        private static bool IsHorizontalCaretNavigationDown()
        {
            return HybridInputRuntime.IsKeyPressed(KeyCode.LeftArrow) ||
                   HybridInputRuntime.IsKeyPressed(KeyCode.RightArrow);
        }

        private bool UpdateComposition(string rawCompositionText)
        {
            if (inputBox == null)
            {
                compositionActive = false;
                lastCompositionText = "";
                return false;
            }

            if (rawCompositionText.Length > 0)
            {
                ResetSurrogateInputState();
                compositionActive = true;
                var compositionText = CanonicalizeTextForLineMode(rawCompositionText,
                    m_lineMode);
                if (compositionText.Length == 0)
                {
                    if (lastCompositionText.Length == 0)
                    {
                        return false;
                    }

                    lastCompositionText = "";
                    return inputBox.ClearComposition();
                }
                if (compositionText == lastCompositionText)
                {
                    return false;
                }

                lastCompositionText = compositionText;
                return inputBox.SetComposition(compositionText);
            }

            if (!compositionActive)
            {
                return false;
            }

            compositionActive = false;
            lastCompositionText = "";
            ResetSurrogateInputState();
            return inputBox.ClearComposition();
        }

        private void UpdateCompositionCursorPosition()
        {
            if (!IsDispatcherFocused || inputBox == null || rectTransformCache == null)
            {
                return;
            }

            var rect = inputBox.GetCompositionRect();
            var metrics = inputBox.GetMetrics();
            var logicalContentPoint = new Vector2(rect.xMax - metrics.ScrollX,
                rect.yMax - metrics.ScrollY);
            var elasticOffset = new Vector2(scrollElasticX.Offset, scrollElasticY.Offset);
            var logicalLocalPoint = ContentPointToLocalPoint(logicalContentPoint);
            var camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            inputRegistration?.SetImeCursorPosition(ResolveElasticPresentationScreenPoint(rectTransformCache,
                logicalLocalPoint,
                elasticOffset,
                camera));
        }

        private static bool IsEscapeSequenceLead(char ch)
        {
            return ch == '[' || ch == 'O';
        }

        private static bool IsEscapeSequenceParameter(char ch)
        {
            return (ch >= '0' && ch <= '9') || ch == ';' || ch == '?' || ch == '=';
        }

        private static int SkipEscapeSequence(string input, int escapeIndex)
        {
            var index = escapeIndex + 1;
            if (index >= input.Length || !IsEscapeSequenceLead(input[index]))
            {
                return index;
            }

            ++index;
            while (index < input.Length && IsEscapeSequenceParameter(input[index]))
            {
                ++index;
            }

            return index < input.Length ? index + 1 : index;
        }

        private static bool IsCommittedTextControl(char ch)
        {
            if (ch == '\n' || ch == '\r')
            {
                return false;
            }

            return char.IsControl(ch) || ch == '\u007f';
        }

        private static bool IsCommittedTextAppKitFunctionKey(char ch)
        {
            // AppKit Function-Key Unicode Values can reach Input.inputString on macOS.
            return ch >= '\uf700' && ch <= '\uf747';
        }

        private void QueueSupplementaryGuiCharacter(char ch)
        {
            ExpirePendingGuiHighSurrogateToQueue();

            if (ch == '\0')
            {
                return;
            }

            if (char.IsHighSurrogate(ch))
            {
                SetPendingGuiHighSurrogate(ch);
                return;
            }

            if (char.IsLowSurrogate(ch))
            {
                if (pendingGuiHighSurrogate != '\0')
                {
                    queuedGuiCommittedInput.Append(pendingGuiHighSurrogate);
                    queuedGuiCommittedInput.Append(ch);
                    ClearPendingGuiHighSurrogate();
                }
                return;
            }

            ClearPendingGuiHighSurrogate();
        }

        private string ReadCommittedText(HybridInputFrame frame, string providerInput)
        {
            if (InputBoxShortcutUtil.IsCommittedTextInputSuppressed(frame))
            {
                ResetSurrogateInputState();
                return string.Empty;
            }

            var queuedGuiInput = TakeQueuedGuiCommittedInput();
            if (queuedGuiInput.Length == 0)
            {
                return FilterCommittedInput(providerInput);
            }

            var mergedInput = MergeSupplementaryFallback(providerInput, queuedGuiInput);
            return FilterCommittedInput(mergedInput, keepReplacementCharacter: true);
        }

        private string TakeQueuedGuiCommittedInput()
        {
            ExpirePendingGuiHighSurrogateToQueue();
            if (queuedGuiCommittedInput.Length == 0)
            {
                return string.Empty;
            }

            var queuedInput = queuedGuiCommittedInput.ToString();
            queuedGuiCommittedInput.Length = 0;
            return queuedInput;
        }

        private void ExpirePendingGuiHighSurrogateToQueue()
        {
            if (!IsPendingSurrogateExpired(pendingGuiHighSurrogate, pendingGuiHighSurrogateTime))
            {
                return;
            }

            queuedGuiCommittedInput.Append(ReplacementCharacter);
            ClearPendingGuiHighSurrogate();
        }

        private static string MergeSupplementaryFallback(string legacyInput, string queuedInput)
        {
            if (string.IsNullOrEmpty(legacyInput))
            {
                return queuedInput ?? string.Empty;
            }

            if (string.IsNullOrEmpty(queuedInput))
            {
                return legacyInput;
            }

            var builder = new StringBuilder(legacyInput.Length + queuedInput.Length);
            var queuedIndex = 0;
            for (var i = 0; i < legacyInput.Length; ++i)
            {
                var ch = legacyInput[i];
                if (IsSupplementaryPlaceholder(ch))
                {
                    var placeholderCount = 1;
                    while (i + 1 < legacyInput.Length && IsSupplementaryPlaceholder(legacyInput[i + 1]))
                    {
                        ++i;
                        ++placeholderCount;
                    }

                    var replacementCount = Math.Max(1, (placeholderCount + 1) / 2);
                    for (var j = 0; j < replacementCount && queuedIndex < queuedInput.Length; ++j)
                    {
                        AppendNextQueuedGuiScalar(builder, queuedInput, ref queuedIndex);
                    }
                    continue;
                }

                builder.Append(ch);
            }

            while (queuedIndex < queuedInput.Length)
            {
                AppendNextQueuedGuiScalar(builder, queuedInput, ref queuedIndex);
            }

            return builder.ToString();
        }

        private static bool IsSupplementaryPlaceholder(char ch)
        {
            return char.IsSurrogate(ch) || ch == ReplacementCharacter;
        }

        private static void AppendNextQueuedGuiScalar(StringBuilder builder, string queuedInput, ref int index)
        {
            var ch = queuedInput[index++];
            builder.Append(ch);
            if (char.IsHighSurrogate(ch) &&
                index < queuedInput.Length &&
                char.IsLowSurrogate(queuedInput[index]))
            {
                builder.Append(queuedInput[index++]);
            }
        }

        private void ResetSurrogateInputState()
        {
            pendingHighSurrogate = '\0';
            pendingHighSurrogateTime = 0;
            pendingGuiHighSurrogate = '\0';
            pendingGuiHighSurrogateTime = 0;
            queuedGuiCommittedInput.Length = 0;
        }

        private string FilterCommittedInput(string input, bool keepReplacementCharacter = false)
        {
            var committedInput = input ?? string.Empty;
            var hasInput = committedInput.Length > 0;
            var hasExpiredPendingHigh = IsPendingSurrogateExpired(pendingHighSurrogate, pendingHighSurrogateTime);
            if (!hasInput && !hasExpiredPendingHigh)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(committedInput.Length + (hasExpiredPendingHigh ? 1 : 0));
            if (hasExpiredPendingHigh)
            {
                builder.Append(ReplacementCharacter);
                ClearPendingHighSurrogate();
            }

            if (!hasInput)
            {
                return builder.ToString();
            }

            for (var i = 0; i < committedInput.Length; ++i)
            {
                var ch = committedInput[i];
                if (ch == '\u001b')
                {
                    ClearPendingHighSurrogate();
                    i = SkipEscapeSequence(committedInput, i) - 1;
                    continue;
                }
                if (ch == '\r' || ch == '\n')
                {
                    ClearPendingHighSurrogate();
                    if (m_lineMode == TextInputLineMode.MultiLine)
                    {
                        builder.Append('\n');
                    }
                    if (ch == '\r' && i + 1 < committedInput.Length && committedInput[i + 1] == '\n')
                    {
                        ++i;
                    }
                    continue;
                }
                if (IsCommittedTextControl(ch) || IsCommittedTextAppKitFunctionKey(ch))
                {
                    ClearPendingHighSurrogate();
                    continue;
                }
                if (ch == ReplacementCharacter && !keepReplacementCharacter)
                {
                    continue;
                }
                if (char.IsHighSurrogate(ch))
                {
                    if (pendingHighSurrogate != '\0')
                    {
                        SetPendingHighSurrogate(ch);
                        continue;
                    }

                    if (i + 1 >= committedInput.Length)
                    {
                        SetPendingHighSurrogate(ch);
                        continue;
                    }

                    var low = committedInput[i + 1];
                    if (char.IsLowSurrogate(low))
                    {
                        builder.Append(ch);
                        builder.Append(low);
                        ++i;
                    }
                    else
                    {
                        SetPendingHighSurrogate(ch);
                    }
                    continue;
                }
                if (char.IsLowSurrogate(ch))
                {
                    if (pendingHighSurrogate != '\0')
                    {
                        builder.Append(pendingHighSurrogate);
                        builder.Append(ch);
                        ClearPendingHighSurrogate();
                    }
                    continue;
                }
                ClearPendingHighSurrogate();
                builder.Append(ch);
            }
            return builder.ToString();
        }

        internal static string CanonicalizeTextForLineMode(string input, TextInputLineMode lineMode)
        {
            var text = Utf16Util.RemoveUnpairedSurrogates(input ?? string.Empty);
            if (text.Length == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(text.Length);
            for (var i = 0; i < text.Length; ++i)
            {
                var ch = text[i];
                if (ch == '\r' || ch == '\n')
                {
                    if (lineMode == TextInputLineMode.MultiLine)
                    {
                        builder.Append('\n');
                    }
                    if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        ++i;
                    }
                    continue;
                }

                builder.Append(ch);
            }

            return builder.ToString();
        }

        private static bool ContainsLineBreak(string input)
        {
            return !string.IsNullOrEmpty(input) && input.IndexOf('\n') >= 0;
        }

        private void CoerceWrapModeForLineMode()
        {
            if (m_lineMode == TextInputLineMode.SingleLine)
            {
                m_wrapMode = TextInputWrapMode.NoWrap;
            }
        }

        private bool EffectiveSoftWrap()
        {
            return m_lineMode == TextInputLineMode.MultiLine && m_wrapMode == TextInputWrapMode.Wrap;
        }

        private static bool IsPendingSurrogateExpired(char pending, float pendingTime)
        {
            return pending != '\0' &&
                   Time.unscaledTime - pendingTime >=
                   MilestroConfiguration.Configuration.TextInput.SurrogatePairTimeout;
        }

        private void SetPendingHighSurrogate(char ch)
        {
            pendingHighSurrogate = ch;
            pendingHighSurrogateTime = Time.unscaledTime;
        }

        private void ClearPendingHighSurrogate()
        {
            pendingHighSurrogate = '\0';
            pendingHighSurrogateTime = 0;
        }

        private void SetPendingGuiHighSurrogate(char ch)
        {
            pendingGuiHighSurrogate = ch;
            pendingGuiHighSurrogateTime = Time.unscaledTime;
        }

        private void ClearPendingGuiHighSurrogate()
        {
            pendingGuiHighSurrogate = '\0';
            pendingGuiHighSurrogateTime = 0;
        }

        private void RebuildResources()
        {
#if UNITY_EDITOR
            if (styleDirty || layoutDirty)
            {
                m_editorSkippedRenderRetries = 0;
            }
#endif
            var sizePixels = CurrentSize();
            var surfaceColorSpace = SurfaceColorSpace();
            if (surface == null || surface.ColorSpace != surfaceColorSpace)
            {
                DisposeSurface();
                SetSurface(new UnityAutoRenderTextureSurface(sizePixels.x, sizePixels.y, surfaceColorSpace));
                ApplySurfaceToGraphic();
                paintDirty = true;
            }
            else if (surface.Width != sizePixels.x || surface.Height != sizePixels.y)
            {
                surface.Resize(sizePixels.x, sizePixels.y);
                ApplySurfaceToGraphic();
                layoutDirty = true;
                paintDirty = true;
            }

            if (styleDirty || inputBox == null)
            {
                RecreateInputBox();
                styleDirty = false;
                layoutDirty = true;
                paintDirty = true;
            }

            if (inputBox == null)
            {
                return;
            }

            ValidateMargin();
            if (layoutDirty)
            {
                CancelScrollTweens();
                inputBox.SetViewport(ContentSize());
                ApplyInputBoxAutoMargin();
                inputBox.EnsureCaretVisible();
                layoutDirty = false;
                paintDirty = true;
            }

            if (!paintDirty)
            {
                return;
            }

            inputBox.SetCaretVisible(IsDispatcherFocused && caretVisible);
            var submitted = surface!.TrySubmit(BuildRenderCommands());
            if (!submitted)
            {
                paintDirty = true;
#if UNITY_EDITOR
                QueueEditorRebuild();
#endif
                return;
            }

            paintDirty = false;
        }

        private void SetSurface(UnityAutoRenderTextureSurface nextSurface)
        {
            surface = nextSurface;
            surface.RenderEventCompleted += OnRenderEventCompleted;
        }

        private void DisposeSurface()
        {
            if (surface == null)
            {
                return;
            }

            surface.RenderEventCompleted -= OnRenderEventCompleted;
            surface.Dispose();
            surface = null;
        }

        private void OnRenderEventCompleted(UnitySkiaRenderTextureSurface.RenderSubmissionStatus status)
        {
            if (status == UnitySkiaRenderTextureSurface.RenderSubmissionStatus.Drawn)
            {
#if UNITY_EDITOR
                m_editorSkippedRenderRetries = 0;
                if (!Application.isPlaying && this && isActiveAndEnabled)
                {
                    SetVerticesDirty();
                    SetMaterialDirty();
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                }
#endif
                return;
            }

#if UNITY_EDITOR
            if (status == UnitySkiaRenderTextureSurface.RenderSubmissionStatus.Skipped &&
                !Application.isPlaying &&
                isActiveAndEnabled &&
                m_editorSkippedRenderRetries < MaxEditorSkippedRenderRetries)
            {
                ++m_editorSkippedRenderRetries;
                paintDirty = true;
                QueueEditorRebuild();
            }
#endif
        }

        private void RecreateInputBox()
        {
            RetireInputBox();

            var paragraphStyle = new ParagraphStyle();
            paragraphStyle.TextAlign = ToParagraphTextAlign(m_textAlignment);
            if (m_lineMode == TextInputLineMode.SingleLine)
            {
                paragraphStyle.MaxLines = 1;
            }
            else
            {
                paragraphStyle.ClearMaxLines();
            }

            var textStyle = new TextStyle();
            textStyle.SetFontFamilies(m_fontFamilies);
            textStyle.FontSize = m_size;
            textStyle.Locale = m_locale;
            textStyle.Color = m_textColor;
            paragraphStyle.SetTextStyle(textStyle);

            inputBox = new InputBox(paragraphStyle, textStyle);
            CoerceWrapModeForLineMode();
            var managedCanonical = CanonicalizeTextForLineMode(m_text, m_lineMode);
            inputBox.SetSoftWrap(EffectiveSoftWrap());
            inputBox.SetFocused(IsDispatcherFocused);
            inputBox.SetTextOverflow(m_textOverflow);
            inputBox.SetEllipsis(m_ellipsisString);
            inputBox.SetMaskInput(m_maskInput);
            inputBox.SetMaskChar(m_maskChar);
            inputBox.Text = managedCanonical;
            CommitCanonicalText(inputBox.Text, notify: false, sessionBound: false);
            compositionActive = false;
            lastCompositionText = "";
            inputBox.SetCaretColor(m_caretColor);
            inputBox.SetSelectionColor(m_selectionColor);
            inputBox.SetCaretWidth(m_caretWidth);
            inputBox.SetViewport(ContentSize());
            ApplyInputBoxAutoMargin();
            ClearSelectionIfDisabled();
        }

        private static TextAlign ToParagraphTextAlign(TextInputAlignment alignment)
        {
            switch (alignment)
            {
                case TextInputAlignment.Center:
                    return TextAlign.Center;
                case TextInputAlignment.Right:
                    return TextAlign.Right;
                case TextInputAlignment.Left:
                default:
                    return TextAlign.Left;
            }
        }

        private UnitySkiaRenderCommandList BuildRenderCommands()
        {
            var commands = new UnitySkiaRenderCommandList();
            if (inputBox != null)
            {
                commands.DrawInputBox(inputBox,
                    ContentRect(),
                    ResolveElasticContentPresentationDelta(new Vector2(scrollElasticX.Offset, scrollElasticY.Offset)));
            }
            return commands;
        }

        private Vector2Int CurrentSize()
        {
            var rect = rectTransformCache.rect;
            return new Vector2Int(Mathf.Max(1, Mathf.CeilToInt(rect.width)),
                Mathf.Max(1, Mathf.CeilToInt(rect.height)));
        }

        private Vector2 ContentSize()
        {
            var currentSize = CurrentSize();
            var margin = CurrentResolvedMargin(currentSize);
            return new Vector2(Mathf.Max(1f, currentSize.x - margin.FixedHorizontalSize),
                Mathf.Max(1f, currentSize.y - margin.FixedVerticalSize));
        }

        private Rect ContentRect()
        {
            var size = ContentSize();
            var margin = CurrentResolvedMargin();
            return new Rect(margin.Left, margin.Top, size.x, size.y);
        }

        private Vector2 ToContentPoint(Vector2 localPoint)
        {
            var rect = rectTransformCache.rect;
            var margin = CurrentResolvedMargin();
            return new Vector2(localPoint.x - rect.xMin - margin.Left,
                rect.yMax - localPoint.y - margin.Top);
        }

        private Vector2 ContentPointToLocalPoint(Vector2 contentPoint)
        {
            var rect = rectTransformCache.rect;
            var margin = CurrentResolvedMargin();
            return new Vector2(rect.xMin + margin.Left + contentPoint.x,
                rect.yMax - margin.Top - contentPoint.y);
        }

        private ResolvedMargin CurrentResolvedMargin()
        {
            return CurrentResolvedMargin(CurrentSize());
        }

        private ResolvedMargin CurrentResolvedMargin(Vector2Int currentSize)
        {
            ValidateMargin();
            return m_margin.Resolve(new MarginResolveContext(currentSize.x, currentSize.y, m_size));
        }

        private ColorSpace SurfaceColorSpace()
        {
            return m_colorSpaceOverride ?? UnitySkiaRenderTextureDescriptor.DefaultColorSpace;
        }

        private void ApplySurfaceToGraphic()
        {
            Texture = surface!.Texture;
            UvRect = surface.DisplayUvRect;
        }

        private void ValidateMargin()
        {
            if (m_margin == null)
            {
                m_margin = new Margin();
            }
            m_margin.Normalize();
        }

        private void ApplyInputBoxAutoMargin()
        {
            inputBox?.SetAutoMargin(m_margin.Left.Auto,
                m_margin.Top.Auto,
                m_margin.Right.Auto,
                m_margin.Bottom.Auto);
        }

        private void RetireInputBox()
        {
            var oldInputBox = inputBox;
            inputBox = null;
            CancelScrollTweens();
            if (oldInputBox == null)
            {
                return;
            }

            if (surface != null)
            {
                surface.DisposeResourceAfterPendingDraws(oldInputBox);
            }
            else
            {
                oldInputBox.Dispose();
            }
        }

        private sealed class InputSink : IHybridInputLifecycleSink
        {
            private readonly TextInput owner;

            internal InputSink(TextInput owner)
            {
                this.owner = owner;
            }

            public GameObject Owner => owner.gameObject;
            public bool IsActiveAndEnabled => owner.isActiveAndEnabled;
            public bool CanConsumeInputNow => owner.CanReadInput();
            public string CommittedText => owner.m_text;

            public void OnInputFrame(HybridInputFrame frame)
            {
                owner.OnInputFrame(frame);
            }

            public void OnInputReset(HybridInputResetReason reason)
            {
                owner.OnInputReset(reason);
            }

            public void OnFocusGained()
            {
                owner.OnDispatcherFocusGained();
            }

            public void OnEndEdit(string finalText)
            {
                owner.OnDispatcherEndEdit(finalText);
            }

            public void OnFocusLost()
            {
                owner.OnDispatcherFocusLost();
            }

            public void OnValueChanged(string value)
            {
                owner.OnDispatcherValueChanged(value);
            }
        }
    }
}
