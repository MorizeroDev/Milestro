using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Milestro.Input;
using UnityEngine;

namespace Milestro.Tests.TextInputLifecycle.Integration
{
    public sealed class TextInputLifecycleIntegrationStrictProvider : IHybridInputProvider,
        IHybridInputFocusSessionProvider
    {
        public const string ProviderId = "milestro-task159-integration";

        private IHybridInputEventSink? sessionSink;

        public string Id => ProviderId;
        public int Priority => int.MaxValue;
        public HybridInputProviderKind Kind => HybridInputProviderKind.Custom;
        public HybridInputCapabilities Capabilities => HybridInputCapabilities.KeyState |
                                                       HybridInputCapabilities.CommittedText |
                                                       HybridInputCapabilities.Composition |
                                                       HybridInputCapabilities.ImeControl;
        public HybridScrollCapability ScrollCapability => HybridScrollCapability.Unsupported;
        public bool HasFocusSession => sessionSink != null;
        public int BeginCount { get; private set; }
        public int EndCount { get; private set; }
        public int SessionGeneration { get; private set; }
        public int ActiveSessionGeneration { get; private set; }
        public int ActiveSinkIdentity => sessionSink == null
            ? 0
            : RuntimeHelpers.GetHashCode(sessionSink);

        public HybridInputProviderMatch Match(HybridInputEnvironment environment)
        {
            return environment.EventSystemCount == 1 &&
                   environment.ActiveModule is TextInputLifecycleIntegrationInputModule
                ? HybridInputProviderMatch.Exact
                : HybridInputProviderMatch.None;
        }

        public void Start(IHybridInputEventSink sink)
        {
        }

        public void Stop()
        {
            sessionSink = null;
            ActiveSessionGeneration = 0;
        }

        public void Collect(HybridInputCollectContext context)
        {
        }

        public void SetImeEnabled(bool enabled)
        {
        }

        public void SetImeCursorPosition(Vector2 position)
        {
        }

        public void BeginFocusSession(IHybridInputEventSink sink)
        {
            sessionSink = sink;
            ++BeginCount;
            ActiveSessionGeneration = ++SessionGeneration;
        }

        public void EndFocusSession()
        {
            ++EndCount;
            sessionSink = null;
            ActiveSessionGeneration = 0;
        }

        public void EnqueueCommittedText(string value)
        {
            if (sessionSink == null)
            {
                throw new System.InvalidOperationException("No focused TextInput Integration session is active.");
            }

            sessionSink.Enqueue(HybridInputEvent.CommittedText(value, Time.unscaledTimeAsDouble));
        }

        public void ReplacePressedKeys(IReadOnlyList<KeyCode> pressedKeys)
        {
            if (sessionSink == null)
            {
                throw new System.InvalidOperationException("No focused TextInput Integration session is active.");
            }

            sessionSink.ReplacePressedKeys(pressedKeys);
        }
    }
}
