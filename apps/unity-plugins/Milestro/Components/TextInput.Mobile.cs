using System;
using Milestro.Components.Internal;
using Milestro.Events;
using Milestro.Model;
using UnityEngine;

namespace Milestro.Components
{
    public partial class TextInput
    {
        [SerializeField] private bool m_shouldHideMobileInput = true;
        [SerializeField]
        private TouchScreenKeyboardEvent m_OnTouchScreenKeyboardStatusChanged = new TouchScreenKeyboardEvent();

        public bool shouldHideMobileInput
        {
            get => m_shouldHideMobileInput;
            set
            {
                if (m_shouldHideMobileInput == value)
                {
                    return;
                }
                m_shouldHideMobileInput = value;
                SyncTouchKeyboardConfiguration();
            }
        }

        public TouchScreenKeyboard? touchScreenKeyboard => touchKeyboardController.Keyboard;

        public TouchScreenKeyboardEvent onTouchScreenKeyboardStatusChanged =>
            m_OnTouchScreenKeyboardStatusChanged ??= new TouchScreenKeyboardEvent();

        private void OpenTouchKeyboardSession()
        {
            touchKeyboardController.Open(GetTouchKeyboardSettings(), m_text, CurrentTouchKeyboardSelection());
        }

        private void CloseTouchKeyboardSession()
        {
            touchKeyboardController.Close();
        }

        private void SyncTouchKeyboardConfiguration()
        {
            touchKeyboardController.UpdateConfiguration(GetTouchKeyboardSettings(),
                m_text,
                CurrentTouchKeyboardSelection());
        }

        private void PollTouchKeyboardSession()
        {
            if (!touchKeyboardController.IsActive)
            {
                return;
            }
            if (!IsDispatcherFocused || m_readOnly)
            {
                CloseTouchKeyboardSession();
                return;
            }

            var poll = touchKeyboardController.Poll();
            if (poll.StatusChanged)
            {
                onTouchScreenKeyboardStatusChanged.Invoke(poll.Status);
            }
            if (poll.HasSnapshot)
            {
                ApplyTouchKeyboardSnapshot(poll.Text, poll.Selection);
            }

            switch (poll.Action)
            {
                case TextInputTouchKeyboardAction.Submit:
                    Submit();
                    CloseTouchKeyboardSession();
                    ReleaseInputFocus();
                    break;
                case TextInputTouchKeyboardAction.Cancel:
                case TextInputTouchKeyboardAction.Close:
                    CloseTouchKeyboardSession();
                    ReleaseInputFocus();
                    break;
            }
        }

        private TextInputTouchKeyboardSettings GetTouchKeyboardSettings()
        {
            return new TextInputTouchKeyboardSettings(GetKeyboardOptions(),
                m_characterLimit,
                m_shouldHideMobileInput);
        }

        private RangeInt CurrentTouchKeyboardSelection()
        {
            var editor = inputBox;
            if (editor == null)
            {
                return new RangeInt(m_text.Length, 0);
            }

            var selection = editor.Selection;
            if (selection.HasSelection)
            {
                var start = ClampUtf16Offset(editor.Utf8ToUtf16(selection.StartUtf8), editor.Text);
                var end = ClampUtf16Offset(editor.Utf8ToUtf16(selection.EndUtf8), editor.Text);
                return new RangeInt(start, end - start);
            }

            var caret = ClampUtf16Offset(editor.Cursor.Utf16Offset, editor.Text);
            return new RangeInt(caret, 0);
        }

        private void SyncTouchKeyboardState()
        {
            touchKeyboardController.SyncHidden(m_text, CurrentTouchKeyboardSelection());
        }

        private void ApplyTouchKeyboardSnapshot(string rawText, RangeInt? rawSelection)
        {
            var editor = inputBox;
            if (editor == null || m_readOnly)
            {
                return;
            }

            var currentText = editor.Text;
            var candidateText = NormalizeFormText(rawText ?? string.Empty);
            var accepted = TryValidateEdit(currentText,
                0,
                currentText.Length,
                candidateText,
                candidateText,
                TextInputEditSource.Typing,
                out var nextText);
            if (!accepted)
            {
                nextText = currentText;
            }

            if (!string.Equals(currentText, nextText, StringComparison.Ordinal))
            {
                editor.BreakUndoGroup();
                editor.SelectAll();
                if (nextText.Length == 0)
                {
                    editor.DeleteForward();
                }
                else
                {
                    editor.InsertText(nextText);
                }
                editor.BreakUndoGroup();
                ApplyInputChange(changed: true, sessionBound: true);
            }

            ApplyTouchKeyboardSelection(rawSelection, editor.Text);
            touchKeyboardController.WriteVisible(editor.Text, CurrentTouchKeyboardSelection());
        }

        private void ApplyTouchKeyboardSelection(RangeInt? rawSelection, string text)
        {
            var editor = inputBox;
            if (editor == null || !rawSelection.HasValue)
            {
                return;
            }

            var selection = rawSelection.Value;
            var start = Mathf.Clamp(selection.start, 0, text.Length);
            var end = Mathf.Clamp(selection.end, 0, text.Length);
            var anchorUtf8 = editor.Utf16ToUtf8((ulong)start);
            var focusUtf8 = editor.Utf16ToUtf8((ulong)end);
            if (!m_selectionEnabled || start == end)
            {
                editor.SetCursorUtf8(focusUtf8);
                return;
            }
            editor.SetSelectionUtf8(anchorUtf8, focusUtf8);
        }

        private static int ClampUtf16Offset(ulong offset, string text)
        {
            return offset > (ulong)text.Length ? text.Length : (int)offset;
        }
    }
}
