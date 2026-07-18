using System;
using System.Collections;
using System.Collections.Generic;
using Milestro.Components;
using Milestro.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace Milestro.TextInputLifecycleQA
{
    public sealed class TextInputLifecycleQaScenarioRunner : MonoBehaviour
    {
        private const int TimeoutFrames = 300;
        private const int ProfilerWarmupEvents = 256;
        private const int ProfilerCaptureEvents = 256;

        [SerializeField] private TextInput? noListenerInput;
        [SerializeField] private TextInput? runtimeListenerInput;
        [SerializeField] private TextInputLifecycleQaRuntimeListener? runtimeListener;
        [SerializeField] private TextInput? persistentListenerInput;
        [SerializeField] private TextInputLifecycleQaReceiver? persistentReceiver;
        [SerializeField] private string sourceHead = string.Empty;
        [SerializeField] private string sourceTree = string.Empty;

        private bool noListenerPassed;
        private bool runtimeListenerPassed;
        private bool persistentListenerPassed;
        private bool exceptionRecoveryPassed;
        private bool initialSceneLoadPassed;
        private bool initialSceneLoadRuntimeListenerPassed;
        private bool initialSceneLoadPersistentReceiverPassed;
        private bool initialSceneLoadStableRecorderPassed;
        private bool targetSceneInitialLoadPassed;
        private int exceptionCasesPassed;
        private int exceptionRecoveriesPassed;
        private bool profilerBurstRunning;
        private readonly List<TextInputLifecycleQaRecoveryCheckpoint> recoveryCheckpoints = new();
        private TextInputLifecycleQaStrictProvider? activeProvider;
        private IDisposable? activeProviderRegistration;

        public bool Completed { get; private set; }
        public TextInputLifecycleQaResult? Result { get; private set; }

        public bool StartProfilerBurst(TextInputLifecycleQaProfilerCase profilerCase)
        {
            if (!Completed || Result?.status != "PASS" || activeProvider == null ||
                activeProviderRegistration == null || profilerBurstRunning)
            {
                return false;
            }
            StartCoroutine(ProfilerBurst(profilerCase));
            return true;
        }

        public void Configure(TextInput noListener,
            TextInput runtimeListener,
            TextInputLifecycleQaRuntimeListener runtimeObserver,
            TextInput persistentListener,
            TextInputLifecycleQaReceiver receiver,
            string exactSourceHead,
            string exactSourceTree)
        {
            noListenerInput = noListener;
            runtimeListenerInput = runtimeListener;
            this.runtimeListener = runtimeObserver;
            persistentListenerInput = persistentListener;
            persistentReceiver = receiver;
            sourceHead = exactSourceHead;
            sourceTree = exactSourceTree;
        }

        private IEnumerator Start()
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(RunScenarios());
            while (stack.Count > 0)
            {
                object? current;
                try
                {
                    var active = stack.Peek();
                    if (!active.MoveNext())
                    {
                        (active as IDisposable)?.Dispose();
                        stack.Pop();
                        continue;
                    }
                    current = active.Current;
                }
                catch (Exception exception)
                {
                    while (stack.Count > 0)
                    {
                        (stack.Pop() as IDisposable)?.Dispose();
                    }
                    CompleteFailure(exception);
                    yield break;
                }

                if (current is IEnumerator nested)
                {
                    stack.Push(nested);
                    continue;
                }
                yield return current;
            }

            CompleteSuccess();
        }

        private IEnumerator RunScenarios()
        {
            Require(noListenerInput != null, "No-listener TextInput is missing.");
            Require(runtimeListenerInput != null, "Runtime-listener TextInput is missing.");
            Require(runtimeListener != null, "Runtime AddListener observer is missing.");
            Require(persistentListenerInput != null, "Persistent-listener TextInput is missing.");
            Require(persistentReceiver != null, "Persistent receiver is missing.");
            Require(EventSystem.current != null, "QA scene has no active EventSystem.");
            Require(sourceHead.Length == 40 && sourceTree.Length == 40,
                "QA source HEAD/tree must be exact 40-character object IDs.");

            ValidateInitialSceneLoadSilence(runtimeListener!,
                persistentReceiver!,
                sourceHead,
                sourceTree);
            initialSceneLoadPassed = true;
            initialSceneLoadRuntimeListenerPassed = true;
            initialSceneLoadPersistentReceiverPassed = true;
            initialSceneLoadStableRecorderPassed = true;
            targetSceneInitialLoadPassed = true;
            TextInputLifecycleQaStableRecorder.Disarm();
            TextInputLifecycleQaStableRecorder.Reset();

            activeProvider = new TextInputLifecycleQaStrictProvider();
            activeProviderRegistration = HybridInputRuntime.RegisterProvider(activeProvider);
            var retainForProfiler = false;
            try
            {
                HybridInputRuntime.SetProviderOverride(TextInputLifecycleQaStrictProvider.ProviderId);
                yield return WaitUntil(() =>
                        HybridInputRuntime.Diagnostics.ProviderId ==
                        TextInputLifecycleQaStrictProvider.ProviderId,
                    "provider selection");

                var diagnosticBaseline = HybridInputRuntime.Diagnostics.DiagnosticCount;
                yield return RunNoListener(activeProvider, noListenerInput!, "qa-no-listener");
                Require(HybridInputRuntime.Diagnostics.DiagnosticCount == diagnosticBaseline,
                    "No-listener scenario changed diagnostics.");
                noListenerPassed = true;

                runtimeListener!.ResetRecords();
                yield return RunObserved(activeProvider,
                    runtimeListenerInput!,
                    "qa-runtime-listener",
                    () => runtimeListener.ValueChangedCount == 1,
                    () => runtimeListener.EndEditCount == 1 && runtimeListener.FocusLostCount == 1);
                ValidateRuntimeListener(runtimeListener,
                    "qa-runtime-listener",
                    "runtime AddListener");
                runtimeListenerPassed = true;

                persistentReceiver!.ResetRecords();
                yield return RunObserved(activeProvider,
                    persistentListenerInput!,
                    "qa-persistent-listener",
                    () => persistentReceiver.ValueChangedCount == 1,
                    () => persistentReceiver.EndEditCount == 1 &&
                          persistentReceiver.FocusLostCount == 1);
                ValidateReceiver(persistentReceiver, "qa-persistent-listener");
                persistentListenerPassed = true;

                yield return RunExceptionAndRecoveryMatrix(activeProvider,
                    runtimeListenerInput!,
                    runtimeListener,
                    HybridInputRuntime.Diagnostics.DiagnosticCount);
                exceptionRecoveryPassed = true;
                retainForProfiler = Application.isEditor;
            }
            finally
            {
                EventSystem.current?.SetSelectedGameObject(null);
                if (!retainForProfiler)
                {
                    ReleaseProvider();
                }
            }
        }

        private IEnumerator ProfilerBurst(TextInputLifecycleQaProfilerCase profilerCase)
        {
            profilerBurstRunning = true;
            var snapshot = CaptureProfilerState();
            var delivered = 0;
            var listenerShapePassed = false;
            var stateRestored = false;
            try
            {
                var input = ResolveProfilerInput(profilerCase);
                EventSystem.current!.SetSelectedGameObject(null);
                yield return null;
                SetProfilerCaseActive(profilerCase);
                yield return null;
                ValidateProfilerIsolation(profilerCase, input);
                listenerShapePassed = true;
                ResetProfilerRecords(profilerCase);
                input.SetTextWithoutNotify("task159-profiler-baseline");
                yield return Select(activeProvider!, input);
                for (var index = 0; index < ProfilerWarmupEvents; ++index)
                {
                    var expectedText = EnqueueProfilerEvent(index);
                    yield return null;
                    Require(input.Text == expectedText,
                        "Profiler warmup committed-text delivery mismatch.");
                }
                yield return null;
                ResetProfilerRecords(profilerCase);
                Debug.Log($"TASK159_QA_PROFILER case={profilerCase} phase=capture-begin " +
                          $"events={ProfilerCaptureEvents} isolated=true");
                yield return null;
                for (var index = 0; index < ProfilerCaptureEvents; ++index)
                {
                    var expectedText = EnqueueProfilerEvent(index);
                    yield return null;
                    Require(input.Text == expectedText,
                        "Profiler capture committed-text delivery mismatch.");
                    ++delivered;
                }
                yield return null;
                yield return null;
                ValidateProfilerCapture(profilerCase);
                Debug.Log($"TASK159_QA_PROFILER case={profilerCase} phase=capture-end " +
                          $"events={ProfilerCaptureEvents} isolated=true");
                EventSystem.current!.SetSelectedGameObject(null);
                yield return WaitUntil(() => !activeProvider!.HasFocusSession,
                    $"profiler {profilerCase} release");
            }
            finally
            {
                try
                {
                    RestoreProfilerState(snapshot);
                    stateRestored = true;
                }
                finally
                {
                    profilerBurstRunning = false;
                }
            }

            var result = new TextInputLifecycleQaProfilerResult
            {
                status = delivered == ProfilerCaptureEvents && listenerShapePassed && stateRestored
                    ? "PASS"
                    : "FAIL",
                profilerCase = profilerCase.ToString(),
                delivered = delivered,
                expectedDeliveries = ProfilerCaptureEvents,
                listenerShapePassed = listenerShapePassed,
                markerFramesSeparated = true,
                stateRestored = stateRestored,
                captureWindow = "idle begin-marker frame, frame boundary, 256 verified delivery " +
                                "frames, full idle frame, end marker"
            };
            Debug.Log("TASK159_QA_PROFILER_RESULT " + JsonUtility.ToJson(result));
        }

        private string EnqueueProfilerEvent(int index)
        {
            var expectedText = (index & 1) == 0
                ? "task159-profiler-a"
                : "task159-profiler-b";
            activeProvider!.EnqueueCommittedText(expectedText);
            return expectedText;
        }

        private ProfilerStateSnapshot CaptureProfilerState()
        {
            return new ProfilerStateSnapshot
            {
                Selection = EventSystem.current!.currentSelectedGameObject,
                ProviderHadFocusSession = activeProvider!.HasFocusSession,
                NoListenerWasActive = noListenerInput!.gameObject.activeSelf,
                RuntimeListenerWasActive = runtimeListenerInput!.gameObject.activeSelf,
                PersistentListenerWasActive = persistentListenerInput!.gameObject.activeSelf,
                RuntimeObserverWasEnabled = runtimeListener!.enabled,
                RuntimeObserverWasBound = runtimeListener.IsBoundTo(runtimeListenerInput),
                NoListenerText = noListenerInput.Text,
                RuntimeListenerText = runtimeListenerInput.Text,
                PersistentListenerText = persistentListenerInput.Text,
                RuntimeRecords = runtimeListener.CaptureRecords(),
                PersistentRecords = persistentReceiver!.CaptureRecords()
            };
        }

        private void RestoreProfilerState(ProfilerStateSnapshot snapshot)
        {
            var eventSystem = EventSystem.current!;
            eventSystem.SetSelectedGameObject(null);
            Require(!activeProvider!.HasFocusSession,
                "Profiler capture session did not release before state restoration.");

            runtimeListener!.enabled = snapshot.RuntimeObserverWasEnabled;
            noListenerInput!.gameObject.SetActive(snapshot.NoListenerWasActive);
            runtimeListenerInput!.gameObject.SetActive(snapshot.RuntimeListenerWasActive);
            persistentListenerInput!.gameObject.SetActive(snapshot.PersistentListenerWasActive);
            noListenerInput.SetTextWithoutNotify(snapshot.NoListenerText);
            runtimeListenerInput.SetTextWithoutNotify(snapshot.RuntimeListenerText);
            persistentListenerInput.SetTextWithoutNotify(snapshot.PersistentListenerText);

            eventSystem.SetSelectedGameObject(snapshot.Selection);
            runtimeListener.RestoreRecords(snapshot.RuntimeRecords);
            persistentReceiver!.RestoreRecords(snapshot.PersistentRecords);

            Require(noListenerInput.gameObject.activeSelf == snapshot.NoListenerWasActive &&
                    runtimeListenerInput.gameObject.activeSelf == snapshot.RuntimeListenerWasActive &&
                    persistentListenerInput.gameObject.activeSelf ==
                    snapshot.PersistentListenerWasActive,
                "Profiler case active state was not restored.");
            Require(noListenerInput.Text == snapshot.NoListenerText &&
                    runtimeListenerInput.Text == snapshot.RuntimeListenerText &&
                    persistentListenerInput.Text == snapshot.PersistentListenerText,
                "Profiler TextInput text state was not restored.");
            Require(eventSystem.currentSelectedGameObject == snapshot.Selection,
                "Profiler EventSystem selection was not restored.");
            Require(activeProvider.HasFocusSession == snapshot.ProviderHadFocusSession,
                "Profiler provider focus-session state was not restored.");
            Require(runtimeListener.enabled == snapshot.RuntimeObserverWasEnabled &&
                    runtimeListener.IsBoundTo(runtimeListenerInput) ==
                    snapshot.RuntimeObserverWasBound,
                "Profiler runtime listener binding state was not restored.");
            Require(runtimeListener.RecordsMatch(snapshot.RuntimeRecords) &&
                    persistentReceiver.RecordsMatch(snapshot.PersistentRecords),
                "Profiler receiver records were not restored.");
            Require(!TextInputLifecycleQaStableRecorder.IsArmed &&
                    TextInputLifecycleQaStableRecorder.ValueChangedCount == 0 &&
                    TextInputLifecycleQaStableRecorder.EndEditCount == 0 &&
                    TextInputLifecycleQaStableRecorder.FocusGainedCount == 0 &&
                    TextInputLifecycleQaStableRecorder.FocusLostCount == 0,
                "Profiler stable-recorder state leaked across the case.");
        }

        private sealed class ProfilerStateSnapshot
        {
            public GameObject? Selection;
            public bool ProviderHadFocusSession;
            public bool NoListenerWasActive;
            public bool RuntimeListenerWasActive;
            public bool PersistentListenerWasActive;
            public bool RuntimeObserverWasEnabled;
            public bool RuntimeObserverWasBound;
            public string NoListenerText = string.Empty;
            public string RuntimeListenerText = string.Empty;
            public string PersistentListenerText = string.Empty;
            public TextInputLifecycleQaRecordsSnapshot RuntimeRecords = null!;
            public TextInputLifecycleQaRecordsSnapshot PersistentRecords = null!;
        }

        private void SetProfilerCaseActive(TextInputLifecycleQaProfilerCase profilerCase)
        {
            noListenerInput!.gameObject.SetActive(
                profilerCase == TextInputLifecycleQaProfilerCase.NoListener);
            runtimeListenerInput!.gameObject.SetActive(
                profilerCase == TextInputLifecycleQaProfilerCase.RuntimeAddListener);
            persistentListenerInput!.gameObject.SetActive(
                profilerCase == TextInputLifecycleQaProfilerCase.InspectorPersistent);
        }

        private void ValidateProfilerIsolation(TextInputLifecycleQaProfilerCase profilerCase,
            TextInput target)
        {
            Require(target.isActiveAndEnabled, $"Profiler target {profilerCase} is inactive.");
            Require(noListenerInput!.isActiveAndEnabled ==
                    (profilerCase == TextInputLifecycleQaProfilerCase.NoListener),
                "No-listener profiler case isolation mismatch.");
            Require(runtimeListenerInput!.isActiveAndEnabled ==
                    (profilerCase == TextInputLifecycleQaProfilerCase.RuntimeAddListener),
                "Runtime-listener profiler case isolation mismatch.");
            Require(persistentListenerInput!.isActiveAndEnabled ==
                    (profilerCase == TextInputLifecycleQaProfilerCase.InspectorPersistent),
                "Persistent-listener profiler case isolation mismatch.");
#if UNITY_2023_1_OR_NEWER
            var activeInputs = UnityEngine.Object.FindObjectsByType<TextInput>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
#else
            var activeInputs = UnityEngine.Object.FindObjectsOfType<TextInput>();
#endif
            Require(activeInputs.Length == 1 && activeInputs[0] == target,
                $"Profiler case {profilerCase} does not have exactly one active TextInput.");

            switch (profilerCase)
            {
                case TextInputLifecycleQaProfilerCase.NoListener:
                {
                    Require(HasNoPersistentListeners(target),
                        "No-listener profiler target has a persistent listener.");
                    var noListenerBehaviours = target.GetComponents<MonoBehaviour>();
                    Require(noListenerBehaviours.Length == 1 &&
                            noListenerBehaviours[0] == target,
                        "No-listener profiler target has an unexpected listener component.");
                    Require(!runtimeListener!.isActiveAndEnabled &&
                            !runtimeListener.IsBoundTo(runtimeListenerInput),
                        "Runtime AddListener observer leaked into the no-listener profiler case.");
                    break;
                }
                case TextInputLifecycleQaProfilerCase.RuntimeAddListener:
                    Require(HasNoPersistentListeners(target),
                        "Runtime AddListener profiler target has a persistent listener.");
                    Require(runtimeListener!.isActiveAndEnabled && runtimeListener.IsBoundTo(target),
                        "Runtime AddListener observer is not uniquely bound to its profiler target.");
                    break;
                case TextInputLifecycleQaProfilerCase.InspectorPersistent:
                    Require(!runtimeListener!.isActiveAndEnabled &&
                            !runtimeListener.IsBoundTo(runtimeListenerInput),
                        "Runtime AddListener observer leaked into the persistent profiler case.");
                    Require(HasExpectedPersistentListeners(target, persistentReceiver!),
                        "Persistent profiler target binding mismatch.");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(profilerCase), profilerCase, null);
            }
        }

        private void ResetProfilerRecords(TextInputLifecycleQaProfilerCase profilerCase)
        {
            if (profilerCase == TextInputLifecycleQaProfilerCase.RuntimeAddListener)
            {
                runtimeListener!.ResetRecords();
            }
            else if (profilerCase == TextInputLifecycleQaProfilerCase.InspectorPersistent)
            {
                persistentReceiver!.ResetRecords();
            }
        }

        private void ValidateProfilerCapture(TextInputLifecycleQaProfilerCase profilerCase)
        {
            if (profilerCase == TextInputLifecycleQaProfilerCase.RuntimeAddListener)
            {
                Require(runtimeListener!.ValueChangedCount == ProfilerCaptureEvents &&
                        runtimeListener.FocusGainedCount == 0 &&
                        runtimeListener.EndEditCount == 0 &&
                        runtimeListener.FocusLostCount == 0 &&
                        runtimeListener.Sequence.Count == ProfilerCaptureEvents,
                    "Runtime AddListener profiler capture count mismatch.");
            }
            else if (profilerCase == TextInputLifecycleQaProfilerCase.InspectorPersistent)
            {
                Require(persistentReceiver!.ValueChangedCount == ProfilerCaptureEvents &&
                        persistentReceiver.FocusGainedCount == 0 &&
                        persistentReceiver.EndEditCount == 0 &&
                        persistentReceiver.FocusLostCount == 0 &&
                        persistentReceiver.Sequence.Count == ProfilerCaptureEvents,
                    "Persistent profiler capture count mismatch.");
            }
        }

        private static bool HasNoPersistentListeners(TextInput input)
        {
            return input.onValueChanged.GetPersistentEventCount() == 0 &&
                   input.onEndEdit.GetPersistentEventCount() == 0 &&
                   input.onFocusGained.GetPersistentEventCount() == 0 &&
                   input.onFocusLost.GetPersistentEventCount() == 0;
        }

        private static bool HasExpectedPersistentListeners(TextInput input,
            TextInputLifecycleQaReceiver receiver)
        {
            return HasExpectedPersistentListener(input.onValueChanged,
                       receiver,
                       "OnValueChanged") &&
                   HasExpectedPersistentListener(input.onEndEdit, receiver, "OnEndEdit") &&
                   HasExpectedPersistentListener(input.onFocusGained,
                       receiver,
                       "OnFocusGained") &&
                   HasExpectedPersistentListener(input.onFocusLost, receiver, "OnFocusLost");
        }

        private static bool HasExpectedPersistentListener(UnityEventBase unityEvent,
            TextInputLifecycleQaReceiver receiver,
            string methodName)
        {
            return unityEvent.GetPersistentEventCount() == 1 &&
                   unityEvent.GetPersistentTarget(0) == receiver &&
                   unityEvent.GetPersistentMethodName(0) == methodName;
        }

        private TextInput ResolveProfilerInput(TextInputLifecycleQaProfilerCase profilerCase)
        {
            return profilerCase switch
            {
                TextInputLifecycleQaProfilerCase.NoListener => noListenerInput!,
                TextInputLifecycleQaProfilerCase.RuntimeAddListener => runtimeListenerInput!,
                TextInputLifecycleQaProfilerCase.InspectorPersistent => persistentListenerInput!,
                _ => throw new ArgumentOutOfRangeException(nameof(profilerCase), profilerCase, null)
            };
        }

        private void OnDestroy()
        {
            ReleaseProvider();
        }

        private void ReleaseProvider()
        {
            EventSystem.current?.SetSelectedGameObject(null);
            HybridInputRuntime.SetProviderOverride(null);
            activeProviderRegistration?.Dispose();
            activeProviderRegistration = null;
            activeProvider = null;
        }

        private static IEnumerator RunNoListener(TextInputLifecycleQaStrictProvider provider,
            TextInput input,
            string payload)
        {
            yield return Select(provider, input);
            provider.EnqueueCommittedText(payload);
            yield return WaitUntil(() => input.Text == payload, "no-listener committed text");
            EventSystem.current!.SetSelectedGameObject(null);
            yield return WaitUntil(() => !provider.HasFocusSession, "no-listener release");
        }

        private static IEnumerator RunObserved(TextInputLifecycleQaStrictProvider provider,
            TextInput input,
            string payload,
            Func<bool> valueObserved,
            Func<bool> terminalObserved)
        {
            yield return Select(provider, input);
            provider.EnqueueCommittedText(payload);
            yield return WaitUntil(() => input.Text == payload && valueObserved(),
                $"{payload} committed text");
            EventSystem.current!.SetSelectedGameObject(null);
            yield return WaitUntil(() => !provider.HasFocusSession && terminalObserved(),
                $"{payload} terminal lifecycle");
        }

        private static IEnumerator Select(TextInputLifecycleQaStrictProvider provider, TextInput input)
        {
            EventSystem.current!.SetSelectedGameObject(null);
            yield return null;
            EventSystem.current.SetSelectedGameObject(input.gameObject);
            yield return WaitUntil(() => provider.HasFocusSession, $"focus {input.name}");
        }

        private IEnumerator RunExceptionAndRecoveryMatrix(
            TextInputLifecycleQaStrictProvider provider,
            TextInput input,
            TextInputLifecycleQaRuntimeListener runtimeListener,
            int diagnosticBaseline)
        {
            Require(HasNoPersistentListeners(input),
                "Exception matrix input must not have persistent listeners.");
            var expectedDiagnosticCount = diagnosticBaseline;
            try
            {
                for (var eventIndex = 0;
                     eventIndex <= (int)TextInputLifecycleQaExceptionEvent.FocusLost;
                     ++eventIndex)
                {
                    var lifecycleEvent = (TextInputLifecycleQaExceptionEvent)eventIndex;
                    runtimeListener.enabled = false;
                    Require(!runtimeListener.IsBoundTo(input),
                        $"Runtime listener remained bound before {lifecycleEvent} exception case.");
                    yield return RunExceptionCase(provider,
                        input,
                        lifecycleEvent,
                        expectedDiagnosticCount);
                    ++exceptionCasesPassed;
                    ++expectedDiagnosticCount;

                    var recoveryPayload = $"qa-{lifecycleEvent}-recovery";
                    yield return RunRecovery(provider,
                        input,
                        runtimeListener,
                        lifecycleEvent,
                        recoveryPayload,
                        expectedDiagnosticCount);
                    ++exceptionRecoveriesPassed;
                }
            }
            finally
            {
                EventSystem.current?.SetSelectedGameObject(null);
                runtimeListener.enabled = true;
            }
        }

        private IEnumerator RunRecovery(TextInputLifecycleQaStrictProvider provider,
            TextInput input,
            TextInputLifecycleQaRuntimeListener runtimeListener,
            TextInputLifecycleQaExceptionEvent lifecycleEvent,
            string payload,
            int expectedDiagnosticCount)
        {
            var releasedGeneration = provider.SessionGeneration;
            var expectedBeginCount = provider.BeginCount + 1;
            var expectedEndCount = provider.EndCount;
            var released = CaptureRecoveryCheckpoint(lifecycleEvent,
                "exception-released",
                provider,
                input,
                runtimeListener);
            RequireRecovery(!released.sessionActive && released.selectedOwner.Length == 0,
                lifecycleEvent,
                "exception release did not close selection and provider session",
                provider,
                input,
                runtimeListener);

            runtimeListener.ResetRecords();
            runtimeListener.enabled = true;
            CaptureRecoveryCheckpoint(lifecycleEvent,
                "listener-bound",
                provider,
                input,
                runtimeListener);
            RequireRecovery(runtimeListener.IsBoundTo(input),
                lifecycleEvent,
                "runtime listener did not bind",
                provider,
                input,
                runtimeListener);

            EventSystem.current!.SetSelectedGameObject(null);
            input.SetTextWithoutNotify(string.Empty);
            RequireRecovery(input.Text.Length == 0,
                lifecycleEvent,
                "silent text reset did not clear the recovery input",
                provider,
                input,
                runtimeListener);
            yield return null;
            EventSystem.current.SetSelectedGameObject(input.gameObject);
            yield return WaitUntilRecovery(() => provider.HasFocusSession,
                lifecycleEvent,
                "fresh-session",
                provider,
                input,
                runtimeListener);
            var freshSession = CaptureRecoveryCheckpoint(lifecycleEvent,
                "fresh-session",
                provider,
                input,
                runtimeListener);
            RequireRecovery(freshSession.selectedInput &&
                            freshSession.sessionActive &&
                            freshSession.providerBeginCount == expectedBeginCount &&
                            freshSession.providerEndCount == expectedEndCount &&
                            freshSession.activeSessionGeneration > releasedGeneration &&
                            freshSession.activeSinkIdentity != 0,
                lifecycleEvent,
                "fresh session identity did not advance exactly",
                provider,
                input,
                runtimeListener);
            RequireRecovery(freshSession.applicationFocused,
                lifecycleEvent,
                "application focus gate is closed",
                provider,
                input,
                runtimeListener);
            RequireRecovery(runtimeListener.FocusGainedCount == 1,
                lifecycleEvent,
                "fresh FocusGained was not delivered to the rebound listener",
                provider,
                input,
                runtimeListener);

            provider.EnqueueCommittedText(payload);
            CaptureRecoveryCheckpoint(lifecycleEvent,
                "text-enqueued",
                provider,
                input,
                runtimeListener);
            yield return WaitUntilRecovery(() =>
                    input.Text == payload && runtimeListener.ValueChangedCount == 1,
                lifecycleEvent,
                "committed-text",
                provider,
                input,
                runtimeListener);
            CaptureRecoveryCheckpoint(lifecycleEvent,
                "committed-text",
                provider,
                input,
                runtimeListener);

            EventSystem.current.SetSelectedGameObject(null);
            yield return WaitUntilRecovery(() =>
                    !provider.HasFocusSession &&
                    runtimeListener.EndEditCount == 1 &&
                    runtimeListener.FocusLostCount == 1,
                lifecycleEvent,
                "terminal-release",
                provider,
                input,
                runtimeListener);
            CaptureRecoveryCheckpoint(lifecycleEvent,
                "terminal-release",
                provider,
                input,
                runtimeListener);
            ValidateRuntimeListener(runtimeListener,
                payload,
                $"{lifecycleEvent} fresh-session recovery");
            RequireRecovery(HybridInputRuntime.Diagnostics.DiagnosticCount ==
                            expectedDiagnosticCount,
                lifecycleEvent,
                "recovery added an unexpected diagnostic",
                provider,
                input,
                runtimeListener);
        }

        private static IEnumerator RunExceptionCase(TextInputLifecycleQaStrictProvider provider,
            TextInput input,
            TextInputLifecycleQaExceptionEvent lifecycleEvent,
            int diagnosticBaseline)
        {
            var payload = $"qa-{lifecycleEvent}-throw";
            EventSystem.current!.SetSelectedGameObject(null);
            input.SetTextWithoutNotify(lifecycleEvent == TextInputLifecycleQaExceptionEvent.FocusGained
                ? $"qa-{lifecycleEvent}-baseline"
                : string.Empty);

            var throwingCount = 0;
            var tailCount = 0;
            var focusGainedCount = 0;
            var valueChangedCount = 0;
            var endEditCount = 0;
            var focusLostCount = 0;
            var callbackStateWasCommitted = false;

            UnityAction focusGainedObserver = () => ++focusGainedCount;
            UnityAction<string> valueChangedObserver = _ => ++valueChangedCount;
            UnityAction<string> endEditObserver = _ => ++endEditCount;
            UnityAction focusLostObserver = () => ++focusLostCount;
            UnityAction throwingVoid = () =>
            {
                ++throwingCount;
                callbackStateWasCommitted = CallbackStateWasCommitted(lifecycleEvent,
                    provider,
                    input,
                    payload,
                    null);
                throw new InvalidOperationException($"task159 {lifecycleEvent} listener");
            };
            UnityAction<string> throwingString = value =>
            {
                ++throwingCount;
                callbackStateWasCommitted = CallbackStateWasCommitted(lifecycleEvent,
                    provider,
                    input,
                    payload,
                    value);
                throw new InvalidOperationException($"task159 {lifecycleEvent} listener");
            };
            UnityAction tailVoid = () => ++tailCount;
            UnityAction<string> tailString = _ => ++tailCount;

            AddExceptionCaseListeners(input,
                lifecycleEvent,
                focusGainedObserver,
                valueChangedObserver,
                endEditObserver,
                focusLostObserver,
                throwingVoid,
                throwingString,
                tailVoid,
                tailString);
            try
            {
                if (lifecycleEvent == TextInputLifecycleQaExceptionEvent.FocusGained)
                {
                    yield return BeginSelection(input);
                }
                else
                {
                    yield return Select(provider, input);
                    provider.EnqueueCommittedText(payload);
                    if (lifecycleEvent == TextInputLifecycleQaExceptionEvent.ValueChanged)
                    {
                        yield return WaitUntil(() =>
                                input.Text == payload && throwingCount == 1 &&
                                HybridInputRuntime.Diagnostics.DiagnosticCount >=
                                diagnosticBaseline + 1,
                            $"{lifecycleEvent} exception diagnostic");
                    }
                    else
                    {
                        yield return WaitUntil(() =>
                                input.Text == payload && valueChangedCount == 1,
                            $"{lifecycleEvent} committed text");
                        EventSystem.current!.SetSelectedGameObject(null);
                    }
                }

                yield return WaitUntil(() =>
                        throwingCount == 1 &&
                        HybridInputRuntime.Diagnostics.DiagnosticCount >= diagnosticBaseline + 1,
                    $"{lifecycleEvent} exception delivery");
                if (lifecycleEvent == TextInputLifecycleQaExceptionEvent.EndEdit ||
                    lifecycleEvent == TextInputLifecycleQaExceptionEvent.FocusLost)
                {
                    yield return WaitUntil(() => !provider.HasFocusSession,
                        $"{lifecycleEvent} exception release");
                }

                ValidateExceptionCase(lifecycleEvent,
                    throwingCount,
                    tailCount,
                    focusGainedCount,
                    valueChangedCount,
                    endEditCount,
                    focusLostCount,
                    callbackStateWasCommitted,
                    diagnosticBaseline);

                // Keep the throwing listener installed across idle dispatcher work. This proves
                // the aborted notification is neither retried nor completed late.
                yield return null;
                yield return null;
                ValidateExceptionCase(lifecycleEvent,
                    throwingCount,
                    tailCount,
                    focusGainedCount,
                    valueChangedCount,
                    endEditCount,
                    focusLostCount,
                    callbackStateWasCommitted,
                    diagnosticBaseline);
            }
            finally
            {
                RemoveExceptionCaseListeners(input,
                    lifecycleEvent,
                    focusGainedObserver,
                    valueChangedObserver,
                    endEditObserver,
                    focusLostObserver,
                    throwingVoid,
                    throwingString,
                    tailVoid,
                    tailString);
                EventSystem.current?.SetSelectedGameObject(null);
            }

            yield return WaitUntil(() => !provider.HasFocusSession,
                $"{lifecycleEvent} cleanup release");
            Require(HybridInputRuntime.Diagnostics.DiagnosticCount == diagnosticBaseline + 1,
                $"{lifecycleEvent} cleanup added an unexpected diagnostic.");
        }

        private static IEnumerator BeginSelection(TextInput input)
        {
            EventSystem.current!.SetSelectedGameObject(null);
            yield return null;
            EventSystem.current.SetSelectedGameObject(input.gameObject);
        }

        private static bool CallbackStateWasCommitted(
            TextInputLifecycleQaExceptionEvent lifecycleEvent,
            TextInputLifecycleQaStrictProvider provider,
            TextInput input,
            string payload,
            string? callbackPayload)
        {
            switch (lifecycleEvent)
            {
                case TextInputLifecycleQaExceptionEvent.FocusGained:
                    return provider.HasFocusSession &&
                           EventSystem.current?.currentSelectedGameObject == input.gameObject;
                case TextInputLifecycleQaExceptionEvent.ValueChanged:
                    return provider.HasFocusSession && input.Text == payload &&
                           callbackPayload == payload;
                case TextInputLifecycleQaExceptionEvent.EndEdit:
                    return !provider.HasFocusSession && input.Text == payload &&
                           callbackPayload == payload;
                case TextInputLifecycleQaExceptionEvent.FocusLost:
                    return !provider.HasFocusSession && input.Text == payload;
                default:
                    throw new ArgumentOutOfRangeException(nameof(lifecycleEvent),
                        lifecycleEvent,
                        null);
            }
        }

        private static void ValidateExceptionCase(
            TextInputLifecycleQaExceptionEvent lifecycleEvent,
            int throwingCount,
            int tailCount,
            int focusGainedCount,
            int valueChangedCount,
            int endEditCount,
            int focusLostCount,
            bool callbackStateWasCommitted,
            int diagnosticBaseline)
        {
            Require(throwingCount == 1, $"{lifecycleEvent} throwing listener count mismatch.");
            Require(tailCount == 0, $"{lifecycleEvent} UnityEvent tail was not suppressed.");
            Require(callbackStateWasCommitted,
                $"{lifecycleEvent} listener observed uncommitted production state.");
            Require(HybridInputRuntime.Diagnostics.LastDiagnostic ==
                    HybridInputDiagnosticCode.ListenerException,
                $"{lifecycleEvent} diagnostic code mismatch.");
            Require(HybridInputRuntime.Diagnostics.DiagnosticCount == diagnosticBaseline + 1,
                $"{lifecycleEvent} diagnostic count mismatch.");

            var expectedFocusGained = lifecycleEvent ==
                TextInputLifecycleQaExceptionEvent.FocusGained ? 0 : 1;
            var expectedValueChanged = lifecycleEvent ==
                TextInputLifecycleQaExceptionEvent.FocusGained ||
                lifecycleEvent == TextInputLifecycleQaExceptionEvent.ValueChanged ? 0 : 1;
            var expectedEndEdit = lifecycleEvent == TextInputLifecycleQaExceptionEvent.FocusLost
                ? 1
                : 0;
            Require(focusGainedCount == expectedFocusGained,
                $"{lifecycleEvent} preceding FocusGained count mismatch.");
            Require(valueChangedCount == expectedValueChanged,
                $"{lifecycleEvent} preceding ValueChanged count mismatch.");
            Require(endEditCount == expectedEndEdit,
                $"{lifecycleEvent} EndEdit count mismatch.");
            Require(focusLostCount == 0,
                $"{lifecycleEvent} FocusLost was delivered or retried unexpectedly.");
        }

        private static void AddExceptionCaseListeners(TextInput input,
            TextInputLifecycleQaExceptionEvent lifecycleEvent,
            UnityAction focusGainedObserver,
            UnityAction<string> valueChangedObserver,
            UnityAction<string> endEditObserver,
            UnityAction focusLostObserver,
            UnityAction throwingVoid,
            UnityAction<string> throwingString,
            UnityAction tailVoid,
            UnityAction<string> tailString)
        {
            if (lifecycleEvent == TextInputLifecycleQaExceptionEvent.FocusGained)
            {
                input.onFocusGained.AddListener(throwingVoid);
                input.onFocusGained.AddListener(tailVoid);
            }
            else
            {
                input.onFocusGained.AddListener(focusGainedObserver);
            }
            if (lifecycleEvent == TextInputLifecycleQaExceptionEvent.ValueChanged)
            {
                input.onValueChanged.AddListener(throwingString);
                input.onValueChanged.AddListener(tailString);
            }
            else
            {
                input.onValueChanged.AddListener(valueChangedObserver);
            }
            if (lifecycleEvent == TextInputLifecycleQaExceptionEvent.EndEdit)
            {
                input.onEndEdit.AddListener(throwingString);
                input.onEndEdit.AddListener(tailString);
            }
            else
            {
                input.onEndEdit.AddListener(endEditObserver);
            }
            if (lifecycleEvent == TextInputLifecycleQaExceptionEvent.FocusLost)
            {
                input.onFocusLost.AddListener(throwingVoid);
                input.onFocusLost.AddListener(tailVoid);
            }
            else
            {
                input.onFocusLost.AddListener(focusLostObserver);
            }
        }

        private static void RemoveExceptionCaseListeners(TextInput input,
            TextInputLifecycleQaExceptionEvent lifecycleEvent,
            UnityAction focusGainedObserver,
            UnityAction<string> valueChangedObserver,
            UnityAction<string> endEditObserver,
            UnityAction focusLostObserver,
            UnityAction throwingVoid,
            UnityAction<string> throwingString,
            UnityAction tailVoid,
            UnityAction<string> tailString)
        {
            if (lifecycleEvent == TextInputLifecycleQaExceptionEvent.FocusGained)
            {
                input.onFocusGained.RemoveListener(throwingVoid);
                input.onFocusGained.RemoveListener(tailVoid);
            }
            else
            {
                input.onFocusGained.RemoveListener(focusGainedObserver);
            }
            if (lifecycleEvent == TextInputLifecycleQaExceptionEvent.ValueChanged)
            {
                input.onValueChanged.RemoveListener(throwingString);
                input.onValueChanged.RemoveListener(tailString);
            }
            else
            {
                input.onValueChanged.RemoveListener(valueChangedObserver);
            }
            if (lifecycleEvent == TextInputLifecycleQaExceptionEvent.EndEdit)
            {
                input.onEndEdit.RemoveListener(throwingString);
                input.onEndEdit.RemoveListener(tailString);
            }
            else
            {
                input.onEndEdit.RemoveListener(endEditObserver);
            }
            if (lifecycleEvent == TextInputLifecycleQaExceptionEvent.FocusLost)
            {
                input.onFocusLost.RemoveListener(throwingVoid);
                input.onFocusLost.RemoveListener(tailVoid);
            }
            else
            {
                input.onFocusLost.RemoveListener(focusLostObserver);
            }
        }

        private IEnumerator WaitUntilRecovery(Func<bool> predicate,
            TextInputLifecycleQaExceptionEvent lifecycleEvent,
            string stage,
            TextInputLifecycleQaStrictProvider provider,
            TextInput input,
            TextInputLifecycleQaRuntimeListener runtimeListener)
        {
            for (var frame = 0; frame < TimeoutFrames; ++frame)
            {
                if (predicate())
                {
                    yield break;
                }
                yield return null;
            }
            var checkpoint = CaptureRecoveryCheckpoint(lifecycleEvent,
                stage + "-timeout",
                provider,
                input,
                runtimeListener);
            throw new TimeoutException(
                $"Timed out at {lifecycleEvent} recovery gate {stage}: " +
                JsonUtility.ToJson(checkpoint));
        }

        private TextInputLifecycleQaRecoveryCheckpoint CaptureRecoveryCheckpoint(
            TextInputLifecycleQaExceptionEvent lifecycleEvent,
            string stage,
            TextInputLifecycleQaStrictProvider provider,
            TextInput input,
            TextInputLifecycleQaRuntimeListener runtimeListener)
        {
            var diagnostics = HybridInputRuntime.Diagnostics;
            var selected = EventSystem.current?.currentSelectedGameObject;
            var checkpoint = new TextInputLifecycleQaRecoveryCheckpoint
            {
                lifecycleEvent = lifecycleEvent.ToString(),
                stage = stage,
                selectedOwner = selected == null ? string.Empty : selected.name,
                selectedInput = selected == input.gameObject,
                applicationFocused = diagnostics.ApplicationFocused,
                inputActive = input.gameObject.activeInHierarchy,
                inputEnabled = input.isActiveAndEnabled,
                sessionActive = provider.HasFocusSession,
                providerBeginCount = provider.BeginCount,
                providerEndCount = provider.EndCount,
                sessionGeneration = provider.SessionGeneration,
                activeSessionGeneration = provider.ActiveSessionGeneration,
                activeSinkIdentity = provider.ActiveSinkIdentity,
                text = input.Text,
                runtimeFocusGainedCount = runtimeListener.FocusGainedCount,
                runtimeValueChangedCount = runtimeListener.ValueChangedCount,
                runtimeEndEditCount = runtimeListener.EndEditCount,
                runtimeFocusLostCount = runtimeListener.FocusLostCount,
                persistentFocusGainedCount = persistentReceiver!.FocusGainedCount,
                persistentValueChangedCount = persistentReceiver.ValueChangedCount,
                persistentEndEditCount = persistentReceiver.EndEditCount,
                persistentFocusLostCount = persistentReceiver.FocusLostCount,
                diagnosticCount = diagnostics.DiagnosticCount,
                lastDiagnostic = diagnostics.LastDiagnostic.ToString()
            };
            recoveryCheckpoints.Add(checkpoint);
            return checkpoint;
        }

        private void RequireRecovery(bool condition,
            TextInputLifecycleQaExceptionEvent lifecycleEvent,
            string gate,
            TextInputLifecycleQaStrictProvider provider,
            TextInput input,
            TextInputLifecycleQaRuntimeListener runtimeListener)
        {
            if (condition)
            {
                return;
            }
            var checkpoint = CaptureRecoveryCheckpoint(lifecycleEvent,
                gate + "-failed",
                provider,
                input,
                runtimeListener);
            throw new InvalidOperationException(
                $"{lifecycleEvent} recovery gate failed ({gate}): " +
                JsonUtility.ToJson(checkpoint));
        }

        private static IEnumerator WaitUntil(Func<bool> predicate, string stage)
        {
            for (var frame = 0; frame < TimeoutFrames; ++frame)
            {
                if (predicate())
                {
                    yield break;
                }
                yield return null;
            }
            throw new TimeoutException($"Timed out while waiting for {stage}.");
        }

        private static void ValidateInitialSceneLoadSilence(
            TextInputLifecycleQaRuntimeListener runtimeRecords,
            TextInputLifecycleQaReceiver persistentRecords,
            string expectedSourceHead,
            string expectedSourceTree)
        {
            Require(TextInputLifecycleQaStableRecorder.IsArmed,
                "Stable recorder was not armed before the initial scene load.");
            Require(TextInputLifecycleQaStableRecorder.ArmedSourceHead == expectedSourceHead &&
                    TextInputLifecycleQaStableRecorder.ArmedSourceTree == expectedSourceTree,
                "Stable recorder was not armed by the exact target-scene bootstrap.");
            Require(runtimeRecords.FocusGainedCount == 0 &&
                    runtimeRecords.ValueChangedCount == 0 &&
                    runtimeRecords.EndEditCount == 0 &&
                    runtimeRecords.FocusLostCount == 0 &&
                    runtimeRecords.Sequence.Count == 0,
                "Initial scene load invoked the runtime AddListener observer.");
            Require(persistentRecords.FocusGainedCount == 0 &&
                    persistentRecords.ValueChangedCount == 0 &&
                    persistentRecords.EndEditCount == 0 &&
                    persistentRecords.FocusLostCount == 0 &&
                    persistentRecords.Sequence.Count == 0,
                "Initial scene load invoked the persistent receiver.");
            Require(TextInputLifecycleQaStableRecorder.FocusGainedCount == 0 &&
                    TextInputLifecycleQaStableRecorder.ValueChangedCount == 0 &&
                    TextInputLifecycleQaStableRecorder.EndEditCount == 0 &&
                    TextInputLifecycleQaStableRecorder.FocusLostCount == 0,
                "Initial scene load invoked an imported or transient persistent receiver.");
        }

        private static void ValidateRuntimeListener(TextInputLifecycleQaRuntimeListener records,
            string payload,
            string label)
        {
            Require(records.FocusGainedCount == 1, $"{label} FocusGained count mismatch.");
            Require(records.ValueChangedCount == 1, $"{label} ValueChanged count mismatch.");
            Require(records.EndEditCount == 1, $"{label} EndEdit count mismatch.");
            Require(records.FocusLostCount == 1, $"{label} FocusLost count mismatch.");
            Require(records.ValueChangedPayload == payload && records.EndEditPayload == payload,
                $"{label} payload mismatch.");
            Require(string.Join(",", records.Sequence) ==
                    "FocusGained,ValueChanged,EndEdit,FocusLost",
                $"{label} sequence mismatch.");
        }

        private static void ValidateReceiver(TextInputLifecycleQaReceiver receiver, string payload)
        {
            Require(receiver.FocusGainedCount == 1, "Persistent FocusGained count mismatch.");
            Require(receiver.ValueChangedCount == 1, "Persistent ValueChanged count mismatch.");
            Require(receiver.EndEditCount == 1, "Persistent EndEdit count mismatch.");
            Require(receiver.FocusLostCount == 1, "Persistent FocusLost count mismatch.");
            Require(receiver.ValueChangedPayload == payload && receiver.EndEditPayload == payload,
                "Persistent payload mismatch.");
            Require(string.Join(",", receiver.Sequence) ==
                    "FocusGained,ValueChanged,EndEdit,FocusLost",
                "Persistent sequence mismatch.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private void CompleteSuccess()
        {
            Result = new TextInputLifecycleQaResult
            {
                status = "PASS",
                sourceHead = sourceHead,
                sourceTree = sourceTree,
                initialSceneLoadPassed = initialSceneLoadPassed,
                initialSceneLoadRuntimeListenerPassed = initialSceneLoadRuntimeListenerPassed,
                initialSceneLoadPersistentReceiverPassed = initialSceneLoadPersistentReceiverPassed,
                initialSceneLoadStableRecorderPassed = initialSceneLoadStableRecorderPassed,
                targetSceneInitialLoadPassed = targetSceneInitialLoadPassed,
                noListenerPassed = noListenerPassed,
                runtimeListenerPassed = runtimeListenerPassed,
                persistentListenerPassed = persistentListenerPassed,
                exceptionRecoveryPassed = exceptionRecoveryPassed,
                exceptionCasesPassed = exceptionCasesPassed,
                exceptionRecoveriesPassed = exceptionRecoveriesPassed,
                persistentFocusGainedCount = persistentReceiver!.FocusGainedCount,
                persistentValueChangedCount = persistentReceiver.ValueChangedCount,
                persistentEndEditCount = persistentReceiver.EndEditCount,
                persistentFocusLostCount = persistentReceiver.FocusLostCount,
                persistentValuePayload = persistentReceiver.ValueChangedPayload,
                persistentEndPayload = persistentReceiver.EndEditPayload,
                persistentSequence = string.Join(",", persistentReceiver.Sequence),
                recoveryCheckpoints = recoveryCheckpoints.ToArray(),
                message = "Production TextInput UnityEvent lifecycle scenarios passed."
            };
            Complete();
        }

        private void CompleteFailure(Exception exception)
        {
            Result = new TextInputLifecycleQaResult
            {
                status = "FAIL",
                sourceHead = sourceHead,
                sourceTree = sourceTree,
                initialSceneLoadPassed = initialSceneLoadPassed,
                initialSceneLoadRuntimeListenerPassed = initialSceneLoadRuntimeListenerPassed,
                initialSceneLoadPersistentReceiverPassed = initialSceneLoadPersistentReceiverPassed,
                initialSceneLoadStableRecorderPassed = initialSceneLoadStableRecorderPassed,
                targetSceneInitialLoadPassed = targetSceneInitialLoadPassed,
                noListenerPassed = noListenerPassed,
                runtimeListenerPassed = runtimeListenerPassed,
                persistentListenerPassed = persistentListenerPassed,
                exceptionRecoveryPassed = exceptionRecoveryPassed,
                exceptionCasesPassed = exceptionCasesPassed,
                exceptionRecoveriesPassed = exceptionRecoveriesPassed,
                recoveryCheckpoints = recoveryCheckpoints.ToArray(),
                message = exception.GetType().Name + ": " + exception.Message
            };
            Complete();
        }

        private void Complete()
        {
            TextInputLifecycleQaStableRecorder.Disarm();
            Completed = true;
            Debug.Log("TASK159_QA_RESULT " + JsonUtility.ToJson(Result));
#if !UNITY_EDITOR
            Application.Quit(Result?.status == "PASS" ? 0 : 1);
#endif
        }

    }

    [Serializable]
    public sealed class TextInputLifecycleQaResult
    {
        public string status = string.Empty;
        public string sourceHead = string.Empty;
        public string sourceTree = string.Empty;
        public bool initialSceneLoadPassed;
        public bool initialSceneLoadRuntimeListenerPassed;
        public bool initialSceneLoadPersistentReceiverPassed;
        public bool initialSceneLoadStableRecorderPassed;
        public bool targetSceneInitialLoadPassed;
        public bool noListenerPassed;
        public bool runtimeListenerPassed;
        public bool persistentListenerPassed;
        public bool exceptionRecoveryPassed;
        public int exceptionCasesPassed;
        public int exceptionRecoveriesPassed;
        public int persistentFocusGainedCount;
        public int persistentValueChangedCount;
        public int persistentEndEditCount;
        public int persistentFocusLostCount;
        public string persistentValuePayload = string.Empty;
        public string persistentEndPayload = string.Empty;
        public string persistentSequence = string.Empty;
        public TextInputLifecycleQaRecoveryCheckpoint[] recoveryCheckpoints =
            Array.Empty<TextInputLifecycleQaRecoveryCheckpoint>();
        public string message = string.Empty;
    }

    [Serializable]
    public sealed class TextInputLifecycleQaRecoveryCheckpoint
    {
        public string lifecycleEvent = string.Empty;
        public string stage = string.Empty;
        public string selectedOwner = string.Empty;
        public bool selectedInput;
        public bool applicationFocused;
        public bool inputActive;
        public bool inputEnabled;
        public bool sessionActive;
        public int providerBeginCount;
        public int providerEndCount;
        public int sessionGeneration;
        public int activeSessionGeneration;
        public int activeSinkIdentity;
        public string text = string.Empty;
        public int runtimeFocusGainedCount;
        public int runtimeValueChangedCount;
        public int runtimeEndEditCount;
        public int runtimeFocusLostCount;
        public int persistentFocusGainedCount;
        public int persistentValueChangedCount;
        public int persistentEndEditCount;
        public int persistentFocusLostCount;
        public int diagnosticCount;
        public string lastDiagnostic = string.Empty;
    }

    [Serializable]
    public sealed class TextInputLifecycleQaProfilerResult
    {
        public string status = string.Empty;
        public string profilerCase = string.Empty;
        public int delivered;
        public int expectedDeliveries;
        public bool listenerShapePassed;
        public bool markerFramesSeparated;
        public bool stateRestored;
        public string captureWindow = string.Empty;
    }

    public enum TextInputLifecycleQaExceptionEvent
    {
        FocusGained = 0,
        ValueChanged = 1,
        EndEdit = 2,
        FocusLost = 3
    }

    public enum TextInputLifecycleQaProfilerCase
    {
        NoListener = 0,
        RuntimeAddListener = 1,
        InspectorPersistent = 2
    }
}
