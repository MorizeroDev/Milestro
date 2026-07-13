using System;
using System.Runtime.CompilerServices;
using System.Text;
using Milestro.Binding;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Milestro.InputSystem.Service
{
    public enum MacScrollPhaseProbeStage
    {
        NativeLifecycle,
        NativePolling,
        InputAction,
        UguiAssociation
    }

    [AddComponentMenu("Milestro/Diagnostics/macOS Scroll Phase Probe")]
    public sealed class MacScrollPhaseProbe : MonoBehaviour, IScrollHandler
    {
        private const string LogPrefix = "[MILESTRO_SCROLL_PHASE_POC]";

        [SerializeField] private MacScrollPhaseProbeStage stage = MacScrollPhaseProbeStage.NativeLifecycle;
        [SerializeField][Min(0.25f)] private float captureDurationSeconds = 3f;

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        private const int MaxPollsPerFrame = 32;
        private const int MaxConsoleTraceLines = 96;
        private const int MaxBufferedTraceLines = MaxConsoleTraceLines - 2;
        private const int MaxTraceCharacters = 8192;

        private readonly StringBuilder trace = new StringBuilder(8192);
        private readonly ScrollPhasePollBudget pollBudget = new ScrollPhasePollBudget(MaxPollsPerFrame);
        private readonly ScrollPhaseAssociationTracker associationTracker = new ScrollPhaseAssociationTracker();
        private readonly ScrollPhaseLifecycleTracker lifecycleTracker = new ScrollPhaseLifecycleTracker();
        private InputSystemUIInputModule? inputModule;
        private InputAction? scrollAction;
        private int traceLines;
        private double captureStartedAt;
        private long lastNativeSequence;
        private long boundGestureId;
        private long monitorLeaseId;
        private bool monitorStarted;
        private bool captureFailed;
        private bool captureComplete;
        private bool traceFlushed;
#endif

        private void OnEnable()
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            trace.Clear();
            traceLines = 0;
            pollBudget.Reset();
            lastNativeSequence = 0;
            boundGestureId = 0;
            associationTracker.Reset();
            lifecycleTracker.Reset();
            captureFailed = false;
            captureComplete = false;
            traceFlushed = false;
            captureStartedAt = Time.realtimeSinceStartupAsDouble;
            var apiResult = BindingC.ScrollPhaseMonitorStart(out var monitorResult, out monitorLeaseId);
            monitorStarted = apiResult == 0 && monitorResult == 0 && monitorLeaseId != 0;
            AppendTrace($"monitor-start stage={stage} api={apiResult} result={monitorResult} lease={monitorLeaseId}");
            if (!monitorStarted)
            {
                FailCapture("monitor-start-failed");
                return;
            }
            if (stage >= MacScrollPhaseProbeStage.InputAction)
            {
                TryAttachScrollAction();
            }
#else
            Debug.Log($"{LogPrefix} unsupported-platform", this);
#endif
        }

        private void Update()
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            if (captureFailed || captureComplete)
            {
                return;
            }
            if (Time.realtimeSinceStartupAsDouble - captureStartedAt >= Math.Max(0.25f, captureDurationSeconds))
            {
                CompleteCapture("duration-elapsed");
                return;
            }
            if (stage >= MacScrollPhaseProbeStage.InputAction)
            {
                TryAttachScrollAction();
            }
            if (stage >= MacScrollPhaseProbeStage.NativePolling)
            {
                DrainNativeSamples("update");
            }
            if (!captureFailed && stage >= MacScrollPhaseProbeStage.UguiAssociation)
            {
                associationTracker.AdvanceFrame(Time.frameCount);
                SynchronizeAssociation();
            }
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            if (!captureFailed && !captureComplete && stage >= MacScrollPhaseProbeStage.UguiAssociation)
            {
                associationTracker.Complete();
                SynchronizeAssociation();
                if (!captureFailed && boundGestureId == 0)
                {
                    FailCapture("association-not-proven");
                }
                if (!captureFailed)
                {
                    ValidateLifecycleCompletion();
                }
            }
            DetachScrollAction();
            StopMonitor("component-disabled");
            FlushTrace();
            boundGestureId = 0;
            associationTracker.Reset();
            lifecycleTracker.Reset();
#endif
        }

        private void LateUpdate()
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            if (captureFailed || captureComplete || stage < MacScrollPhaseProbeStage.UguiAssociation)
            {
                return;
            }

            DrainNativeSamples("late-update");
            if (!captureFailed)
            {
                associationTracker.AdvanceFrame(Time.frameCount);
                SynchronizeAssociation();
            }
#endif
        }

        public void OnScroll(PointerEventData eventData)
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            if (captureFailed || captureComplete || stage < MacScrollPhaseProbeStage.UguiAssociation)
            {
                return;
            }
            var identity = RuntimeHelpers.GetHashCode(eventData);
            associationTracker.ObserveUgui(Time.frameCount, identity, eventData.scrollDelta);
            AppendTrace($"ugui frame={Time.frameCount} event={identity} delta={Format(eventData.scrollDelta)} " +
                        $"owner={GetInstanceID()} bound={boundGestureId}");
            SynchronizeAssociation();
#endif
        }

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        private void TryAttachScrollAction()
        {
            var currentModule = EventSystem.current?.currentInputModule as InputSystemUIInputModule;
            var currentAction = currentModule?.scrollWheel?.action;
            if (currentModule == inputModule && currentAction == scrollAction)
            {
                return;
            }

            DetachScrollAction();
            inputModule = currentModule;
            scrollAction = currentAction;
            if (scrollAction == null)
            {
                return;
            }

            scrollAction.performed += OnScrollAction;
            AppendTrace($"action-attached name={scrollAction.name}");
        }

        private void DetachScrollAction()
        {
            if (scrollAction != null)
            {
                scrollAction.performed -= OnScrollAction;
            }
            scrollAction = null;
            inputModule = null;
        }

        private void OnScrollAction(InputAction.CallbackContext context)
        {
            if (captureFailed || captureComplete || stage < MacScrollPhaseProbeStage.InputAction)
            {
                return;
            }
            var delta = context.ReadValue<Vector2>();
            if (stage >= MacScrollPhaseProbeStage.UguiAssociation)
            {
                associationTracker.ObserveAction(Time.frameCount, context.time, delta);
            }
            AppendTrace($"action frame={Time.frameCount} time={context.time:F6} " +
                        $"delta={Format(delta)} control={context.control.path} bound={boundGestureId}");
            SynchronizeAssociation();
        }

        private void DrainNativeSamples(string source)
        {
            if (!monitorStarted)
            {
                return;
            }

            while (pollBudget.TryConsume(Time.frameCount))
            {
                var apiResult = BindingC.ScrollPhaseMonitorPoll(out var monitorResult,
                    monitorLeaseId,
                    out var hasSample,
                    out var sequence,
                    out var gestureId,
                    out var timestamp,
                    out var windowNumber,
                    out var keyWindowNumber,
                    out var eventNumber,
                    out var deltaX,
                    out var deltaY,
                    out var scrollingDeltaX,
                    out var scrollingDeltaY,
                    out var gesturePhase,
                    out var momentumPhase,
                    out var precise,
                    out var directionInvertedFromDevice,
                    out var queueOverflowed);
                if (apiResult != 0 || monitorResult != 0)
                {
                    FailCapture($"poll-failed api={apiResult} result={monitorResult}");
                    return;
                }
                if (hasSample == 0)
                {
                    return;
                }

                if (lastNativeSequence != 0 && sequence != lastNativeSequence + 1)
                {
                    FailCapture($"native-sequence-gap previous={lastNativeSequence} current={sequence}");
                    return;
                }
                lastNativeSequence = sequence;
                if (queueOverflowed != 0)
                {
                    FailCapture("native-queue-overflow");
                    return;
                }

                if (stage >= MacScrollPhaseProbeStage.UguiAssociation)
                {
                    associationTracker.ObserveNative(Time.frameCount,
                        new NativeScrollPhaseEvidence(sequence,
                            gestureId,
                            timestamp,
                            windowNumber,
                            keyWindowNumber,
                            eventNumber,
                            new Vector2((float)scrollingDeltaX, (float)scrollingDeltaY),
                            gesturePhase,
                            momentumPhase,
                            false));
                }
                var lifecycle = lifecycleTracker.Observe(gestureId, gesturePhase, momentumPhase);
                AppendTrace($"native source={source} frame={Time.frameCount} seq={sequence} " +
                            $"gesture={gestureId} timestamp={timestamp:F6} window={windowNumber} " +
                            $"keyWindow={keyWindowNumber} " +
                            $"event={eventNumber} delta=({deltaX:F4},{deltaY:F4}) " +
                            $"scrolling=({scrollingDeltaX:F4},{scrollingDeltaY:F4}) " +
                            $"phase={gesturePhase} momentum={momentumPhase} precise={precise} " +
                            $"natural={directionInvertedFromDevice} overflow={queueOverflowed} " +
                            $"owner={GetInstanceID()} bound={boundGestureId} lifecycle={lifecycle}");
                SynchronizeAssociation();
                if (captureFailed)
                {
                    return;
                }
            }

            FailCapture($"poll-budget-exhausted frame={Time.frameCount} budget={MaxPollsPerFrame}");
        }

        private void AppendTrace(string message)
        {
            if (traceFlushed || captureFailed)
            {
                return;
            }
            if (traceLines >= MaxBufferedTraceLines || trace.Length + message.Length + LogPrefix.Length + 2 >
                MaxTraceCharacters)
            {
                FailCapture("managed-trace-capacity-exhausted");
                return;
            }

            trace.Append(LogPrefix).Append(' ').AppendLine(message);
            ++traceLines;
        }

        private void FailCapture(string reason)
        {
            if (captureFailed)
            {
                return;
            }

            captureFailed = true;
            AppendTerminalTrace($"FAIL {reason}");
            DetachScrollAction();
            StopMonitor("fail-closed");
            FlushTrace();
        }

        private void CompleteCapture(string reason)
        {
            if (captureFailed || captureComplete)
            {
                return;
            }

            if (stage >= MacScrollPhaseProbeStage.UguiAssociation)
            {
                associationTracker.Complete();
                SynchronizeAssociation();
                if (captureFailed)
                {
                    return;
                }
                if (boundGestureId == 0)
                {
                    FailCapture("association-not-proven");
                    return;
                }
                ValidateLifecycleCompletion();
                if (captureFailed)
                {
                    return;
                }
            }

            captureComplete = true;
            DetachScrollAction();
            StopMonitor(reason);
            FlushTrace();
        }

        private void ValidateLifecycleCompletion()
        {
            if (lifecycleTracker.HasReturned)
            {
                return;
            }
            FailCapture(lifecycleTracker.IsPendingEnd
                ? "no-non-timeout-no-momentum-signal"
                : "lifecycle-not-complete");
        }

        private void StopMonitor(string reason)
        {
            if (!monitorStarted)
            {
                return;
            }

            var apiResult = BindingC.ScrollPhaseMonitorStop(out var monitorResult, monitorLeaseId);
            AppendTerminalTrace($"monitor-stop reason={reason} api={apiResult} result={monitorResult} " +
                                $"lease={monitorLeaseId}");
            if (apiResult == 0 && monitorResult == 0)
            {
                monitorStarted = false;
                monitorLeaseId = 0;
            }
        }

        private void AppendTerminalTrace(string message)
        {
            var line = $"{LogPrefix} {message}{Environment.NewLine}";
            if (line.Length > MaxTraceCharacters)
            {
                line = $"{LogPrefix} terminal-trace-too-large{Environment.NewLine}";
            }
            if (traceLines >= MaxConsoleTraceLines || trace.Length + line.Length > MaxTraceCharacters)
            {
                trace.Clear();
                trace.Append(LogPrefix).Append(" trace-truncated ").AppendLine(message);
                traceLines = 1;
                if (trace.Length > MaxTraceCharacters)
                {
                    trace.Length = MaxTraceCharacters;
                }
                return;
            }
            trace.Append(line);
            ++traceLines;
        }

        private void FlushTrace()
        {
            if (traceFlushed || trace.Length == 0)
            {
                return;
            }

            traceFlushed = true;
            if (captureFailed)
            {
                Debug.LogError(trace.ToString(), this);
            }
            else
            {
                Debug.Log(trace.ToString(), this);
            }
        }

        private void SynchronizeAssociation()
        {
            if (stage < MacScrollPhaseProbeStage.UguiAssociation || captureFailed || captureComplete)
            {
                return;
            }
            if (associationTracker.IsInvalid)
            {
                FailCapture($"association-failed reason={associationTracker.FailureReason}");
                return;
            }
            if (!associationTracker.Association.HasValue)
            {
                return;
            }

            var association = associationTracker.Association.Value;
            if (boundGestureId != 0)
            {
                if (boundGestureId != association.GestureId)
                {
                    FailCapture("association-changed-after-bind");
                }
                return;
            }

            boundGestureId = association.GestureId;
            lifecycleTracker.Bind(boundGestureId);
            AppendTrace($"associated gesture={association.GestureId} window={association.WindowNumber} " +
                        $"ugui={association.PointerEventIdentity} " +
                        $"observedClockOffset={association.ObservedTimestampOffset:F6}");
        }

        private static string Format(Vector2 value)
        {
            return $"({value.x:F4},{value.y:F4})";
        }
#endif
    }
}
