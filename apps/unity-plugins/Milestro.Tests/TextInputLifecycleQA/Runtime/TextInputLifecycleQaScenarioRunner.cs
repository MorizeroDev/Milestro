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
        private bool profilerBurstRunning;
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

                yield return RunExceptionAndRecovery(activeProvider,
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
            try
            {
                var input = ResolveProfilerInput(profilerCase);
                if (profilerCase == TextInputLifecycleQaProfilerCase.RuntimeAddListener)
                {
                    runtimeListener!.ResetRecords();
                }
                else if (profilerCase == TextInputLifecycleQaProfilerCase.InspectorPersistent)
                {
                    persistentReceiver!.ResetRecords();
                }

                yield return Select(activeProvider!, input);
                for (var index = 0; index < 256; ++index)
                {
                    activeProvider!.EnqueueCommittedText((index & 1) == 0
                        ? "task159-profiler-a"
                        : "task159-profiler-b");
                    yield return null;
                }
                EventSystem.current!.SetSelectedGameObject(null);
                yield return WaitUntil(() => !activeProvider!.HasFocusSession,
                    $"profiler {profilerCase} release");
            }
            finally
            {
                EventSystem.current?.SetSelectedGameObject(null);
                profilerBurstRunning = false;
            }
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

        private static IEnumerator RunExceptionAndRecovery(TextInputLifecycleQaStrictProvider provider,
            TextInput input,
            TextInputLifecycleQaRuntimeListener runtimeListener,
            int diagnosticBaseline)
        {
            runtimeListener.enabled = false;
            var tailCount = 0;
            UnityAction<string> throwing = _ => throw new InvalidOperationException("task159 listener");
            UnityAction<string> tail = _ => ++tailCount;
            input.onValueChanged.AddListener(throwing);
            input.onValueChanged.AddListener(tail);
            try
            {
                yield return Select(provider, input);
                provider.EnqueueCommittedText("qa-listener-throw");
                yield return WaitUntil(() =>
                        input.Text == "qa-listener-throw" &&
                        HybridInputRuntime.Diagnostics.DiagnosticCount == diagnosticBaseline + 1,
                    "listener exception diagnostic");
                Require(tailCount == 0, "UnityEvent did not short-circuit after listener exception.");
            }
            finally
            {
                input.onValueChanged.RemoveListener(throwing);
                input.onValueChanged.RemoveListener(tail);
                EventSystem.current!.SetSelectedGameObject(null);
            }

            yield return WaitUntil(() => !provider.HasFocusSession, "listener exception release");
            runtimeListener.ResetRecords();
            runtimeListener.enabled = true;
            yield return RunObserved(provider,
                input,
                "qa-listener-recovery",
                () => runtimeListener.ValueChangedCount == 1,
                () => runtimeListener.EndEditCount == 1 && runtimeListener.FocusLostCount == 1);
            ValidateRuntimeListener(runtimeListener,
                "qa-listener-recovery",
                "listener recovery");
            Require(HybridInputRuntime.Diagnostics.DiagnosticCount == diagnosticBaseline + 1,
                "Recovery added an unexpected diagnostic.");
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
                noListenerPassed = noListenerPassed,
                runtimeListenerPassed = runtimeListenerPassed,
                persistentListenerPassed = persistentListenerPassed,
                exceptionRecoveryPassed = exceptionRecoveryPassed,
                persistentFocusGainedCount = persistentReceiver!.FocusGainedCount,
                persistentValueChangedCount = persistentReceiver.ValueChangedCount,
                persistentEndEditCount = persistentReceiver.EndEditCount,
                persistentFocusLostCount = persistentReceiver.FocusLostCount,
                persistentValuePayload = persistentReceiver.ValueChangedPayload,
                persistentEndPayload = persistentReceiver.EndEditPayload,
                persistentSequence = string.Join(",", persistentReceiver.Sequence),
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
                message = exception.GetType().Name + ": " + exception.Message
            };
            Complete();
        }

        private void Complete()
        {
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
        public bool noListenerPassed;
        public bool runtimeListenerPassed;
        public bool persistentListenerPassed;
        public bool exceptionRecoveryPassed;
        public int persistentFocusGainedCount;
        public int persistentValueChangedCount;
        public int persistentEndEditCount;
        public int persistentFocusLostCount;
        public string persistentValuePayload = string.Empty;
        public string persistentEndPayload = string.Empty;
        public string persistentSequence = string.Empty;
        public string message = string.Empty;
    }

    public enum TextInputLifecycleQaProfilerCase
    {
        NoListener = 0,
        RuntimeAddListener = 1,
        InspectorPersistent = 2
    }
}
