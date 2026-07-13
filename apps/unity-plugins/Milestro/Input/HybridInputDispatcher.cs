using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Milestro.Input
{
    /// <summary>
    /// Selects one provider and routes immutable input frames to the primary focused sink.
    /// </summary>
    internal sealed class HybridInputDispatcher
    {
        private readonly HybridInputProviderRegistry providers = new HybridInputProviderRegistry();
        private readonly List<TaggedInputEvent> pendingEvents = new List<TaggedInputEvent>();
        private readonly HashSet<KeyCode> pressedKeys = new HashSet<KeyCode>();
        private readonly Dictionary<int, IHybridInputFrameSink> sinks = new Dictionary<int, IHybridInputFrameSink>();

        private IHybridInputProvider? activeProvider;
        private HybridInputFrame? sealedFrame;
        private HybridInputEnvironment environment;
        private HybridInputSelectionStatus selectionStatus = HybridInputSelectionStatus.NoMatch;
        private string? overrideProviderId;
        private int providerGeneration;
        private int focusEpoch;
        private int nextSinkId = 1;
        private int focusedSinkId;
        private bool imeEnabled;
        private bool environmentInitialized;
        private bool activeModuleWasAlive;
        private Vector2 imeCursorPosition;
        private long nextEventSequence = 1L;
        private double lastEventTimestamp;

        internal HybridInputDiagnostics Diagnostics
        {
            get
            {
                var provider = activeProvider;
                return new HybridInputDiagnostics(selectionStatus,
                    provider?.Id ?? string.Empty,
                    provider?.Kind ?? HybridInputProviderKind.Unknown,
                    provider?.Capabilities ?? HybridInputCapabilities.None,
                    provider?.ScrollCapability ?? HybridScrollCapability.Unsupported,
                    environment.ActiveModule == null
                        ? string.Empty
                        : environment.ActiveModule.GetType().FullName ?? string.Empty,
                    environment.EventSystemCount,
                    environment.ApplicationFocused);
            }
        }

        internal HybridInputProviderHandle RegisterProvider(IHybridInputProvider provider)
        {
            var handle = providers.Register(provider);
            if (environmentInitialized)
            {
                RefreshEnvironment(environment);
            }
            return handle;
        }

        internal HybridScrollInput ResolveScrollInput(PointerEventData eventData)
        {
            var provider = activeProvider;
            if (provider == null ||
                provider.ScrollCapability == HybridScrollCapability.Unsupported ||
                (provider.Capabilities & HybridInputCapabilities.ScrollDelta) == 0)
            {
                return UnsupportedScroll(eventData.scrollDelta);
            }

            if (!(provider is IHybridScrollInputProvider scrollProvider) ||
                !scrollProvider.TryResolveScrollInput(eventData, out var resolved))
            {
                return DeltaOnlyScroll(eventData.scrollDelta);
            }

            if (!SameDelta(eventData.scrollDelta, resolved.Delta))
            {
                return UnsupportedScroll(eventData.scrollDelta);
            }

            var metadata = resolved.Metadata;
            if (metadata.Capability == HybridScrollCapability.Unsupported)
            {
                return UnsupportedScroll(eventData.scrollDelta);
            }
            if (metadata.Capability == HybridScrollCapability.DeltaOnly)
            {
                return provider.ScrollCapability == HybridScrollCapability.Unsupported
                    ? UnsupportedScroll(eventData.scrollDelta)
                    : resolved;
            }

            if (metadata.Capability != HybridScrollCapability.Phased ||
                provider.ScrollCapability != HybridScrollCapability.Phased ||
                (provider.Capabilities & HybridInputCapabilities.ScrollPhase) == 0 ||
                (provider.Capabilities & HybridInputCapabilities.ScrollDevice) == 0 ||
                metadata.DeviceKind == HybridInputDeviceKind.Unknown ||
                metadata.GestureId <= 0L ||
                !HasReliablePhase(metadata))
            {
                return DeltaOnlyScroll(eventData.scrollDelta);
            }

            return resolved;
        }

        internal void UnregisterProvider(HybridInputProviderHandle handle)
        {
            if (!providers.Unregister(handle))
            {
                return;
            }

            if (environmentInitialized)
            {
                RefreshEnvironment(environment);
            }
        }

        internal void SetProviderOverride(string? providerId)
        {
            overrideProviderId = string.IsNullOrWhiteSpace(providerId) ? null : providerId;
            if (environmentInitialized)
            {
                RefreshEnvironment(environment);
            }
        }

        internal HybridInputSinkRegistration RegisterSink(IHybridInputFrameSink sink)
        {
            if (sink == null)
            {
                throw new ArgumentNullException(nameof(sink));
            }

            var sinkId = nextSinkId++;
            sinks.Add(sinkId, sink);
            return new HybridInputSinkRegistration(this, sinkId);
        }

        internal void RefreshEnvironment(HybridInputEnvironment nextEnvironment)
        {
            var hadEnvironment = environmentInitialized;
            var nextModuleIsAlive = IsModuleAlive(nextEnvironment.ActiveModule);
            var ownerChanged = hadEnvironment &&
                               (environment.EventSystemCount != nextEnvironment.EventSystemCount ||
                                !ReferenceEquals(environment.ActiveModule, nextEnvironment.ActiveModule) ||
                                activeModuleWasAlive != nextModuleIsAlive);
            environmentInitialized = true;
            var wasFocused = environment.ApplicationFocused;
            environment = nextEnvironment;
            activeModuleWasAlive = nextModuleIsAlive;

            var selection = providers.Select(environment, overrideProviderId);
            var providerChanged = ownerChanged || !ReferenceEquals(selection.Provider, activeProvider) ||
                                  selection.Status != selectionStatus;
            if (providerChanged)
            {
                var resetReason = wasFocused && !environment.ApplicationFocused
                    ? HybridInputResetReason.ApplicationFocusLost
                    : HybridInputResetReason.ProviderChanged;
                TransitionProvider(selection, resetReason);
            }
            else
            {
                selectionStatus = selection.Status;
            }

            if (providerChanged)
            {
                return;
            }

            if (wasFocused && !environment.ApplicationFocused)
            {
                ResetFocusedSink(HybridInputResetReason.ApplicationFocusLost, keepOwner: true);
                pressedKeys.Clear();
                activeProvider?.SetImeEnabled(false);
            }
            else if (!wasFocused && environment.ApplicationFocused)
            {
                ApplyImeState();
            }
        }

        internal void Drain(int frameIndex, double unscaledTime)
        {
            sealedFrame = null;
            var provider = activeProvider;
            if (provider == null || !environment.ApplicationFocused)
            {
                pendingEvents.Clear();
                pressedKeys.Clear();
                return;
            }

            provider.Collect(new HybridInputCollectContext(frameIndex, unscaledTime));
            if (focusedSinkId == 0)
            {
                pendingEvents.Clear();
                return;
            }

            var frameEvents = new List<HybridInputEvent>(pendingEvents.Count);
            for (var i = 0; i < pendingEvents.Count; ++i)
            {
                var taggedEvent = pendingEvents[i];
                if (taggedEvent.ProviderGeneration == providerGeneration && taggedEvent.FocusEpoch == focusEpoch)
                {
                    frameEvents.Add(taggedEvent.Event);
                }
            }
            pendingEvents.Clear();

            var pressedKeySnapshot = new KeyCode[pressedKeys.Count];
            pressedKeys.CopyTo(pressedKeySnapshot);
            Array.Sort(pressedKeySnapshot);
            sealedFrame = new HybridInputFrame(frameIndex,
                NormalizeTime(unscaledTime),
                provider.Id,
                providerGeneration,
                focusEpoch,
                frameEvents.ToArray(),
                pressedKeySnapshot);

            var frame = sealedFrame;
            sealedFrame = null;
            if (frame == null || !environment.ApplicationFocused || frame.ProviderGeneration != providerGeneration ||
                frame.FocusEpoch != focusEpoch || focusedSinkId == 0 ||
                !sinks.TryGetValue(focusedSinkId, out var sink))
            {
                return;
            }

            sink.OnInputFrame(frame);
        }

        internal void Reset()
        {
            StopActiveProvider();
            ResetFocusedSink(HybridInputResetReason.DispatcherReset, keepOwner: false);
            providers.Clear();
            sinks.Clear();
            pressedKeys.Clear();
            pendingEvents.Clear();
            sealedFrame = null;
            selectionStatus = HybridInputSelectionStatus.NoMatch;
            overrideProviderId = null;
            environment = default;
            environmentInitialized = false;
            activeModuleWasAlive = false;
            lastEventTimestamp = 0d;
        }

        private void TransitionProvider(HybridInputProviderSelection selection,
            HybridInputResetReason resetReason = HybridInputResetReason.ProviderChanged)
        {
            StopActiveProvider();
            ++providerGeneration;
            pressedKeys.Clear();
            ResetFocusedSink(resetReason, keepOwner: true);
            selectionStatus = selection.Status;
            activeProvider = selection.Provider;
            if (activeProvider == null)
            {
                return;
            }

            activeProvider.Start(new ProviderEventSink(this, activeProvider, providerGeneration));
            ApplyImeState();
        }

        private void StopActiveProvider()
        {
            var provider = activeProvider;
            activeProvider = null;
            if (provider != null)
            {
                if (focusedSinkId != 0)
                {
                    provider.SetImeEnabled(false);
                }
                provider.Stop();
            }
        }

        private void Enqueue(IHybridInputProvider provider, int generation, HybridInputEvent inputEvent)
        {
            if (!ReferenceEquals(provider, activeProvider) || generation != providerGeneration)
            {
                return;
            }

            if (!environment.ApplicationFocused || focusedSinkId == 0)
            {
                return;
            }

            var timestamp = Math.Max(lastEventTimestamp, inputEvent.Timestamp);
            lastEventTimestamp = timestamp;
            var orderedEvent = inputEvent.WithOrdering(nextEventSequence++, timestamp);
            pendingEvents.Add(new TaggedInputEvent(providerGeneration, focusEpoch, orderedEvent));
        }

        private void ReplacePressedKeys(IHybridInputProvider provider,
            int generation,
            IReadOnlyList<KeyCode> nextPressedKeys)
        {
            if (!ReferenceEquals(provider, activeProvider) || generation != providerGeneration ||
                !environment.ApplicationFocused)
            {
                return;
            }

            pressedKeys.Clear();
            for (var i = 0; i < nextPressedKeys.Count; ++i)
            {
                pressedKeys.Add(nextPressedKeys[i]);
            }
        }

        internal bool IsKeyPressed(KeyCode key)
        {
            return pressedKeys.Contains(key);
        }

        private void ResetDeviceState(IHybridInputProvider provider, int generation)
        {
            if (!ReferenceEquals(provider, activeProvider) || generation != providerGeneration)
            {
                return;
            }

            ResetFocusedSink(HybridInputResetReason.DeviceChanged, keepOwner: true);
            ApplyImeState();
        }

        private bool AcquireFocus(int sinkId)
        {
            if (!sinks.ContainsKey(sinkId))
            {
                return false;
            }
            if (focusedSinkId == sinkId)
            {
                return true;
            }

            if (focusedSinkId != 0)
            {
                ResetFocusedSink(HybridInputResetReason.FocusChanged, keepOwner: false);
            }
            else
            {
                pendingEvents.Clear();
                sealedFrame = null;
                pressedKeys.Clear();
            }
            focusedSinkId = sinkId;
            ++focusEpoch;
            ApplyImeState();
            return true;
        }

        private void ReleaseFocus(int sinkId)
        {
            if (focusedSinkId != sinkId)
            {
                return;
            }

            ResetFocusedSink(HybridInputResetReason.FocusChanged, keepOwner: false);
        }

        private void UnregisterSink(int sinkId)
        {
            if (focusedSinkId == sinkId)
            {
                ResetFocusedSink(HybridInputResetReason.OwnerDisabled, keepOwner: false);
            }
            sinks.Remove(sinkId);
        }

        private bool SetImeEnabled(int sinkId, bool enabled)
        {
            if (focusedSinkId != sinkId)
            {
                return false;
            }

            imeEnabled = enabled;
            ApplyImeState();
            return true;
        }

        private bool SetImeCursorPosition(int sinkId, Vector2 position)
        {
            if (focusedSinkId != sinkId)
            {
                return false;
            }

            imeCursorPosition = position;
            if (activeProvider != null &&
                (activeProvider.Capabilities & HybridInputCapabilities.ImeControl) != 0)
            {
                activeProvider.SetImeCursorPosition(position);
            }
            return true;
        }

        private void ResetFocusedSink(HybridInputResetReason reason, bool keepOwner)
        {
            var previousSinkId = focusedSinkId;
            if (!keepOwner)
            {
                DisableIme();
            }
            ++focusEpoch;
            pendingEvents.Clear();
            sealedFrame = null;
            pressedKeys.Clear();
            if (!keepOwner)
            {
                focusedSinkId = 0;
                imeEnabled = false;
            }

            if (previousSinkId != 0 && sinks.TryGetValue(previousSinkId, out var sink))
            {
                sink.OnInputReset(reason);
            }
        }

        private void DisableIme()
        {
            var provider = activeProvider;
            if (provider != null &&
                (provider.Capabilities & HybridInputCapabilities.ImeControl) != 0)
            {
                provider.SetImeEnabled(false);
            }
        }

        private void ApplyImeState()
        {
            var provider = activeProvider;
            if (provider == null || !environment.ApplicationFocused || focusedSinkId == 0 ||
                (provider.Capabilities & HybridInputCapabilities.ImeControl) == 0)
            {
                return;
            }

            provider.SetImeEnabled(imeEnabled);
            if (imeEnabled)
            {
                provider.SetImeCursorPosition(imeCursorPosition);
            }
        }

        private static double NormalizeTime(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value) ? 0d : Math.Max(0d, value);
        }

        private static HybridScrollInput UnsupportedScroll(Vector2 delta)
        {
            return new HybridScrollInput(delta,
                new HybridScrollMetadata(HybridScrollCapability.Unsupported,
                    HybridInputDeviceKind.Unknown,
                    HybridInputPhase.Unknown,
                    HybridInputPhase.Unknown,
                    0d,
                    0L));
        }

        private static HybridScrollInput DeltaOnlyScroll(Vector2 delta)
        {
            return new HybridScrollInput(delta,
                new HybridScrollMetadata(HybridScrollCapability.DeltaOnly,
                    HybridInputDeviceKind.Unknown,
                    HybridInputPhase.Unknown,
                    HybridInputPhase.Unknown,
                    Time.unscaledTimeAsDouble,
                    0L));
        }

        private static bool SameDelta(Vector2 expected, Vector2 actual)
        {
            return expected.x.Equals(actual.x) && expected.y.Equals(actual.y);
        }

        private static bool HasReliablePhase(HybridScrollMetadata metadata)
        {
            if (!IsDefinedPhase(metadata.GesturePhase) || !IsDefinedPhase(metadata.MomentumPhase))
            {
                return false;
            }

            return metadata.GesturePhase != HybridInputPhase.None ||
                   metadata.MomentumPhase != HybridInputPhase.None;
        }

        private static bool IsDefinedPhase(HybridInputPhase phase)
        {
            return phase >= HybridInputPhase.None && phase <= HybridInputPhase.Canceled;
        }

        private static bool IsModuleAlive(BaseInputModule? module)
        {
            return module != null;
        }

        private readonly struct TaggedInputEvent
        {
            internal TaggedInputEvent(int providerGeneration, int focusEpoch, HybridInputEvent inputEvent)
            {
                ProviderGeneration = providerGeneration;
                FocusEpoch = focusEpoch;
                Event = inputEvent;
            }

            internal int ProviderGeneration { get; }
            internal int FocusEpoch { get; }
            internal HybridInputEvent Event { get; }
        }

        private sealed class ProviderEventSink : IHybridInputEventSink
        {
            private readonly HybridInputDispatcher dispatcher;
            private readonly IHybridInputProvider provider;
            private readonly int generation;

            internal ProviderEventSink(HybridInputDispatcher dispatcher,
                IHybridInputProvider provider,
                int generation)
            {
                this.dispatcher = dispatcher;
                this.provider = provider;
                this.generation = generation;
            }

            public void Enqueue(HybridInputEvent inputEvent)
            {
                dispatcher.Enqueue(provider, generation, inputEvent);
            }

            public void ReplacePressedKeys(IReadOnlyList<KeyCode> pressedKeys)
            {
                if (pressedKeys == null)
                {
                    throw new ArgumentNullException(nameof(pressedKeys));
                }

                dispatcher.ReplacePressedKeys(provider, generation, pressedKeys);
            }

            public void ResetDeviceState()
            {
                dispatcher.ResetDeviceState(provider, generation);
            }
        }

        internal sealed class HybridInputSinkRegistration : IDisposable
        {
            private HybridInputDispatcher? dispatcher;
            private readonly int sinkId;

            internal HybridInputSinkRegistration(HybridInputDispatcher dispatcher, int sinkId)
            {
                this.dispatcher = dispatcher;
                this.sinkId = sinkId;
            }

            internal bool AcquireFocus()
            {
                return dispatcher?.AcquireFocus(sinkId) == true;
            }

            internal void ReleaseFocus()
            {
                dispatcher?.ReleaseFocus(sinkId);
            }

            internal bool SetImeEnabled(bool enabled)
            {
                return dispatcher?.SetImeEnabled(sinkId, enabled) == true;
            }

            internal bool SetImeCursorPosition(Vector2 position)
            {
                return dispatcher?.SetImeCursorPosition(sinkId, position) == true;
            }

            public void Dispose()
            {
                var owner = dispatcher;
                dispatcher = null;
                owner?.UnregisterSink(sinkId);
            }
        }
    }
}
