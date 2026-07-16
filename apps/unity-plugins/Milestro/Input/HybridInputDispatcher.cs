using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Milestro.Input
{
    /// <summary>
    /// Selects one provider and serializes focus, input frames, and lifecycle callbacks.
    /// All methods and provider callbacks are main-thread-only.
    /// </summary>
    internal sealed class HybridInputDispatcher
    {
        internal const int MaxPendingInputEventsPerFocusSession = 128;
        internal const int MaxPendingNotifications = 64;
        internal const int MaxOuterDrainWorkSteps = 256;

        private static readonly HybridInputCapabilities FocusedEventCapabilities =
            HybridInputCapabilities.KeyState |
            HybridInputCapabilities.CommittedText |
            HybridInputCapabilities.Composition;

        private readonly HybridInputProviderRegistry providers = new HybridInputProviderRegistry();
        private readonly Dictionary<int, SinkEntry> sinks = new Dictionary<int, SinkEntry>();
        private readonly FixedRing<HybridInputEvent> pendingEvents =
            new FixedRing<HybridInputEvent>(MaxPendingInputEventsPerFocusSession);
        private readonly FixedRing<Notification> notifications =
            new FixedRing<Notification>(MaxPendingNotifications);
        private readonly HashSet<KeyCode> pressedKeys = new HashSet<KeyCode>();
        private readonly HashSet<KeyCode> stagedPressedKeys = new HashSet<KeyCode>();
        private readonly int mainThreadId = Thread.CurrentThread.ManagedThreadId;

        private IHybridInputProvider? activeProvider;
        private SessionEventSink? activeSession;
        private HybridInputEnvironment environment;
        private HybridInputEnvironment pendingEnvironment;
        private HybridInputProviderSelection pendingProviderSelection;
        private HybridInputSelectionStatus selectionStatus = HybridInputSelectionStatus.NoMatch;
        private HybridInputDiagnosticCode lastDiagnostic;
        private HybridInputResetReason pendingReleaseReason = HybridInputResetReason.FocusChanged;
        private HybridInputResetReason releasingReason = HybridInputResetReason.FocusChanged;
        private string? overrideProviderId;
        private int dispatcherGeneration = 1;
        private int providerGeneration;
        private int focusEpoch;
        private int focusSessionToken;
        private int nextSinkId = 1;
        private int focusedSinkId;
        private int pendingFocusSinkId;
        private int providerTransitionReacquireSinkId;
        private int releasingSinkId;
        private int diagnosticCount;
        private bool imeEnabled;
        private bool environmentInitialized;
        private bool activeModuleWasAlive;
        private bool hasPendingEnvironment;
        private bool hasPendingProviderTransition;
        private bool hasPendingFocusIntent;
        private bool sessionBoundaryPending;
        private bool releaseInProgress;
        private bool resetRequested;
        private bool draining;
        private bool transactionAborted;
        private bool stagedPressedKeysValid;
        private bool inputOverflowLocked;
        private bool deliveringSealedReleaseFrame;
        private Vector2 imeCursorPosition;
        private long focusIntentVersion;
        private long sessionBoundaryVersion;
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
                    environment.ApplicationFocused,
                    lastDiagnostic,
                    diagnosticCount);
            }
        }

        internal HybridInputProviderHandle RegisterProvider(IHybridInputProvider provider)
        {
            var handle = providers.Register(provider);
            if (environmentInitialized)
            {
                RequestEnvironmentRefresh(environment);
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
                RequestEnvironmentRefresh(environment);
            }
        }

        internal void SetProviderOverride(string? providerId)
        {
            overrideProviderId = string.IsNullOrWhiteSpace(providerId) ? null : providerId;
            if (environmentInitialized)
            {
                RequestEnvironmentRefresh(environment);
            }
        }

        internal HybridInputSinkRegistration RegisterSink(IHybridInputFrameSink sink)
        {
            if (sink == null)
            {
                throw new ArgumentNullException(nameof(sink));
            }

            var sinkId = nextSinkId++;
            sinks.Add(sinkId, new SinkEntry(sink));
            return new HybridInputSinkRegistration(this, sinkId);
        }

        internal void RefreshEnvironment(HybridInputEnvironment nextEnvironment)
        {
            RequestEnvironmentRefresh(nextEnvironment);
        }

        internal void Drain(int frameIndex, double unscaledTime)
        {
            if (!IsMainThread())
            {
                return;
            }

            Pump();
            var provider = activeProvider;
            if (provider == null || !environment.ApplicationFocused)
            {
                ClearPendingInput();
                pressedKeys.Clear();
                return;
            }

            provider.Collect(new HybridInputCollectContext(frameIndex, unscaledTime));
            if (inputOverflowLocked)
            {
                Pump();
                return;
            }

            if (focusedSinkId != 0 && !releaseInProgress)
            {
                SealFrame(frameIndex, unscaledTime, includeEmptyFrame: true);
            }
            else
            {
                ClearPendingInput();
            }
            Pump();
        }

        internal void Reset()
        {
            if (!IsMainThread())
            {
                return;
            }

            unchecked
            {
                ++dispatcherGeneration;
            }
            activeSession?.Seal();
            resetRequested = true;
            hasPendingFocusIntent = false;
            sessionBoundaryPending = focusedSinkId != 0;
            pendingReleaseReason = HybridInputResetReason.DispatcherReset;
            Pump();
        }

        internal bool IsKeyPressed(KeyCode key)
        {
            return pressedKeys.Contains(key);
        }

        internal void NotifyValueChanged(IHybridInputLifecycleSink sink, string value, bool sessionBound)
        {
            if (!IsMainThread() || IsRejectingTransactionWork())
            {
                return;
            }

            var sinkId = FindSinkId(sink);
            var sessionToken = sessionBound && sinkId == focusedSinkId ? focusSessionToken : 0;
            TryQueueNotification(Notification.ValueChanged(sink,
                sinkId,
                sessionToken,
                value ?? string.Empty,
                sessionBound,
                sessionBound && deliveringSealedReleaseFrame));
            Pump();
        }

        private void RequestEnvironmentRefresh(HybridInputEnvironment nextEnvironment)
        {
            if (!IsMainThread() || IsRejectingTransactionWork())
            {
                return;
            }

            pendingEnvironment = nextEnvironment;
            hasPendingEnvironment = true;
            Pump();
        }

        private void ApplyPendingEnvironment()
        {
            hasPendingEnvironment = false;
            var nextEnvironment = pendingEnvironment;
            var hadEnvironment = environmentInitialized;
            var nextModuleIsAlive = IsModuleAlive(nextEnvironment.ActiveModule);
            var ownerChanged = hadEnvironment &&
                               (environment.EventSystemCount != nextEnvironment.EventSystemCount ||
                                !ReferenceEquals(environment.ActiveModule, nextEnvironment.ActiveModule) ||
                                activeModuleWasAlive != nextModuleIsAlive);
            var wasApplicationFocused = environment.ApplicationFocused;
            environmentInitialized = true;
            environment = nextEnvironment;
            activeModuleWasAlive = nextModuleIsAlive;

            var selection = providers.Select(environment, overrideProviderId);
            var providerChanged = ownerChanged || !ReferenceEquals(selection.Provider, activeProvider) ||
                                  selection.Status != selectionStatus;
            if (providerChanged)
            {
                pendingProviderSelection = selection;
                hasPendingProviderTransition = true;
                if (activeProvider != null)
                {
                    hasPendingFocusIntent = false;
                }
                if (focusedSinkId != 0)
                {
                    providerTransitionReacquireSinkId = CanOwnStrictFocus(selection.Provider)
                        ? focusedSinkId
                        : 0;
                    activeSession?.Seal();
                    sessionBoundaryPending = true;
                    pendingReleaseReason = wasApplicationFocused && !environment.ApplicationFocused
                        ? HybridInputResetReason.ApplicationFocusLost
                        : HybridInputResetReason.ProviderChanged;
                }
                return;
            }

            selectionStatus = selection.Status;
            if (wasApplicationFocused && !environment.ApplicationFocused)
            {
                SuspendFocusedSessionForApplicationLoss();
            }
            else if (!wasApplicationFocused && environment.ApplicationFocused)
            {
                ApplyImeState();
            }
        }

        private void ExecutePendingProviderTransition()
        {
            hasPendingProviderTransition = false;
            StopActiveProvider();
            unchecked
            {
                ++providerGeneration;
            }
            ClearPendingInput();
            pressedKeys.Clear();
            selectionStatus = pendingProviderSelection.Status;
            var reacquireSinkId = providerTransitionReacquireSinkId;
            providerTransitionReacquireSinkId = 0;
            activeProvider = pendingProviderSelection.Provider;
            if (activeProvider == null)
            {
                return;
            }

            activeProvider.Start(new ProviderEventSink(this,
                activeProvider,
                dispatcherGeneration,
                providerGeneration));
            ApplyImeState();
            if (reacquireSinkId != 0 && !hasPendingFocusIntent)
            {
                pendingFocusSinkId = reacquireSinkId;
                hasPendingFocusIntent = true;
            }
        }

        private void StopActiveProvider()
        {
            var provider = activeProvider;
            activeProvider = null;
            if (provider == null)
            {
                return;
            }

            if (activeSession != null && provider is IHybridInputFocusSessionProvider sessionProvider)
            {
                activeSession.Seal();
                sessionProvider.EndFocusSession();
            }
            if (focusedSinkId != 0)
            {
                provider.SetImeEnabled(false);
            }
            provider.Stop();
        }

        private void Pump()
        {
            if (draining || !IsMainThread())
            {
                return;
            }

            draining = true;
            transactionAborted = false;
            var workSteps = 0;
            try
            {
                while (!transactionAborted)
                {
                    if (++workSteps > MaxOuterDrainWorkSteps)
                    {
                        AbortTransaction(HybridInputDiagnosticCode.WorkLimitExceeded);
                        break;
                    }

                    if (sessionBoundaryPending && focusedSinkId != 0 && !releaseInProgress)
                    {
                        BeginRelease(pendingReleaseReason);
                        continue;
                    }

                    if (notifications.TryDequeue(out var notification))
                    {
                        DeliverNotification(notification);
                        continue;
                    }

                    if (releaseInProgress)
                    {
                        CompleteRelease();
                        continue;
                    }

                    if (resetRequested && focusedSinkId == 0)
                    {
                        CompleteReset();
                        continue;
                    }

                    if (hasPendingProviderTransition && focusedSinkId == 0)
                    {
                        ExecutePendingProviderTransition();
                        continue;
                    }

                    if (hasPendingEnvironment)
                    {
                        ApplyPendingEnvironment();
                        continue;
                    }

                    if (hasPendingFocusIntent && environmentInitialized)
                    {
                        ReconcileFocusIntent();
                        continue;
                    }

                    break;
                }
            }
            finally
            {
                draining = false;
                transactionAborted = false;
            }
        }

        private void ReconcileFocusIntent()
        {
            if (sessionBoundaryPending && focusedSinkId != 0)
            {
                return;
            }

            var targetSinkId = pendingFocusSinkId;
            hasPendingFocusIntent = false;
            if (targetSinkId == 0 || targetSinkId == focusedSinkId)
            {
                return;
            }

            TryAcquireFocus(targetSinkId);
        }

        private bool RequestFocus(int sinkId)
        {
            if (!IsMainThread() || IsRejectingTransactionWork() ||
                !TryGetRegisteredSink(sinkId, out var entry) || !CanAdmitFocus(entry))
            {
                return false;
            }
            if (focusedSinkId == sinkId && !sessionBoundaryPending)
            {
                return true;
            }

            pendingFocusSinkId = sinkId;
            hasPendingFocusIntent = true;
            unchecked
            {
                ++focusIntentVersion;
            }
            if (focusedSinkId != 0 && focusedSinkId != sinkId)
            {
                activeSession?.Seal();
                sessionBoundaryPending = true;
                pendingReleaseReason = HybridInputResetReason.FocusChanged;
                sessionBoundaryVersion = focusIntentVersion;
            }
            Pump();
            return focusedSinkId == sinkId;
        }

        private void RequestRelease(int sinkId, HybridInputResetReason reason)
        {
            if (!IsMainThread() || IsRejectingTransactionWork())
            {
                return;
            }
            if (focusedSinkId != sinkId)
            {
                if (hasPendingFocusIntent && pendingFocusSinkId == sinkId)
                {
                    pendingFocusSinkId = 0;
                    hasPendingFocusIntent = false;
                }
                return;
            }

            activeSession?.Seal();
            pendingFocusSinkId = 0;
            hasPendingFocusIntent = true;
            sessionBoundaryPending = true;
            pendingReleaseReason = reason;
            unchecked
            {
                sessionBoundaryVersion = ++focusIntentVersion;
            }
            Pump();
        }

        private void TryAcquireFocus(int sinkId)
        {
            if (!TryGetRegisteredSink(sinkId, out var entry) || !CanAdmitFocus(entry))
            {
                return;
            }
            if (!environment.ApplicationFocused)
            {
                return;
            }

            var provider = activeProvider;
            if (provider == null)
            {
                if (!environmentInitialized)
                {
                    pendingFocusSinkId = sinkId;
                    hasPendingFocusIntent = true;
                }
                return;
            }

            var publishesFocusedEvents = (provider.Capabilities & FocusedEventCapabilities) != 0;
            if (publishesFocusedEvents && !(provider is IHybridInputFocusSessionProvider))
            {
                RecordDiagnostic(HybridInputDiagnosticCode.SessionIsolationUnsupported);
                return;
            }

            ClearPendingInput();
            pressedKeys.Clear();
            focusedSinkId = sinkId;
            unchecked
            {
                ++focusEpoch;
                ++focusSessionToken;
            }
            imeEnabled = false;
            inputOverflowLocked = false;

            if (provider is IHybridInputFocusSessionProvider sessionProvider)
            {
                var session = new SessionEventSink(this,
                    provider,
                    dispatcherGeneration,
                    providerGeneration,
                    focusSessionToken,
                    sinkId);
                activeSession = session;
                try
                {
                    sessionProvider.BeginFocusSession(session);
                }
                catch
                {
                    session.Seal();
                    activeSession = null;
                    focusedSinkId = 0;
                    RecordDiagnostic(HybridInputDiagnosticCode.FocusSessionStartFailed);
                    return;
                }
            }

            ApplyImeState();

            if (entry.Sink is IHybridInputLifecycleSink lifecycleSink)
            {
                TryQueueNotification(Notification.FocusGained(lifecycleSink, sinkId, focusSessionToken));
            }
        }

        private void BeginRelease(HybridInputResetReason reason)
        {
            if (focusedSinkId == 0)
            {
                sessionBoundaryPending = false;
                return;
            }

            activeSession?.Seal();
            releasingSinkId = focusedSinkId;
            releasingReason = reason;
            releaseInProgress = true;
            sessionBoundaryPending = false;
            SealFrame(0, lastEventTimestamp, includeEmptyFrame: false);
        }

        private void CompleteRelease()
        {
            releaseInProgress = false;
            var sinkId = releasingSinkId;
            releasingSinkId = 0;
            if (sinkId == 0 || focusedSinkId != sinkId)
            {
                return;
            }

            var provider = activeProvider;
            if (provider is IHybridInputFocusSessionProvider sessionProvider)
            {
                sessionProvider.EndFocusSession();
            }
            activeSession = null;
            DisableIme();
            focusedSinkId = 0;
            sessionBoundaryPending = false;
            if (sessionBoundaryVersion != 0)
            {
                sessionBoundaryVersion = 0;
            }
            imeEnabled = false;
            unchecked
            {
                ++focusEpoch;
            }
            ClearPendingInput();
            pressedKeys.Clear();

            if (!sinks.TryGetValue(sinkId, out var entry))
            {
                return;
            }

            entry.Sink.OnInputReset(releasingReason);
            if (entry.Sink is IHybridInputLifecycleSink lifecycleSink)
            {
                var finalText = lifecycleSink.CommittedText ?? string.Empty;
                TryQueueNotification(Notification.EndEdit(lifecycleSink, sinkId, finalText));
                TryQueueNotification(Notification.FocusLost(lifecycleSink, sinkId));
            }
            else if (!entry.Registered)
            {
                sinks.Remove(sinkId);
            }
        }

        private void SuspendFocusedSessionForApplicationLoss()
        {
            ClearPendingInput();
            pressedKeys.Clear();
            DisableIme();
            if (focusedSinkId != 0 && sinks.TryGetValue(focusedSinkId, out var entry))
            {
                entry.Sink.OnInputReset(HybridInputResetReason.ApplicationFocusLost);
            }
        }

        private void RestartFocusedSessionForDeviceChange(IHybridInputProvider provider,
            int expectedDispatcherGeneration,
            int expectedProviderGeneration)
        {
            if (!IsMainThread() || !IsCurrentProvider(provider,
                    expectedDispatcherGeneration,
                    expectedProviderGeneration))
            {
                return;
            }

            var sinkId = focusedSinkId;
            if (sinkId == 0)
            {
                pressedKeys.Clear();
                ClearPendingInput();
                return;
            }

            activeSession?.Seal();
            if (provider is IHybridInputFocusSessionProvider sessionProvider)
            {
                sessionProvider.EndFocusSession();
            }
            ClearPendingInput();
            pressedKeys.Clear();
            if (sinks.TryGetValue(sinkId, out var entry))
            {
                entry.Sink.OnInputReset(HybridInputResetReason.DeviceChanged);
            }

            unchecked
            {
                ++focusEpoch;
                ++focusSessionToken;
            }
            if (provider is IHybridInputFocusSessionProvider nextSessionProvider)
            {
                var session = new SessionEventSink(this,
                    provider,
                    dispatcherGeneration,
                    providerGeneration,
                    focusSessionToken,
                    sinkId);
                activeSession = session;
                nextSessionProvider.BeginFocusSession(session);
            }
            ApplyImeState();
        }

        private void RestartSessionForDeviceChange(SessionEventSink scope)
        {
            if (!IsMainThread() || !IsCurrentSession(scope))
            {
                return;
            }
            RestartFocusedSessionForDeviceChange(scope.Provider,
                scope.DispatcherGeneration,
                scope.ProviderGeneration);
        }

        private void SealFrame(int frameIndex, double unscaledTime, bool includeEmptyFrame)
        {
            if (focusedSinkId == 0 || inputOverflowLocked)
            {
                ClearPendingInput();
                return;
            }
            if (!includeEmptyFrame && pendingEvents.Count == 0)
            {
                ClearPendingInput();
                return;
            }
            if (!sinks.TryGetValue(focusedSinkId, out var entry))
            {
                ClearPendingInput();
                return;
            }

            var frameEvents = new HybridInputEvent[pendingEvents.Count];
            pendingEvents.CopyTo(frameEvents);
            var pressedKeySnapshot = new KeyCode[stagedPressedKeysValid ? stagedPressedKeys.Count : pressedKeys.Count];
            if (stagedPressedKeysValid)
            {
                stagedPressedKeys.CopyTo(pressedKeySnapshot);
            }
            else
            {
                pressedKeys.CopyTo(pressedKeySnapshot);
            }
            Array.Sort(pressedKeySnapshot);
            var frame = new HybridInputFrame(frameIndex,
                NormalizeTime(unscaledTime),
                activeProvider?.Id ?? string.Empty,
                providerGeneration,
                focusEpoch,
                frameEvents,
                pressedKeySnapshot);
            ClearPendingInput();
            TryQueueNotification(Notification.ForFrame(entry.Sink,
                focusedSinkId,
                focusSessionToken,
                frame,
                releaseInProgress));
        }

        private void DeliverNotification(Notification notification)
        {
            try
            {
                switch (notification.Kind)
                {
                    case NotificationKind.Frame:
                        if (!CanDeliverSessionNotification(notification))
                        {
                            return;
                        }
                        ReplaceCommittedPressedKeys(notification.Frame!.PressedKeys);
                        var wasDeliveringSealedReleaseFrame = deliveringSealedReleaseFrame;
                        deliveringSealedReleaseFrame = notification.AllowSealedSession;
                        try
                        {
                            notification.FrameSink!.OnInputFrame(notification.Frame);
                        }
                        finally
                        {
                            deliveringSealedReleaseFrame = wasDeliveringSealedReleaseFrame;
                        }
                        break;
                    case NotificationKind.FocusGained:
                        if (!CanDeliverSessionNotification(notification))
                        {
                            return;
                        }
                        notification.LifecycleSink!.OnFocusGained();
                        break;
                    case NotificationKind.EndEdit:
                        notification.LifecycleSink!.OnEndEdit(notification.Text);
                        break;
                    case NotificationKind.FocusLost:
                        notification.LifecycleSink!.OnFocusLost();
                        RemoveSinkAfterTerminalNotification(notification.SinkId);
                        break;
                    case NotificationKind.ValueChanged:
                        if (notification.SessionBound && !CanDeliverSessionNotification(notification))
                        {
                            return;
                        }
                        notification.LifecycleSink!.OnValueChanged(notification.Text);
                        break;
                }
            }
            catch
            {
                AbortTransaction(HybridInputDiagnosticCode.ListenerException);
            }
        }

        private bool CanDeliverSessionNotification(Notification notification)
        {
            var isSealedReleaseFrame = releaseInProgress && notification.SinkId == releasingSinkId;
            if (notification.SinkId == 0 || notification.SinkId != focusedSinkId ||
                notification.SessionToken != focusSessionToken || !environment.ApplicationFocused ||
                !sinks.TryGetValue(notification.SinkId, out var entry) || (!entry.Registered && !isSealedReleaseFrame))
            {
                return false;
            }
            if (!(entry.Sink is IHybridInputLifecycleSink lifecycleSink))
            {
                return activeSession == null || activeSession.AdmissionOpen || notification.AllowSealedSession;
            }
            return (activeSession == null || activeSession.AdmissionOpen || notification.AllowSealedSession) &&
                   lifecycleSink.IsActiveAndEnabled && lifecycleSink.CanConsumeInputNow;
        }

        private void EnqueueSessionEvent(SessionEventSink scope, HybridInputEvent inputEvent)
        {
            if (!IsMainThread() || IsRejectingTransactionWork() || !IsCurrentSession(scope) ||
                !environment.ApplicationFocused ||
                inputOverflowLocked)
            {
                return;
            }

            if (pendingEvents.Count == MaxPendingInputEventsPerFocusSession)
            {
                HandleInputOverflow();
                Pump();
                return;
            }

            var timestamp = Math.Max(lastEventTimestamp, inputEvent.Timestamp);
            lastEventTimestamp = timestamp;
            var orderedEvent = inputEvent.WithOrdering(nextEventSequence++, timestamp);
            pendingEvents.TryEnqueue(orderedEvent);
        }

        private void ReplaceSessionPressedKeys(SessionEventSink scope, IReadOnlyList<KeyCode> nextPressedKeys)
        {
            if (!IsMainThread() || IsRejectingTransactionWork() || !IsCurrentSession(scope) ||
                !environment.ApplicationFocused ||
                inputOverflowLocked)
            {
                return;
            }

            stagedPressedKeys.Clear();
            for (var i = 0; i < nextPressedKeys.Count; ++i)
            {
                stagedPressedKeys.Add(nextPressedKeys[i]);
            }
            stagedPressedKeysValid = true;
        }

        private void EnqueueProviderEvent(IHybridInputProvider provider,
            int expectedDispatcherGeneration,
            int expectedProviderGeneration,
            HybridInputEvent inputEvent)
        {
            // Focused events must never fall back to an unscoped provider sink.
            if (!IsMainThread() || !IsCurrentProvider(provider,
                    expectedDispatcherGeneration,
                    expectedProviderGeneration))
            {
                return;
            }
        }

        private void ReplaceProviderPressedKeys(IHybridInputProvider provider,
            int expectedDispatcherGeneration,
            int expectedProviderGeneration,
            IReadOnlyList<KeyCode> nextPressedKeys)
        {
            if (!IsMainThread() || !IsCurrentProvider(provider,
                    expectedDispatcherGeneration,
                    expectedProviderGeneration) || !environment.ApplicationFocused || focusedSinkId != 0)
            {
                return;
            }

            ReplaceCommittedPressedKeys(nextPressedKeys);
        }

        private void HandleInputOverflow()
        {
            if (inputOverflowLocked)
            {
                return;
            }

            inputOverflowLocked = true;
            activeSession?.Seal();
            ClearPendingInput();
            notifications.Clear();
            hasPendingFocusIntent = false;
            RecordDiagnostic(HybridInputDiagnosticCode.InputEventBufferOverflow);
            if (focusedSinkId != 0)
            {
                sessionBoundaryPending = true;
                pendingReleaseReason = HybridInputResetReason.InputEventBufferOverflow;
            }
        }

        private void AbortTransaction(HybridInputDiagnosticCode diagnostic)
        {
            RecordDiagnostic(diagnostic);
            transactionAborted = true;
            notifications.Clear();
            ClearPendingInput();
            hasPendingFocusIntent = false;
        }

        private bool TryQueueNotification(Notification notification)
        {
            if (IsRejectingTransactionWork())
            {
                return false;
            }
            if (notifications.TryEnqueue(notification))
            {
                return true;
            }

            AbortTransaction(HybridInputDiagnosticCode.NotificationBufferOverflow);
            return false;
        }

        private void CompleteReset()
        {
            resetRequested = false;
            StopActiveProvider();
            providers.Clear();
            sinks.Clear();
            pressedKeys.Clear();
            ClearPendingInput();
            notifications.Clear();
            hasPendingEnvironment = false;
            hasPendingProviderTransition = false;
            hasPendingFocusIntent = false;
            sessionBoundaryPending = false;
            selectionStatus = HybridInputSelectionStatus.NoMatch;
            overrideProviderId = null;
            environment = default;
            pendingEnvironment = default;
            environmentInitialized = false;
            activeModuleWasAlive = false;
            lastEventTimestamp = 0d;
            imeEnabled = false;
            inputOverflowLocked = false;
            providerTransitionReacquireSinkId = 0;
        }

        private void UnregisterSink(int sinkId)
        {
            if (!IsMainThread() || !sinks.TryGetValue(sinkId, out var entry) || !entry.Registered)
            {
                return;
            }

            entry.Registered = false;
            if (focusedSinkId == sinkId)
            {
                activeSession?.Seal();
                RequestRelease(sinkId, HybridInputResetReason.OwnerDisabled);
                return;
            }
            sinks.Remove(sinkId);
        }

        private bool SetImeEnabled(int sinkId, bool enabled)
        {
            if (!IsMainThread() || focusedSinkId != sinkId)
            {
                return false;
            }

            imeEnabled = enabled;
            ApplyImeState();
            return true;
        }

        private bool SetImeCursorPosition(int sinkId, Vector2 position)
        {
            if (!IsMainThread() || focusedSinkId != sinkId)
            {
                return false;
            }

            imeCursorPosition = position;
            if (activeProvider != null && environment.ApplicationFocused &&
                (activeProvider.Capabilities & HybridInputCapabilities.ImeControl) != 0)
            {
                activeProvider.SetImeCursorPosition(position);
            }
            return true;
        }

        private void DisableIme()
        {
            var provider = activeProvider;
            if (provider != null && (provider.Capabilities & HybridInputCapabilities.ImeControl) != 0)
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

        private bool CanAdmitFocus(SinkEntry entry)
        {
            if (!entry.Registered)
            {
                return false;
            }
            if (!(entry.Sink is IHybridInputLifecycleSink lifecycleSink))
            {
                return true;
            }
            if (!lifecycleSink.IsActiveAndEnabled)
            {
                return false;
            }

            var eventSystem = EventSystem.current;
            return eventSystem == null || eventSystem.currentSelectedGameObject == lifecycleSink.Owner;
        }

        private static bool CanOwnStrictFocus(IHybridInputProvider? provider)
        {
            if (provider == null)
            {
                return false;
            }
            return (provider.Capabilities & FocusedEventCapabilities) == 0 ||
                   provider is IHybridInputFocusSessionProvider;
        }

        private bool TryGetRegisteredSink(int sinkId, out SinkEntry entry)
        {
            return sinks.TryGetValue(sinkId, out entry) && entry.Registered;
        }

        private bool IsCurrentSession(SessionEventSink scope)
        {
            return scope.AdmissionOpen && ReferenceEquals(scope, activeSession) &&
                   scope.DispatcherGeneration == dispatcherGeneration &&
                   scope.ProviderGeneration == providerGeneration &&
                   scope.SessionToken == focusSessionToken && scope.SinkId == focusedSinkId &&
                   ReferenceEquals(scope.Provider, activeProvider);
        }

        private bool IsCurrentProvider(IHybridInputProvider provider,
            int expectedDispatcherGeneration,
            int expectedProviderGeneration)
        {
            return expectedDispatcherGeneration == dispatcherGeneration &&
                   expectedProviderGeneration == providerGeneration &&
                   ReferenceEquals(provider, activeProvider);
        }

        private void ReplaceCommittedPressedKeys(IReadOnlyList<KeyCode> nextPressedKeys)
        {
            pressedKeys.Clear();
            for (var i = 0; i < nextPressedKeys.Count; ++i)
            {
                pressedKeys.Add(nextPressedKeys[i]);
            }
        }

        private void ClearPendingInput()
        {
            pendingEvents.Clear();
            stagedPressedKeys.Clear();
            stagedPressedKeysValid = false;
        }

        private int FindSinkId(IHybridInputLifecycleSink sink)
        {
            foreach (var pair in sinks)
            {
                if (ReferenceEquals(pair.Value.Sink, sink))
                {
                    return pair.Key;
                }
            }
            return 0;
        }

        private void RemoveSinkAfterTerminalNotification(int sinkId)
        {
            if (sinks.TryGetValue(sinkId, out var entry) && !entry.Registered)
            {
                sinks.Remove(sinkId);
            }
        }

        private void RecordDiagnostic(HybridInputDiagnosticCode diagnostic)
        {
            lastDiagnostic = diagnostic;
            ++diagnosticCount;
        }

        private bool IsMainThread()
        {
            return Thread.CurrentThread.ManagedThreadId == mainThreadId;
        }

        private bool IsRejectingTransactionWork()
        {
            return draining && transactionAborted;
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

        private sealed class SinkEntry
        {
            internal SinkEntry(IHybridInputFrameSink sink)
            {
                Sink = sink;
            }

            internal IHybridInputFrameSink Sink { get; }
            internal bool Registered { get; set; } = true;
        }

        private sealed class ProviderEventSink : IHybridInputEventSink
        {
            private readonly HybridInputDispatcher dispatcher;
            private readonly IHybridInputProvider provider;
            private readonly int dispatcherGeneration;
            private readonly int providerGeneration;

            internal ProviderEventSink(HybridInputDispatcher dispatcher,
                IHybridInputProvider provider,
                int dispatcherGeneration,
                int providerGeneration)
            {
                this.dispatcher = dispatcher;
                this.provider = provider;
                this.dispatcherGeneration = dispatcherGeneration;
                this.providerGeneration = providerGeneration;
            }

            public void Enqueue(HybridInputEvent inputEvent)
            {
                dispatcher.EnqueueProviderEvent(provider,
                    dispatcherGeneration,
                    providerGeneration,
                    inputEvent);
            }

            public void ReplacePressedKeys(IReadOnlyList<KeyCode> nextPressedKeys)
            {
                if (nextPressedKeys == null)
                {
                    throw new ArgumentNullException(nameof(nextPressedKeys));
                }
                dispatcher.ReplaceProviderPressedKeys(provider,
                    dispatcherGeneration,
                    providerGeneration,
                    nextPressedKeys);
            }

            public void ResetDeviceState()
            {
                dispatcher.RestartFocusedSessionForDeviceChange(provider,
                    dispatcherGeneration,
                    providerGeneration);
            }
        }

        private sealed class SessionEventSink : IHybridInputEventSink
        {
            private readonly HybridInputDispatcher dispatcher;

            internal SessionEventSink(HybridInputDispatcher dispatcher,
                IHybridInputProvider provider,
                int dispatcherGeneration,
                int providerGeneration,
                int sessionToken,
                int sinkId)
            {
                this.dispatcher = dispatcher;
                Provider = provider;
                DispatcherGeneration = dispatcherGeneration;
                ProviderGeneration = providerGeneration;
                SessionToken = sessionToken;
                SinkId = sinkId;
                AdmissionOpen = true;
            }

            internal IHybridInputProvider Provider { get; }
            internal int DispatcherGeneration { get; }
            internal int ProviderGeneration { get; }
            internal int SessionToken { get; }
            internal int SinkId { get; }
            internal bool AdmissionOpen { get; private set; }

            internal void Seal()
            {
                AdmissionOpen = false;
            }

            public void Enqueue(HybridInputEvent inputEvent)
            {
                dispatcher.EnqueueSessionEvent(this, inputEvent);
            }

            public void ReplacePressedKeys(IReadOnlyList<KeyCode> nextPressedKeys)
            {
                if (nextPressedKeys == null)
                {
                    throw new ArgumentNullException(nameof(nextPressedKeys));
                }
                dispatcher.ReplaceSessionPressedKeys(this, nextPressedKeys);
            }

            public void ResetDeviceState()
            {
                dispatcher.RestartSessionForDeviceChange(this);
            }
        }

        private enum NotificationKind
        {
            Frame,
            FocusGained,
            EndEdit,
            FocusLost,
            ValueChanged
        }

        private readonly struct Notification
        {
            private Notification(NotificationKind kind,
                IHybridInputFrameSink? frameSink,
                IHybridInputLifecycleSink? lifecycleSink,
                int sinkId,
                int sessionToken,
                HybridInputFrame? frame,
                string text,
                bool sessionBound,
                bool allowSealedSession)
            {
                Kind = kind;
                FrameSink = frameSink;
                LifecycleSink = lifecycleSink;
                SinkId = sinkId;
                SessionToken = sessionToken;
                Frame = frame;
                Text = text;
                SessionBound = sessionBound;
                AllowSealedSession = allowSealedSession;
            }

            internal NotificationKind Kind { get; }
            internal IHybridInputFrameSink? FrameSink { get; }
            internal IHybridInputLifecycleSink? LifecycleSink { get; }
            internal int SinkId { get; }
            internal int SessionToken { get; }
            internal HybridInputFrame? Frame { get; }
            internal string Text { get; }
            internal bool SessionBound { get; }
            internal bool AllowSealedSession { get; }

            internal static Notification ForFrame(IHybridInputFrameSink sink,
                int sinkId,
                int sessionToken,
                HybridInputFrame frame,
                bool allowSealedSession)
            {
                return new Notification(NotificationKind.Frame,
                    sink,
                    sink as IHybridInputLifecycleSink,
                    sinkId,
                    sessionToken,
                    frame,
                    string.Empty,
                    true,
                    allowSealedSession);
            }

            internal static Notification FocusGained(IHybridInputLifecycleSink sink,
                int sinkId,
                int sessionToken)
            {
                return new Notification(NotificationKind.FocusGained,
                    sink,
                    sink,
                    sinkId,
                    sessionToken,
                    null,
                    string.Empty,
                    true,
                    false);
            }

            internal static Notification EndEdit(IHybridInputLifecycleSink sink, int sinkId, string text)
            {
                return new Notification(NotificationKind.EndEdit,
                    sink,
                    sink,
                    sinkId,
                    0,
                    null,
                    text,
                    false,
                    false);
            }

            internal static Notification FocusLost(IHybridInputLifecycleSink sink, int sinkId)
            {
                return new Notification(NotificationKind.FocusLost,
                    sink,
                    sink,
                    sinkId,
                    0,
                    null,
                    string.Empty,
                    false,
                    false);
            }

            internal static Notification ValueChanged(IHybridInputLifecycleSink sink,
                int sinkId,
                int sessionToken,
                string text,
                bool sessionBound,
                bool allowSealedSession)
            {
                return new Notification(NotificationKind.ValueChanged,
                    sink,
                    sink,
                    sinkId,
                    sessionToken,
                    null,
                    text,
                    sessionBound,
                    allowSealedSession);
            }
        }

        private sealed class FixedRing<T>
        {
            private readonly T[] items;
            private int head;
            private int tail;

            internal FixedRing(int capacity)
            {
                items = new T[capacity];
            }

            internal int Count { get; private set; }

            internal bool TryEnqueue(T item)
            {
                if (Count == items.Length)
                {
                    return false;
                }
                items[tail] = item;
                tail = (tail + 1) % items.Length;
                ++Count;
                return true;
            }

            internal bool TryDequeue(out T item)
            {
                if (Count == 0)
                {
                    item = default!;
                    return false;
                }
                item = items[head];
                items[head] = default!;
                head = (head + 1) % items.Length;
                --Count;
                return true;
            }

            internal void CopyTo(T[] destination)
            {
                for (var i = 0; i < Count; ++i)
                {
                    destination[i] = items[(head + i) % items.Length];
                }
            }

            internal void Clear()
            {
                while (TryDequeue(out _))
                {
                }
                head = 0;
                tail = 0;
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

            internal bool IsFocused => dispatcher?.focusedSinkId == sinkId;

            internal bool AcquireFocus()
            {
                return dispatcher?.RequestFocus(sinkId) == true;
            }

            internal void ReleaseFocus()
            {
                dispatcher?.RequestRelease(sinkId, HybridInputResetReason.FocusChanged);
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
