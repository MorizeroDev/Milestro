using System;
using Milestro.InputSystem.Model;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace Milestro.InputSystem.Service
{
    internal sealed class UnityInputSystemKeyboard : IUnityInputSystemKeyboard
    {
        private readonly Keyboard keyboard;
        private Action<char>? textInput;
        private Action<string>? compositionChanged;
        private Action<IMECompositionString>? compositionHandler;

        internal UnityInputSystemKeyboard(Keyboard keyboard)
        {
            this.keyboard = keyboard;
        }

        public void Bind(Action<char> nextTextInput, Action<string> nextCompositionChanged)
        {
            textInput = nextTextInput;
            compositionChanged = nextCompositionChanged;
            compositionHandler = composition => compositionChanged?.Invoke(composition.ToString());
            keyboard.onTextInput += OnTextInput;
            keyboard.onIMECompositionChange += compositionHandler;
        }

        public void Unbind()
        {
            keyboard.onTextInput -= OnTextInput;
            if (compositionHandler != null)
            {
                keyboard.onIMECompositionChange -= compositionHandler;
            }
            textInput = null;
            compositionChanged = null;
            compositionHandler = null;
        }

        public bool GetKey(KeyCode key)
        {
            return GetControl(key)?.isPressed == true;
        }

        public bool GetKeyDown(KeyCode key)
        {
            return GetControl(key)?.wasPressedThisFrame == true;
        }

        public bool GetKeyUp(KeyCode key)
        {
            return GetControl(key)?.wasReleasedThisFrame == true;
        }

        public void SetImeEnabled(bool enabled)
        {
            keyboard.SetIMEEnabled(enabled);
        }

        public void SetImeCursorPosition(Vector2 position)
        {
            keyboard.SetIMECursorPosition(position);
        }

        private KeyControl? GetControl(KeyCode key)
        {
            return keyboard[UnityInputSystemSource.MapKey(key)];
        }

        private void OnTextInput(char character)
        {
            textInput?.Invoke(character);
        }
    }
}
