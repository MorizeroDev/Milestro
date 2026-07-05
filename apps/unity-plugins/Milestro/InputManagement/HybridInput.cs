using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Milestro.InputManagement
{
    public class HybridInput
    {
        public static bool GetKey(KeyCode key)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(key);
#elif ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                return keyboard[GetNewKey(key)].isPressed;
            }

            return false;
#endif
        }

        public static bool GetKeyDown(KeyCode key)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(key);
#elif ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                return keyboard[GetNewKey(key)].wasPressedThisFrame;
            }

            return false;
#endif
        }

        public static bool GetKeyUp(KeyCode key)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyUp(key);
#elif ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                return keyboard[GetNewKey(key)].wasReleasedThisFrame;
            }

            return false;
#endif
        }

        public static void SetIMEEnabled(bool enabled)
        {
            Input.imeCompositionMode = enabled ? IMECompositionMode.On : IMECompositionMode.Off;

#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            keyboard.SetIMEEnabled(enabled);
#endif
        }

        public static void SetIMECursorPosition(Vector2 position)
        {
            Input.compositionCursorPos = position;

#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            keyboard.SetIMECursorPosition(position);
#endif
        }
#if ENABLE_INPUT_SYSTEM
        public static Key GetNewKey(KeyCode key)
        {
            return key switch
            {
                KeyCode.Keypad0 => Key.Numpad0,
                KeyCode.LeftArrow => Key.LeftArrow,
                KeyCode.RightArrow => Key.RightArrow,
                KeyCode.UpArrow => Key.UpArrow,
                KeyCode.DownArrow => Key.DownArrow,
                KeyCode.Home => Key.Home,
                KeyCode.End => Key.End,
                KeyCode.Backspace => Key.Backspace,
                KeyCode.Delete => Key.Delete,
                KeyCode.None => Key.None,
                KeyCode.Insert => Key.Insert,
                KeyCode.Return => Key.Enter,
                KeyCode.KeypadEnter => Key.NumpadEnter,
                KeyCode.LeftShift => Key.LeftShift,
                KeyCode.RightShift => Key.RightShift,
                KeyCode.LeftControl => Key.LeftCtrl,
                KeyCode.RightControl => Key.RightCtrl,
                KeyCode.LeftCommand => Key.LeftCommand,
                KeyCode.RightCommand => Key.RightCommand,
                KeyCode.LeftAlt => Key.LeftAlt,
                KeyCode.RightAlt => Key.RightAlt,
                KeyCode.PageUp => Key.PageUp,
                KeyCode.PageDown => Key.PageDown,
                KeyCode.A => Key.A,
                KeyCode.B => Key.B,
                KeyCode.C => Key.C,
                KeyCode.D => Key.D,
                KeyCode.E => Key.E,
                KeyCode.F => Key.F,
                KeyCode.G => Key.G,
                KeyCode.H => Key.H,
                KeyCode.I => Key.I,
                KeyCode.J => Key.J,
                KeyCode.K => Key.K,
                KeyCode.L => Key.L,
                KeyCode.M => Key.M,
                KeyCode.N => Key.N,
                KeyCode.O => Key.O,
                KeyCode.P => Key.P,
                KeyCode.Q => Key.Q,
                KeyCode.R => Key.R,
                KeyCode.S => Key.S,
                KeyCode.T => Key.T,
                KeyCode.U => Key.U,
                KeyCode.V => Key.V,
                KeyCode.W => Key.W,
                KeyCode.X => Key.X,
                KeyCode.Y => Key.Y,
                KeyCode.Z => Key.Z,
                KeyCode.Tab => Key.Tab,
                KeyCode.Clear => Key.Delete,
                KeyCode.Pause => Key.Pause,
                KeyCode.Escape => Key.Escape,
                KeyCode.Space => Key.Space,
                KeyCode.Keypad1 => Key.Numpad1,
                KeyCode.Keypad2 => Key.Numpad2,
                KeyCode.Keypad3 => Key.Numpad3,
                KeyCode.Keypad4 => Key.Numpad4,
                KeyCode.Keypad5 => Key.Numpad5,
                KeyCode.Keypad6 => Key.Numpad6,
                KeyCode.Keypad7 => Key.Numpad7,
                KeyCode.Keypad8 => Key.Numpad8,
                KeyCode.Keypad9 => Key.Numpad9,
                KeyCode.KeypadPeriod => Key.NumpadPeriod,
                KeyCode.KeypadDivide => Key.NumpadDivide,
                KeyCode.KeypadMultiply => Key.NumpadMultiply,
                KeyCode.KeypadMinus => Key.NumpadMinus,
                KeyCode.KeypadPlus => Key.NumpadPlus,
                KeyCode.KeypadEquals => Key.NumpadEquals,
                KeyCode.F1 => Key.F1,
                KeyCode.F2 => Key.F2,
                KeyCode.F3 => Key.F3,
                KeyCode.F4 => Key.F4,
                KeyCode.F5 => Key.F5,
                KeyCode.F6 => Key.F6,
                KeyCode.F7 => Key.F7,
                KeyCode.F8 => Key.F8,
                KeyCode.F9 => Key.F9,
                KeyCode.F10 => Key.F10,
                KeyCode.F11 => Key.F11,
                KeyCode.F12 => Key.F12,
                KeyCode.F13 => Key.F13,
                KeyCode.F14 => Key.F14,
                KeyCode.F15 => Key.F15,
                KeyCode.Alpha0 => Key.Digit0,
                KeyCode.Alpha1 => Key.Digit1,
                KeyCode.Alpha2 => Key.Digit2,
                KeyCode.Alpha3 => Key.Digit3,
                KeyCode.Alpha4 => Key.Digit4,
                KeyCode.Alpha5 => Key.Digit5,
                KeyCode.Alpha6 => Key.Digit6,
                KeyCode.Alpha7 => Key.Digit7,
                KeyCode.Alpha8 => Key.Digit8,
                KeyCode.Alpha9 => Key.Digit9,
                KeyCode.Exclaim => Key.Digit1,
                KeyCode.DoubleQuote => Key.Quote,
                KeyCode.Hash => Key.Digit3,
                KeyCode.Dollar => Key.Digit4,
                KeyCode.Percent => Key.Digit5,
                KeyCode.Ampersand => Key.Digit7,
                KeyCode.Quote => Key.Quote,
                KeyCode.LeftParen => Key.Digit9,
                KeyCode.RightParen => Key.Digit0,
                KeyCode.Asterisk => Key.Digit8,
                KeyCode.Plus => Key.Equals,
                KeyCode.Comma => Key.Comma,
                KeyCode.Minus => Key.Minus,
                KeyCode.Period => Key.Period,
                KeyCode.Slash => Key.Slash,
                KeyCode.Colon => Key.Semicolon,
                KeyCode.Semicolon => Key.Semicolon,
                KeyCode.Less => Key.Comma,
                KeyCode.Equals => Key.Equals,
                KeyCode.Greater => Key.Period,
                KeyCode.Question => Key.Slash,
                KeyCode.At => Key.Digit2,
                KeyCode.LeftBracket => Key.LeftBracket,
                KeyCode.Backslash => Key.Backslash,
                KeyCode.RightBracket => Key.RightBracket,
                KeyCode.Caret => Key.Digit6,
                KeyCode.Underscore => Key.Minus,
                KeyCode.BackQuote => Key.Backquote,
                KeyCode.LeftCurlyBracket => Key.LeftBracket,
                KeyCode.Pipe => Key.Backslash,
                KeyCode.RightCurlyBracket => Key.RightBracket,
                KeyCode.Tilde => Key.Backquote,
                KeyCode.Numlock => Key.NumLock,
                KeyCode.CapsLock => Key.CapsLock,
                KeyCode.ScrollLock => Key.ScrollLock,
                KeyCode.LeftWindows => Key.LeftWindows,
                KeyCode.RightWindows => Key.RightWindows,
                KeyCode.AltGr => Key.AltGr,
                KeyCode.Help => Key.Numpad7,
                KeyCode.Print => Key.PrintScreen,
                KeyCode.SysReq => Key.PrintScreen,
                KeyCode.Break => Key.Pause,
                KeyCode.Menu => Key.LeftWindows,
                _ => throw new ArgumentOutOfRangeException(nameof(key), key, null)
            };
        }
#endif
    }
}