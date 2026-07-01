using System;
using System.Collections.Generic;
using System.Text;
using Milestro.Model;
using Milestro.Skia;
using Milestro.Skia.TextLayout;
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
    public class SkParagraphInputBox : SkiaRenderTextureGraphic, IPointerClickHandler, ISelectHandler, IDeselectHandler
    {
        private const float KeyRepeatInitialDelay = 0.42f;
        private const float KeyRepeatInterval = 0.045f;

        [TextArea(1, 1)]
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
        [FormerlySerializedAs("caretWidth")]
        private float m_caretWidth = 2;

        [SerializeField]
        [FormerlySerializedAs("blinkInterval")]
        private float m_blinkInterval = 0.5f;

        [SerializeField]
        [FormerlySerializedAs("locale")]
        private string m_locale = "zh-Hans";

        [NonSerialized] private RectTransform rectTransform;
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
        [NonSerialized] private float nextLeftRepeatTime;
        [NonSerialized] private float nextRightRepeatTime;
        [NonSerialized] private float nextBackspaceRepeatTime;
        [NonSerialized] private float nextDeleteRepeatTime;
#if UNITY_EDITOR
        [NonSerialized] private bool m_editorRebuildQueued;
#endif

        public string Text
        {
            get => m_text;
            set
            {
                var next = value ?? "";
                if (m_text == next)
                {
                    return;
                }

                m_text = next;
                if (inputBox != null)
                {
                    inputBox.Text = m_text;
                }
                compositionActive = false;
                lastCompositionText = "";
                pendingHighSurrogate = '\0';
                ResetKeyRepeatState();
                layoutDirty = true;
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

        protected override void OnEnable()
        {
            base.OnEnable();
            rectTransform = GetComponent<RectTransform>();
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

            RebuildResources();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            EventSystem.current?.SetSelectedGameObject(gameObject, eventData);
            Focus();

            if (inputBox == null ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out var localPoint))
            {
                return;
            }

            inputBox.HitTest(ToContentPoint(localPoint));
            compositionActive = false;
            lastCompositionText = "";
            pendingHighSurrogate = '\0';
            ResetKeyRepeatState();
            m_text = inputBox.Text;
            ResetBlink();
            paintDirty = true;
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
            Input.imeCompositionMode = IMECompositionMode.On;
            ResetBlink();
            paintDirty = true;
        }

        private void ReleaseInputFocus()
        {
            focused = false;
            compositionActive = false;
            lastCompositionText = "";
            pendingHighSurrogate = '\0';
            ResetKeyRepeatState();
            Input.imeCompositionMode = IMECompositionMode.Auto;
            caretVisible = false;
            if (inputBox != null)
            {
                inputBox.ClearComposition();
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
            var changed = false;
            var committedText = FilterCommittedInput(Input.inputString);
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

            changed |= ReadEditingKeys();
            ApplyInputChange(changed);
        }

        private bool ReadEditingKeys()
        {
            if (compositionActive || inputBox == null)
            {
                ResetKeyRepeatState();
                return false;
            }

            var changed = false;
            if (ShouldProcessRepeatingKey(KeyCode.LeftArrow, ref nextLeftRepeatTime))
            {
                pendingHighSurrogate = '\0';
                changed |= inputBox.MovePrevious();
            }
            if (ShouldProcessRepeatingKey(KeyCode.RightArrow, ref nextRightRepeatTime))
            {
                pendingHighSurrogate = '\0';
                changed |= inputBox.MoveNext();
            }
            if (ShouldProcessRepeatingKey(KeyCode.Backspace, ref nextBackspaceRepeatTime))
            {
                pendingHighSurrogate = '\0';
                changed |= inputBox.DeleteBackward();
            }
            if (ShouldProcessRepeatingKey(KeyCode.Delete, ref nextDeleteRepeatTime))
            {
                pendingHighSurrogate = '\0';
                changed |= inputBox.DeleteForward();
            }
            return changed;
        }

        private bool ShouldProcessRepeatingKey(KeyCode keyCode, ref float nextRepeatTime)
        {
            var now = Time.unscaledTime;
            if (Input.GetKeyDown(keyCode))
            {
                nextRepeatTime = now + KeyRepeatInitialDelay;
                return true;
            }

            if (!Input.GetKey(keyCode))
            {
                nextRepeatTime = 0;
                return false;
            }

            if (nextRepeatTime <= 0)
            {
                nextRepeatTime = now + KeyRepeatInitialDelay;
                return false;
            }

            if (now < nextRepeatTime)
            {
                return false;
            }

            nextRepeatTime = now + KeyRepeatInterval;
            return true;
        }

        private void ResetKeyRepeatState()
        {
            nextLeftRepeatTime = 0;
            nextRightRepeatTime = 0;
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
                ResetBlink();
                layoutDirty = true;
                paintDirty = true;
            }
        }

        private bool UpdateComposition()
        {
            if (inputBox == null)
            {
                compositionActive = false;
                lastCompositionText = "";
                return false;
            }

            var compositionText = Input.compositionString ?? "";
            if (compositionText.Length > 0)
            {
                compositionActive = true;
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
            return inputBox.ClearComposition();
        }

        private void UpdateCompositionCursorPosition()
        {
            if (inputBox == null || rectTransform == null)
            {
                return;
            }

            var rect = inputBox.GetCompositionRect();
            var metrics = inputBox.GetMetrics();
            var localPoint = ContentPointToLocalPoint(new Vector2(rect.right - metrics.ScrollX, rect.bottom));
            var worldPoint = rectTransform.TransformPoint(localPoint);
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
            return char.IsControl(ch) || ch == '\u007f';
        }

        private string FilterCommittedInput(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(input.Length + (pendingHighSurrogate == '\0' ? 0 : 1));
            for (var i = 0; i < input.Length; ++i)
            {
                var ch = input[i];
                if (ch == '\u001b')
                {
                    pendingHighSurrogate = '\0';
                    i = SkipEscapeSequence(input, i) - 1;
                    continue;
                }
                if (IsCommittedTextControl(ch))
                {
                    pendingHighSurrogate = '\0';
                    continue;
                }
                if (char.IsHighSurrogate(ch))
                {
                    if (pendingHighSurrogate != '\0')
                    {
                        pendingHighSurrogate = ch;
                        continue;
                    }

                    if (i + 1 >= input.Length)
                    {
                        pendingHighSurrogate = ch;
                        continue;
                    }

                    var low = input[i + 1];
                    if (char.IsLowSurrogate(low))
                    {
                        builder.Append(ch);
                        builder.Append(low);
                        ++i;
                    }
                    else
                    {
                        pendingHighSurrogate = ch;
                    }
                    continue;
                }
                if (char.IsLowSurrogate(ch))
                {
                    if (pendingHighSurrogate != '\0')
                    {
                        builder.Append(pendingHighSurrogate);
                        builder.Append(ch);
                        pendingHighSurrogate = '\0';
                    }
                    continue;
                }
                pendingHighSurrogate = '\0';
                builder.Append(ch);
            }
            return builder.ToString();
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
            paragraphStyle.TextAlign = (int)TextAlign.Left;
            paragraphStyle.MaxLines = 1;

            var textStyle = new TextStyle();
            textStyle.SetFontFamilies(m_fontFamilies);
            textStyle.FontSize = m_size;
            textStyle.Locale = m_locale;
            textStyle.Color = m_textColor;
            paragraphStyle.SetTextStyle(textStyle);

            inputBox = new InputBox(paragraphStyle, textStyle);
            inputBox.Text = m_text;
            compositionActive = false;
            lastCompositionText = "";
            inputBox.SetCaretColor(m_caretColor);
            inputBox.SetCaretWidth(m_caretWidth);
            inputBox.SetViewport(ContentSize());
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
            var rect = rectTransform.rect;
            return new Vector2Int(Mathf.Max(1, Mathf.CeilToInt(rect.width)),
                Mathf.Max(1, Mathf.CeilToInt(rect.height)));
        }

        private Vector2 ContentSize()
        {
            var rect = rectTransform.rect;
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
            var rect = rectTransform.rect;
            return new Vector2(localPoint.x - rect.xMin - m_margin.left,
                rect.yMax - localPoint.y - m_margin.top);
        }

        private Vector2 ContentPointToLocalPoint(Vector2 contentPoint)
        {
            var rect = rectTransform.rect;
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
