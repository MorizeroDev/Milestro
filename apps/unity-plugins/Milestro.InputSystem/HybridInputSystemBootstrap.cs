using System;
using Milestro.Input;
using Milestro.InputSystem.Service;
using UnityEngine;

namespace Milestro.InputSystem
{
    /// <summary>Registers the optional Input System provider after Core subsystem reset.</summary>
    internal static class HybridInputSystemBootstrap
    {
        private static IDisposable? registration;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistration()
        {
            registration?.Dispose();
            registration = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterProvider()
        {
            registration?.Dispose();
            registration = HybridInputRuntime.RegisterProvider(new HybridInputSystemProvider());
        }
    }
}
