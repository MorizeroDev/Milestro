using System;
using UnityEngine;

namespace Milestro.InputSystem.Model
{
    internal interface IUnityInputSystemKeyboard
    {
        void Bind(Action<char> textInput, Action<string> compositionChanged);
        void Unbind();
        bool GetKey(KeyCode key);
        bool GetKeyDown(KeyCode key);
        bool GetKeyUp(KeyCode key);
        void SetImeEnabled(bool enabled);
        void SetImeCursorPosition(Vector2 position);
    }
}
