using System.Collections.Generic;
using Milestro.Input;
using Milestro.InputSystem.Model;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Milestro.InputSystem.Service
{
    /// <summary>Provides keyboard, text, IME, and delta-only scroll capability through Input System.</summary>
    internal sealed class HybridInputSystemProvider : IHybridInputProvider, IHybridScrollInputProvider
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
        private IHybridInputEventSink? sink;

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
            sink = eventSink;
            pressedKeys.Clear();
            source.TextInput += OnTextInput;
            source.CompositionChanged += OnCompositionChanged;
            source.DeviceChanged += OnDeviceChanged;
            source.Start();
        }

        public void Stop()
        {
            source.TextInput -= OnTextInput;
            source.CompositionChanged -= OnCompositionChanged;
            source.DeviceChanged -= OnDeviceChanged;
            source.Stop();
            sink = null;
            pressedKeys.Clear();
        }

        public void Collect(HybridInputCollectContext context)
        {
            var eventSink = sink;
            if (eventSink == null)
            {
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
                if (source.GetKeyDown(key))
                {
                    eventSink.Enqueue(HybridInputEvent.KeyState(key, true, context.UnscaledTime));
                }
                if (source.GetKeyUp(key))
                {
                    eventSink.Enqueue(HybridInputEvent.KeyState(key, false, context.UnscaledTime));
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

        private void OnTextInput(char character, double timestamp)
        {
            sink?.Enqueue(HybridInputEvent.CommittedText(character.ToString(), timestamp));
        }

        private void OnCompositionChanged(string composition, double timestamp)
        {
            sink?.Enqueue(HybridInputEvent.Composition(composition, timestamp));
        }

        private void OnDeviceChanged()
        {
            sink?.ResetDeviceState();
        }
    }
}
