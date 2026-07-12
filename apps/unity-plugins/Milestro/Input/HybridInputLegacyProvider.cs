#if ENABLE_LEGACY_INPUT_MANAGER
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Milestro.Input
{
    /// <summary>Provides basic keyboard, text, and IME input through Unity's legacy Input API.</summary>
    internal sealed class HybridInputLegacyProvider : IHybridInputProvider
    {
        internal const string ProviderId = "legacy";

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

        private readonly ILegacyInputSource source;
        private readonly IHybridInputImeSessionCanceller imeSessionCanceller;
        private readonly List<KeyCode> pressedKeys = new List<KeyCode>();
        private IHybridInputEventSink? sink;
        private string composition = string.Empty;
        private string observedComposition = string.Empty;
        private bool hasImeIntent;
        private bool imeEnabled;
        private bool imeAppliedEnabled;
        private bool imeSessionQuiescing;
        private bool imeSessionCanCompleteAtPollBoundary = true;
        private bool hasImeCursorIntent;
        private Vector2 imeCursorPosition;
        private bool hasAppliedImeCursor;
        private Vector2 appliedImeCursorPosition;

        internal HybridInputLegacyProvider()
            : this(new UnityLegacyInputSource(), HybridInputNativeImeSessionCanceller.Shared)
        {
        }

        internal HybridInputLegacyProvider(ILegacyInputSource source)
            : this(source, HybridInputNoopImeSessionCanceller.Shared)
        {
        }

        internal HybridInputLegacyProvider(ILegacyInputSource source,
            IHybridInputImeSessionCanceller imeSessionCanceller)
        {
            this.source = source;
            this.imeSessionCanceller = imeSessionCanceller;
        }

        public string Id => ProviderId;
        public int Priority => 0;
        public HybridInputProviderKind Kind => HybridInputProviderKind.Legacy;
        public HybridInputCapabilities Capabilities => HybridInputCapabilities.KeyState |
                                                       HybridInputCapabilities.CommittedText |
                                                       HybridInputCapabilities.Composition |
                                                       HybridInputCapabilities.ImeControl;
        public HybridScrollCapability ScrollCapability => HybridScrollCapability.Unsupported;

        public HybridInputProviderMatch Match(HybridInputEnvironment environment)
        {
            return environment.EventSystemCount == 1 &&
                   environment.ActiveModule != null &&
                   environment.ActiveModule.GetType() == typeof(StandaloneInputModule)
                ? HybridInputProviderMatch.Exact
                : HybridInputProviderMatch.None;
        }

        public void Start(IHybridInputEventSink eventSink)
        {
            sink = eventSink;
            composition = string.Empty;
            observedComposition = string.Empty;
            pressedKeys.Clear();
            hasImeIntent = false;
            imeEnabled = false;
            imeAppliedEnabled = false;
            imeSessionQuiescing = false;
            imeSessionCanCompleteAtPollBoundary = true;
            hasImeCursorIntent = false;
            imeCursorPosition = default;
            hasAppliedImeCursor = false;
            appliedImeCursorPosition = default;
        }

        public void Stop()
        {
            sink = null;
            composition = string.Empty;
            observedComposition = string.Empty;
            pressedKeys.Clear();
            hasImeIntent = false;
            imeEnabled = false;
            imeAppliedEnabled = false;
            imeSessionQuiescing = false;
            imeSessionCanCompleteAtPollBoundary = true;
            hasImeCursorIntent = false;
            imeCursorPosition = default;
            hasAppliedImeCursor = false;
            appliedImeCursorPosition = default;
        }

        public void Collect(HybridInputCollectContext context)
        {
            var eventSink = sink;
            if (eventSink == null)
            {
                return;
            }

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

            if (imeSessionQuiescing)
            {
                // Keep key state current while discarding the old IME session's final polled text boundary.
                _ = source.InputString;
                var discardedComposition = source.CompositionString ?? string.Empty;
                observedComposition = discardedComposition;
                composition = string.Empty;
                if (!imeSessionCanCompleteAtPollBoundary && !string.IsNullOrEmpty(discardedComposition))
                {
                    return;
                }
                imeSessionQuiescing = false;
                imeSessionCanCompleteAtPollBoundary = true;
                ApplyImeIntent();
                return;
            }

            var text = source.InputString;
            if (!string.IsNullOrEmpty(text))
            {
                eventSink.Enqueue(HybridInputEvent.CommittedText(text, context.UnscaledTime));
                composition = string.Empty;
            }

            var nextComposition = source.CompositionString ?? string.Empty;
            if (nextComposition != observedComposition)
            {
                composition = nextComposition;
                eventSink.Enqueue(HybridInputEvent.Composition(composition, context.UnscaledTime));
            }
            observedComposition = nextComposition;
        }

        public void SetImeEnabled(bool enabled)
        {
            hasImeIntent = true;
            imeEnabled = enabled;
            if (!enabled)
            {
                var currentComposition = source.CompositionString ?? string.Empty;
                var hasActiveComposition = !string.IsNullOrEmpty(composition) ||
                                           (!string.IsNullOrEmpty(currentComposition) &&
                                            currentComposition != observedComposition);
                observedComposition = currentComposition;
                if (hasActiveComposition)
                {
                    composition = string.Empty;
                    imeSessionQuiescing = true;
                    imeSessionCanCompleteAtPollBoundary = true;
                    if (imeSessionCanceller.IsRequired)
                    {
                        var result = imeSessionCanceller.CancelComposition();
                        HybridInputImeCancellationDiagnostics.Record(result);
                        imeSessionCanCompleteAtPollBoundary =
                            result == HybridInputImeCancellationResult.Succeeded;
                    }
                }
                if (imeAppliedEnabled)
                {
                    source.SetImeEnabled(false);
                    imeAppliedEnabled = false;
                }
                hasAppliedImeCursor = false;
                return;
            }

            if (!imeSessionQuiescing)
            {
                ApplyImeIntent();
            }
        }

        public void SetImeCursorPosition(Vector2 position)
        {
            hasImeCursorIntent = true;
            imeCursorPosition = position;
            if (!imeSessionQuiescing && imeAppliedEnabled &&
                (!hasAppliedImeCursor || appliedImeCursorPosition != position))
            {
                source.SetImeCursorPosition(position);
                hasAppliedImeCursor = true;
                appliedImeCursorPosition = position;
            }
        }

        private void ApplyImeIntent()
        {
            if (!hasImeIntent)
            {
                return;
            }

            if (imeAppliedEnabled != imeEnabled)
            {
                source.SetImeEnabled(imeEnabled);
                imeAppliedEnabled = imeEnabled;
            }
            if (imeEnabled && hasImeCursorIntent &&
                (!hasAppliedImeCursor || appliedImeCursorPosition != imeCursorPosition))
            {
                source.SetImeCursorPosition(imeCursorPosition);
                hasAppliedImeCursor = true;
                appliedImeCursorPosition = imeCursorPosition;
            }
        }
    }

    internal interface ILegacyInputSource
    {
        string InputString { get; }
        string CompositionString { get; }
        bool GetKey(KeyCode key);
        bool GetKeyDown(KeyCode key);
        bool GetKeyUp(KeyCode key);
        void SetImeEnabled(bool enabled);
        void SetImeCursorPosition(Vector2 position);
    }

    internal sealed class UnityLegacyInputSource : ILegacyInputSource
    {
        public string InputString => UnityEngine.Input.inputString;
        public string CompositionString => UnityEngine.Input.compositionString;

        public bool GetKey(KeyCode key) => UnityEngine.Input.GetKey(key);
        public bool GetKeyDown(KeyCode key) => UnityEngine.Input.GetKeyDown(key);
        public bool GetKeyUp(KeyCode key) => UnityEngine.Input.GetKeyUp(key);

        public void SetImeEnabled(bool enabled)
        {
            UnityEngine.Input.imeCompositionMode = enabled ? IMECompositionMode.On : IMECompositionMode.Off;
        }

        public void SetImeCursorPosition(Vector2 position)
        {
            UnityEngine.Input.compositionCursorPos = position;
        }
    }
}
#endif
