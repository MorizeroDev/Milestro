using System;
using System.Collections.Generic;
using Milestro.Components.Internal;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_6000_3_OR_NEWER && MILESTRO_UNITY_ACCESSIBILITY
using UnityEngine.Accessibility;
#endif

namespace Milestro.Components
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextBox))]
    [AddComponentMenu("Milestro/Text Box Accessibility")]
    public sealed class TextBoxAccessibility : Selectable, ISubmitHandler, ICancelHandler
    {
        [SerializeField] private Color m_focusColor = new Color(0.2f, 0.49f, 1f, 0.9f);
        [SerializeField][Min(0f)] private float m_focusThickness = 2f;
        [SerializeField] private string m_linkHint = "Link";
        [SerializeField] private bool m_screenReaderEnabled = true;

        [NonSerialized] private TextBox? textBox;
        [NonSerialized] private TextBoxLinkFocusIndicator? focusIndicator;
        [NonSerialized] private readonly List<Rect> focusRects = new List<Rect>();
        [NonSerialized] private readonly List<Rect> screenRects = new List<Rect>();
        [NonSerialized] private bool navigationFocused;
        [NonSerialized] private bool accessibilityFocused;
        [NonSerialized] private bool suppressPointerFocus;
        [NonSerialized] private bool hasActiveLink;
        [NonSerialized] private long activeContentVersion;
        [NonSerialized] private int activeOccurrence;
#if UNITY_6000_3_OR_NEWER && MILESTRO_UNITY_ACCESSIBILITY
        [NonSerialized] private UnityAccessibilityAdapter? accessibilityAdapter;
#endif

        protected override void Awake()
        {
            base.Awake();
            textBox = GetComponent<TextBox>();
            transition = Transition.None;
#if UNITY_6000_3_OR_NEWER && MILESTRO_UNITY_ACCESSIBILITY
            accessibilityAdapter = new UnityAccessibilityAdapter(this);
#endif
        }

        protected override void OnEnable()
        {
            base.OnEnable();
#if !UNITY_6000_3_OR_NEWER || !MILESTRO_UNITY_ACCESSIBILITY
            if (m_screenReaderEnabled)
            {
                Debug.LogWarning("TextBoxAccessibility screen-reader support requires Unity 6000.3 or newer and the built-in Accessibility module.", this);
            }
#endif
        }

        protected override void OnDisable()
        {
            navigationFocused = false;
            accessibilityFocused = false;
            ClearFocusIndicator();
#if UNITY_6000_3_OR_NEWER && MILESTRO_UNITY_ACCESSIBILITY
            accessibilityAdapter?.Clear();
#endif
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
#if UNITY_6000_3_OR_NEWER && MILESTRO_UNITY_ACCESSIBILITY
            accessibilityAdapter?.Dispose();
            accessibilityAdapter = null;
#endif
            if (focusIndicator != null)
            {
                DestroyImmediate(focusIndicator.gameObject);
                focusIndicator = null;
            }

            base.OnDestroy();
        }

        private void Update()
        {
            var target = TargetTextBox();
            if (target == null)
            {
                return;
            }

#if UNITY_6000_3_OR_NEWER && MILESTRO_UNITY_ACCESSIBILITY
            if (m_screenReaderEnabled && target.isActiveAndEnabled)
            {
                if (target.TryGetLinkInteractionSnapshot(out var content, out var geometry, out _))
                {
                    accessibilityAdapter?.Sync(content, geometry);
                }
                else if (target.TryGetContentSnapshot(out content))
                {
                    accessibilityAdapter?.Sync(content, null);
                }
                else
                {
                    accessibilityAdapter?.Clear();
                }
            }
            else
            {
                accessibilityAdapter?.Clear();
            }
#endif

            RefreshFocusIndicator();
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            suppressPointerFocus = true;
            try
            {
                base.OnPointerDown(eventData);
            }
            finally
            {
                suppressPointerFocus = false;
            }
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            if (suppressPointerFocus)
            {
                return;
            }

            navigationFocused = true;
            EnsureActiveLink();
            RefreshFocusIndicator();
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            navigationFocused = false;
            RefreshFocusIndicator();
            base.OnDeselect(eventData);
        }

        public override void OnMove(AxisEventData eventData)
        {
            var delta = NavigationDelta(eventData.moveDir);
            if (delta != 0 && TryMoveActiveLink(delta))
            {
                navigationFocused = true;
                RefreshFocusIndicator();
                eventData.Use();
                return;
            }

            base.OnMove(eventData);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            var target = TargetTextBox();
            if (target != null && hasActiveLink &&
                target.ActivateLink(activeContentVersion, activeOccurrence))
            {
                eventData.Use();
            }
        }

        public void OnCancel(BaseEventData eventData)
        {
            navigationFocused = false;
            RefreshFocusIndicator();
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            eventData.Use();
        }

        private static int NavigationDelta(MoveDirection direction)
        {
            switch (direction)
            {
                case MoveDirection.Left:
                case MoveDirection.Up:
                    return -1;
                case MoveDirection.Right:
                case MoveDirection.Down:
                    return 1;
                default:
                    return 0;
            }
        }

        private void EnsureActiveLink()
        {
            var target = TargetTextBox();
            if (target == null ||
                !target.TryGetLinkInteractionSnapshot(out var content, out var geometry, out _))
            {
                return;
            }

            if (hasActiveLink &&
                activeContentVersion == content.ContentVersion &&
                HasVisibleGeometry(geometry, activeOccurrence))
            {
                target.EnsureLinkVisible(activeContentVersion, activeOccurrence);
                return;
            }

            foreach (var candidate in geometry.Geometry)
            {
                if (candidate.Rects.Count > 0 && content.TryGetLink(candidate.Occurrence, out _))
                {
                    SetActiveLink(content.ContentVersion, candidate.Occurrence);
                    target.EnsureLinkVisible(activeContentVersion, activeOccurrence);
                    return;
                }
            }
        }

        private bool TryMoveActiveLink(int delta)
        {
            var target = TargetTextBox();
            if (target == null ||
                !target.TryGetLinkInteractionSnapshot(out var content, out var geometry, out _))
            {
                return false;
            }

            var eligible = new List<int>();
            foreach (var candidate in geometry.Geometry)
            {
                if (candidate.Rects.Count > 0 && content.TryGetLink(candidate.Occurrence, out _))
                {
                    eligible.Add(candidate.Occurrence);
                }
            }

            if (eligible.Count == 0)
            {
                return false;
            }

            var currentIndex = -1;
            if (hasActiveLink && activeContentVersion == content.ContentVersion)
            {
                currentIndex = eligible.IndexOf(activeOccurrence);
            }

            if (currentIndex < 0)
            {
                var initialIndex = delta > 0 ? 0 : eligible.Count - 1;
                SetActiveLink(content.ContentVersion, eligible[initialIndex]);
                target.EnsureLinkVisible(activeContentVersion, activeOccurrence);
                return true;
            }

            var nextIndex = currentIndex + delta;
            if (nextIndex < 0 || nextIndex >= eligible.Count)
            {
                return false;
            }

            SetActiveLink(content.ContentVersion, eligible[nextIndex]);
            target.EnsureLinkVisible(activeContentVersion, activeOccurrence);
            return true;
        }

        private static bool HasVisibleGeometry(TextBoxLinkGeometrySnapshot snapshot, int occurrence)
        {
            return snapshot.TryGetGeometry(occurrence, out var geometry) && geometry.Rects.Count > 0;
        }

        private void SetActiveLink(long contentVersion, int occurrence)
        {
            hasActiveLink = true;
            activeContentVersion = contentVersion;
            activeOccurrence = occurrence;
        }

        private TextBox? TargetTextBox()
        {
            if (textBox == null)
            {
                textBox = GetComponent<TextBox>();
            }

            return textBox;
        }

        private void RefreshFocusIndicator()
        {
            var target = TargetTextBox();
            var visible = target != null &&
                          (navigationFocused || accessibilityFocused) &&
                          hasActiveLink &&
                          target.TryGetLinkLocalRects(activeContentVersion, activeOccurrence, focusRects);
            if (!visible)
            {
                ClearFocusIndicator();
                return;
            }

            EnsureFocusIndicator(target!);
            focusIndicator!.color = m_focusColor;
            focusIndicator.Thickness = m_focusThickness;
            focusIndicator.SetRects(focusRects);
            focusIndicator.enabled = true;
        }

        private void EnsureFocusIndicator(TextBox target)
        {
            if (focusIndicator != null)
            {
                return;
            }

            var indicatorObject = new GameObject("Milestro link focus",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextBoxLinkFocusIndicator));
            indicatorObject.hideFlags = HideFlags.HideAndDontSave;
            var indicatorTransform = indicatorObject.GetComponent<RectTransform>();
            indicatorTransform.SetParent(target.rectTransform, false);
            indicatorTransform.anchorMin = Vector2.zero;
            indicatorTransform.anchorMax = Vector2.one;
            indicatorTransform.pivot = target.rectTransform.pivot;
            indicatorTransform.offsetMin = Vector2.zero;
            indicatorTransform.offsetMax = Vector2.zero;
            indicatorTransform.SetAsLastSibling();
            focusIndicator = indicatorObject.GetComponent<TextBoxLinkFocusIndicator>();
            focusIndicator.raycastTarget = false;
        }

        private void ClearFocusIndicator()
        {
            focusRects.Clear();
            if (focusIndicator != null)
            {
                focusIndicator.SetRects(focusRects);
                focusIndicator.enabled = false;
            }
        }

        private Rect GetTextBoxScreenRect()
        {
            var target = TargetTextBox();
            return target == null ? default : LocalRectToScreen(target, target.GetPixelAdjustedRect());
        }

        private Rect GetLinkScreenRect(long contentVersion, int occurrence)
        {
            var target = TargetTextBox();
            if (target == null ||
                !target.TryGetLinkLocalRects(contentVersion, occurrence, screenRects) ||
                screenRects.Count == 0)
            {
                return default;
            }

            var result = LocalRectToScreen(target, screenRects[0]);
            for (var index = 1; index < screenRects.Count; ++index)
            {
                var next = LocalRectToScreen(target, screenRects[index]);
                result = Rect.MinMaxRect(Mathf.Min(result.xMin, next.xMin),
                    Mathf.Min(result.yMin, next.yMin),
                    Mathf.Max(result.xMax, next.xMax),
                    Mathf.Max(result.yMax, next.yMax));
            }

            return result;
        }

        private static Rect LocalRectToScreen(TextBox target, Rect localRect)
        {
            var canvas = target.canvas;
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            var worldMin = target.rectTransform.TransformPoint(new Vector3(localRect.xMin, localRect.yMin));
            var worldMax = target.rectTransform.TransformPoint(new Vector3(localRect.xMax, localRect.yMax));
            var screenMin = RectTransformUtility.WorldToScreenPoint(camera, worldMin);
            var screenMax = RectTransformUtility.WorldToScreenPoint(camera, worldMax);
            return Rect.MinMaxRect(Mathf.Min(screenMin.x, screenMax.x),
                Mathf.Min(screenMin.y, screenMax.y),
                Mathf.Max(screenMin.x, screenMax.x),
                Mathf.Max(screenMin.y, screenMax.y));
        }

        private void OnAccessibilityFocus(long contentVersion, int occurrence, bool focused)
        {
            accessibilityFocused = focused;
            if (focused)
            {
                SetActiveLink(contentVersion, occurrence);
                TargetTextBox()?.EnsureLinkVisible(contentVersion, occurrence);
            }

            RefreshFocusIndicator();
        }

#if UNITY_6000_3_OR_NEWER && MILESTRO_UNITY_ACCESSIBILITY
        private sealed class UnityAccessibilityAdapter : IDisposable
        {
            private readonly TextBoxAccessibility owner;
            private readonly List<LinkNode> linkNodes = new List<LinkNode>();
            private AccessibilityHierarchy? hierarchy;
            private AccessibilityNode? container;
            private long contentVersion;

            internal UnityAccessibilityAdapter(TextBoxAccessibility owner)
            {
                this.owner = owner;
            }

            internal void Sync(TextBoxContentSnapshot content, TextBoxLinkGeometrySnapshot? geometry)
            {
                EnsureHierarchy();
                if (hierarchy == null)
                {
                    return;
                }

                if (container == null || contentVersion != content.ContentVersion)
                {
                    Rebuild(content);
                }

                var geometryValue = geometry.GetValueOrDefault();
                var hasGeometry = geometry.HasValue && geometryValue.ContentVersion == content.ContentVersion;
                foreach (var linkNode in linkNodes)
                {
                    linkNode.Node.isActive = hasGeometry &&
                                             geometryValue.TryGetGeometry(linkNode.Occurrence, out var linkGeometry) &&
                                             linkGeometry.Rects.Count > 0;
                }
            }

            internal void Clear()
            {
                if (container != null && hierarchy != null)
                {
                    hierarchy.RemoveNode(container, true);
                }

                container = null;
                linkNodes.Clear();
                contentVersion = 0;
            }

            public void Dispose()
            {
                Clear();
                hierarchy = null;
            }

            private void EnsureHierarchy()
            {
                if (hierarchy != null && AssistiveSupport.activeHierarchy == hierarchy)
                {
                    return;
                }

                Clear();
                hierarchy = AssistiveSupport.activeHierarchy;
                if (hierarchy == null)
                {
                    hierarchy = new AccessibilityHierarchy();
                    AssistiveSupport.activeHierarchy = hierarchy;
                }
            }

            private void Rebuild(TextBoxContentSnapshot content)
            {
                Clear();
                contentVersion = content.ContentVersion;
                if (string.IsNullOrEmpty(content.PlainText) && content.Links.Count == 0)
                {
                    return;
                }

                container = hierarchy!.AddNode("", null);
                container.role = AccessibilityRole.Container;
                container.frameGetter = owner.GetTextBoxScreenRect;

                var cursor = 0;
                foreach (var link in content.Links)
                {
                    if (link.StartUtf16 < cursor ||
                        link.EndUtf16 < link.StartUtf16 ||
                        link.EndUtf16 > content.PlainText.Length)
                    {
                        continue;
                    }

                    AddStaticText(content.PlainText.Substring(cursor, link.StartUtf16 - cursor));
                    AddLink(content, link);
                    cursor = link.EndUtf16;
                }

                if (cursor < content.PlainText.Length)
                {
                    AddStaticText(content.PlainText.Substring(cursor));
                }
            }

            private void AddStaticText(string text)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    return;
                }

                var node = hierarchy!.AddNode(text, container);
                node.role = AccessibilityRole.StaticText;
                node.frameGetter = owner.GetTextBoxScreenRect;
            }

            private void AddLink(TextBoxContentSnapshot content, RichTextParser.LinkAnnotation link)
            {
                var label = content.GetLinkLabel(link);
                if (string.IsNullOrEmpty(label))
                {
                    label = !string.IsNullOrEmpty(link.Id) ? link.Id : link.Href;
                }

                var version = content.ContentVersion;
                var occurrence = link.Occurrence;
                var node = hierarchy!.AddNode(label, container);
                node.role = AccessibilityRole.Button;
                node.hint = owner.m_linkHint;
                node.frameGetter = () => owner.GetLinkScreenRect(version, occurrence);
                node.invoked += () => owner.TargetTextBox()?.ActivateLink(version, occurrence) == true;
                node.focusChanged += (_, focused) => owner.OnAccessibilityFocus(version, occurrence, focused);
                linkNodes.Add(new LinkNode(version, occurrence, node));
            }

            private sealed class LinkNode
            {
                internal LinkNode(long contentVersion, int occurrence, AccessibilityNode node)
                {
                    ContentVersion = contentVersion;
                    Occurrence = occurrence;
                    Node = node;
                }

                internal long ContentVersion { get; }
                internal int Occurrence { get; }
                internal AccessibilityNode Node { get; }
            }
        }
#endif
    }
}
