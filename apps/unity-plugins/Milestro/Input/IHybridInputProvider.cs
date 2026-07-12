using System.Collections.Generic;
using UnityEngine;

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

    internal interface IHybridInputFrameSink
    {
        void OnInputFrame(HybridInputFrame frame);
        void OnInputReset(HybridInputResetReason reason);
    }
}
