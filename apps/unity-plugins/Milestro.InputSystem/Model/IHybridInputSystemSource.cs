using System;
using UnityEngine;

namespace Milestro.InputSystem.Model
{
    internal interface IHybridInputSystemSource
    {
        event Action<char, double>? TextInput;
        event Action<string, double>? CompositionChanged;
        event Action<KeyCode, bool, double>? KeyStateChanged;
        event Action? DeviceChanged;

        void Start();
        void Stop();
        void Refresh();
        bool GetKey(KeyCode key);
        bool GetKeyDown(KeyCode key);
        bool GetKeyUp(KeyCode key);
        void SetImeEnabled(bool enabled);
        void SetImeCursorPosition(Vector2 position);
    }
}
