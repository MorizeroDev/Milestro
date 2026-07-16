using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Milestro.Input
{
    public interface IHybridInputEventSink
    {
        void Enqueue(HybridInputEvent inputEvent);
        void ReplacePressedKeys(IReadOnlyList<KeyCode> pressedKeys);
        void ResetDeviceState();
    }

    public interface IHybridInputProvider
    {
        string Id { get; }
        int Priority { get; }
        HybridInputProviderKind Kind { get; }
        HybridInputCapabilities Capabilities { get; }
        HybridScrollCapability ScrollCapability { get; }

        HybridInputProviderMatch Match(HybridInputEnvironment environment);
        void Start(IHybridInputEventSink sink);
        void Stop();
        void Collect(HybridInputCollectContext context);
        void SetImeEnabled(bool enabled);
        void SetImeCursorPosition(Vector2 position);
    }

    /// <summary>
    /// Optionally enriches the scroll delta already delivered by uGUI for the same pointer event.
    /// Implementations must not read or introduce a second scroll delta source.
    /// </summary>
    public interface IHybridScrollInputProvider
    {
        bool TryResolveScrollInput(PointerEventData eventData, out HybridScrollInput scrollInput);
    }

    /// <summary>
    /// Captures focused input against an immutable dispatcher-owned session sink.
    /// Providers that publish focused text, composition, or key events must implement
    /// this contract before they can own strict TextInput focus.
    /// </summary>
    public interface IHybridInputFocusSessionProvider
    {
        void BeginFocusSession(IHybridInputEventSink sessionSink);
        void EndFocusSession();
    }

    internal interface IHybridInputFrameSink
    {
        void OnInputFrame(HybridInputFrame frame);
        void OnInputReset(HybridInputResetReason reason);
    }

    internal interface IHybridInputLifecycleSink : IHybridInputFrameSink
    {
        GameObject Owner { get; }
        bool IsActiveAndEnabled { get; }
        bool CanConsumeInputNow { get; }
        string CommittedText { get; }

        void OnFocusGained();
        void OnEndEdit(string finalText);
        void OnFocusLost();
        void OnValueChanged(string value);
    }
}
