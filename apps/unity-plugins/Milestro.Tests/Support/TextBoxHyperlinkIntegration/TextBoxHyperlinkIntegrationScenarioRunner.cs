using System;
using System.Collections;
using System.Collections.Generic;
using Milestro.Components;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Scripting;

namespace Milestro.Tests.TextBoxHyperlinkIntegration
{
    [Preserve]
    public sealed class TextBoxHyperlinkIntegrationScenarioRunner : MonoBehaviour
    {
        private const string ExpectedHref = "milestro://integration/task201";
        private const string ExpectedId = "task201-link";
        private const float PhysicalClickTimeoutSeconds = 120f;

        [SerializeField] private TextBox? textBox;
        [SerializeField] private TextBoxHyperlinkIntegrationReceiver? receiver;
        [SerializeField] private string sourceHead = string.Empty;
        [SerializeField] private string sourceTree = string.Empty;

        public bool Completed { get; private set; }
        public bool Passed { get; private set; }

        public void Configure(TextBox target,
            TextBoxHyperlinkIntegrationReceiver persistentReceiver,
            string exactSourceHead,
            string exactSourceTree)
        {
            textBox = target;
            receiver = persistentReceiver;
            sourceHead = exactSourceHead;
            sourceTree = exactSourceTree;
        }

        private IEnumerator Start()
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(RunScenario());
            while (stack.Count > 0)
            {
                object current;
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

        private IEnumerator RunScenario()
        {
            for (var frame = 0; frame < 10; ++frame)
            {
                yield return null;
            }

            Require(textBox != null && receiver != null, "TextBox or persistent receiver is missing.");
            Require(EventSystem.current != null, "Integration scene has no EventSystem.");
            Require(sourceHead.Length == 40 && sourceTree.Length == 40,
                "Integration source HEAD/tree must be exact object IDs.");

            receiver!.ResetRecords();
            Debug.Log("TASK201_HYPERLINK_INTEGRATION_READY click the visible link with the primary mouse button.");
            var clickDeadline = Time.realtimeSinceStartup + PhysicalClickTimeoutSeconds;
            while (receiver.Count == 0 && Time.realtimeSinceStartup < clickDeadline)
            {
                yield return null;
            }

            Require(receiver.Count == 1, "Physical mouse click was not delivered exactly once by EventSystem.");
            Require(receiver.LastHref == ExpectedHref, "Physical mouse click href payload mismatch.");
            Require(receiver.LastId == ExpectedId, "Physical mouse click id payload mismatch.");

            FindClickablePoint(pointerId: 42);
            yield return null;

            Require(receiver.Count == 1, "Touch pointer dispatch did not invoke the persistent listener once.");
            Require(receiver.LastHref == ExpectedHref, "Touch pointer href payload mismatch.");
            Require(receiver.LastId == ExpectedId, "Touch pointer id payload mismatch.");
        }

        private void CompleteSuccess()
        {
            Passed = true;
            Completed = true;
            Debug.Log("TASK201_HYPERLINK_INTEGRATION_RESULT " + JsonUtility.ToJson(new Result
            {
                status = "PASS",
                clickCount = 2,
                href = receiver!.LastHref,
                id = receiver.LastId,
                exactHead = sourceHead,
                exactTree = sourceTree,
                physicalMouse = true,
                touchPointerDispatch = true,
                persistentListener = true
            }));
        }

        private void CompleteFailure(Exception exception)
        {
            Passed = false;
            Completed = true;
            Debug.LogError("TASK201_HYPERLINK_INTEGRATION_RESULT " + JsonUtility.ToJson(new Result
            {
                status = "FAIL",
                exactHead = sourceHead,
                exactTree = sourceTree,
                error = exception.ToString()
            }));
        }

        private Vector2 FindClickablePoint(int pointerId)
        {
            var target = textBox!;
            var rect = target.rectTransform.rect;
            var canvas = target.canvas;
            var eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            for (var y = 8f; y <= 48f; y += 8f)
            {
                for (var x = 8f; x <= 240f; x += 8f)
                {
                    var localPoint = new Vector2(rect.xMin + x, rect.yMax - y);
                    var worldPoint = target.rectTransform.TransformPoint(localPoint);
                    var screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, worldPoint);
                    receiver!.ResetRecords();
                    DispatchPointer(pointerId, screenPoint);
                    if (receiver.Count == 1 &&
                        receiver.LastHref == ExpectedHref &&
                        receiver.LastId == ExpectedId)
                    {
                        return screenPoint;
                    }
                }
            }

            throw new InvalidOperationException("No visible hyperlink glyph accepted a pointer click.");
        }

        private void DispatchPointer(int pointerId, Vector2 screenPoint)
        {
            var target = textBox!;
            var eventData = new PointerEventData(EventSystem.current)
            {
                pointerId = pointerId,
                button = PointerEventData.InputButton.Left,
                eligibleForClick = true,
                position = screenPoint
            };

            ExecuteEvents.Execute(target.gameObject, eventData, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(target.gameObject, eventData, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(target.gameObject, eventData, ExecuteEvents.pointerClickHandler);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        [Serializable]
        private sealed class Result
        {
            public string status = string.Empty;
            public int clickCount;
            public string href = string.Empty;
            public string id = string.Empty;
            public string exactHead = string.Empty;
            public string exactTree = string.Empty;
            public bool physicalMouse;
            public bool touchPointerDispatch;
            public bool persistentListener;
            public string error = string.Empty;
        }
    }
}
