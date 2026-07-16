using System;
using Milestro.Input;
using Milestro.InputSystem.Model;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Milestro.InputSystem.Service
{
    internal sealed class UnityInputSystemSource : IHybridInputSystemSource
    {
        private static readonly KeyCode[] TrackedKeys =
        {
            KeyCode.A,
            KeyCode.Backspace,
            KeyCode.C,
            KeyCode.Delete,
            KeyCode.DownArrow,
            KeyCode.End,
            KeyCode.Home,
            KeyCode.Insert,
            KeyCode.KeypadEnter,
            KeyCode.LeftAlt,
            KeyCode.LeftArrow,
            KeyCode.LeftCommand,
            KeyCode.LeftControl,
            KeyCode.LeftShift,
            KeyCode.PageDown,
            KeyCode.PageUp,
            KeyCode.Return,
            KeyCode.RightAlt,
            KeyCode.RightArrow,
            KeyCode.RightCommand,
            KeyCode.RightControl,
            KeyCode.RightShift,
            KeyCode.UpArrow,
            KeyCode.V,
            KeyCode.X,
            KeyCode.Y,
            KeyCode.Z
        };

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
        private bool imeSessionAwaitingEmptyAck;
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
        public event Action<KeyCode, bool, double>? KeyStateChanged;
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
            imeSessionAwaitingEmptyAck = false;
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
                if (hadEnabledSession || imeSessionAwaitingEmptyAck)
                {
                    EndImeSession();
                }
                return;
            }

            if (!imeSessionAwaitingEmptyAck)
            {
                ApplyImeIntent();
            }
        }

        public void SetImeCursorPosition(Vector2 position)
        {
            hasImeCursorIntent = true;
            imeCursorPosition = position;
            if (keyboard != null && !imeSessionAwaitingEmptyAck && imeAppliedEnabled &&
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
            imeSessionAwaitingEmptyAck = false;
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
            if (imeSessionAwaitingEmptyAck)
            {
                return;
            }

            // Invalidate callbacks before disabling: IME-off may synchronously emit the empty-composition ack.
            var shouldAwaitEmptyAck = compositionActive;
            ++imeSessionGeneration;
            compositionActive = false;
            imeSessionAwaitingEmptyAck = shouldAwaitEmptyAck;
            var cancellationSucceeded = false;
            if (shouldAwaitEmptyAck && imeSessionCanceller.IsRequired)
            {
                var result = imeSessionCanceller.CancelComposition();
                HybridInputImeCancellationDiagnostics.Record(result);
                cancellationSucceeded = result == HybridInputImeCancellationResult.Succeeded;
            }
            if (imeAppliedEnabled)
            {
                imeAppliedEnabled = false;
                keyboard?.SetImeEnabled(false);
            }
            hasAppliedImeCursor = false;

            if (shouldAwaitEmptyAck)
            {
                if (cancellationSucceeded)
                {
                    CompleteImeSessionBoundary();
                }
                return;
            }

            RebindImeSession();
        }

        private void CompleteImeSessionBoundary()
        {
            if (!imeSessionAwaitingEmptyAck)
            {
                return;
            }

            // A failed cancellation remains closed until the source reports a real empty acknowledgement.
            imeSessionAwaitingEmptyAck = false;
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
            if (boundKeyboard == null || !hasImeIntent || imeSessionAwaitingEmptyAck)
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
            var boundKeyboard = keyboard;
            if (boundKeyboard == null || imeSessionAwaitingEmptyAck)
            {
                return;
            }

            var timestamp = Time.unscaledTimeAsDouble;
            for (var i = 0; i < TrackedKeys.Length; ++i)
            {
                var key = TrackedKeys[i];
                if (boundKeyboard.GetKeyDown(key))
                {
                    KeyStateChanged?.Invoke(key, true, timestamp);
                }
                if (boundKeyboard.GetKeyUp(key))
                {
                    KeyStateChanged?.Invoke(key, false, timestamp);
                }
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
            if (imeSessionAwaitingEmptyAck && string.IsNullOrEmpty(composition))
            {
                CompleteImeSessionBoundary();
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
