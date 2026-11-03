using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Milestro.Components;
using Milestro.Components.Internal;
using Milestro.Configuration;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Milestro.Tests
{
    public class MilestroDropdownPlayModeTests
    {
        private GameObject? root;
        private Canvas? canvas;
        private GameObject? eventSystemObject;
        private RenderSurfaceConfiguration? previousConfiguration;

        [SetUp]
        public void SetUp()
        {
            previousConfiguration = MilestroConfiguration.Configuration.RenderSurface;
            MilestroConfiguration.Configuration.RenderSurface = new RenderSurfaceConfiguration
            {
                AtlasPageSize = 128
            };
            root = new GameObject("Dropdown Test Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            root.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        }

        [TearDown]
        public void TearDown()
        {
            if (eventSystemObject != null)
            {
                UnityEngine.Object.DestroyImmediate(eventSystemObject);
            }

            if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            if (previousConfiguration != null)
            {
                MilestroConfiguration.Configuration.RenderSurface = previousConfiguration;
            }
        }

        [Test]
        public void DataNormalizationAndNotificationContract()
        {
            var dropdown = CreateDropdown();
            var notifications = new List<int>();
            dropdown.onValueChanged.AddListener(notifications.Add);

            dropdown.SetOptions(new string?[] { null, "same", "same" }, 99);
            Assert.That(dropdown.optionCount, Is.EqualTo(3));
            Assert.That(dropdown.value, Is.EqualTo(2));
            Assert.That(dropdown.GetOptionText(0), Is.Empty);
            Assert.That(dropdown.options, Is.EqualTo(new[] { "", "same", "same" }));
            Assert.That(notifications, Is.Empty);

            dropdown.value = -10;
            Assert.That(dropdown.value, Is.EqualTo(-1));
            dropdown.value = -1;
            dropdown.value = 99;
            Assert.That(dropdown.value, Is.EqualTo(2));
            Assert.That(notifications, Is.EqualTo(new[] { -1, 2 }));

            dropdown.SetValueWithoutNotify(1);
            Assert.That(dropdown.value, Is.EqualTo(1));
            Assert.That(notifications, Is.EqualTo(new[] { -1, 2 }));

            dropdown.SetOptions(new[] { "replacement" });
            Assert.That(dropdown.value, Is.Zero);
            dropdown.ClearOptions();
            Assert.That(dropdown.value, Is.EqualTo(-1));
            Assert.That(dropdown.optionCount, Is.Zero);
            dropdown.value = 10;
            Assert.That(dropdown.value, Is.EqualTo(-1));
            Assert.That(notifications, Is.EqualTo(new[] { -1, 2 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => dropdown.GetOptionText(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => dropdown.SetOptionText(0, "invalid"));
        }

        [Test]
        public void PlaceholderAndPlainTextRemainConsistent()
        {
            var dropdown = CreateDropdown();
            dropdown.placeholder = "Select a value";
            dropdown.SetOptions(new[] { "first", "second" }, -1);

            Assert.That(dropdown.placeholderText!.Text, Is.EqualTo("Select a value"));
            Assert.That(dropdown.placeholderText.gameObject.activeSelf, Is.True);
            Assert.That(dropdown.captionText!.gameObject.activeSelf, Is.False);

            var captionRect = (RectTransform)dropdown.captionText.transform;
            var originalSize = captionRect.rect.size;
            dropdown.SetOptionText(0, "<b>plain text</b> 多语言 1234567890");
            dropdown.value = 0;
            Assert.That(dropdown.captionText.Text, Is.EqualTo("<b>plain text</b> 多语言 1234567890"));
            Assert.That(captionRect.rect.size, Is.EqualTo(originalSize));
            Assert.That(dropdown.placeholderText.gameObject.activeSelf, Is.False);

            dropdown.placeholder = null;
            dropdown.SetValueWithoutNotify(-1);
            Assert.That(dropdown.placeholderText.Text, Is.Empty);
        }

        [Test]
        public void TextRefreshUpdatesCaptionAndOpenRowsWithoutChangingValue()
        {
            var dropdown = CreateDropdown("zero", "one");
            var notifications = 0;
            dropdown.onValueChanged.AddListener(_ => ++notifications);
            dropdown.Show();

            dropdown.SetOptionText(0, "translated zero");
            dropdown.SetOptionText(1, null);

            Assert.That(dropdown.value, Is.Zero);
            Assert.That(dropdown.isExpanded, Is.True);
            Assert.That(dropdown.captionText!.Text, Is.EqualTo("translated zero"));
            Assert.That(VisibleItems().Select(ItemText), Is.EqualTo(new[] { "translated zero", "" }));
            Assert.That(notifications, Is.Zero);

            dropdown.SetOptions(new[] { "new" });
            Assert.That(dropdown.isExpanded, Is.False);
            Assert.That(dropdown.value, Is.Zero);
            Assert.That(notifications, Is.Zero);
        }

        [Test]
        public void ConfirmCurrentOptionClosesWithoutNotificationAndCancelKeepsCommittedValue()
        {
            var dropdown = CreateDropdown("zero", "one", "two");
            var eventSystem = EnsureEventSystem();
            var notifications = 0;
            dropdown.onValueChanged.AddListener(_ => ++notifications);

            dropdown.Show();
            ClickItemDirect(0, eventSystem);
            Assert.That(dropdown.isExpanded, Is.False);
            Assert.That(dropdown.value, Is.Zero);
            Assert.That(notifications, Is.Zero);

            dropdown.Show();
            VisibleItems()[1].OnPointerEnter(new PointerEventData(eventSystem));
            dropdown.OnCancel(new BaseEventData(eventSystem));
            Assert.That(dropdown.isExpanded, Is.False);
            Assert.That(dropdown.value, Is.Zero);
            Assert.That(notifications, Is.Zero);
        }

        [Test]
        public void SelectionCallbackCanReplaceAndReopenWithoutOldOperationClosingNewPopup()
        {
            var dropdown = CreateDropdown("zero", "one");
            var eventSystem = EnsureEventSystem();
            var notifications = 0;
            dropdown.onValueChanged.AddListener(_ =>
            {
                ++notifications;
                dropdown.SetOptions(new[] { "new zero", "new one" });
                dropdown.Show();
            });

            dropdown.Show();
            ClickItemDirect(1, eventSystem);

            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(dropdown.value, Is.Zero);
            Assert.That(dropdown.options, Is.EqualTo(new[] { "new zero", "new one" }));
            Assert.That(dropdown.isExpanded, Is.True);
            Assert.That(VisibleItems().Select(ItemText), Is.EqualTo(new[] { "new zero", "new one" }));
        }

        [Test]
        public void NestedProgrammaticSelectionUsesCurrentValueAndDoesNotResumeOldOperation()
        {
            var dropdown = CreateDropdown("zero", "one", "two");
            var eventSystem = EnsureEventSystem();
            var notifications = new List<int>();
            dropdown.onValueChanged.AddListener(index =>
            {
                notifications.Add(index);
                if (index == 1)
                {
                    dropdown.value = 2;
                }
            });

            dropdown.Show();
            ClickItemDirect(1, eventSystem);

            Assert.That(dropdown.value, Is.EqualTo(2));
            Assert.That(dropdown.captionText!.Text, Is.EqualTo("two"));
            Assert.That(notifications, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(dropdown.isExpanded, Is.False);
        }

        [UnityTest]
        public IEnumerator DisableAndDestroyInsideSelectionCallbackCleanPopup()
        {
            var disabledDropdown = CreateDropdown("zero", "one");
            var eventSystem = EnsureEventSystem();
            disabledDropdown.onValueChanged.AddListener(_ => disabledDropdown.interactable = false);
            disabledDropdown.Show();
            ClickItemDirect(1, eventSystem);
            yield return null;

            Assert.That(disabledDropdown.value, Is.EqualTo(1));
            Assert.That(disabledDropdown.isExpanded, Is.False);
            Assert.That(ActivePopupCount(), Is.Zero);

            var destroyedDropdown = CreateDropdown("zero", "one");
            destroyedDropdown.onValueChanged.AddListener(_ => UnityEngine.Object.Destroy(destroyedDropdown.gameObject));
            destroyedDropdown.Show();
            ClickItemDirect(1, eventSystem);
            yield return null;
            yield return null;

            Assert.That(ActivePopupCount(), Is.Zero);
        }

        [UnityTest]
        public IEnumerator SecondDropdownCanvasGroupDeactivationAndDestroyCleanPopup()
        {
            var first = CreateDropdown("first");
            var second = CreateDropdown("second");
            first.Show();
            second.Show();
            Assert.That(first.isExpanded, Is.False);
            Assert.That(second.isExpanded, Is.True);
            Assert.That(ActivePopupCount(), Is.EqualTo(1));

            first.interactable = false;
            first.Show();
            Assert.That(first.isExpanded, Is.False);

            second.interactable = false;
            yield return null;
            Assert.That(second.isExpanded, Is.False);
            second.interactable = true;

            var group = second.gameObject.AddComponent<CanvasGroup>();
            second.Show();
            group.blocksRaycasts = false;
            Assert.That(second.isExpanded, Is.False);
            second.Show();
            Assert.That(second.isExpanded, Is.False);

            group.blocksRaycasts = true;
            second.Show();
            group.interactable = false;
            Assert.That(second.isExpanded, Is.False);

            group.interactable = true;
            second.Show();
            second.gameObject.SetActive(false);
            Assert.That(second.isExpanded, Is.False);
            second.gameObject.SetActive(true);
            second.Show();
            UnityEngine.Object.Destroy(second.gameObject);
            yield return null;
            yield return null;

            Assert.That(ActivePopupCount(), Is.Zero);
        }

        [UnityTest]
        public IEnumerator GeometryChangeClosesInsteadOfLeavingDetachedPopup()
        {
            var dropdown = CreateDropdown("zero", "one");
            dropdown.Show();
            yield return null;
            Assert.That(dropdown.isExpanded, Is.True);

            ((RectTransform)dropdown.transform).anchoredPosition += new Vector2(40f, 0f);
            yield return null;

            Assert.That(dropdown.isExpanded, Is.False);
            Assert.That(ActivePopupCount(), Is.Zero);
        }

        [UnityTest]
        public IEnumerator HundredOptionsScrollAndRepeatedOpenCloseDoNotAccumulatePopups()
        {
            var dropdown = CreateDropdown();
            dropdown.maxVisibleItems = 5;
            dropdown.SetOptions(Enumerable.Range(0, 100).Select(index => "Option " + index));
            dropdown.Show();

            Assert.That(VisibleItems().Length, Is.EqualTo(100));
            var scrollRect = VisibleList();
            scrollRect.verticalNormalizedPosition = 0f;
            yield return null;
            Assert.That(scrollRect.verticalNormalizedPosition, Is.EqualTo(0f).Within(0.001f));
            Assert.That(dropdown.value, Is.Zero);

            for (var iteration = 0; iteration < 20; ++iteration)
            {
                dropdown.Hide();
                dropdown.Show();
            }

            Assert.That(dropdown.isExpanded, Is.True);
            Assert.That(ActivePopupCount(), Is.EqualTo(1));
            dropdown.Hide();
            yield return null;
            Assert.That(ActivePopupCount(), Is.Zero);
        }

        [UnityTest]
        public IEnumerator OverlayAndCameraTemplatesStayInsideScaledCanvasAtAllEdges()
        {
            yield return ExerciseEdges(RenderMode.ScreenSpaceOverlay);
            yield return ExerciseEdges(RenderMode.ScreenSpaceCamera);
        }

        [Test]
        public void DefaultTemplateUsesOnlyMilestroTextAndNoHiddenLegacyText()
        {
            CreateDropdown("option");
            var components = root!.GetComponentsInChildren<Component>(true);

            Assert.That(components.Any(component => component is UnityEngine.UI.Text), Is.False);
            Assert.That(components.Any(component => component != null &&
                component.GetType().Namespace != null && component.GetType().Namespace.StartsWith("TMPro",
                    StringComparison.Ordinal)), Is.False);
            Assert.That(components.OfType<CanvasSlimText>().Count(), Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void DefaultScrollbarHandleStaysInsideItsTrack()
        {
            var dropdown = CreateDropdown("zero", "one");
            dropdown.Show();
            Canvas.ForceUpdateCanvases();

            var scrollbar = VisibleList().verticalScrollbar;
            Assert.That(scrollbar, Is.Not.Null);
            var handle = scrollbar.handleRect;
            var track = (RectTransform)handle.parent;
            Assert.That(handle.sizeDelta, Is.EqualTo(Vector2.zero));
            Assert.That(handle.rect.width, Is.LessThanOrEqualTo(track.rect.width + 0.01f));
            Assert.That(handle.rect.height, Is.LessThanOrEqualTo(track.rect.height + 0.01f));
        }

        private IEnumerator ExerciseEdges(RenderMode renderMode)
        {
            UnityEngine.Object.DestroyImmediate(root);
            root = new GameObject("Dropdown " + renderMode, typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = renderMode;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 2f;
            if (renderMode == RenderMode.ScreenSpaceCamera)
            {
                var cameraObject = new GameObject("UI Camera", typeof(Camera));
                cameraObject.transform.SetParent(root.transform, false);
                var camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10f;
            }

            var nestedObject = new GameObject("Nested Canvas", typeof(RectTransform), typeof(Canvas));
            nestedObject.transform.SetParent(root.transform, false);
            var nestedCanvas = nestedObject.GetComponent<Canvas>();
            nestedCanvas.overrideSorting = true;
            var maskedParent = new GameObject("Masked Settings Row", typeof(RectTransform), typeof(RectMask2D));
            maskedParent.transform.SetParent(nestedObject.transform, false);
            var dropdown = CreateDropdown(maskedParent.transform, "zero", "one", "two", "three", "four");
            dropdown.maxVisibleItems = 3;
            yield return null;

            var corners = new[]
            {
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 0f), new Vector2(1f, 1f)
            };
            foreach (var corner in corners)
            {
                var dropdownRect = (RectTransform)dropdown.transform;
                dropdownRect.anchorMin = corner;
                dropdownRect.anchorMax = corner;
                dropdownRect.anchoredPosition = Vector2.zero;
                dropdown.Show();
                Assert.That(dropdown.isExpanded, Is.True, renderMode + " at " + corner);

                var list = VisibleList();
                var listRect = (RectTransform)list.transform;
                Assert.That(listRect.GetComponentInParent<RectMask2D>(), Is.Null);
                var worldCorners = new Vector3[4];
                listRect.GetWorldCorners(worldCorners);
                var rootRect = (RectTransform)canvas.transform;
                foreach (var worldCorner in worldCorners)
                {
                    var local = (Vector2)rootRect.InverseTransformPoint(worldCorner);
                    Assert.That(local.x, Is.GreaterThanOrEqualTo(rootRect.rect.xMin - 0.1f));
                    Assert.That(local.x, Is.LessThanOrEqualTo(rootRect.rect.xMax + 0.1f));
                    Assert.That(local.y, Is.GreaterThanOrEqualTo(rootRect.rect.yMin - 0.1f));
                    Assert.That(local.y, Is.LessThanOrEqualTo(rootRect.rect.yMax + 0.1f));
                }

                dropdown.Hide();
            }
        }

        private MilestroDropdown CreateDropdown(params string[] options)
        {
            return CreateDropdown(root!.transform, options);
        }

        private static MilestroDropdown CreateDropdown(Transform parent, params string[] options)
        {
            var dropdown = MilestroDropdownDefaults.Create(parent);
            dropdown.SetOptions(options);
            return dropdown;
        }

        private EventSystem EnsureEventSystem()
        {
            if (eventSystemObject == null)
            {
                eventSystemObject = new GameObject("Dropdown Test EventSystem", typeof(EventSystem));
            }

            return eventSystemObject.GetComponent<EventSystem>();
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

        private int ActivePopupCount()
        {
            return root!.GetComponentsInChildren<MilestroDropdownBlocker>(false)
                .Count(blocker => blocker.isActiveAndEnabled);
        }

        private static string ItemText(MilestroDropdownItem item)
        {
            return item.transform.Find("Item Text").GetComponent<CanvasSlimText>().Text;
        }

        private void ClickItemDirect(int index, EventSystem eventSystem)
        {
            var item = VisibleItems().First(candidate => candidate.OptionIndex == index);
            item.OnPointerClick(new PointerEventData(eventSystem)
            {
                button = PointerEventData.InputButton.Left
            });
        }
    }
}
