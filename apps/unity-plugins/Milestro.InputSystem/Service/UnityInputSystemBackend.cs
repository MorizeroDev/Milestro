using System;
using Milestro.InputSystem.Model;
using UnityEngine.InputSystem;

namespace Milestro.InputSystem.Service
{
    internal sealed class UnityInputSystemBackend : IUnityInputSystemBackend
    {
        private Keyboard? rawKeyboard;
        private UnityInputSystemKeyboard? wrappedKeyboard;
        private event Action? DeviceChanged;
        private event Action? AfterUpdate;

        public IUnityInputSystemKeyboard? CurrentKeyboard
        {
            get
            {
                var current = Keyboard.current;
                if (!ReferenceEquals(rawKeyboard, current))
                {
                    rawKeyboard = current;
                    wrappedKeyboard = current == null ? null : new UnityInputSystemKeyboard(current);
                }
                return wrappedKeyboard;
            }
        }

        public void Start(Action deviceChanged, Action afterUpdate)
        {
            if (DeviceChanged == null)
            {
                UnityEngine.InputSystem.InputSystem.onDeviceChange += OnDeviceChange;
            }
            if (AfterUpdate == null)
            {
                UnityEngine.InputSystem.InputSystem.onAfterUpdate += OnAfterUpdate;
            }
            DeviceChanged += deviceChanged;
            AfterUpdate += afterUpdate;
        }

        public void Stop(Action deviceChanged, Action afterUpdate)
        {
            DeviceChanged -= deviceChanged;
            AfterUpdate -= afterUpdate;
            if (DeviceChanged == null)
            {
                UnityEngine.InputSystem.InputSystem.onDeviceChange -= OnDeviceChange;
            }
            if (AfterUpdate == null)
            {
                UnityEngine.InputSystem.InputSystem.onAfterUpdate -= OnAfterUpdate;
            }
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            DeviceChanged?.Invoke();
        }

        private void OnAfterUpdate()
        {
            AfterUpdate?.Invoke();
        }
    }
}
