using System;
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

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        private InputSystemUIInputModule? inputModule;
        private InputAction? scrollAction;
        private int lastActionFrame = -1;
        private double lastActionTime;
        private Vector2 lastActionDelta;
        private long lastActionGestureId;
        private bool lastActionGestureAmbiguous;
        private long boundGestureId;
        private bool monitorStarted;
#endif

        private void OnEnable()
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            var apiResult = BindingC.ScrollPhaseMonitorStart(out var monitorResult);
            monitorStarted = apiResult == 0 && monitorResult == 0;
            Debug.Log($"{LogPrefix} monitor-start api={apiResult} result={monitorResult}", this);
            TryAttachScrollAction();
#else
            Debug.Log($"{LogPrefix} unsupported-platform", this);
#endif
        }

        private void Update()
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            TryAttachScrollAction();
            DrainNativeSamples("update", out _, out _);
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            DetachScrollAction();
            if (monitorStarted)
            {
                var apiResult = BindingC.ScrollPhaseMonitorStop(out var monitorResult);
                Debug.Log($"{LogPrefix} monitor-stop api={apiResult} result={monitorResult}", this);
            }
            monitorStarted = false;
            boundGestureId = 0;
#endif
        }

        public void OnScroll(PointerEventData eventData)
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            DrainNativeSamples("ugui", out var currentGestureId, out var currentGestureAmbiguous);
            var candidateGestureId = MergeGestureCandidates(lastActionGestureId,
                lastActionGestureAmbiguous,
                currentGestureId,
                currentGestureAmbiguous,
                out var candidateAmbiguous);
            var sameFrame = lastActionFrame == Time.frameCount;
            var sameDirection = SameDirection(lastActionDelta, eventData.scrollDelta);
            var associated = sameFrame && sameDirection && !candidateAmbiguous && candidateGestureId != 0;
            if (associated)
            {
                boundGestureId = candidateGestureId;
            }

            Debug.Log($"{LogPrefix} ugui frame={Time.frameCount} event={RuntimeHelpers.GetHashCode(eventData)} " +
                      $"delta={Format(eventData.scrollDelta)} actionFrame={lastActionFrame} " +
                      $"actionTime={lastActionTime:F6} actionDelta={Format(lastActionDelta)} " +
                      $"candidate={candidateGestureId} ambiguous={candidateAmbiguous} associated={associated} " +
                      $"owner={GetInstanceID()} bound={boundGestureId}",
                this);
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
            Debug.Log($"{LogPrefix} action-attached name={scrollAction.name}", this);
        }

        private void DetachScrollAction()
        {
            if (scrollAction != null)
            {
                scrollAction.performed -= OnScrollAction;
            }
            scrollAction = null;
            inputModule = null;
            lastActionFrame = -1;
            lastActionGestureId = 0;
            lastActionGestureAmbiguous = false;
        }

        private void OnScrollAction(InputAction.CallbackContext context)
        {
            DrainNativeSamples("action", out lastActionGestureId, out lastActionGestureAmbiguous);
            lastActionFrame = Time.frameCount;
            lastActionTime = context.time;
            lastActionDelta = context.ReadValue<Vector2>();
            Debug.Log($"{LogPrefix} action frame={lastActionFrame} time={lastActionTime:F6} " +
                      $"delta={Format(lastActionDelta)} candidate={lastActionGestureId} " +
                      $"ambiguous={lastActionGestureAmbiguous} control={context.control.path}",
                this);
        }

        private void DrainNativeSamples(string source, out long candidateGestureId, out bool candidateAmbiguous)
        {
            candidateGestureId = 0;
            candidateAmbiguous = false;
            if (!monitorStarted)
            {
                return;
            }

            while (true)
            {
                var apiResult = BindingC.ScrollPhaseMonitorPoll(out var monitorResult,
                    out var hasSample,
                    out var sequence,
                    out var gestureId,
                    out var timestamp,
                    out var windowNumber,
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
                    Debug.LogError($"{LogPrefix} poll-failed api={apiResult} result={monitorResult}", this);
                    monitorStarted = false;
                    return;
                }
                if (hasSample == 0)
                {
                    return;
                }

                var hasDelta = Math.Abs(scrollingDeltaX) > double.Epsilon ||
                               Math.Abs(scrollingDeltaY) > double.Epsilon;
                if (hasDelta && gestureId != 0)
                {
                    if (candidateGestureId == 0)
                    {
                        candidateGestureId = gestureId;
                    }
                    else if (candidateGestureId != gestureId)
                    {
                        candidateAmbiguous = true;
                    }
                }

                var lifecycle = ResolveLifecycle(boundGestureId,
                    gestureId,
                    gesturePhase,
                    momentumPhase);
                Debug.Log($"{LogPrefix} native source={source} frame={Time.frameCount} seq={sequence} " +
                          $"gesture={gestureId} timestamp={timestamp:F6} window={windowNumber} " +
                          $"event={eventNumber} delta=({deltaX:F4},{deltaY:F4}) " +
                          $"scrolling=({scrollingDeltaX:F4},{scrollingDeltaY:F4}) " +
                          $"phase={gesturePhase} momentum={momentumPhase} precise={precise} " +
                          $"natural={directionInvertedFromDevice} overflow={queueOverflowed} " +
                          $"owner={GetInstanceID()} bound={boundGestureId} lifecycle={lifecycle}",
                    this);
            }
        }

        private static long MergeGestureCandidates(long first,
            bool firstAmbiguous,
            long second,
            bool secondAmbiguous,
            out bool ambiguous)
        {
            ambiguous = firstAmbiguous || secondAmbiguous || (first != 0 && second != 0 && first != second);
            return first != 0 ? first : second;
        }

        private static bool SameDirection(Vector2 actionDelta, Vector2 eventDelta)
        {
            var actionHasX = !Mathf.Approximately(actionDelta.x, 0f);
            var actionHasY = !Mathf.Approximately(actionDelta.y, 0f);
            var eventHasX = !Mathf.Approximately(eventDelta.x, 0f);
            var eventHasY = !Mathf.Approximately(eventDelta.y, 0f);
            if (actionHasX != eventHasX || actionHasY != eventHasY)
            {
                return false;
            }

            return (!actionHasX || Mathf.Sign(actionDelta.x) == Mathf.Sign(eventDelta.x)) &&
                   (!actionHasY || Mathf.Sign(actionDelta.y) == Mathf.Sign(eventDelta.y));
        }

        private static string ResolveLifecycle(long boundGestureId,
            long sampleGestureId,
            int gesturePhase,
            int momentumPhase)
        {
            if (boundGestureId == 0 || sampleGestureId != boundGestureId)
            {
                return "unbound";
            }
            if (momentumPhase == 5 || momentumPhase == 6)
            {
                return "return-after-momentum";
            }
            if (gesturePhase == 6)
            {
                return "return-after-cancel";
            }
            if (gesturePhase == 5 && momentumPhase == 1)
            {
                return "return-without-momentum";
            }
            if (gesturePhase == 5 && momentumPhase == 2)
            {
                return "wait-for-momentum";
            }
            return "continue";
        }

        private static string Format(Vector2 value)
        {
            return $"({value.x:F4},{value.y:F4})";
        }
#endif
    }
}
