using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Milestro.Components;
using Milestro.Components.Internal;
using Milestro.Model;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.TestTools;
using UnityEngine.UI;
#if UNITY_6000_3_OR_NEWER && MILESTRO_UNITY_ACCESSIBILITY
using UnityEngine.Accessibility;
#endif

namespace Milestro.Tests
{
    public class TextBoxHyperlinkInteractionTests
    {
        private const BindingFlags DeclaredInstance =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        [Test]
        public void FacadeUsesSerializableGetterOnlyNamedUnityEvent()
        {
            Assert.That(typeof(LinkClickedEventArgs).IsSealed, Is.True);
            Assert.That(typeof(LinkClickedEventArgs).IsSerializable, Is.True);
            Assert.That(typeof(LinkClickedEvent).IsSealed, Is.True);
            Assert.That(typeof(LinkClickedEvent).IsSerializable, Is.True);

            AssertGetterOnlyProperty(typeof(LinkClickedEventArgs), nameof(LinkClickedEventArgs.Href), typeof(string));
            AssertGetterOnlyProperty(typeof(LinkClickedEventArgs), nameof(LinkClickedEventArgs.Id), typeof(string));

            var fixture = CreateFixture();
            try
            {
                var field = typeof(TextBox).GetField("m_OnLinkClicked", DeclaredInstance);
                Assert.That(field, Is.Not.Null);
                Assert.That(field!.IsPrivate, Is.True);
                Assert.That(field.FieldType, Is.EqualTo(typeof(LinkClickedEvent)));
                Assert.That(field.GetCustomAttributes(typeof(SerializeField), inherit: false).Any(), Is.True);

                var property = typeof(TextBox).GetProperty("onLinkClicked", DeclaredInstance);
                Assert.That(property, Is.Not.Null);
                Assert.That(property!.PropertyType, Is.EqualTo(typeof(LinkClickedEvent)));
                Assert.That(property.GetMethod, Is.Not.Null);
                Assert.That(property.GetMethod!.IsPublic, Is.True);
                Assert.That(property.SetMethod, Is.Null);
                Assert.That(fixture.TextBox.onLinkClicked, Is.SameAs(fixture.TextBox.onLinkClicked));

                Assert.That(typeof(TextBox).GetEvents(BindingFlags.Instance |
                                                       BindingFlags.Public |
                                                       BindingFlags.DeclaredOnly),
                    Is.Empty,
                    "TextBox must not expose a second public C# event channel.");
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void SameRangePressAndReleaseInvokesPayloadOnce()
        {
            var fixture = CreateFixture();
            try
            {
                LinkClickedEventArgs? received = null;
                fixture.TextBox.onLinkClicked.AddListener(value => received = value);
                var pointer = fixture.PointerAt(0.25f, 0.5f, pointerId: -1);

                fixture.TextBox.OnPointerDown(pointer);
                fixture.TextBox.OnPointerUp(pointer);
                fixture.TextBox.OnPointerClick(pointer);
                fixture.TextBox.OnPointerClick(pointer);

                Assert.That(received, Is.Not.Null);
                Assert.That(received!.Href, Is.EqualTo("same-target"));
                Assert.That(received.Id, Is.EqualTo("same-id"));
                Assert.That(fixture.Target.HitTestCalls, Is.EqualTo(2));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void ListenerReenteringSameClickCannotRedeliverObservedPress()
        {
            var fixture = CreateFixture();
            try
            {
                var calls = 0;
                var reentered = false;
                var pointer = fixture.PointerAt(0.25f, 0.5f, pointerId: 20);
                fixture.TextBox.onLinkClicked.AddListener(_ =>
                {
                    ++calls;
                    if (!reentered)
                    {
                        reentered = true;
                        fixture.TextBox.OnPointerClick(pointer);
                    }
                });

                fixture.TextBox.OnPointerDown(pointer);
                fixture.TextBox.OnPointerClick(pointer);

                Assert.That(calls, Is.EqualTo(1));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void ListenerCreatedSamePointerPressSurvivesOuterClick()
        {
            var fixture = CreateFixture();
            try
            {
                var calls = 0;
                var pointer = fixture.PointerAt(0.25f, 0.5f, pointerId: 21);
                fixture.TextBox.onLinkClicked.AddListener(_ =>
                {
                    ++calls;
                    if (calls == 1)
                    {
                        fixture.TextBox.OnPointerDown(pointer);
                    }
                });

                fixture.TextBox.OnPointerDown(pointer);
                fixture.TextBox.OnPointerClick(pointer);
                fixture.TextBox.OnPointerClick(pointer);
                fixture.TextBox.OnPointerClick(pointer);

                Assert.That(calls, Is.EqualTo(2));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void ListenerChangingTextCannotMakeObservedPressRetryable()
        {
            var fixture = CreateFixture();
            try
            {
                var calls = 0;
                var pointer = fixture.PointerAt(0.25f, 0.5f, pointerId: 22);
                fixture.TextBox.onLinkClicked.AddListener(_ =>
                {
                    ++calls;
                    fixture.Producer.content = "listener changed text";
                });

                fixture.TextBox.OnPointerDown(pointer);
                fixture.TextBox.OnPointerClick(pointer);
                fixture.TextBox.OnPointerClick(pointer);

                Assert.That(calls, Is.EqualTo(1));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void ListenerDisablingTextBoxCannotDeleteLaterPress()
        {
            var fixture = CreateFixture();
            try
            {
                var calls = 0;
                var pointer = fixture.PointerAt(0.25f, 0.5f, pointerId: 23);
                fixture.TextBox.onLinkClicked.AddListener(_ =>
                {
                    ++calls;
                    fixture.TextBox.enabled = false;
                });

                fixture.TextBox.OnPointerDown(pointer);
                fixture.TextBox.OnPointerClick(pointer);
                fixture.TextBox.enabled = true;
                fixture.TextBox.OnPointerClick(pointer);
                fixture.TextBox.OnPointerDown(pointer);
                fixture.TextBox.OnPointerClick(pointer);

                Assert.That(calls, Is.EqualTo(2));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void ListenerDestroyingTextBoxReturnsWithoutPostCallbackAccess()
        {
            var fixture = CreateFixture();
            try
            {
                var calls = 0;
                var pointer = fixture.PointerAt(0.25f, 0.5f, pointerId: 24);
                fixture.TextBox.onLinkClicked.AddListener(_ =>
                {
                    ++calls;
                    UnityEngine.Object.DestroyImmediate(fixture.TextBox);
                });

                fixture.TextBox.OnPointerDown(pointer);
                Assert.That(() => fixture.TextBox.OnPointerClick(pointer), Throws.Nothing);
                Assert.That(calls, Is.EqualTo(1));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void ThrowingListenerConsumesOldPressAndAllowsLaterPress()
        {
            var fixture = CreateFixture();
            try
            {
                var pointer = fixture.PointerAt(0.25f, 0.5f, pointerId: 25);
                UnityAction<LinkClickedEventArgs> throwing = _ =>
                    throw new InvalidOperationException("listener failure");
                fixture.TextBox.onLinkClicked.AddListener(throwing);

                fixture.TextBox.OnPointerDown(pointer);
                Assert.Throws<InvalidOperationException>(() => fixture.TextBox.OnPointerClick(pointer));
                Assert.That(() => fixture.TextBox.OnPointerClick(pointer), Throws.Nothing);

                fixture.TextBox.onLinkClicked.RemoveListener(throwing);
                var calls = 0;
                fixture.TextBox.onLinkClicked.AddListener(_ => ++calls);
                fixture.TextBox.OnPointerDown(pointer);
                fixture.TextBox.OnPointerClick(pointer);

                Assert.That(calls, Is.EqualTo(1));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void EqualPayloadDifferentOccurrenceDoesNotInvoke()
        {
            var fixture = CreateFixture();
            try
            {
                var calls = 0;
                fixture.TextBox.onLinkClicked.AddListener(_ => ++calls);
                var pointer = fixture.PointerAt(0.25f, 0.5f, pointerId: 4);
                fixture.TextBox.OnPointerDown(pointer);

                pointer.position = fixture.ScreenPoint(0.75f, 0.5f);
                fixture.TextBox.OnPointerClick(pointer);
                pointer.position = fixture.ScreenPoint(0.25f, 0.5f);
                fixture.TextBox.OnPointerClick(pointer);

                Assert.That(calls, Is.Zero);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void MissingReleaseHitConsumesObservedPress()
        {
            var fixture = CreateFixture();
            try
            {
                var calls = 0;
                fixture.TextBox.onLinkClicked.AddListener(_ => ++calls);
                var pointer = fixture.PointerAt(0.25f, 0.5f, pointerId: 26);
                fixture.TextBox.OnPointerDown(pointer);

                pointer.position = fixture.ScreenPoint(0.5f, 0.5f);
                fixture.TextBox.OnPointerClick(pointer);
                pointer.position = fixture.ScreenPoint(0.25f, 0.5f);
                fixture.TextBox.OnPointerClick(pointer);

                Assert.That(calls, Is.Zero);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void ContentGenerationChangeCancelsOldPress()
        {
            var fixture = CreateFixture();
            try
            {
                var calls = 0;
                fixture.TextBox.onLinkClicked.AddListener(_ => ++calls);
                var pointer = fixture.PointerAt(0.25f, 0.5f, pointerId: 5);
                fixture.TextBox.OnPointerDown(pointer);

                fixture.Producer.content = "new layout generation";
                fixture.TextBox.OnPointerClick(pointer);

                Assert.That(calls, Is.Zero);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void DragCancelledClickAndDisableBothClearPress()
        {
            var fixture = CreateFixture();
            try
            {
                var calls = 0;
                fixture.TextBox.onLinkClicked.AddListener(_ => ++calls);
                var pointer = fixture.PointerAt(0.25f, 0.5f, pointerId: 6);
                fixture.TextBox.OnPointerDown(pointer);
                pointer.eligibleForClick = false;
                fixture.TextBox.OnPointerClick(pointer);

                pointer.eligibleForClick = true;
                fixture.TextBox.OnPointerClick(pointer);
                fixture.TextBox.OnPointerDown(pointer);
                fixture.TextBox.enabled = false;
                fixture.TextBox.enabled = true;
                fixture.TextBox.OnPointerClick(pointer);

                Assert.That(calls, Is.Zero);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void EventSystemCancelClearsEveryPendingPress()
        {
            var fixture = CreateFixture();
            try
            {
                var calls = 0;
                fixture.TextBox.onLinkClicked.AddListener(_ => ++calls);
                var first = fixture.PointerAt(0.25f, 0.5f, pointerId: 61);
                var second = fixture.PointerAt(0.25f, 0.5f, pointerId: 62);
                fixture.TextBox.OnPointerDown(first);
                fixture.TextBox.OnPointerDown(second);

                fixture.TextBox.OnCancel(new BaseEventData(fixture.EventSystem));
                fixture.TextBox.OnPointerClick(first);
                fixture.TextBox.OnPointerClick(second);

                Assert.That(calls, Is.Zero);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator PointerUpWithoutClickClearsPressOnNextFrame()
        {
            var fixture = CreateFixture();
            try
            {
                var calls = 0;
                fixture.TextBox.onLinkClicked.AddListener(_ => ++calls);
                var pointer = fixture.PointerAt(0.25f, 0.5f, pointerId: 7);
                fixture.TextBox.OnPointerDown(pointer);
                fixture.TextBox.OnPointerUp(pointer);

                yield return null;
                fixture.TextBox.OnPointerClick(pointer);

                Assert.That(calls, Is.Zero);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void FlowSliceMapsOnlyVisibleMeshToNormalizedTargetCoordinates()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.TextBox.SetFlowModeActive(true);
                fixture.TextBox.SetFlowVisibleRange(true, 20f, 60f, 40f);
                var pointer = fixture.PointerAtFlowLocalDistance(30f, pointerId: 8);

                fixture.TextBox.OnPointerDown(pointer);

                Assert.That(fixture.Target.HitTestCalls, Is.EqualTo(1));
                Assert.That(fixture.Target.LastNormalizedPoint.x, Is.EqualTo(0.25f).Within(0.001f));
                Assert.That(fixture.Target.LastNormalizedPoint.y, Is.EqualTo(0.25f).Within(0.001f));

                pointer.position = fixture.ScreenPointFromTop(0.25f, 10f);
                fixture.TextBox.OnPointerDown(pointer);
                Assert.That(fixture.Target.HitTestCalls, Is.EqualTo(1),
                    "Flow content outside the visible slice must not reach range hit testing.");
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void SecondaryMouseButtonNeverStartsLinkPress()
        {
            var fixture = CreateFixture();
            try
            {
                var calls = 0;
                fixture.TextBox.onLinkClicked.AddListener(_ => ++calls);
                var pointer = fixture.PointerAt(0.25f, 0.5f, pointerId: -2);
                pointer.button = PointerEventData.InputButton.Right;

                fixture.TextBox.OnPointerDown(pointer);
                fixture.TextBox.OnPointerClick(pointer);

                Assert.That(calls, Is.Zero);
                Assert.That(fixture.Target.HitTestCalls, Is.Zero);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void KeyboardAndControllerNavigateActivateAndShowFocus()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.Target.SetContent("firstsecond",
                    new RichTextParser.LinkAnnotation("first-target", "first-id", 0, 5, 0),
                    new RichTextParser.LinkAnnotation("second-target", "second-id", 5, 11, 1));
                var accessibility = fixture.TextBox.gameObject.AddComponent<TextBoxAccessibility>();
                var received = new List<string>();
                fixture.TextBox.onLinkClicked.AddListener(value => received.Add(value.Href));

                accessibility.OnSelect(new BaseEventData(fixture.EventSystem));
                var indicator = fixture.TextBox.GetComponentInChildren<TextBoxLinkFocusIndicator>(true);
                Assert.That(indicator, Is.Not.Null);
                Assert.That(indicator!.enabled, Is.True);

                accessibility.OnSubmit(new BaseEventData(fixture.EventSystem));
                accessibility.OnMove(new AxisEventData(fixture.EventSystem) { moveDir = MoveDirection.Right });
                accessibility.OnSubmit(new BaseEventData(fixture.EventSystem));

                Assert.That(received, Is.EqualTo(new[] { "first-target", "second-target" }));
                accessibility.OnDeselect(new BaseEventData(fixture.EventSystem));
                Assert.That(indicator.enabled, Is.False);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void NavigatingToOffscreenLinkScrollsItIntoView()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.Target.SetGeometry(
                    new TextBoxLinkGeometry(0,
                        new[] { new Rect(10f, 10f, 80f, 20f) },
                        Array.Empty<TextBoxLinkBlockRange>()),
                    new TextBoxLinkGeometry(1,
                        new[] { new Rect(10f, 140f, 80f, 20f) },
                        Array.Empty<TextBoxLinkBlockRange>()));
                fixture.Target.SetVerticalScrollLimit(100f);
                var accessibility = fixture.TextBox.gameObject.AddComponent<TextBoxAccessibility>();

                accessibility.OnSelect(new BaseEventData(fixture.EventSystem));
                accessibility.OnMove(new AxisEventData(fixture.EventSystem) { moveDir = MoveDirection.Down });

                Assert.That(fixture.Producer.scrollY, Is.EqualTo(60f).Within(0.001f));
            }
            finally
            {
                fixture.Dispose();
            }
        }

#if UNITY_6000_3_OR_NEWER && MILESTRO_UNITY_ACCESSIBILITY
        [Test]
        public void ScreenReaderHierarchyExposesTextAndLinksAndClearsOnDisable()
        {
            var fixture = CreateFixture();
            var previousHierarchy = AssistiveSupport.activeHierarchy;
            var hierarchy = new AccessibilityHierarchy();
            try
            {
                AssistiveSupport.activeHierarchy = hierarchy;
                fixture.Target.SetContent("a first b second c",
                    new RichTextParser.LinkAnnotation("first-target", "first-id", 2, 7, 0),
                    new RichTextParser.LinkAnnotation("second-target", "second-id", 10, 16, 1));
                var accessibility = fixture.TextBox.gameObject.AddComponent<TextBoxAccessibility>();
                typeof(TextBoxAccessibility).GetMethod("Update", DeclaredInstance)!
                    .Invoke(accessibility, null);

                Assert.That(hierarchy.rootNodes.Count, Is.EqualTo(1));
                var container = hierarchy.rootNodes[0];
                Assert.That(container.role, Is.EqualTo(AccessibilityRole.Container));
                Assert.That(container.children.Select(node => node.role),
                    Is.EqualTo(new[]
                    {
                        AccessibilityRole.StaticText,
                        AccessibilityRole.Button,
                        AccessibilityRole.StaticText,
                        AccessibilityRole.Button,
                        AccessibilityRole.StaticText
                    }));
                Assert.That(container.children.Select(node => node.label),
                    Is.EqualTo(new[] { "a ", "first", " b ", "second", " c" }));
                Assert.That(container.children[1].hint, Is.EqualTo("Link"));
                hierarchy.RefreshNodeFrames();
                Assert.That(container.children[1].frame.width, Is.GreaterThan(0f));
                Assert.That(container.children[1].frame.height, Is.GreaterThan(0f));

                accessibility.enabled = false;
                Assert.That(hierarchy.rootNodes, Is.Empty);
            }
            finally
            {
                fixture.Dispose();
                AssistiveSupport.activeHierarchy = previousHierarchy;
            }
        }
#endif

        private static void AssertGetterOnlyProperty(Type owner, string name, Type propertyType)
        {
            var property = owner.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null);
            Assert.That(property!.PropertyType, Is.EqualTo(propertyType));
            Assert.That(property.GetMethod, Is.Not.Null);
            Assert.That(property.SetMethod, Is.Null);
        }

        private static Fixture CreateFixture()
        {
            return new Fixture();
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root;
            private readonly EventSystem eventSystem;

            internal Fixture()
            {
                root = new GameObject("TextBox hyperlink test canvas",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster));
                root.SetActive(false);
                root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

                var eventObject = new GameObject("TextBox hyperlink test EventSystem", typeof(EventSystem));
                eventObject.transform.SetParent(root.transform, false);
                eventSystem = eventObject.GetComponent<EventSystem>();

                var child = new GameObject("TextBox hyperlink test target", typeof(RectTransform));
                child.transform.SetParent(root.transform, false);
                var rectTransform = child.GetComponent<RectTransform>();
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.sizeDelta = new Vector2(200f, 100f);
                rectTransform.anchoredPosition = Vector2.zero;

                Producer = child.AddComponent<TextBoxRenderTextureProducer>();
                TextBox = child.AddComponent<TextBox>();
                Target = new LinkRenderTarget();
                Producer.DisposeRenderTarget();
                Producer.renderTargetFactory = () => Target;
                root.SetActive(true);
            }

            internal TextBox TextBox { get; }
            internal TextBoxRenderTextureProducer Producer { get; }
            internal LinkRenderTarget Target { get; }
            internal EventSystem EventSystem => eventSystem;

            internal PointerEventData PointerAt(float normalizedX, float normalizedY, int pointerId)
            {
                return new PointerEventData(eventSystem)
                {
                    pointerId = pointerId,
                    button = PointerEventData.InputButton.Left,
                    eligibleForClick = true,
                    position = ScreenPoint(normalizedX, normalizedY)
                };
            }

            internal PointerEventData PointerAtFlowLocalDistance(float distanceFromTop, int pointerId)
            {
                return new PointerEventData(eventSystem)
                {
                    pointerId = pointerId,
                    button = PointerEventData.InputButton.Left,
                    eligibleForClick = true,
                    position = ScreenPointFromTop(0.25f, distanceFromTop)
                };
            }

            internal Vector2 ScreenPoint(float normalizedX, float normalizedY)
            {
                var rect = TextBox.rectTransform.rect;
                return ScreenPointForLocal(new Vector2(rect.xMin + rect.width * normalizedX,
                    rect.yMax - rect.height * normalizedY));
            }

            internal Vector2 ScreenPointFromTop(float normalizedX, float distanceFromTop)
            {
                var rect = TextBox.rectTransform.rect;
                return ScreenPointForLocal(new Vector2(rect.xMin + rect.width * normalizedX,
                    rect.yMax - distanceFromTop));
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            private Vector2 ScreenPointForLocal(Vector2 localPoint)
            {
                var worldPoint = TextBox.rectTransform.TransformPoint(localPoint);
                return RectTransformUtility.WorldToScreenPoint(null, worldPoint);
            }
        }

        private sealed class LinkRenderTarget : ITextBoxRenderTarget
        {
            private long generation = 1;
            private string plainText = "firstsecond";
            private RichTextParser.LinkAnnotation[] links =
            {
                new RichTextParser.LinkAnnotation("same-target", "same-id", 0, 5, 0),
                new RichTextParser.LinkAnnotation("same-target", "same-id", 5, 11, 1)
            };
            private IReadOnlyList<TextBoxLinkGeometry> geometry = new[]
            {
                new TextBoxLinkGeometry(0,
                    new[] { new Rect(10f, 10f, 80f, 20f) },
                    Array.Empty<TextBoxLinkBlockRange>()),
                new TextBoxLinkGeometry(1,
                    new[] { new Rect(110f, 60f, 80f, 20f) },
                    Array.Empty<TextBoxLinkBlockRange>())
            };

            public Texture? OutputTexture => null;
            public Rect OutputUvRect => new Rect(0f, 0f, 1f, 1f);
            public int OutputWidth => 200;
            public int OutputHeight => 100;
            public bool HasOutput => false;
            public long OutputVersion => 0;
            public Vector2 ScrollOffset { get; private set; }
            public Vector2 ContentSize => new Vector2(200f, 100f);
            public Vector2 ViewportSize => new Vector2(200f, 100f);
            public Vector2 MaxScrollOffset { get; private set; }
            public TextBoxHorizontalScrollState HorizontalScrollState => default;
            internal int HitTestCalls { get; private set; }
            internal Vector2 LastNormalizedPoint { get; private set; }

            public event Action<Milestro.Skia.UnitySkiaRenderTextureSurface.RenderSubmissionStatus>?
                RenderEventCompleted;

            public void MarkPropertiesChanged()
            {
                ++generation;
            }

            public void MarkPaintChanged()
            {
                ++generation;
            }

            public bool TryHitLink(Vector2 normalizedViewportPoint, out TextBoxLinkHit hit)
            {
                ++HitTestCalls;
                LastNormalizedPoint = normalizedViewportPoint;
                if (normalizedViewportPoint.x < 0.45f)
                {
                    hit = new TextBoxLinkHit("same-target", "same-id", 0, generation);
                    return true;
                }

                if (normalizedViewportPoint.x > 0.55f)
                {
                    hit = new TextBoxLinkHit("same-target", "same-id", 1, generation);
                    return true;
                }

                hit = default;
                return false;
            }

            public bool TryGetContentSnapshot(out TextBoxContentSnapshot snapshot)
            {
                snapshot = new TextBoxContentSnapshot(1, plainText, links);
                return true;
            }

            public bool TryGetLinkGeometrySnapshot(out TextBoxLinkGeometrySnapshot snapshot, out long publishedGeneration)
            {
                publishedGeneration = generation;
                snapshot = new TextBoxLinkGeometrySnapshot(1,
                    null!,
                    links,
                    geometry,
                    new Vector2(0f, -ScrollOffset.y),
                    new Rect(0f, 0f, 200f, 100f),
                    new Vector2Int(200, 100));
                return true;
            }

            internal void SetContent(string text, params RichTextParser.LinkAnnotation[] annotations)
            {
                plainText = text;
                links = annotations;
            }

            internal void SetGeometry(params TextBoxLinkGeometry[] value)
            {
                geometry = value;
            }

            internal void SetVerticalScrollLimit(float value)
            {
                MaxScrollOffset = new Vector2(0f, value);
            }

            public bool Rebuild(TextBoxRenderViewport viewport,
                ColorSpace colorSpace,
                TextBoxRenderTargetSettings settings,
                bool forceText,
                UnityEngine.Object? logContext)
            {
                ScrollOffset = new Vector2(ScrollOffset.x,
                    Mathf.Clamp(viewport.RequestedScrollY, 0f, MaxScrollOffset.y));
                return true;
            }

            public void Dispose()
            {
            }
        }
    }
}
