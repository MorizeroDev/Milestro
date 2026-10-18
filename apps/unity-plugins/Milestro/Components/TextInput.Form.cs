using System;
using Milestro.Components.Internal;
using Milestro.Input;
using Milestro.Model;
using Milestro.Util;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Milestro.Components
{
    public partial class TextInput
    {
        [SerializeField] private Graphic? m_placeholder;
        [SerializeField, Min(0)] private int m_characterLimit;
        [SerializeField] private TextInputContentType m_contentType;
        [SerializeField] private TextInputCharacterValidation m_characterValidation;
        [SerializeField] private TouchScreenKeyboardType m_keyboardType;
        [SerializeField] private bool m_submitOnEnter;
        [SerializeField] private UnityEvent<string> m_OnSubmit = new UnityEvent<string>();
        [NonSerialized] private TextInputValidateInput? m_onValidateInput;
        [NonSerialized] private TextInputValidateEdit? m_onValidateEdit;
        [NonSerialized] private bool submitRequested;
        [NonSerialized] private TextInputContentType appliedContentType;

        public Graphic? placeholder
        {
            get => m_placeholder;
            set
            {
                if (value == this)
                {
                    throw new ArgumentException("The input cannot be its own placeholder.", nameof(value));
                }
                if (m_placeholder != null && m_placeholder != value)
                {
                    m_placeholder.enabled = false;
                }
                m_placeholder = value;
                UpdatePlaceholder();
            }
        }

        public int characterLimit
        {
            get => m_characterLimit;
            set
            {
                var next = Math.Max(0, value);
                if (m_characterLimit == next) return;
                m_characterLimit = next;
                RevalidateFormText();
            }
        }

        public TextInputContentType contentType
        {
            get => m_contentType;
            set
            {
                if (m_contentType == value) return;
                m_contentType = value;
                ApplyContentType();
                appliedContentType = value;
                RevalidateFormText();
                ApplyKeyboardOptions();
            }
        }

        public TextInputCharacterValidation characterValidation
        {
            get => m_characterValidation;
            set
            {
                if (m_characterValidation == value) return;
                m_contentType = TextInputContentType.Custom;
                m_characterValidation = value;
                RevalidateFormText();
                ApplyKeyboardOptions();
            }
        }

        public TouchScreenKeyboardType keyboardType
        {
            get => m_keyboardType;
            set
            {
                if (m_keyboardType == value) return;
                m_contentType = TextInputContentType.Custom;
                m_keyboardType = value;
                ApplyKeyboardOptions();
            }
        }

        public TextInputValidateInput? onValidateInput
        {
            get => m_onValidateInput;
            set
            {
                if (m_onValidateInput == value) return;
                m_onValidateInput = value;
                RevalidateFormText();
            }
        }

        public TextInputValidateEdit? onValidateEdit
        {
            get => m_onValidateEdit;
            set => m_onValidateEdit = value;
        }

        public bool submitOnEnter
        {
            get => m_submitOnEnter;
            set => m_submitOnEnter = value;
        }

        public UnityEvent<string> onSubmit => m_OnSubmit ??= new UnityEvent<string>();

        public void Submit()
        {
            if (!isActiveAndEnabled || !IsDispatcherFocused || m_readOnly || compositionActive)
            {
                return;
            }
            inputBox?.BreakUndoGroup();
            inputSink ??= new InputSink(this);
            HybridInputRuntime.NotifySubmit(inputSink, m_text);
        }

        private bool ShouldSubmitOnEnter(HybridInputFrame frame)
        {
            return m_lineMode == TextInputLineMode.SingleLine ||
                   m_submitOnEnter && !frame.IsKeyPressed(KeyCode.LeftShift) &&
                   !frame.IsKeyPressed(KeyCode.RightShift);
        }

        private void UpdatePlaceholder()
        {
            if (m_placeholder != null && m_placeholder != this)
            {
                m_placeholder.enabled = string.IsNullOrEmpty(m_text) && !compositionActive;
            }
        }

        private void ApplyContentType()
        {
            if (m_contentType == TextInputContentType.Custom) return;
            m_characterValidation = TextInputCharacterValidation.None;
            m_keyboardType = TouchScreenKeyboardType.Default;
            switch (m_contentType)
            {
                case TextInputContentType.IntegerNumber:
                    m_characterValidation = TextInputCharacterValidation.Integer;
                    m_keyboardType = TouchScreenKeyboardType.NumbersAndPunctuation;
                    break;
                case TextInputContentType.DecimalNumber:
                    m_characterValidation = TextInputCharacterValidation.Decimal;
                    m_keyboardType = TouchScreenKeyboardType.NumbersAndPunctuation;
                    break;
                case TextInputContentType.Alphanumeric:
                    m_characterValidation = TextInputCharacterValidation.Alphanumeric;
                    m_keyboardType = TouchScreenKeyboardType.ASCIICapable;
                    break;
                case TextInputContentType.Name:
                    m_characterValidation = TextInputCharacterValidation.Name;
                    break;
                case TextInputContentType.EmailAddress:
                    m_characterValidation = TextInputCharacterValidation.EmailAddress;
                    m_keyboardType = TouchScreenKeyboardType.EmailAddress;
                    break;
                case TextInputContentType.Pin:
                    m_characterValidation = TextInputCharacterValidation.Integer;
                    m_keyboardType = TouchScreenKeyboardType.NumberPad;
                    break;
            }
            if (m_contentType != TextInputContentType.Standard &&
                m_contentType != TextInputContentType.Autocorrected)
            {
                m_lineMode = TextInputLineMode.SingleLine;
                CoerceWrapModeForLineMode();
                inputBox?.SetSoftWrap(false);
                styleDirty = true;
                layoutDirty = true;
            }
            maskInput = m_contentType == TextInputContentType.Password ||
                        m_contentType == TextInputContentType.Pin;
            m_allowCopy = !m_maskInput;
        }

        private void ValidateFormSettings()
        {
            m_characterLimit = Math.Max(0, m_characterLimit);
            if (m_placeholder == this) m_placeholder = null;
            if (appliedContentType != m_contentType)
            {
                ApplyContentType();
                appliedContentType = m_contentType;
            }
        }

        private string NormalizeFormText(string value, bool applyCustomValidation = true)
        {
            return TextInputValidation.FilterInsertion(string.Empty, 0, 0,
                CanonicalizeTextForLineMode(value, m_lineMode), m_characterLimit,
                m_characterValidation, applyCustomValidation ? m_onValidateInput : null,
                m_lineMode, m_contentType == TextInputContentType.Pin);
        }

        private void RevalidateFormText()
        {
            ClearPendingTextInput();
            inputBox?.ClearComposition();
            SetText(m_text, notify: true);
            if (inputBox != null) inputBox.Text = m_text;
            UpdatePlaceholder();
            paintDirty = true;
        }

        private bool InsertValidatedText(string value, bool commitComposition = false,
            TextInputEditSource source = TextInputEditSource.Typing)
        {
            var editor = inputBox;
            if (editor == null) return false;
            var currentText = editor.Text;
            var selection = editor.Selection;
            var start = selection.HasSelection
                ? Utf16Util.Utf8ToUtf16Offset(currentText, selection.StartUtf8)
                : (int)editor.Cursor.Utf16Offset;
            var end = selection.HasSelection
                ? Utf16Util.Utf8ToUtf16Offset(currentText, selection.EndUtf8)
                : start;
            var accepted = TextInputValidation.FilterInsertion(currentText, start, end,
                CanonicalizeTextForLineMode(value, m_lineMode), m_characterLimit,
                m_characterValidation, m_onValidateInput, m_lineMode,
                m_contentType == TextInputContentType.Pin);
            if (accepted.Length == 0)
            {
                return commitComposition && editor.ClearComposition();
            }
            var proposedText = currentText.Remove(start, end - start).Insert(start, accepted);
            var editSource = commitComposition ? TextInputEditSource.ImeCommit : source;
            if (!TryValidateEdit(currentText, start, end, accepted, proposedText, editSource,
                    out var validatedText))
            {
                return commitComposition && editor.ClearComposition();
            }
            if (!string.Equals(validatedText, proposedText, StringComparison.Ordinal))
            {
                if (string.Equals(validatedText, currentText, StringComparison.Ordinal))
                {
                    return commitComposition && editor.ClearComposition();
                }
                editor.SelectAll();
                if (validatedText.Length == 0)
                {
                    if (commitComposition)
                    {
                        editor.ClearComposition();
                    }
                    return editor.DeleteForward();
                }
                if (commitComposition)
                {
                    return editor.CommitComposition(validatedText);
                }
                editor.InsertText(validatedText);
                return true;
            }
            if (commitComposition) return editor.CommitComposition(accepted);
            editor.InsertText(accepted);
            return true;
        }

        private bool TryValidateEdit(string currentText, int selectionStart, int selectionEnd,
            string insertedText, string proposedText, TextInputEditSource source, out string validatedText)
        {
            validatedText = proposedText;
            if (m_onValidateEdit == null)
            {
                return true;
            }
            var result = m_onValidateEdit(new TextInputEditContext(currentText, proposedText, insertedText,
                selectionStart, selectionEnd, source));
            if (result.Action == TextInputEditAction.Reject)
            {
                return false;
            }
            if (result.Action == TextInputEditAction.Replace)
            {
                validatedText = NormalizeFormText(result.ReplacementText ?? string.Empty);
            }
            return true;
        }

        private void ApplyKeyboardOptions()
        {
            inputRegistration?.SetKeyboardOptions(GetKeyboardOptions());
            SyncTouchKeyboardConfiguration();
        }

        private HybridInputKeyboardOptions GetKeyboardOptions()
        {
            return new HybridInputKeyboardOptions(m_keyboardType,
                m_contentType == TextInputContentType.Autocorrected,
                m_lineMode == TextInputLineMode.MultiLine, m_maskInput, m_readOnly);
        }
    }
}
