using Milestro.Input;
using UnityEngine;

namespace Milestro.Components.Internal
{
    internal readonly struct TextInputTouchKeyboardSettings
    {
        internal TextInputTouchKeyboardSettings(HybridInputKeyboardOptions keyboardOptions,
            int characterLimit,
            bool hideMobileInput)
        {
            KeyboardOptions = keyboardOptions;
            CharacterLimit = characterLimit < 0 ? 0 : characterLimit;
            HideMobileInput = hideMobileInput;
        }

        internal HybridInputKeyboardOptions KeyboardOptions { get; }
        internal int CharacterLimit { get; }
        internal bool HideMobileInput { get; }

        internal bool HasSameSessionConfiguration(TextInputTouchKeyboardSettings other)
        {
            return KeyboardOptions.KeyboardType == other.KeyboardOptions.KeyboardType &&
                   KeyboardOptions.Autocorrection == other.KeyboardOptions.Autocorrection &&
                   KeyboardOptions.Multiline == other.KeyboardOptions.Multiline &&
                   KeyboardOptions.Secure == other.KeyboardOptions.Secure &&
                   KeyboardOptions.ReadOnly == other.KeyboardOptions.ReadOnly &&
                   HideMobileInput == other.HideMobileInput;
        }
    }

    internal interface ITextInputTouchKeyboardHandle
    {
        TouchScreenKeyboard? Keyboard { get; }
        string Text { get; set; }
        bool Active { get; set; }
        TouchScreenKeyboard.Status Status { get; }
        int CharacterLimit { get; set; }
        bool CanGetSelection { get; }
        bool CanSetSelection { get; }
        RangeInt Selection { get; set; }
    }

    internal interface ITextInputTouchKeyboardSource
    {
        bool IsSupported { get; }
        bool IsInPlaceEditingAllowed { get; }
        bool TouchSupported { get; }
        RuntimePlatform Platform { get; }
        bool HideInput { set; }

        ITextInputTouchKeyboardHandle Open(string text,
            TouchScreenKeyboardType keyboardType,
            bool autocorrection,
            bool multiline,
            bool secure,
            bool alert,
            string textPlaceholder,
            int characterLimit);
    }

    internal sealed class UnityTextInputTouchKeyboardSource : ITextInputTouchKeyboardSource
    {
        public bool IsSupported => TouchScreenKeyboard.isSupported;
        public bool IsInPlaceEditingAllowed => TouchScreenKeyboard.isInPlaceEditingAllowed;
        public bool TouchSupported => UnityEngine.Input.touchSupported;
        public RuntimePlatform Platform => Application.platform;

        public bool HideInput
        {
            set => TouchScreenKeyboard.hideInput = value;
        }

        public ITextInputTouchKeyboardHandle Open(string text,
            TouchScreenKeyboardType keyboardType,
            bool autocorrection,
            bool multiline,
            bool secure,
            bool alert,
            string textPlaceholder,
            int characterLimit)
        {
            return new UnityTextInputTouchKeyboardHandle(TouchScreenKeyboard.Open(text,
                keyboardType,
                autocorrection,
                multiline,
                secure,
                alert,
                textPlaceholder,
                characterLimit));
        }
    }

    internal sealed class UnityTextInputTouchKeyboardHandle : ITextInputTouchKeyboardHandle
    {
        private readonly TouchScreenKeyboard keyboard;

        internal UnityTextInputTouchKeyboardHandle(TouchScreenKeyboard keyboard)
        {
            this.keyboard = keyboard;
        }

        public TouchScreenKeyboard Keyboard => keyboard;

        public string Text
        {
            get => keyboard.text;
            set => keyboard.text = value ?? string.Empty;
        }

        public bool Active
        {
            get => keyboard.active;
            set => keyboard.active = value;
        }

        public TouchScreenKeyboard.Status Status => keyboard.status;
        public int CharacterLimit
        {
            get => keyboard.characterLimit;
            set => keyboard.characterLimit = value;
        }
        public bool CanGetSelection => keyboard.canGetSelection;
        public bool CanSetSelection => keyboard.canSetSelection;
        public RangeInt Selection
        {
            get => keyboard.selection;
            set => keyboard.selection = value;
        }
    }

    internal enum TextInputTouchKeyboardAction
    {
        None = 0,
        Snapshot = 1,
        Submit = 2,
        Cancel = 3,
        Close = 4
    }

    internal readonly struct TextInputTouchKeyboardPoll
    {
        internal TextInputTouchKeyboardPoll(TouchScreenKeyboard.Status status,
            bool statusChanged,
            TextInputTouchKeyboardAction action,
            string text,
            RangeInt? selection,
            bool hasSnapshot)
        {
            Status = status;
            StatusChanged = statusChanged;
            Action = action;
            Text = text;
            Selection = selection;
            HasSnapshot = hasSnapshot;
        }

        internal TouchScreenKeyboard.Status Status { get; }
        internal bool StatusChanged { get; }
        internal TextInputTouchKeyboardAction Action { get; }
        internal string Text { get; }
        internal RangeInt? Selection { get; }
        internal bool HasSnapshot { get; }
    }

    internal sealed class TextInputTouchKeyboardController
    {
        private readonly ITextInputTouchKeyboardSource source;
        private ITextInputTouchKeyboardHandle? keyboard;
        private TextInputTouchKeyboardSettings settings;
        private TouchScreenKeyboard.Status? lastStatus;

        internal TextInputTouchKeyboardController()
            : this(new UnityTextInputTouchKeyboardSource())
        {
        }

        internal TextInputTouchKeyboardController(ITextInputTouchKeyboardSource source)
        {
            this.source = source;
        }

        internal TouchScreenKeyboard? Keyboard => keyboard?.Keyboard;
        internal bool IsActive => keyboard != null;
        internal bool UsesHiddenInput => keyboard != null && settings.HideMobileInput;

        internal bool Open(TextInputTouchKeyboardSettings nextSettings,
            string text,
            RangeInt selection)
        {
            if (!ShouldOpen(nextSettings))
            {
                Close();
                return false;
            }

            if (keyboard != null && settings.HasSameSessionConfiguration(nextSettings))
            {
                settings = nextSettings;
                keyboard.CharacterLimit = nextSettings.CharacterLimit;
                if (UsesHiddenInput)
                {
                    SyncHidden(text, selection);
                }
                else
                {
                    WriteVisible(text, selection);
                }
                return true;
            }

            Close();
            settings = nextSettings;
            if (source.TouchSupported)
            {
                source.HideInput = settings.HideMobileInput;
            }

            var options = settings.KeyboardOptions;
            keyboard = source.Open(text ?? string.Empty,
                options.KeyboardType,
                options.Autocorrection && !options.Secure,
                options.Multiline,
                options.Secure,
                alert: false,
                string.Empty,
                settings.CharacterLimit);
            if (keyboard.CanSetSelection)
            {
                keyboard.Selection = ClampSelection(selection, keyboard.Text);
            }
            lastStatus = null;
            return true;
        }

        internal bool UpdateConfiguration(TextInputTouchKeyboardSettings nextSettings,
            string text,
            RangeInt selection)
        {
            if (keyboard == null)
            {
                return false;
            }
            if (!settings.HasSameSessionConfiguration(nextSettings))
            {
                return Open(nextSettings, text, selection);
            }

            settings = nextSettings;
            keyboard.CharacterLimit = nextSettings.CharacterLimit;
            if (UsesHiddenInput)
            {
                SyncHidden(text, selection);
            }
            else
            {
                WriteVisible(text, selection);
            }
            return true;
        }

        internal void SyncHidden(string text, RangeInt selection)
        {
            var current = keyboard;
            if (current == null || !UsesHiddenInput)
            {
                return;
            }

            var nextText = text ?? string.Empty;
            if (!string.Equals(current.Text, nextText, System.StringComparison.Ordinal))
            {
                current.Text = nextText;
            }
            if (current.CanSetSelection)
            {
                current.Selection = ClampSelection(selection, nextText);
            }
        }

        internal void WriteVisible(string text, RangeInt selection)
        {
            var current = keyboard;
            if (current == null || UsesHiddenInput)
            {
                return;
            }

            var nextText = text ?? string.Empty;
            if (!string.Equals(current.Text, nextText, System.StringComparison.Ordinal))
            {
                current.Text = nextText;
            }
            if (current.CanSetSelection)
            {
                current.Selection = ClampSelection(selection, nextText);
            }
        }

        internal TextInputTouchKeyboardPoll Poll()
        {
            var current = keyboard;
            if (current == null)
            {
                return default;
            }

            var status = current.Status;
            var statusChanged = lastStatus != status;
            lastStatus = status;
            var includeSnapshot = !UsesHiddenInput;
            var text = includeSnapshot ? current.Text : string.Empty;
            RangeInt? selection = includeSnapshot && current.CanGetSelection
                ? ClampSelection(current.Selection, text)
                : null;
            var action = status == TouchScreenKeyboard.Status.Visible
                ? includeSnapshot ? TextInputTouchKeyboardAction.Snapshot : TextInputTouchKeyboardAction.None
                : status == TouchScreenKeyboard.Status.Done
                    ? TextInputTouchKeyboardAction.Submit
                    : status == TouchScreenKeyboard.Status.Canceled
                        ? TextInputTouchKeyboardAction.Cancel
                        : TextInputTouchKeyboardAction.Close;
            if (action != TextInputTouchKeyboardAction.Snapshot && !includeSnapshot)
            {
                text = string.Empty;
                selection = null;
            }
            return new TextInputTouchKeyboardPoll(status,
                statusChanged,
                action,
                text,
                selection,
                includeSnapshot);
        }

        internal void Close()
        {
            var current = keyboard;
            keyboard = null;
            settings = default;
            lastStatus = null;
            if (current != null)
            {
                current.Active = false;
            }
        }

        private bool ShouldOpen(TextInputTouchKeyboardSettings nextSettings)
        {
            if (!source.IsSupported || nextSettings.KeyboardOptions.ReadOnly)
            {
                return false;
            }
            if (source.Platform == RuntimePlatform.Android)
            {
                return !source.IsInPlaceEditingAllowed;
            }
            return true;
        }

        private static RangeInt ClampSelection(RangeInt selection, string text)
        {
            var length = text?.Length ?? 0;
            var start = Mathf.Clamp(selection.start, 0, length);
            var end = Mathf.Clamp(selection.end, 0, length);
            return new RangeInt(start, end - start);
        }
    }
}
