using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Milestro.Binding;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Milestro.InputSystem.Service
{
    [AddComponentMenu("Milestro/Diagnostics/macOS Scroll Phase Probe")]
    public sealed class MacScrollPhaseProbe : MonoBehaviour, IScrollHandler
    {
        private const string LogPrefix = "[MILESTRO_SCROLL_PHASE_POC]";
        private const double ActionTraceReadinessTimeoutSeconds = 1d;

        [SerializeField] private MacScrollPhaseProbeStage stage = MacScrollPhaseProbeSession.DefaultStage;
        [SerializeField][Min(0.25f)] private float captureDurationSeconds = 3f;

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        private InputSystemUIInputModule? inputModule;
        private InputAction? scrollAction;
        private ActionTraceSubscription? actionTraceSubscription;
        private MacScrollPhaseProbeSession? session;
        private bool actionTracePreparationPending;
        private bool actionTracePreflightPending;
        private double actionTraceReadinessDeadline;
#endif

        private void OnEnable()
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            if (session?.RequiresCleanup == true)
            {
                session.Disable();
                if (session.RequiresCleanup)
                {
                    return;
                }
            }
            actionTraceSubscription = stage == MacScrollPhaseProbeStage.NativeMinimalInputActionTrace
                ? new ActionTraceSubscription(this)
                : null;
            session = new MacScrollPhaseProbeSession(new BindingTransport(),
                new UnityLogSink(this),
                stage,
                captureDurationSeconds,
                GetInstanceID(),
                actionTraceSubscription);
            if (session.TracesInputActionCandidates)
            {
                actionTracePreparationPending = true;
                actionTracePreflightPending = false;
                return;
            }

            session.Start(Time.realtimeSinceStartupAsDouble);
            if (!session.IsFinished && session.RequiresInputAction)
            {
                TryAttachScrollAction();
            }
#else
            Debug.Log($"{LogPrefix} unsupported-platform", this);
#endif
        }

        private void Start()
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            PrepareActionTracePreflight(Time.realtimeSinceStartupAsDouble);
#endif
        }

        private void Update()
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            if (session == null)
            {
                return;
            }
            PrepareActionTracePreflight(Time.realtimeSinceStartupAsDouble);
            AdvanceActionTracePreflight(Time.realtimeSinceStartupAsDouble);
            if (actionTracePreflightPending)
            {
                return;
            }
            if (!session.IsFinished && session.TracesInputActionCandidates)
            {
                if (actionTraceSubscription?.ValidateOwner() != true)
                {
                    session.ReportActionTraceFault(MacScrollActionTraceFault.OwnerChanged);
                }
            }
            else if (!session.IsFinished && session.RequiresInputAction)
            {
                TryAttachScrollAction();
            }
            session.Update(Time.frameCount, Time.realtimeSinceStartupAsDouble);
            if (!session.TracesInputActionCandidates)
            {
                DetachIfFinished();
            }
#endif
        }

        private void LateUpdate()
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            if (session == null)
            {
                return;
            }
            PrepareActionTracePreflight(Time.realtimeSinceStartupAsDouble);
            AdvanceActionTracePreflight(Time.realtimeSinceStartupAsDouble);
            if (actionTracePreflightPending)
            {
                return;
            }
            if (!session.IsFinished && session.TracesInputActionCandidates &&
                actionTraceSubscription?.ValidateOwner() != true)
            {
                session.ReportActionTraceFault(MacScrollActionTraceFault.OwnerChanged);
            }
            session.LateUpdate(Time.frameCount);
            if (!session.TracesInputActionCandidates)
            {
                DetachIfFinished();
            }
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            actionTracePreparationPending = false;
            actionTracePreflightPending = false;
            if (session?.TracesInputActionCandidates != true)
            {
                DetachScrollAction();
            }
            session?.Disable();
            if (session?.RequiresCleanup == false)
            {
                session = null;
                actionTraceSubscription = null;
            }
#endif
        }

        public void OnScroll(PointerEventData eventData)
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            session?.ObserveUgui(Time.frameCount,
                RuntimeHelpers.GetHashCode(eventData),
                eventData.scrollDelta);
            DetachIfFinished();
#endif
        }

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        private void PrepareActionTracePreflight(double now)
        {
            if (!actionTracePreparationPending || session == null || actionTraceSubscription == null)
            {
                return;
            }

            actionTracePreparationPending = false;
            actionTracePreflightPending = actionTraceSubscription.TryPrepare();
            if (actionTracePreflightPending)
            {
                actionTraceReadinessDeadline = now + ActionTraceReadinessTimeoutSeconds;
                return;
            }
            session.ReportActionTracePreflightFault("action-preflight-owner-invalid");
        }

        private void AdvanceActionTracePreflight(double now)
        {
            if (!actionTracePreflightPending || session == null || actionTraceSubscription == null)
            {
                return;
            }

            var readiness = actionTraceSubscription.GetReadiness();
            if (readiness == ActionTraceReadiness.SceneChanged)
            {
                actionTracePreflightPending = false;
                session.ReportActionTracePreflightFault("action-scene-changed-before-ready");
                return;
            }
            if (readiness == ActionTraceReadiness.Invalid)
            {
                actionTracePreflightPending = false;
                session.ReportActionTracePreflightFault("action-owner-changed-before-ready");
                return;
            }
            if (now >= actionTraceReadinessDeadline)
            {
                actionTracePreflightPending = false;
                session.ReportActionTracePreflightFault("action-readiness-timeout");
                return;
            }
            if (readiness == ActionTraceReadiness.Waiting)
            {
                return;
            }

            actionTracePreflightPending = false;
            if (!actionTraceSubscription.TryAttachPrepared())
            {
                session.ReportActionTracePreflightFault("action-attach-failed");
                return;
            }
            session.StartActionTrace(now);
        }

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
            session?.RecordActionAttachment(scrollAction.name);
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

        private void DetachIfFinished()
        {
            if (session?.IsFinished == true)
            {
                DetachScrollAction();
            }
        }

        private void OnScrollAction(InputAction.CallbackContext context)
        {
            session?.ObserveAction(Time.frameCount,
                context.time,
                context.ReadValue<Vector2>(),
                context.control.path);
            DetachIfFinished();
        }

        private void OnActionTracePerformed(InputAction.CallbackContext context)
        {
            var currentSession = session;
            if (currentSession == null ||
                !currentSession.TryBeginActionRecord(Environment.CurrentManagedThreadId))
            {
                return;
            }

            try
            {
                var control = context.control;
                var device = control?.device;
                if (control == null || device == null)
                {
                    currentSession.CancelActionRecord();
                    return;
                }

                var stateBlock = control.stateBlock;
                currentSession.CommitActionRecord(Time.frameCount,
                    Stopwatch.GetTimestamp(),
                    context.time,
                    (int)context.phase,
                    context.ReadValue<Vector2>(),
                    device.deviceId,
                    stateBlock.byteOffset,
                    stateBlock.bitOffset,
                    stateBlock.sizeInBits);
            }
            catch (Exception)
            {
                currentSession.CancelActionRecord();
            }
        }

        private sealed class ActionTraceSubscription : IMacScrollActionTraceOwner
        {
            private readonly MacScrollPhaseProbe owner;

            private EventSystem? eventSystem;
            private InputSystemUIInputModule? inputModule;
            private InputAction? action;
            private bool possiblySubscribed;
            private bool sceneEventsSubscribed;
            private int sceneValidationRequired;

            internal ActionTraceSubscription(MacScrollPhaseProbe owner)
            {
                this.owner = owner;
            }

            internal bool TryPrepare()
            {
                try
                {
                    SubscribeSceneEvents();
                    return TryResolveUniqueCandidate(out eventSystem, out inputModule, out action);
                }
                catch (Exception)
                {
                    return false;
                }
            }

            internal ActionTraceReadiness GetReadiness()
            {
                try
                {
                    if (Interlocked.Exchange(ref sceneValidationRequired, 0) != 0)
                    {
                        return ActionTraceReadiness.SceneChanged;
                    }
                    if (eventSystem == null || !eventSystem.isActiveAndEnabled ||
                        inputModule == null || !inputModule.isActiveAndEnabled ||
                        action == null ||
                        !ReferenceEquals(inputModule.scrollWheel?.action, action))
                    {
                        return ActionTraceReadiness.Invalid;
                    }

                    var currentEventSystem = EventSystem.current;
                    if (currentEventSystem == null)
                    {
                        return ActionTraceReadiness.Waiting;
                    }
                    if (!ReferenceEquals(currentEventSystem, eventSystem))
                    {
                        return ActionTraceReadiness.Invalid;
                    }

                    var currentInputModule = eventSystem.currentInputModule;
                    if (currentInputModule != null && !ReferenceEquals(currentInputModule, inputModule))
                    {
                        return ActionTraceReadiness.Invalid;
                    }
                    return currentInputModule == null || !action.enabled
                        ? ActionTraceReadiness.Waiting
                        : ActionTraceReadiness.Ready;
                }
                catch (Exception)
                {
                    return ActionTraceReadiness.Invalid;
                }
            }

            internal bool TryAttachPrepared()
            {
                try
                {
                    if (GetReadiness() != ActionTraceReadiness.Ready)
                    {
                        return false;
                    }

                    possiblySubscribed = true;
                    action!.performed += owner.OnActionTracePerformed;
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            internal bool ValidateOwner()
            {
                try
                {
                    if (Interlocked.Exchange(ref sceneValidationRequired, 0) != 0)
                    {
                        return TryResolveUniqueReadyOwner(out var currentEventSystem,
                                   out var currentInputModule,
                                   out var currentAction) &&
                               ReferenceEquals(currentEventSystem, eventSystem) &&
                               ReferenceEquals(currentInputModule, inputModule) &&
                               ReferenceEquals(currentAction, action);
                    }

                    return eventSystem != null &&
                           eventSystem.isActiveAndEnabled &&
                           ReferenceEquals(EventSystem.current, eventSystem) &&
                           inputModule != null &&
                           inputModule.isActiveAndEnabled &&
                           ReferenceEquals(eventSystem.currentInputModule, inputModule) &&
                           ReferenceEquals(inputModule.scrollWheel?.action, action) &&
                           action?.enabled == true;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            public bool TryDetachActionTrace()
            {
                try
                {
                    if (possiblySubscribed && action != null)
                    {
                        action.performed -= owner.OnActionTracePerformed;
                    }
                    UnsubscribeSceneEvents();
                }
                catch (Exception)
                {
                    return false;
                }

                possiblySubscribed = false;
                action = null;
                inputModule = null;
                eventSystem = null;
                return true;
            }

            private static bool TryResolveUniqueCandidate(out EventSystem? resolvedEventSystem,
                out InputSystemUIInputModule? resolvedInputModule,
                out InputAction? resolvedAction)
            {
                resolvedEventSystem = null;
                resolvedInputModule = null;
                resolvedAction = null;
                var eventSystems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
                foreach (var candidate in eventSystems)
                {
                    if (!candidate.isActiveAndEnabled)
                    {
                        continue;
                    }
                    if (resolvedEventSystem != null)
                    {
                        return false;
                    }
                    resolvedEventSystem = candidate;
                }

                if (resolvedEventSystem == null)
                {
                    return false;
                }

                InputSystemUIInputModule? candidateModule = null;
                foreach (var module in resolvedEventSystem.GetComponents<InputSystemUIInputModule>())
                {
                    if (!module.isActiveAndEnabled)
                    {
                        continue;
                    }
                    if (candidateModule != null)
                    {
                        return false;
                    }
                    candidateModule = module;
                }

                var candidateAction = candidateModule?.scrollWheel?.action;
                if (candidateModule == null || candidateAction == null)
                {
                    return false;
                }

                resolvedInputModule = candidateModule;
                resolvedAction = candidateAction;
                return true;
            }

            private static bool TryResolveUniqueReadyOwner(out EventSystem? resolvedEventSystem,
                out InputSystemUIInputModule? resolvedInputModule,
                out InputAction? resolvedAction)
            {
                if (!TryResolveUniqueCandidate(out resolvedEventSystem, out resolvedInputModule,
                        out resolvedAction) || resolvedEventSystem == null || resolvedInputModule == null ||
                    resolvedAction == null)
                {
                    return false;
                }
                return ReferenceEquals(EventSystem.current, resolvedEventSystem) &&
                       ReferenceEquals(resolvedEventSystem.currentInputModule, resolvedInputModule) &&
                       resolvedAction.enabled;
            }

            private void SubscribeSceneEvents()
            {
                if (sceneEventsSubscribed)
                {
                    return;
                }
                sceneEventsSubscribed = true;
                SceneManager.sceneLoaded += OnSceneLoaded;
                SceneManager.sceneUnloaded += OnSceneUnloaded;
                SceneManager.activeSceneChanged += OnActiveSceneChanged;
            }

            private void UnsubscribeSceneEvents()
            {
                if (!sceneEventsSubscribed)
                {
                    return;
                }
                SceneManager.sceneLoaded -= OnSceneLoaded;
                SceneManager.sceneUnloaded -= OnSceneUnloaded;
                SceneManager.activeSceneChanged -= OnActiveSceneChanged;
                sceneEventsSubscribed = false;
            }

            private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
            {
                Interlocked.Exchange(ref sceneValidationRequired, 1);
            }

            private void OnSceneUnloaded(Scene scene)
            {
                Interlocked.Exchange(ref sceneValidationRequired, 1);
            }

            private void OnActiveSceneChanged(Scene previous, Scene next)
            {
                Interlocked.Exchange(ref sceneValidationRequired, 1);
            }
        }

        private enum ActionTraceReadiness
        {
            Waiting,
            Ready,
            Invalid,
            SceneChanged
        }

        private sealed class BindingTransport : IMacScrollPhaseMonitorTransport
        {
            public long Start(MacScrollPhaseMonitorMode mode, out int result, out long leaseId)
            {
                return BindingC.ScrollPhaseMonitorStart(out result, (int)mode, out leaseId);
            }

            public long Poll(out int result, long leaseId, out MacScrollPhaseNativeSample sample)
            {
                var apiResult = BindingC.ScrollPhaseMonitorPoll(out result,
                    leaseId,
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
                sample = new MacScrollPhaseNativeSample(hasSample != 0,
                    sequence,
                    gestureId,
                    timestamp,
                    windowNumber,
                    keyWindowNumber,
                    eventNumber,
                    deltaX,
                    deltaY,
                    scrollingDeltaX,
                    scrollingDeltaY,
                    gesturePhase,
                    momentumPhase,
                    precise,
                    directionInvertedFromDevice,
                    queueOverflowed != 0);
                return apiResult;
            }

            public long PollMinimal(out int result, long leaseId, out MacScrollPhaseMinimalPoll poll)
            {
                var apiResult = BindingC.ScrollPhaseMonitorPollMinimal(out result,
                    leaseId,
                    out var captureInvalidReason,
                    out var hasSample,
                    out var hasMore,
                    out var remaining,
                    out var validFields,
                    out var sequence,
                    out var gestureId,
                    out var timestamp,
                    out var windowNumber,
                    out var scrollingDeltaX,
                    out var scrollingDeltaY,
                    out var gesturePhase,
                    out var momentumPhase);
                poll = new MacScrollPhaseMinimalPoll(captureInvalidReason,
                    hasSample != 0,
                    hasMore != 0,
                    remaining,
                    new MacScrollPhaseMinimalSample(validFields,
                        sequence,
                        gestureId,
                        timestamp,
                        windowNumber,
                        scrollingDeltaX,
                        scrollingDeltaY,
                        gesturePhase,
                        momentumPhase));
                return apiResult;
            }

            public long GetMinimalInvalidDetail(out int result,
                long leaseId,
                out MacScrollPhaseMinimalInvalidDetail detail)
            {
                var apiResult = BindingC.ScrollPhaseMonitorGetMinimalInvalidDetail(out result,
                    leaseId,
                    out var hasDetail,
                    out var failure,
                    out var priorTrackerState,
                    out var priorGestureId,
                    out var sequence,
                    out var gesturePhaseBits,
                    out var momentumPhaseBits,
                    out var windowNumber);
                detail = new MacScrollPhaseMinimalInvalidDetail(hasDetail,
                    failure,
                    priorTrackerState,
                    priorGestureId,
                    sequence,
                    gesturePhaseBits,
                    momentumPhaseBits,
                    windowNumber);
                return apiResult;
            }

            public long Stop(out int result, long leaseId)
            {
                return BindingC.ScrollPhaseMonitorStop(out result, leaseId);
            }
        }

        private sealed class UnityLogSink : IMacScrollPhaseProbeSink
        {
            private readonly UnityEngine.Object context;

            internal UnityLogSink(UnityEngine.Object context)
            {
                this.context = context;
            }

            public void Flush(bool failed, string trace)
            {
                if (failed)
                {
                    Debug.LogError(trace, context);
                }
                else
                {
                    Debug.Log(trace, context);
                }
            }
        }
#endif
    }
}
