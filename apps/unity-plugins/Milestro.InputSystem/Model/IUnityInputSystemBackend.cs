using System;

namespace Milestro.InputSystem.Model
{
    internal interface IUnityInputSystemBackend
    {
        IUnityInputSystemKeyboard? CurrentKeyboard { get; }
        void Start(Action deviceChanged, Action afterUpdate);
        void Stop(Action deviceChanged, Action afterUpdate);
    }
}
