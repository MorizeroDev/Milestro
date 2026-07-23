using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Milestro.Input
{
    /// <summary>Owns the process-wide input dispatcher, provider registry, and PlayerLoop drain.</summary>
    public static class HybridInputRuntime
    {
        private static readonly HybridInputDispatcher Dispatcher = new HybridInputDispatcher();
        private static bool playerLoopInstalled;

        public static HybridInputDiagnostics Diagnostics => Dispatcher.Diagnostics.WithInputSystemPackageStatus(
            HybridInputSystemCompatibility.PackageStatus);
        public static bool IsPlayerLoopInstalled => playerLoopInstalled;

        public static bool IsKeyPressed(KeyCode key)
        {
            return Dispatcher.IsKeyPressed(key);
        }

        /// <summary>Resolves metadata for the delta already carried by this uGUI event.</summary>
        public static HybridScrollInput ResolveScrollInput(PointerEventData eventData)
        {
            if (eventData == null)
            {
                throw new ArgumentNullException(nameof(eventData));
            }

            return Dispatcher.ResolveScrollInput(eventData);
        }

        public static IDisposable RegisterProvider(IHybridInputProvider provider)
        {
            return new ProviderRegistration(Dispatcher.RegisterProvider(provider));
        }

        public static void SetProviderOverride(string? providerId)
        {
            Dispatcher.SetProviderOverride(providerId);
        }

        internal static HybridInputDispatcher.HybridInputSinkRegistration RegisterSink(IHybridInputFrameSink sink)
        {
            return Dispatcher.RegisterSink(sink);
        }

        internal static void NotifyValueChanged(IHybridInputLifecycleSink sink, string value, bool sessionBound)
        {
            Dispatcher.NotifyValueChanged(sink, value, sessionBound);
        }

        internal static void ResetAndInitialize()
        {
            HybridInputPlayerLoop.Uninstall();
            playerLoopInstalled = false;
            Dispatcher.Reset();
            HybridInputSystemCompatibility.ResetRuntimeReport();
#if ENABLE_LEGACY_INPUT_MANAGER
            Dispatcher.RegisterProvider(new HybridInputLegacyProvider());
#endif
            playerLoopInstalled = HybridInputPlayerLoop.Install(Drain);
        }

        internal static void ResetState()
        {
            Dispatcher.Reset();
        }

        private static void Drain()
        {
            var environment = CaptureEnvironment();
            HybridInputSystemCompatibility.ReportRuntimeIssueIfNeeded(environment);
            Dispatcher.RefreshEnvironment(environment);
            Dispatcher.Drain(Time.frameCount, Time.unscaledTimeAsDouble);
        }

        private static HybridInputEnvironment CaptureEnvironment()
        {
            var eventSystem = EventSystem.current;
            BaseInputModule? activeModule = null;
            if (eventSystem != null && eventSystem.isActiveAndEnabled)
            {
                var currentModule = eventSystem.currentInputModule;
                if (currentModule != null && currentModule.isActiveAndEnabled)
                {
                    activeModule = currentModule;
                }
            }

            return new HybridInputEnvironment(activeModule,
                CountActiveEventSystems(),
                Application.isFocused);
        }

        private static int CountActiveEventSystems()
        {
#if UNITY_2023_1_OR_NEWER
            var eventSystems = UnityEngine.Object.FindObjectsByType<EventSystem>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
#else
            var eventSystems = UnityEngine.Object.FindObjectsOfType<EventSystem>();
#endif
            var count = 0;
            for (var i = 0; i < eventSystems.Length; ++i)
            {
                if (eventSystems[i].isActiveAndEnabled)
                {
                    ++count;
                }
            }
            return count;
        }

        private sealed class ProviderRegistration : IDisposable
        {
            private HybridInputProviderHandle? handle;

            internal ProviderRegistration(HybridInputProviderHandle handle)
            {
                this.handle = handle;
            }

            public void Dispose()
            {
                var registeredHandle = handle;
                handle = null;
                if (registeredHandle.HasValue)
                {
                    Dispatcher.UnregisterProvider(registeredHandle.Value);
                }
            }
        }
    }
}
