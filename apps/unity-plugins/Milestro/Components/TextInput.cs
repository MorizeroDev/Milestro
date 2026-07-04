using System;
using System.Collections.Generic;
using System.Text;
using Milestro.Components.Internal;
using Milestro.Configuration;
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
        IDeselectHandler
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
        private RectOffset m_margin = new RectOffset();

        [SerializeField]
        [FormerlySerializedAs("fontFamilies")]
        private List<string> m_fontFamilies = new List<string>() { "Source Han Sans VF" };

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
        private bool m_readOnly;

        [SerializeField]
        private bool m_maskInput;

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
        [FormerlySerializedAs("blinkInterval")]
        private float m_blinkInterval = 0.5f;

        [SerializeField]
        [FormerlySerializedAs("locale")]
        private string m_locale = "zh-Hans";

        [NonSerialized] private RectTransform rectTransformCache;
        [NonSerialized] private UnityAutoRenderTextureSurface? surface;
        [NonSerialized] private InputBox? inputBox;
        [NonSerialized] private ColorSpace? m_colorSpaceOverride;
        [NonSerialized] private bool styleDirty = true;
        [NonSerialized] private bool layoutDirty = true;
        [NonSerialized] private bool paintDirty = true;
        [NonSerialized] private bool focused;
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
        [NonSerialized] private bool m_editorRebuildQueued;
#endif

        public string Text
        {
            get => m_text;
            set
            {
                var next = NormalizeTextForLineMode(value, m_lineMode);
                if (m_text == next)
                {
                    return;
                }

                m_text = next;
                if (inputBox != null)
                {
                    inputBox.Text = m_text;
                }
                CancelScrollTweens();
                compositionActive = false;
                lastCompositionText = "";
                ResetSurrogateInputState();
                ResetKeyRepeatState();
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
                var nextText = NormalizeTextForLineMode(m_text, m_lineMode);
                if (m_text != nextText)
                {
                    m_text = nextText;
                    if (inputBox != null)
                    {
                        inputBox.Text = m_text;
                    }
                }
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

        public bool srgb
        {
            get => SurfaceColorSpace() == ColorSpace.Linear;
            set
            {
                m_colorSpaceOverride = value ? ColorSpace.Linear : ColorSpace.Gamma;
                paintDirty = true;
            }
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

        protected override void OnEnable()
        {
            base.OnEnable();
            rectTransformCache = GetComponent<RectTransform>();
            styleDirty = true;
            layoutDirty = true;
            paintDirty = true;
            ResetBlink();
            RebuildResources();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ReleaseInputFocus();
            Texture = null;
            RetireInputBox();
            surface?.Dispose();
            surface = null;
            CancelScrollTweens();
            scrollAxisLock.Reset();
            ResetKeyboardScrollInterlock();
            styleDirty = true;
            layoutDirty = true;
            paintDirty = true;
        }

        private void Update()
        {
            if (focused)
            {
                if (CanReadInput())
                {
                    ReadKeyboardInput();
                    UpdateCaretBlink();
                }
                else
                {
                    ReleaseInputFocus();
                }
            }

            if (inputBox != null && !styleDirty && !layoutDirty)
            {
                TickScrollTweens(inputBox);
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

            var axis = scrollAxisLock.Resolve(eventData.scrollDelta,
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
            var handledX = (axis == ScrollAxis.Horizontal || axis == ScrollAxis.Free) &&
                           TryScrollX(inputBox, contentOffsetDelta.x, stepPixels);
            var handledY = (axis == ScrollAxis.Vertical || axis == ScrollAxis.Free) &&
                           TryScrollY(inputBox, contentOffsetDelta.y, stepPixels);
            if (!handledX && !handledY)
            {
                ScrollEventUtil.PassScrollToParent(transform, eventData, lockedScrollDelta);
                return;
            }

            if (!ShouldTweenScroll())
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

            eventData.Use();
        }

        public Vector2 ScrollPercent
        {
            get
            {
                if (!TryGetScrollMetrics(out var metrics))
                {
                    return Vector2.zero;
                }

                return new Vector2(GetScrollPercentX(metrics), GetScrollPercentY(metrics));
            }
            set
            {
                if (!TryGetScrollEditor(out var editor, out var metrics))
                {
                    return;
                }

                var changed = SetScrollPercentX(editor, metrics, value.x);
                changed |= SetScrollPercentY(editor, metrics, value.y);
                if (!changed)
                {
                    return;
                }

                paintDirty = true;
                RebuildResources();
            }
        }

        public float ScrollPercentX
        {
            get
            {
                return TryGetScrollMetrics(out var metrics) ? GetScrollPercentX(metrics) : 0f;
            }
            set
            {
                if (!TryGetScrollEditor(out var editor, out var metrics) ||
                    !SetScrollPercentX(editor, metrics, value))
                {
                    return;
                }

                paintDirty = true;
                RebuildResources();
            }
        }

        public float ScrollPercentY
        {
            get
            {
                return TryGetScrollMetrics(out var metrics) ? GetScrollPercentY(metrics) : 0f;
            }
            set
            {
                if (!TryGetScrollEditor(out var editor, out var metrics) ||
                    !SetScrollPercentY(editor, metrics, value))
                {
                    return;
                }

                paintDirty = true;
                RebuildResources();
            }
        }

        private bool TryScrollX(InputBox editor, float contentOffsetDelta, float stepPixels)
        {
            if (Mathf.Approximately(contentOffsetDelta, 0f))
            {
                return false;
            }

            var metrics = editor.GetMetrics();
            scrollTweenX.CancelIfExternallyMoved(metrics.ScrollX);
            var previousScrollX = scrollTweenX.IsActive ? scrollTweenX.Target : metrics.ScrollX;
            var nextScrollX = Mathf.Clamp(previousScrollX + contentOffsetDelta * stepPixels,
                0f,
                MaxScrollX(metrics));
            if (!Mathf.Approximately(previousScrollX, nextScrollX))
            {
                ScrollToX(editor, metrics, nextScrollX);
                return true;
            }

            return scrollTweenX.IsActive;
        }

        private bool TryScrollY(InputBox editor, float contentOffsetDelta, float stepPixels)
        {
            if (Mathf.Approximately(contentOffsetDelta, 0f))
            {
                return false;
            }

            var metrics = editor.GetMetrics();
            scrollTweenY.CancelIfExternallyMoved(metrics.ScrollY);
            var previousScrollY = scrollTweenY.IsActive ? scrollTweenY.Target : metrics.ScrollY;
            var nextScrollY = Mathf.Clamp(previousScrollY + contentOffsetDelta * stepPixels,
                0f,
                MaxScrollY(metrics));
            if (!Mathf.Approximately(previousScrollY, nextScrollY))
            {
                ScrollToY(editor, metrics, nextScrollY);
                return true;
            }

            return scrollTweenY.IsActive;
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

        private static float GetScrollPercentX(InputBoxMetrics metrics)
        {
            return FloatUtil.ScrollOffsetToPercent(metrics.ScrollX, MaxScrollX(metrics));
        }

        private static float GetScrollPercentY(InputBoxMetrics metrics)
        {
            return FloatUtil.ScrollOffsetToPercent(metrics.ScrollY, MaxScrollY(metrics));
        }

        private bool SetScrollPercentX(InputBox editor, InputBoxMetrics metrics, float percent)
        {
            var nextScrollX = FloatUtil.PercentToScrollOffset(percent, MaxScrollX(metrics));
            scrollTweenX.Cancel();
            if (Mathf.Approximately(metrics.ScrollX, nextScrollX))
            {
                return false;
            }

            return editor.ScrollByX(nextScrollX - metrics.ScrollX);
        }

        private bool SetScrollPercentY(InputBox editor, InputBoxMetrics metrics, float percent)
        {
            var nextScrollY = FloatUtil.PercentToScrollOffset(percent, MaxScrollY(metrics));
            scrollTweenY.Cancel();
            if (Mathf.Approximately(metrics.ScrollY, nextScrollY))
            {
                return false;
            }

            return editor.ScrollByY(nextScrollY - metrics.ScrollY);
        }

        private void ScrollToX(InputBox editor, InputBoxMetrics metrics, float nextScrollX)
        {
            if (!ShouldTweenScroll())
            {
                scrollTweenX.Cancel();
                if (editor.ScrollByX(nextScrollX - metrics.ScrollX))
                {
                    paintDirty = true;
                }
                return;
            }

            scrollTweenX.Start(metrics.ScrollX, nextScrollX);
        }

        private void ScrollToY(InputBox editor, InputBoxMetrics metrics, float nextScrollY)
        {
            if (!ShouldTweenScroll())
            {
                scrollTweenY.Cancel();
                if (editor.ScrollByY(nextScrollY - metrics.ScrollY))
                {
                    paintDirty = true;
                }
                return;
            }

            scrollTweenY.Start(metrics.ScrollY, nextScrollY);
        }

        private void TickScrollTweens(InputBox editor)
        {
            if (!scrollTweenX.IsActive && !scrollTweenY.IsActive)
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
                ScrollTweenDurationSeconds(),
                out nextValue);
        }

        private bool ShouldTweenScroll()
        {
            return Application.isPlaying && m_smoothScroll && ScrollTweenDurationSeconds() > 0f;
        }

        private void CancelScrollTweens()
        {
            scrollTweenX.Cancel();
            scrollTweenY.Cancel();
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
            EventSystem.current?.SetSelectedGameObject(gameObject, eventData);
            Focus();

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
            m_text = inputBox.Text;
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
            if (!focused || m_readOnly || !CanReadInput())
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
            Focus();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            ReleaseInputFocus();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            CoerceWrapModeForLineMode();
            m_text = NormalizeTextForLineMode(m_text, m_lineMode);
            if (m_readOnly)
            {
                ClearPendingTextInput();
                inputBox?.ClearComposition();
                inputBox?.BreakUndoGroup();
            }
            inputBox?.SetMaskInput(m_maskInput);
            ClearSelectionIfDisabled();
            ApplyImeCompositionMode();
            m_scrollTweenDurationSeconds = FloatUtil.IsFinite(m_scrollTweenDurationSeconds)
                ? Mathf.Max(0f, m_scrollTweenDurationSeconds)
                : DefaultScrollTweenDurationSeconds;
            if (surface != null) UvRect = surface.DisplayUvRect;
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

        private void Focus()
        {
            focused = true;
            ApplyImeCompositionMode();
            ResetBlink();
            paintDirty = true;
        }

        private void ReleaseInputFocus()
        {
            focused = false;
            compositionActive = false;
            lastCompositionText = "";
            ResetSurrogateInputState();
            ResetKeyRepeatState();
            Input.imeCompositionMode = IMECompositionMode.Auto;
            caretVisible = false;
            if (inputBox != null)
            {
                inputBox.BreakUndoGroup();
                inputBox.ClearComposition();
                inputBox.ClearSelection();
                inputBox.SetCaretVisible(false);
            }
            paintDirty = true;
        }

        private bool CanReadInput()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.currentSelectedGameObject != gameObject)
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

        private void ReadKeyboardInput()
        {
            if (m_readOnly)
            {
                ClearPendingTextInput();
                var readOnlyChanged = ClearActiveComposition();
                readOnlyChanged |= ReadEditingKeys(false, false);
                ApplyInputChange(readOnlyChanged);
                return;
            }

            var changed = false;
            var hadCompositionBeforeInput = compositionActive || !string.IsNullOrEmpty(Input.compositionString);
            var committedText = ReadCommittedText();
            var committedTextHadLineBreak = ContainsLineBreak(committedText);
            if (committedText.Length > 0 && inputBox != null)
            {
                if (compositionActive)
                {
                    changed |= inputBox.CommitComposition(committedText);
                    compositionActive = false;
                    lastCompositionText = "";
                }
                else
                {
                    inputBox.InsertText(committedText);
                    changed = true;
                }
            }
            else
            {
                changed |= UpdateComposition();
            }

            changed |= ReadEditingKeys(hadCompositionBeforeInput, committedTextHadLineBreak);
            ApplyInputChange(changed);
        }

        private bool ReadEditingKeys(bool suppressForComposition, bool suppressEnterInsertion)
        {
            if (suppressForComposition || compositionActive || inputBox == null)
            {
                ResetKeyRepeatState();
                return false;
            }

            if (TryHandleUndoRedoShortcut(out var historyChanged))
            {
                return historyChanged;
            }

            if (TryHandleClipboardShortcut(out var clipboardChanged))
            {
                return clipboardChanged;
            }

            var changed = false;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                ResetSurrogateInputState();
                if (!m_readOnly && m_lineMode == TextInputLineMode.MultiLine && !suppressEnterInsertion)
                {
                    inputBox.InsertText("\n");
                    changed = true;
                }
            }

            if (InputBoxShortcutUtil.IsSelectAllDown())
            {
                ResetSurrogateInputState();
                if (m_selectionEnabled)
                {
                    changed |= inputBox.SelectAll();
                }
            }

            var extendSelection = m_selectionEnabled && InputBoxShortcutUtil.IsSelectionExtendDown();
            if (ShouldProcessRepeatingKey(KeyCode.LeftArrow, ref nextLeftRepeatTime))
            {
                ResetSurrogateInputState();
                changed |= inputBox.MovePrevious(extendSelection);
            }
            if (ShouldProcessRepeatingKey(KeyCode.RightArrow, ref nextRightRepeatTime))
            {
                ResetSurrogateInputState();
                changed |= inputBox.MoveNext(extendSelection);
            }
            if (ShouldProcessRepeatingKey(KeyCode.UpArrow, ref nextUpRepeatTime))
            {
                ResetSurrogateInputState();
                changed |= inputBox.MoveUp(extendSelection);
            }
            if (ShouldProcessRepeatingKey(KeyCode.DownArrow, ref nextDownRepeatTime))
            {
                ResetSurrogateInputState();
                changed |= inputBox.MoveDown(extendSelection);
            }
            if (ShouldProcessRepeatingKey(KeyCode.Home, ref nextHomeRepeatTime))
            {
                ResetSurrogateInputState();
                changed |= InputBoxShortcutUtil.IsDocumentBoundaryModifierDown()
                    ? inputBox.MoveDocumentStart(extendSelection)
                    : inputBox.MoveLineStart(extendSelection);
            }
            if (ShouldProcessRepeatingKey(KeyCode.End, ref nextEndRepeatTime))
            {
                ResetSurrogateInputState();
                changed |= InputBoxShortcutUtil.IsDocumentBoundaryModifierDown()
                    ? inputBox.MoveDocumentEnd(extendSelection)
                    : inputBox.MoveLineEnd(extendSelection);
            }
            if (ShouldProcessRepeatingKey(KeyCode.Backspace, ref nextBackspaceRepeatTime))
            {
                ResetSurrogateInputState();
                if (!m_readOnly)
                {
                    changed |= inputBox.DeleteBackward();
                }
            }
            if (ShouldProcessRepeatingKey(KeyCode.Delete, ref nextDeleteRepeatTime))
            {
                ResetSurrogateInputState();
                if (!m_readOnly)
                {
                    changed |= inputBox.DeleteForward();
                }
            }
            return changed;
        }

        private bool TryHandleUndoRedoShortcut(out bool changed)
        {
            changed = false;
            if (inputBox == null)
            {
                return false;
            }

            if (InputBoxShortcutUtil.IsUndoDown())
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

            if (InputBoxShortcutUtil.IsRedoDown())
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

        private bool TryHandleClipboardShortcut(out bool changed)
        {
            changed = false;
            if (inputBox == null)
            {
                return false;
            }

            if (InputBoxShortcutUtil.IsCopyDown())
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

            if (InputBoxShortcutUtil.IsCutDown())
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

            if (InputBoxShortcutUtil.IsPasteDown())
            {
                ResetSurrogateInputState();
                ResetKeyRepeatState();
                if (m_readOnly)
                {
                    return true;
                }

                var clipboardText = NormalizeTextForLineMode(GUIUtility.systemCopyBuffer, m_lineMode);
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
            if (!focused)
            {
                return;
            }

            Input.imeCompositionMode = m_readOnly ? IMECompositionMode.Off : IMECompositionMode.On;
        }

        private bool ShouldProcessRepeatingKey(KeyCode keyCode, ref float nextRepeatTime)
        {
            var now = Time.unscaledTime;
            if (Input.GetKeyDown(keyCode))
            {
                nextRepeatTime = now + MilestroConfiguration.Configuration.TextInput.KeyRepeatInitialDelay;
                return true;
            }

            if (!Input.GetKey(keyCode))
            {
                nextRepeatTime = 0;
                return false;
            }

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

        private void ApplyInputChange(bool changed)
        {
            if (inputBox != null)
            {
                UpdateCompositionCursorPosition();
            }

            if (changed && inputBox != null)
            {
                m_text = inputBox.Text;
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
            return Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow);
        }

        private bool UpdateComposition()
        {
            if (inputBox == null)
            {
                compositionActive = false;
                lastCompositionText = "";
                return false;
            }

            var rawCompositionText = Input.compositionString ?? "";
            if (rawCompositionText.Length > 0)
            {
                ResetSurrogateInputState();
                compositionActive = true;
                var compositionText = NormalizeTextForLineMode(Utf16Util.RemoveUnpairedSurrogates(rawCompositionText),
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
            if (inputBox == null || rectTransformCache == null)
            {
                return;
            }

            var rect = inputBox.GetCompositionRect();
            var metrics = inputBox.GetMetrics();
            var localPoint = ContentPointToLocalPoint(new Vector2(rect.xMax - metrics.ScrollX,
                rect.yMax - metrics.ScrollY));
            var worldPoint = rectTransformCache.TransformPoint(localPoint);
            var camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Input.compositionCursorPos = RectTransformUtility.WorldToScreenPoint(camera, worldPoint);
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

        private string ReadCommittedText()
        {
            if (InputBoxShortcutUtil.IsCommittedTextInputSuppressed())
            {
                ResetSurrogateInputState();
                return string.Empty;
            }

            var queuedGuiInput = TakeQueuedGuiCommittedInput();
            if (queuedGuiInput.Length == 0)
            {
                return FilterCommittedInput(Input.inputString);
            }

            var mergedInput = MergeSupplementaryFallback(Input.inputString, queuedGuiInput);
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

        private static string NormalizeTextForLineMode(string input, TextInputLineMode lineMode)
        {
            var text = input ?? string.Empty;
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
            var sizePixels = CurrentSize();
            var surfaceColorSpace = SurfaceColorSpace();
            if (surface == null || surface.ColorSpace != surfaceColorSpace)
            {
                surface?.Dispose();
                surface = new UnityAutoRenderTextureSurface(sizePixels.x, sizePixels.y, surfaceColorSpace);
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
                inputBox.EnsureCaretVisible();
                layoutDirty = false;
                paintDirty = true;
            }

            if (!paintDirty)
            {
                return;
            }

            inputBox.SetCaretVisible(focused && caretVisible);
            if (!surface!.TrySubmit(BuildRenderCommands()))
            {
                paintDirty = true;
                return;
            }

            paintDirty = false;
        }

        private void RecreateInputBox()
        {
            RetireInputBox();

            var paragraphStyle = new ParagraphStyle();
            paragraphStyle.TextAlign = (int)ToParagraphTextAlign(m_textAlignment);
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
            m_text = NormalizeTextForLineMode(m_text, m_lineMode);
            inputBox.SetSoftWrap(EffectiveSoftWrap());
            inputBox.SetMaskInput(m_maskInput);
            inputBox.Text = m_text;
            compositionActive = false;
            lastCompositionText = "";
            inputBox.SetCaretColor(m_caretColor);
            inputBox.SetSelectionColor(m_selectionColor);
            inputBox.SetCaretWidth(m_caretWidth);
            inputBox.SetViewport(ContentSize());
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
                commands.DrawInputBox(inputBox, ContentRect());
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
            var rect = rectTransformCache.rect;
            return new Vector2(Mathf.Max(1, Mathf.CeilToInt(rect.width) - m_margin.horizontal),
                Mathf.Max(1, Mathf.CeilToInt(rect.height) - m_margin.vertical));
        }

        private Rect ContentRect()
        {
            var size = ContentSize();
            return new Rect(m_margin.left, m_margin.top, size.x, size.y);
        }

        private Vector2 ToContentPoint(Vector2 localPoint)
        {
            var rect = rectTransformCache.rect;
            return new Vector2(localPoint.x - rect.xMin - m_margin.left,
                rect.yMax - localPoint.y - m_margin.top);
        }

        private Vector2 ContentPointToLocalPoint(Vector2 contentPoint)
        {
            var rect = rectTransformCache.rect;
            return new Vector2(rect.xMin + m_margin.left + contentPoint.x,
                rect.yMax - m_margin.top - contentPoint.y);
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
            if (m_margin.left < 0) m_margin.left = 0;
            if (m_margin.top < 0) m_margin.top = 0;
            if (m_margin.right < 0) m_margin.right = 0;
            if (m_margin.bottom < 0) m_margin.bottom = 0;
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
    }
}
