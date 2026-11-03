using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Milestro.Components;
using Milestro.Components.Internal;
using Milestro.Configuration;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Milestro.Tests
{
    public class MilestroDropdownInputSystemPlayModeTests
    {
        private readonly List<EventSystem> disabledEventSystems = new();
        private GameObject? root;
        private Canvas? canvas;
        private GameObject? eventSystemObject;
        private EventSystem? eventSystem;
        private InputSystemUIInputModule? inputModule;
        private Keyboard? keyboard;
        private Mouse? mouse;
        private Touchscreen? touchscreen;
        private RenderSurfaceConfiguration? previousConfiguration;

        [SetUp]
        public void SetUp()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("Real InputSystem UI integration requires a graphics device; run without -nographics.");
            }

            previousConfiguration = MilestroConfiguration.Configuration.RenderSurface;
            MilestroConfiguration.Configuration.RenderSurface = new RenderSurfaceConfiguration
            {
                AtlasPageSize = 128
            };

            foreach (var existing in Object.FindObjectsOfType<EventSystem>())
            {
                if (!existing.enabled)
                {
                    continue;
                }

                existing.enabled = false;
                disabledEventSystems.Add(existing);
            }

            root = new GameObject("Dropdown InputSystem Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            keyboard = InputSystem.AddDevice<Keyboard>();
            eventSystemObject = new GameObject("Dropdown Real InputSystem", typeof(EventSystem));
            eventSystem = eventSystemObject.GetComponent<EventSystem>();
            inputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();
            inputModule.submit.action.AddBinding("<Keyboard>/space");
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }

            if (eventSystemObject != null)
            {
                Object.DestroyImmediate(eventSystemObject);
            }

            foreach (var existing in disabledEventSystems)
            {
                if (existing != null)
                {
                    existing.enabled = true;
                }
            }

            disabledEventSystems.Clear();
            RemoveDevice(touchscreen);
            RemoveDevice(mouse);
            RemoveDevice(keyboard);
            if (previousConfiguration != null)
            {
                MilestroConfiguration.Configuration.RenderSurface = previousConfiguration;
            }
        }

        [UnityTest]
        public IEnumerator RealModuleSubmitMoveSubmitAndCancelExecuteOnce()
        {
            var dropdown = CreateDropdown("zero", "one", "two");
            dropdown.SetOptions(new[] { "zero", "one", "two" }, -1);
            var notifications = new List<int>();
            dropdown.onValueChanged.AddListener(notifications.Add);
            dropdown.Select();
            yield return null;

            yield return TapKey(Key.Enter);
            Assert.That(dropdown.isExpanded, Is.True);
            Assert.That(dropdown.value, Is.EqualTo(-1), "Opening must not also confirm the candidate.");

            yield return TapKey(Key.DownArrow);
            Assert.That(dropdown.value, Is.EqualTo(-1));
            Assert.That(notifications, Is.Empty);
            yield return TapKey(Key.Space);
            Assert.That(dropdown.isExpanded, Is.False);
            Assert.That(dropdown.value, Is.EqualTo(1));
            Assert.That(notifications, Is.EqualTo(new[] { 1 }));
            Assert.That(eventSystem!.currentSelectedGameObject, Is.EqualTo(dropdown.gameObject));

            yield return TapKey(Key.Enter);
            yield return TapKey(Key.DownArrow);
            yield return TapKey(Key.Escape);
            Assert.That(dropdown.isExpanded, Is.False);
            Assert.That(dropdown.value, Is.EqualTo(1));
            Assert.That(notifications, Is.EqualTo(new[] { 1 }));
            Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(dropdown.gameObject));
        }

        [UnityTest]
        public IEnumerator RealModuleDoesNotSwallowClosedButtonOrSubmitUnderlyingButton()
        {
            var dropdown = CreateDropdown("zero", "one");
            var button = CreateButton("Other Button", new Vector2(0f, -140f));
            var clicks = 0;
            button.onClick.AddListener(() => ++clicks);
            yield return null;

            button.Select();
            yield return TapKey(Key.Enter);
            Assert.That(clicks, Is.EqualTo(1));
            Assert.That(dropdown.isExpanded, Is.False);

            dropdown.Select();
            yield return TapKey(Key.Enter);
            yield return TapKey(Key.DownArrow);
            yield return TapKey(Key.Enter);
            Assert.That(dropdown.value, Is.EqualTo(1));
            Assert.That(clicks, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator RealMouseOutsideClickIsConsumedAndSecondClickActivatesButton()
        {
            var dropdown = CreateDropdown("zero", "one", "two");
            ((RectTransform)dropdown.transform).anchoredPosition = new Vector2(0f, 140f);
            var button = CreateButton("Outside Button", new Vector2(0f, -150f));
            var clicks = 0;
            button.onClick.AddListener(() => ++clicks);
            mouse = InputSystem.AddDevice<Mouse>();
            yield return null;

            yield return Click((RectTransform)dropdown.transform);
            Assert.That(dropdown.isExpanded, Is.True);
            yield return Click(button);
            Assert.That(dropdown.isExpanded, Is.False);
            Assert.That(clicks, Is.Zero, "The outside click must be consumed by the blocker.");
            Assert.That(eventSystem!.currentSelectedGameObject, Is.EqualTo(dropdown.gameObject));

            yield return Click(button);
            Assert.That(clicks, Is.EqualTo(1));
            Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(button.gameObject));
        }

        [UnityTest]
        public IEnumerator RealMouseOptionClickCommitsOnlyThatOption()
        {
            var dropdown = CreateDropdown("zero", "one", "two");
            var notifications = new List<int>();
            dropdown.onValueChanged.AddListener(notifications.Add);
            mouse = InputSystem.AddDevice<Mouse>();
            yield return null;

            yield return Click((RectTransform)dropdown.transform);
            var item = VisibleItems().First(candidate => candidate.OptionIndex == 1);
            yield return Click(item.RectTransform);

            Assert.That(dropdown.value, Is.EqualTo(1));
            Assert.That(notifications, Is.EqualTo(new[] { 1 }));
            Assert.That(dropdown.isExpanded, Is.False);
        }

        [UnityTest]
        public IEnumerator SyntheticTouchDragScrollsWithoutSelectingDragOrigin()
        {
            var dropdown = CreateDropdown();
            dropdown.maxVisibleItems = 4;
            dropdown.SetOptions(Enumerable.Range(0, 30).Select(index => "Option " + index));
            touchscreen = InputSystem.AddDevice<Touchscreen>();
            yield return null;
            dropdown.Show();
            yield return null;

            var scrollRect = VisibleList();
            var startItem = VisibleItems().First(candidate => candidate.OptionIndex == 1);
            var start = ScreenPoint(startItem.RectTransform);
            var moved = start + new Vector2(0f, 100f);
            InputSystem.QueueStateEvent(touchscreen, new TouchState
            {
                touchId = 1,
                phase = UnityEngine.InputSystem.TouchPhase.Began,
                position = start
            });
            yield return null;
            for (var step = 1; step <= 5; ++step)
            {
                InputSystem.QueueStateEvent(touchscreen, new TouchState
                {
                    touchId = 1,
                    phase = UnityEngine.InputSystem.TouchPhase.Moved,
                    position = Vector2.Lerp(start, moved, step / 5f)
                });
                yield return null;
            }

            InputSystem.QueueStateEvent(touchscreen, new TouchState
            {
                touchId = 1,
                phase = UnityEngine.InputSystem.TouchPhase.Ended,
                position = moved
            });
            yield return null;
            yield return null;

            Assert.That(dropdown.isExpanded, Is.True);
            Assert.That(dropdown.value, Is.Zero);
            Assert.That(scrollRect.verticalNormalizedPosition, Is.LessThan(1f));

            dropdown.Hide();
            dropdown.Show();
            yield return null;
            var tapItem = VisibleItems().First(candidate => candidate.OptionIndex == 1);
            yield return TapTouch(tapItem.RectTransform);
            Assert.That(dropdown.value, Is.EqualTo(1));
            Assert.That(dropdown.isExpanded, Is.False);
        }

        [UnityTest]
        public IEnumerator RealMouseWheelScrollsWithoutCommittingCandidate()
        {
            var dropdown = CreateDropdown();
            dropdown.maxVisibleItems = 4;
            dropdown.SetOptions(Enumerable.Range(0, 30).Select(index => "Option " + index));
            mouse = InputSystem.AddDevice<Mouse>();
            yield return null;
            dropdown.Show();
            yield return null;

            var scrollRect = VisibleList();
            var position = ScreenPoint(scrollRect.viewport);
            yield return MoveMouse(position);
            InputSystem.QueueStateEvent(mouse, new MouseState
            {
                position = position,
                scroll = new Vector2(0f, -1f)
            });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState
            {
                position = position
            });
            yield return null;

            Assert.That(dropdown.isExpanded, Is.True);
            Assert.That(dropdown.value, Is.Zero);
            Assert.That(scrollRect.verticalNormalizedPosition, Is.LessThan(1f));
        }

        [UnityTest]
        public IEnumerator ExistingBusinessSelectionIsNotOverwrittenOnHide()
        {
            var dropdown = CreateDropdown("zero", "one");
            var button = CreateButton("Business Selection", new Vector2(0f, -140f));
            dropdown.Select();
            yield return TapKey(Key.Enter);

            eventSystem!.SetSelectedGameObject(button.gameObject);
            dropdown.Hide();

            Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(button.gameObject));
        }

        private MilestroDropdown CreateDropdown(params string[] options)
        {
            var dropdown = MilestroDropdownDefaults.Create(root!.transform);
            dropdown.SetOptions(options);
            return dropdown;
        }

        private Button CreateButton(string name, Vector2 position)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            gameObject.transform.SetParent(root!.transform, false);
            var rect = gameObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(180f, 40f);
            rect.anchoredPosition = position;
            return gameObject.GetComponent<Button>();
        }

        private IEnumerator TapKey(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }

        private IEnumerator Click(Component component)
        {
            var position = ScreenPoint((RectTransform)component.transform);
            yield return MoveMouse(position);
            InputSystem.QueueStateEvent(mouse, new MouseState
            {
                position = position
            }.WithButton(MouseButton.Left));
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState
            {
                position = position
            });
            yield return null;
        }

        private IEnumerator MoveMouse(Vector2 position)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState
            {
                position = position
            });
            yield return null;
        }

        private IEnumerator TapTouch(RectTransform rect)
        {
            var position = ScreenPoint(rect);
            InputSystem.QueueStateEvent(touchscreen, new TouchState
            {
                touchId = 2,
                phase = UnityEngine.InputSystem.TouchPhase.Began,
                position = position
            });
            yield return null;
            InputSystem.QueueStateEvent(touchscreen, new TouchState
            {
                touchId = 2,
                phase = UnityEngine.InputSystem.TouchPhase.Ended,
                position = position
            });
            yield return null;
            yield return null;
        }

        private Vector2 ScreenPoint(RectTransform rect)
        {
            var world = rect.TransformPoint(rect.rect.center);
            return RectTransformUtility.WorldToScreenPoint(canvas!.worldCamera, world);
        }

        private MilestroDropdownItem[] VisibleItems()
        {
            return root!.GetComponentsInChildren<MilestroDropdownItem>(false)
                .Where(item => item.isActiveAndEnabled)
                .OrderBy(item => item.OptionIndex)
                .ToArray();
        }

        private ScrollRect VisibleList()
        {
            return root!.GetComponentsInChildren<ScrollRect>(false)
                .First(scrollRect => scrollRect.isActiveAndEnabled && scrollRect.name == "Dropdown List");
        }

        private static void RemoveDevice(InputDevice? device)
        {
            if (device != null && device.added)
            {
                InputSystem.RemoveDevice(device);
            }
        }
    }
}
