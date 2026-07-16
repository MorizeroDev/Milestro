using System.Collections.Generic;
using Milestro.Input;
using Milestro.InputSystem.Model;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Milestro.InputSystem.Service
{
    /// <summary>Provides keyboard, text, IME, and delta-only scroll capability through Input System.</summary>
    internal sealed class HybridInputSystemProvider :
        IHybridInputProvider,
        IHybridInputFocusSessionProvider,
        IHybridScrollInputProvider
    {
        internal const string ProviderId = "input-system";

        private static readonly KeyCode[] TrackedKeys =
        {
            KeyCode.A,
            KeyCode.Backspace,
            KeyCode.C,
            KeyCode.Delete,
            KeyCode.DownArrow,
            KeyCode.End,
            KeyCode.Home,
            KeyCode.Insert,
            KeyCode.KeypadEnter,
            KeyCode.LeftAlt,
            KeyCode.LeftArrow,
            KeyCode.LeftCommand,
            KeyCode.LeftControl,
            KeyCode.LeftShift,
            KeyCode.PageDown,
            KeyCode.PageUp,
            KeyCode.Return,
            KeyCode.RightAlt,
            KeyCode.RightArrow,
            KeyCode.RightCommand,
            KeyCode.RightControl,
            KeyCode.RightShift,
            KeyCode.UpArrow,
            KeyCode.V,
            KeyCode.X,
            KeyCode.Y,
            KeyCode.Z
        };

        private readonly IHybridInputSystemSource source;
        private readonly List<KeyCode> pressedKeys = new List<KeyCode>();
        private IHybridInputEventSink? providerSink;
        private IHybridInputEventSink? sessionSink;
        private System.Action<char, double>? sessionTextInput;
        private System.Action<string, double>? sessionCompositionChanged;
        private System.Action<KeyCode, bool, double>? sessionKeyStateChanged;

        internal HybridInputSystemProvider()
            : this(new UnityInputSystemSource())
        {
        }

        internal HybridInputSystemProvider(IHybridInputSystemSource source)
        {
            this.source = source;
        }

        public string Id => ProviderId;
        public int Priority => 0;
        public HybridInputProviderKind Kind => HybridInputProviderKind.InputSystem;
        public HybridInputCapabilities Capabilities => HybridInputCapabilities.KeyState |
                                                       HybridInputCapabilities.CommittedText |
                                                       HybridInputCapabilities.Composition |
                                                       HybridInputCapabilities.ImeControl |
                                                       HybridInputCapabilities.ScrollDelta;
        public HybridScrollCapability ScrollCapability => HybridScrollCapability.DeltaOnly;

        public HybridInputProviderMatch Match(HybridInputEnvironment environment)
        {
            return environment.EventSystemCount == 1 &&
                   environment.ActiveModule != null &&
                   environment.ActiveModule.GetType() == typeof(InputSystemUIInputModule)
                ? HybridInputProviderMatch.Exact
                : HybridInputProviderMatch.None;
        }

        public bool TryResolveScrollInput(PointerEventData eventData, out HybridScrollInput scrollInput)
        {
            if (eventData == null)
            {
                scrollInput = default;
                return false;
            }

            scrollInput = new HybridScrollInput(eventData.scrollDelta,
                new HybridScrollMetadata(HybridScrollCapability.DeltaOnly,
                    HybridInputDeviceKind.Unknown,
                    HybridInputPhase.Unknown,
                    HybridInputPhase.Unknown,
                    Time.unscaledTimeAsDouble,
                    0L));
            return true;
        }

        public void Start(IHybridInputEventSink eventSink)
        {
            providerSink = eventSink;
            pressedKeys.Clear();
            source.DeviceChanged += OnDeviceChanged;
            source.Start();
        }

        public void Stop()
        {
            EndFocusSession();
            source.DeviceChanged -= OnDeviceChanged;
            source.Stop();
            providerSink = null;
            pressedKeys.Clear();
        }

        public void BeginFocusSession(IHybridInputEventSink nextSessionSink)
        {
            if (nextSessionSink == null)
            {
                throw new System.ArgumentNullException(nameof(nextSessionSink));
            }

            EndFocusSession();
            sessionSink = nextSessionSink;
            sessionTextInput = (character, timestamp) =>
                nextSessionSink.Enqueue(HybridInputEvent.CommittedText(character.ToString(), timestamp));
            sessionCompositionChanged = (composition, timestamp) =>
                nextSessionSink.Enqueue(HybridInputEvent.Composition(composition, timestamp));
            sessionKeyStateChanged = (key, pressed, timestamp) =>
                nextSessionSink.Enqueue(HybridInputEvent.KeyState(key, pressed, timestamp));
            source.TextInput += sessionTextInput;
            source.CompositionChanged += sessionCompositionChanged;
            source.KeyStateChanged += sessionKeyStateChanged;
        }

        public void EndFocusSession()
        {
            if (sessionTextInput != null)
            {
                source.TextInput -= sessionTextInput;
                sessionTextInput = null;
            }
            if (sessionCompositionChanged != null)
            {
                source.CompositionChanged -= sessionCompositionChanged;
                sessionCompositionChanged = null;
            }
            if (sessionKeyStateChanged != null)
            {
                source.KeyStateChanged -= sessionKeyStateChanged;
                sessionKeyStateChanged = null;
            }
            sessionSink = null;
        }

        public void Collect(HybridInputCollectContext context)
        {
            // Key edges arrive through session-captured source callbacks. Collection
            // only snapshots held state for the already selected session.
            var eventSink = sessionSink;
            if (eventSink == null)
            {
                source.Refresh();
                pressedKeys.Clear();
                for (var i = 0; i < TrackedKeys.Length; ++i)
                {
                    var key = TrackedKeys[i];
                    if (source.GetKey(key))
                    {
                        pressedKeys.Add(key);
                    }
                }
                providerSink?.ReplacePressedKeys(pressedKeys);
                return;
            }

            source.Refresh();
            pressedKeys.Clear();
            for (var i = 0; i < TrackedKeys.Length; ++i)
            {
                var key = TrackedKeys[i];
                if (source.GetKey(key))
                {
                    pressedKeys.Add(key);
                }
            }
            eventSink.ReplacePressedKeys(pressedKeys);
        }

        public void SetImeEnabled(bool enabled)
        {
            source.Refresh();
            source.SetImeEnabled(enabled);
        }

        public void SetImeCursorPosition(Vector2 position)
        {
            source.Refresh();
            source.SetImeCursorPosition(position);
        }

        private void OnDeviceChanged()
        {
            providerSink?.ResetDeviceState();
        }
    }
}
