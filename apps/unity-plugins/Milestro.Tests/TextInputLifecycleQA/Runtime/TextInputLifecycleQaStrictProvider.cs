using System.Collections.Generic;
using Milestro.Input;
using UnityEngine;

namespace Milestro.TextInputLifecycleQA
{
    public sealed class TextInputLifecycleQaStrictProvider : IHybridInputProvider,
        IHybridInputFocusSessionProvider
    {
        public const string ProviderId = "milestro-task159-qa";

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

        public HybridInputProviderMatch Match(HybridInputEnvironment environment)
        {
            return environment.EventSystemCount == 1 &&
                   environment.ActiveModule is TextInputLifecycleQaInputModule
                ? HybridInputProviderMatch.Exact
                : HybridInputProviderMatch.None;
        }

        public void Start(IHybridInputEventSink sink)
        {
        }

        public void Stop()
        {
            sessionSink = null;
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
        }

        public void EndFocusSession()
        {
            sessionSink = null;
        }

        public void EnqueueCommittedText(string value)
        {
            if (sessionSink == null)
            {
                throw new System.InvalidOperationException("No focused TextInput QA session is active.");
            }

            sessionSink.Enqueue(HybridInputEvent.CommittedText(value, Time.unscaledTimeAsDouble));
        }

        public void ReplacePressedKeys(IReadOnlyList<KeyCode> pressedKeys)
        {
            if (sessionSink == null)
            {
                throw new System.InvalidOperationException("No focused TextInput QA session is active.");
            }

            sessionSink.ReplacePressedKeys(pressedKeys);
        }
    }
}
