using System;
using Milestro.Input;
using Milestro.InputSystem.Model;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Milestro.InputSystem.Service
{
    internal sealed class UnityInputSystemSource : IHybridInputSystemSource
    {
        private readonly IUnityInputSystemBackend backend;
        private readonly IHybridInputImeSessionCanceller imeSessionCanceller;
        private IUnityInputSystemKeyboard? keyboard;
        private bool hasImeIntent;
        private bool imeEnabled;
        private bool imeAppliedEnabled;
        private bool hasImeCursorIntent;
        private Vector2 imeCursorPosition;
        private bool hasAppliedImeCursor;
        private Vector2 appliedImeCursorPosition;
        private bool compositionActive;
        private bool imeSessionQuiescing;
        private bool imeSessionCanCompleteAfterUpdate = true;
        private bool keyboardChangePending;
        private long deviceGeneration;
        private long imeSessionGeneration;
        private Action<char>? textInputHandler;
        private Action<string>? compositionHandler;

        internal UnityInputSystemSource()
            : this(new UnityInputSystemBackend(), HybridInputNativeImeSessionCanceller.Shared)
        {
        }

        internal UnityInputSystemSource(IUnityInputSystemBackend backend)
            : this(backend, HybridInputNoopImeSessionCanceller.Shared)
        {
        }

        internal UnityInputSystemSource(IUnityInputSystemBackend backend,
            IHybridInputImeSessionCanceller imeSessionCanceller)
        {
            this.backend = backend;
            this.imeSessionCanceller = imeSessionCanceller;
        }

        public event Action<char, double>? TextInput;
        public event Action<string, double>? CompositionChanged;
        public event Action? DeviceChanged;

        public void Start()
        {
            backend.Start(OnDeviceChange, OnAfterUpdate);
            RebindKeyboard(notify: false);
        }

        public void Stop()
        {
            backend.Stop(OnDeviceChange, OnAfterUpdate);
            UnbindKeyboard();
            keyboardChangePending = false;
            hasImeIntent = false;
            imeEnabled = false;
            imeAppliedEnabled = false;
            hasImeCursorIntent = false;
            imeCursorPosition = default;
            hasAppliedImeCursor = false;
            appliedImeCursorPosition = default;
            compositionActive = false;
            imeSessionQuiescing = false;
            imeSessionCanCompleteAfterUpdate = true;
        }

        public void Refresh()
        {
            if (keyboardChangePending)
            {
                keyboardChangePending = false;
                RebindKeyboard(notify: true, forceNotify: true);
                return;
            }

            if (!ReferenceEquals(keyboard, backend.CurrentKeyboard))
            {
                RebindKeyboard(notify: true);
            }
        }

        public bool GetKey(KeyCode key)
        {
            return keyboard?.GetKey(key) == true;
        }

        public bool GetKeyDown(KeyCode key)
        {
            return keyboard?.GetKeyDown(key) == true;
        }

        public bool GetKeyUp(KeyCode key)
        {
            return keyboard?.GetKeyUp(key) == true;
        }

        public void SetImeEnabled(bool enabled)
        {
            var hadEnabledSession = imeAppliedEnabled || (hasImeIntent && imeEnabled) || compositionActive;
            hasImeIntent = true;
            imeEnabled = enabled;
            if (keyboard == null)
            {
                return;
            }

            if (!enabled)
            {
                if (hadEnabledSession || imeSessionQuiescing)
                {
                    EndImeSession();
                }
                return;
            }

            if (!imeSessionQuiescing)
            {
                ApplyImeIntent();
            }
        }

        public void SetImeCursorPosition(Vector2 position)
        {
            hasImeCursorIntent = true;
            imeCursorPosition = position;
            if (keyboard != null && !imeSessionQuiescing && imeAppliedEnabled &&
                (!hasAppliedImeCursor || appliedImeCursorPosition != position))
            {
                keyboard.SetImeCursorPosition(position);
                hasAppliedImeCursor = true;
                appliedImeCursorPosition = position;
            }
        }

        private void OnDeviceChange()
        {
            if (keyboardChangePending || ReferenceEquals(keyboard, backend.CurrentKeyboard))
            {
                return;
            }

            // Removal callbacks run before Input System promotes a fallback keyboard.
            // Invalidate the old binding now, then observe the final current keyboard in Refresh.
            if (keyboard != null && backend.CurrentKeyboard == null)
            {
                DisableOldKeyboardIme();
                UnbindKeyboard();
                keyboardChangePending = true;
                return;
            }

            RebindKeyboard(notify: true);
        }

        private void RebindKeyboard(bool notify, bool forceNotify = false)
        {
            var nextKeyboard = backend.CurrentKeyboard;
            if (ReferenceEquals(keyboard, nextKeyboard))
            {
                if (notify && forceNotify)
                {
                    DeviceChanged?.Invoke();
                }
                return;
            }

            DisableOldKeyboardIme();
            UnbindKeyboard();
            compositionActive = false;
            imeSessionQuiescing = false;
            imeAppliedEnabled = false;
            hasAppliedImeCursor = false;
            keyboard = nextKeyboard;
            if (keyboard != null)
            {
                ++deviceGeneration;
                ++imeSessionGeneration;
                BindKeyboardCallbacks();
            }
            if (notify)
            {
                DeviceChanged?.Invoke();
            }
        }

        private void DisableOldKeyboardIme()
        {
            if (keyboard != null && imeAppliedEnabled)
            {
                keyboard.SetImeEnabled(false);
                imeAppliedEnabled = false;
            }
        }

        private void UnbindKeyboard()
        {
            ++deviceGeneration;
            ++imeSessionGeneration;
            if (keyboard != null)
            {
                keyboard.Unbind();
                keyboard = null;
            }
            textInputHandler = null;
            compositionHandler = null;
        }

        private void BindKeyboardCallbacks()
        {
            var boundKeyboard = keyboard;
            if (boundKeyboard == null)
            {
                return;
            }

            var bindingGeneration = deviceGeneration;
            var sessionGeneration = imeSessionGeneration;
            textInputHandler = character =>
                OnKeyboardTextInput(boundKeyboard, bindingGeneration, sessionGeneration, character);
            compositionHandler = composition =>
                OnKeyboardCompositionChanged(boundKeyboard, bindingGeneration, sessionGeneration, composition);
            boundKeyboard.Bind(textInputHandler, compositionHandler);
        }

        private void EndImeSession()
        {
            if (imeSessionQuiescing)
            {
                return;
            }

            // Invalidate callbacks before disabling: IME-off may synchronously emit the empty-composition ack.
            var shouldQuiesce = compositionActive;
            ++imeSessionGeneration;
            compositionActive = false;
            imeSessionQuiescing = shouldQuiesce;
            imeSessionCanCompleteAfterUpdate = true;
            if (shouldQuiesce && imeSessionCanceller.IsRequired)
            {
                var result = imeSessionCanceller.CancelComposition();
                HybridInputImeCancellationDiagnostics.Record(result);
                imeSessionCanCompleteAfterUpdate = result == HybridInputImeCancellationResult.Succeeded;
            }
            if (imeAppliedEnabled)
            {
                imeAppliedEnabled = false;
                keyboard?.SetImeEnabled(false);
            }
            hasAppliedImeCursor = false;

            if (shouldQuiesce)
            {
                return;
            }

            RebindImeSession();
        }

        private void CompleteImeSessionQuiesce()
        {
            if (!imeSessionQuiescing)
            {
                return;
            }

            // Only an empty ack or the next Input System after-update boundary can rotate to the new session.
            imeSessionQuiescing = false;
            imeSessionCanCompleteAfterUpdate = true;
            RebindImeSession();
            ApplyImeIntent();
        }

        private void RebindImeSession()
        {
            var boundKeyboard = keyboard;
            if (boundKeyboard == null)
            {
                return;
            }

            boundKeyboard.Unbind();
            ++imeSessionGeneration;
            hasAppliedImeCursor = false;
            BindKeyboardCallbacks();
        }

        private void ApplyImeIntent()
        {
            var boundKeyboard = keyboard;
            if (boundKeyboard == null || !hasImeIntent || imeSessionQuiescing)
            {
                return;
            }

            if (imeAppliedEnabled != imeEnabled)
            {
                boundKeyboard.SetImeEnabled(imeEnabled);
                imeAppliedEnabled = imeEnabled;
            }
            if (imeEnabled && hasImeCursorIntent &&
                (!hasAppliedImeCursor || appliedImeCursorPosition != imeCursorPosition))
            {
                boundKeyboard.SetImeCursorPosition(imeCursorPosition);
                hasAppliedImeCursor = true;
                appliedImeCursorPosition = imeCursorPosition;
            }
        }

        private void OnAfterUpdate()
        {
            if (imeSessionCanCompleteAfterUpdate)
            {
                CompleteImeSessionQuiesce();
            }
        }

        private void OnKeyboardTextInput(IUnityInputSystemKeyboard boundKeyboard,
            long bindingGeneration,
            long sessionGeneration,
            char character)
        {
            if (bindingGeneration == deviceGeneration && sessionGeneration == imeSessionGeneration &&
                ReferenceEquals(keyboard, boundKeyboard))
            {
                compositionActive = false;
                TextInput?.Invoke(character, Time.unscaledTimeAsDouble);
            }
        }

        private void OnKeyboardCompositionChanged(IUnityInputSystemKeyboard boundKeyboard,
            long bindingGeneration,
            long sessionGeneration,
            string composition)
        {
            if (bindingGeneration != deviceGeneration || !ReferenceEquals(keyboard, boundKeyboard))
            {
                return;
            }
            if (imeSessionQuiescing && string.IsNullOrEmpty(composition))
            {
                CompleteImeSessionQuiesce();
                return;
            }
            if (sessionGeneration == imeSessionGeneration)
            {
                compositionActive = !string.IsNullOrEmpty(composition);
                CompositionChanged?.Invoke(composition, Time.unscaledTimeAsDouble);
            }
        }

        internal static Key MapKey(KeyCode key)
        {
            return key switch
            {
                KeyCode.A => Key.A,
                KeyCode.Backspace => Key.Backspace,
                KeyCode.C => Key.C,
                KeyCode.Delete => Key.Delete,
                KeyCode.DownArrow => Key.DownArrow,
                KeyCode.End => Key.End,
                KeyCode.Home => Key.Home,
                KeyCode.Insert => Key.Insert,
                KeyCode.KeypadEnter => Key.NumpadEnter,
                KeyCode.LeftAlt => Key.LeftAlt,
                KeyCode.LeftArrow => Key.LeftArrow,
                KeyCode.LeftCommand => Key.LeftCommand,
                KeyCode.LeftControl => Key.LeftCtrl,
                KeyCode.LeftShift => Key.LeftShift,
                KeyCode.PageDown => Key.PageDown,
                KeyCode.PageUp => Key.PageUp,
                KeyCode.Return => Key.Enter,
                KeyCode.RightAlt => Key.RightAlt,
                KeyCode.RightArrow => Key.RightArrow,
                KeyCode.RightCommand => Key.RightCommand,
                KeyCode.RightControl => Key.RightCtrl,
                KeyCode.RightShift => Key.RightShift,
                KeyCode.UpArrow => Key.UpArrow,
                KeyCode.V => Key.V,
                KeyCode.X => Key.X,
                KeyCode.Y => Key.Y,
                KeyCode.Z => Key.Z,
                _ => Key.None
            };
        }
    }
}
