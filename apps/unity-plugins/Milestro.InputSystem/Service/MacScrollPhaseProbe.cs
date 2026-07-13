using System.Runtime.CompilerServices;
using Milestro.Binding;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Milestro.InputSystem.Service
{
    [AddComponentMenu("Milestro/Diagnostics/macOS Scroll Phase Probe")]
    public sealed class MacScrollPhaseProbe : MonoBehaviour, IScrollHandler
    {
        private const string LogPrefix = "[MILESTRO_SCROLL_PHASE_POC]";

        [SerializeField] private MacScrollPhaseProbeStage stage = MacScrollPhaseProbeSession.DefaultStage;
        [SerializeField][Min(0.25f)] private float captureDurationSeconds = 3f;

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        private InputSystemUIInputModule? inputModule;
        private InputAction? scrollAction;
        private MacScrollPhaseProbeSession? session;
#endif

        private void OnEnable()
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            if (session?.HasLease == true)
            {
                session.Disable();
                if (session.HasLease)
                {
                    return;
                }
            }
            session = new MacScrollPhaseProbeSession(new BindingTransport(),
                new UnityLogSink(this),
                stage,
                captureDurationSeconds,
                GetInstanceID());
            session.Start(Time.realtimeSinceStartupAsDouble);
            if (!session.IsFinished && session.RequiresInputAction)
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
            if (session == null)
            {
                return;
            }
            if (!session.IsFinished && session.RequiresInputAction)
            {
                TryAttachScrollAction();
            }
            session.Update(Time.frameCount, Time.realtimeSinceStartupAsDouble);
            DetachIfFinished();
#endif
        }

        private void LateUpdate()
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            if (session == null)
            {
                return;
            }
            session.LateUpdate(Time.frameCount);
            DetachIfFinished();
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            DetachScrollAction();
            session?.Disable();
            if (session?.HasLease == false)
            {
                session = null;
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

            public long Stop(out int result, long leaseId)
            {
                return BindingC.ScrollPhaseMonitorStop(out result, leaseId);
            }
        }

        private sealed class UnityLogSink : IMacScrollPhaseProbeSink
        {
            private readonly Object context;

            internal UnityLogSink(Object context)
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
