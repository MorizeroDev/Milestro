using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Milestro.Input
{
    public enum HybridInputProviderKind
    {
        Unknown = 0,
        Legacy = 1,
        InputSystem = 2,
        Custom = 3
    }

    public enum HybridInputProviderMatch
    {
        None = 0,
        Compatible = 1,
        Preferred = 2,
        Exact = 3
    }

    [Flags]
    public enum HybridInputCapabilities
    {
        None = 0,
        KeyState = 1 << 0,
        CommittedText = 1 << 1,
        Composition = 1 << 2,
        ImeControl = 1 << 3,
        ScrollDelta = 1 << 4,
        ScrollDevice = 1 << 5,
        ScrollPhase = 1 << 6
    }

    public enum HybridScrollCapability
    {
        Unsupported = 0,
        DeltaOnly = 1,
        Phased = 2
    }

    public enum HybridInputDeviceKind
    {
        Unknown = 0,
        Mouse = 1,
        Touchpad = 2,
        Touchscreen = 3,
        Pen = 4,
        Tracked = 5,
        Custom = 6
    }

    public enum HybridInputPhase
    {
        Unknown = 0,
        None = 1,
        Began = 2,
        Changed = 3,
        Stationary = 4,
        Ended = 5,
        Canceled = 6
    }

    [Flags]
    public enum HybridScrollAxes
    {
        None = 0,
        Horizontal = 1 << 0,
        Vertical = 1 << 1
    }

    public enum HybridInputEventKind
    {
        KeyState = 0,
        CommittedText = 1,
        Composition = 2
    }

    public enum HybridInputResetReason
    {
        FocusChanged = 0,
        OwnerDisabled = 1,
        ProviderChanged = 2,
        ApplicationFocusLost = 3,
        DispatcherReset = 4,
        DeviceChanged = 5,
        InputEventBufferOverflow = 6,
        FocusSessionFailure = 7
    }

    public enum HybridInputDiagnosticCode
    {
        None = 0,
        SessionIsolationUnsupported = 1,
        InputEventBufferOverflow = 2,
        NotificationBufferOverflow = 3,
        WorkLimitExceeded = 4,
        ListenerException = 5,
        FocusSessionStartFailed = 6,
        FocusSessionEndFailed = 7,
        FocusSessionRestartFailed = 8
    }

    public enum HybridInputSelectionStatus
    {
        NoMatch = 0,
        Selected = 1,
        Conflict = 2,
        OverrideMissing = 3,
        OverrideRejected = 4
    }

    public enum HybridInputSystemPackageStatus
    {
        NotApplicable = 0,
        Missing = 1,
        BelowMinimum = 2,
        Supported = 3,
        Unsupported = 4
    }

    public enum HybridInputImeCancellationResult
    {
        NotAttempted = 0,
        Succeeded = 1,
        Unsupported = 2,
        WrongThread = 3,
        NoActiveContext = 4,
        Failed = 5,
        NativeUnavailable = 6
    }

    public readonly struct HybridInputEnvironment
    {
        public HybridInputEnvironment(BaseInputModule? activeModule,
            int eventSystemCount,
            bool applicationFocused)
        {
            ActiveModule = activeModule;
            EventSystemCount = Math.Max(0, eventSystemCount);
            ApplicationFocused = applicationFocused;
        }

        public BaseInputModule? ActiveModule { get; }
        public int EventSystemCount { get; }
        public bool ApplicationFocused { get; }
    }

    public readonly struct HybridInputCollectContext
    {
        public HybridInputCollectContext(int frameIndex, double unscaledTime)
        {
            FrameIndex = frameIndex;
            UnscaledTime = unscaledTime;
        }

        public int FrameIndex { get; }
        public double UnscaledTime { get; }
    }

    public readonly struct HybridInputEvent
    {
        private HybridInputEvent(HybridInputEventKind kind,
            KeyCode key,
            bool keyPressed,
            string text,
            double timestamp,
            long sequence)
        {
            Kind = kind;
            Key = key;
            KeyPressed = keyPressed;
            Text = text ?? string.Empty;
            Timestamp = NormalizeTimestamp(timestamp);
            Sequence = Math.Max(0L, sequence);
        }

        public HybridInputEventKind Kind { get; }
        public KeyCode Key { get; }
        public bool KeyPressed { get; }
        public string Text { get; }
        public double Timestamp { get; }
        public long Sequence { get; }

        public static HybridInputEvent KeyState(KeyCode key, bool pressed, double timestamp)
        {
            return new HybridInputEvent(HybridInputEventKind.KeyState,
                key,
                pressed,
                string.Empty,
                timestamp,
                0L);
        }

        public static HybridInputEvent CommittedText(string text, double timestamp)
        {
            return new HybridInputEvent(HybridInputEventKind.CommittedText,
                KeyCode.None,
                false,
                text,
                timestamp,
                0L);
        }

        public static HybridInputEvent Composition(string text, double timestamp)
        {
            return new HybridInputEvent(HybridInputEventKind.Composition,
                KeyCode.None,
                false,
                text,
                timestamp,
                0L);
        }

        internal HybridInputEvent WithOrdering(long sequence, double timestamp)
        {
            return new HybridInputEvent(Kind, Key, KeyPressed, Text, timestamp, sequence);
        }

        private static double NormalizeTimestamp(double timestamp)
        {
            return double.IsNaN(timestamp) || double.IsInfinity(timestamp)
                ? 0d
                : Math.Max(0d, timestamp);
        }
    }

    public readonly struct HybridScrollMetadata
    {
        public HybridScrollMetadata(HybridScrollCapability capability,
            HybridInputDeviceKind deviceKind,
            HybridInputPhase gesturePhase,
            HybridInputPhase momentumPhase,
            double timestamp,
            long gestureId)
        {
            Capability = capability;
            DeviceKind = deviceKind;
            GesturePhase = gesturePhase;
            MomentumPhase = momentumPhase;
            Timestamp = double.IsNaN(timestamp) || double.IsInfinity(timestamp)
                ? 0d
                : Math.Max(0d, timestamp);
            GestureId = Math.Max(0L, gestureId);
        }

        public HybridScrollCapability Capability { get; }
        public HybridInputDeviceKind DeviceKind { get; }
        public HybridInputPhase GesturePhase { get; }
        public HybridInputPhase MomentumPhase { get; }
        public double Timestamp { get; }
        public long GestureId { get; }
    }

    public readonly struct HybridScrollInput
    {
        public HybridScrollInput(Vector2 delta, HybridScrollMetadata metadata)
        {
            Delta = delta;
            Metadata = metadata;
        }

        public Vector2 Delta { get; }
        public HybridScrollMetadata Metadata { get; }
    }

    public readonly struct HybridScrollCaptureRequest
    {
        public HybridScrollCaptureRequest(long gestureId, HybridScrollAxes axes)
        {
            GestureId = Math.Max(0L, gestureId);
            Axes = axes & (HybridScrollAxes.Horizontal | HybridScrollAxes.Vertical);
        }

        public long GestureId { get; }
        public HybridScrollAxes Axes { get; }
    }

    /// <summary>
    /// A callback-scoped immutable view of one input frame. The dispatcher may reuse the backing
    /// storage after <c>OnInputFrame</c> returns; consumers that retain data must copy it.
    /// </summary>
    public sealed class HybridInputFrame
    {
        private readonly HybridInputEvent[] eventBuffer;
        private readonly KeyCode[] pressedKeyBuffer;
        private readonly FixedBufferReadOnlyList<HybridInputEvent> eventView;
        private readonly FixedBufferReadOnlyList<KeyCode> pressedKeyView;

        internal HybridInputFrame(int frameIndex,
            double unscaledTime,
            string providerId,
            int providerGeneration,
            int focusEpoch,
            HybridInputEvent[] events,
            KeyCode[] pressedKeys)
            : this(events.Length, pressedKeys.Length)
        {
            Array.Copy(events, eventBuffer, events.Length);
            Array.Copy(pressedKeys, pressedKeyBuffer, pressedKeys.Length);
            Set(frameIndex,
                unscaledTime,
                providerId,
                providerGeneration,
                focusEpoch,
                events.Length,
                pressedKeys.Length);
        }

        internal HybridInputFrame(int eventCapacity, int pressedKeyCapacity)
        {
            eventBuffer = new HybridInputEvent[eventCapacity];
            pressedKeyBuffer = new KeyCode[pressedKeyCapacity];
            eventView = new FixedBufferReadOnlyList<HybridInputEvent>(eventBuffer);
            pressedKeyView = new FixedBufferReadOnlyList<KeyCode>(pressedKeyBuffer);
            Events = eventView;
            PressedKeys = pressedKeyView;
            ProviderId = string.Empty;
        }

        internal HybridInputEvent[] EventBuffer => eventBuffer;
        internal KeyCode[] PressedKeyBuffer => pressedKeyBuffer;

        internal void Set(int frameIndex,
            double unscaledTime,
            string providerId,
            int providerGeneration,
            int focusEpoch,
            int eventCount,
            int pressedKeyCount)
        {
            FrameIndex = frameIndex;
            UnscaledTime = unscaledTime;
            ProviderId = providerId;
            ProviderGeneration = providerGeneration;
            FocusEpoch = focusEpoch;
            eventView.SetCount(eventCount);
            pressedKeyView.SetCount(pressedKeyCount);
        }

        internal void Release()
        {
            Array.Clear(eventBuffer, 0, eventView.Count);
            eventView.SetCount(0);
            pressedKeyView.SetCount(0);
        }

        public int FrameIndex { get; private set; }
        public double UnscaledTime { get; private set; }
        public string ProviderId { get; private set; }
        public int ProviderGeneration { get; private set; }
        public int FocusEpoch { get; private set; }
        public IReadOnlyList<HybridInputEvent> Events { get; }
        public IReadOnlyList<KeyCode> PressedKeys { get; }

        public bool IsKeyPressed(KeyCode key)
        {
            for (var i = 0; i < PressedKeys.Count; ++i)
            {
                if (PressedKeys[i] == key)
                {
                    return true;
                }
            }

            return false;
        }

        public bool WasKeyPressed(KeyCode key)
        {
            return HasKeyEdge(key, pressed: true);
        }

        public bool WasKeyReleased(KeyCode key)
        {
            return HasKeyEdge(key, pressed: false);
        }

        private bool HasKeyEdge(KeyCode key, bool pressed)
        {
            for (var i = 0; i < Events.Count; ++i)
            {
                var inputEvent = Events[i];
                if (inputEvent.Kind == HybridInputEventKind.KeyState && inputEvent.Key == key &&
                    inputEvent.KeyPressed == pressed)
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class FixedBufferReadOnlyList<T> : IReadOnlyList<T>
        {
            private readonly T[] buffer;

            internal FixedBufferReadOnlyList(T[] buffer)
            {
                this.buffer = buffer;
            }

            public int Count { get; private set; }

            public T this[int index]
            {
                get
                {
                    if ((uint)index >= (uint)Count)
                    {
                        throw new ArgumentOutOfRangeException(nameof(index));
                    }
                    return buffer[index];
                }
            }

            internal void SetCount(int count)
            {
                if ((uint)count > (uint)buffer.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(count));
                }
                Count = count;
            }

            public IEnumerator<T> GetEnumerator()
            {
                for (var i = 0; i < Count; ++i)
                {
                    yield return buffer[i];
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }

    public readonly struct HybridInputDiagnostics
    {
        internal HybridInputDiagnostics(HybridInputSelectionStatus selectionStatus,
            string providerId,
            HybridInputProviderKind providerKind,
            HybridInputCapabilities capabilities,
            HybridScrollCapability scrollCapability,
            string activeModuleType,
            int eventSystemCount,
            bool applicationFocused,
            HybridInputDiagnosticCode lastDiagnostic,
            int diagnosticCount)
        {
            SelectionStatus = selectionStatus;
            ProviderId = providerId;
            ProviderKind = providerKind;
            Capabilities = capabilities;
            ScrollCapability = scrollCapability;
            ActiveModuleType = activeModuleType;
            EventSystemCount = eventSystemCount;
            ApplicationFocused = applicationFocused;
            LastDiagnostic = lastDiagnostic;
            DiagnosticCount = Math.Max(0, diagnosticCount);
            ImeCancellationResult = HybridInputImeCancellationDiagnostics.LastResult;
            ImeCancellationFailureCount = HybridInputImeCancellationDiagnostics.FailureCount;
            InputSystemPackageStatus = HybridInputSystemPackageStatus.NotApplicable;
        }

        private HybridInputDiagnostics(HybridInputDiagnostics source,
            HybridInputSystemPackageStatus inputSystemPackageStatus)
        {
            SelectionStatus = source.SelectionStatus;
            ProviderId = source.ProviderId;
            ProviderKind = source.ProviderKind;
            Capabilities = source.Capabilities;
            ScrollCapability = source.ScrollCapability;
            ActiveModuleType = source.ActiveModuleType;
            EventSystemCount = source.EventSystemCount;
            ApplicationFocused = source.ApplicationFocused;
            LastDiagnostic = source.LastDiagnostic;
            DiagnosticCount = source.DiagnosticCount;
            ImeCancellationResult = source.ImeCancellationResult;
            ImeCancellationFailureCount = source.ImeCancellationFailureCount;
            InputSystemPackageStatus = inputSystemPackageStatus;
        }

        internal HybridInputDiagnostics WithInputSystemPackageStatus(
            HybridInputSystemPackageStatus inputSystemPackageStatus)
        {
            return new HybridInputDiagnostics(this, inputSystemPackageStatus);
        }

        public HybridInputSelectionStatus SelectionStatus { get; }
        public string ProviderId { get; }
        public HybridInputProviderKind ProviderKind { get; }
        public HybridInputCapabilities Capabilities { get; }
        public HybridScrollCapability ScrollCapability { get; }
        public string ActiveModuleType { get; }
        public int EventSystemCount { get; }
        public bool ApplicationFocused { get; }
        public HybridInputDiagnosticCode LastDiagnostic { get; }
        public int DiagnosticCount { get; }
        public HybridInputImeCancellationResult ImeCancellationResult { get; }
        public int ImeCancellationFailureCount { get; }
        public HybridInputSystemPackageStatus InputSystemPackageStatus { get; }
    }
}
