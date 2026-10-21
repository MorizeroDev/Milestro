using System;
using System.Collections.Generic;
using Milestro.Components.Internal;
using Milestro.Events;
using Milestro.Util;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Milestro.Components
{
    public partial class TextBox
    {
        [SerializeField]
        private LinkHoverEvent? m_OnLinkHoverChanged = new LinkHoverEvent();

        [SerializeField] private Color m_linkHoverColor = new Color(0.2f, 0.49f, 1f, 0.08f);
        [SerializeField] private Color m_linkHoverUnderlineColor = new Color(0.2f, 0.49f, 1f, 0.9f);
        [SerializeField][Min(0f)] private float m_linkHoverUnderlineThickness = 1f;
        [SerializeField] private bool m_linkHoverCursorEnabled = true;
        [SerializeField] private Texture2D? m_linkHoverCursor;
        [SerializeField] private Vector2 m_linkHoverCursorHotspot;
        [SerializeField] private CursorMode m_linkHoverCursorMode = CursorMode.Auto;

        private static TextBox? linkHoverCursorOwner;

        [NonSerialized] private PointerEventData? linkHoverPointer;
        [NonSerialized] private TextBoxLinkHit hoveredLink;
        [NonSerialized] private bool hasHoveredLink;
        [NonSerialized] private long hoveredContentVersion;
        [NonSerialized] private ulong linkHoverRevision;
        [NonSerialized] private TextBoxLinkHoverIndicator? linkHoverIndicator;
        [NonSerialized] private readonly List<Rect> linkHoverRects = new List<Rect>();
        [NonSerialized] private Texture2D? defaultLinkHoverCursor;
        [NonSerialized] private Texture2D? appliedLinkHoverCursor;
        [NonSerialized] private Vector2 appliedLinkHoverCursorHotspot;
        [NonSerialized] private CursorMode appliedLinkHoverCursorMode;

        public LinkHoverEvent onLinkHoverChanged => m_OnLinkHoverChanged ??= new LinkHoverEvent();

        public Color linkHoverColor
        {
            get => m_linkHoverColor;
            set
            {
                m_linkHoverColor = value;
                RefreshLinkHover();
            }
        }

        public Color linkHoverUnderlineColor
        {
            get => m_linkHoverUnderlineColor;
            set
            {
                m_linkHoverUnderlineColor = value;
                RefreshLinkHover();
            }
        }

        public float linkHoverUnderlineThickness
        {
            get => m_linkHoverUnderlineThickness;
            set
            {
                m_linkHoverUnderlineThickness = NormalizeLinkHoverThickness(value);
                RefreshLinkHover();
            }
        }

        public bool linkHoverCursorEnabled
        {
            get => m_linkHoverCursorEnabled;
            set
            {
                m_linkHoverCursorEnabled = value;
                RefreshLinkHover();
            }
        }

        public Texture2D? linkHoverCursor
        {
            get => m_linkHoverCursor;
            set
            {
                m_linkHoverCursor = value;
                RefreshLinkHover();
            }
        }

        public Vector2 linkHoverCursorHotspot
        {
            get => m_linkHoverCursorHotspot;
            set
            {
                m_linkHoverCursorHotspot = NormalizeCursorHotspot(value);
                RefreshLinkHover();
            }
        }

        public CursorMode linkHoverCursorMode
        {
            get => m_linkHoverCursorMode;
            set
            {
                m_linkHoverCursorMode = value;
                RefreshLinkHover();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            OnPointerMove(eventData);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (eventData == null || !isActiveAndEnabled || !AcceptsLinkPointer(eventData))
            {
                return;
            }

            linkHoverPointer = eventData;
            RefreshLinkHover();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (eventData == null || linkHoverPointer == null ||
                eventData.pointerId != linkHoverPointer.pointerId)
            {
                return;
            }

            ResetLinkHover(notify: true);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
            {
                ResetLinkHover(notify: true);
            }
        }

        private void RefreshLinkHover()
        {
            ++linkHoverRevision;
            var pointer = linkHoverPointer;
            if (pointer == null)
            {
                return;
            }

            var raycastObject = pointer.pointerCurrentRaycast.gameObject;
            if (!isActiveAndEnabled || !raycastTarget ||
                (raycastObject != null && !raycastObject.transform.IsChildOf(transform)) ||
                !TryScreenPointToNormalizedViewport(pointer.position,
                    pointer.enterEventCamera,
                    out var normalizedPoint) ||
                !Raycast(pointer.position, pointer.enterEventCamera))
            {
                ClearHoveredLink(notify: true);
                return;
            }

            if (!TryGetLinkInteractionSnapshot(out var content, out var geometry, out var generation))
            {
                HideLinkHoverFeedback();
                if (hasHoveredLink && TryGetContentSnapshot(out content) &&
                    content.ContentVersion != hoveredContentVersion)
                {
                    ClearHoveredLink(notify: true);
                }

                return;
            }

            if (!ProducerComponent().TryHitLink(normalizedPoint, out var hit) ||
                hit.Generation != generation ||
                !TryGetLinkLocalRects(geometry, hit.Occurrence, linkHoverRects))
            {
                ClearHoveredLink(notify: true);
                return;
            }

            if (hasHoveredLink && hoveredContentVersion == content.ContentVersion &&
                hoveredLink.Occurrence == hit.Occurrence)
            {
                hoveredLink = hit;
                ShowLinkHoverFeedback();
                return;
            }

            var revision = ClearHoveredLink(notify: true);
            if (!this || !isActiveAndEnabled || linkHoverRevision != revision ||
                linkHoverPointer != pointer ||
                !TryGetLinkInteractionSnapshot(out content, out geometry, out generation) ||
                generation != hit.Generation ||
                !TryGetLinkLocalRects(geometry, hit.Occurrence, linkHoverRects))
            {
                return;
            }

            hoveredLink = hit;
            hoveredContentVersion = content.ContentVersion;
            hasHoveredLink = true;
            ShowLinkHoverFeedback();
            onLinkHoverChanged.Invoke(new LinkHoverEventArgs(hit.Href, hit.Id, hovered: true));
        }

        private void ShowLinkHoverFeedback()
        {
            var showIndicator = m_linkHoverColor.a > 0f ||
                                (m_linkHoverUnderlineColor.a > 0f && m_linkHoverUnderlineThickness > 0f);
            if (showIndicator)
            {
                EnsureLinkHoverIndicator();
                linkHoverIndicator!.SetPresentation(linkHoverRects,
                    m_linkHoverColor,
                    m_linkHoverUnderlineColor,
                    m_linkHoverUnderlineThickness);
                linkHoverIndicator.enabled = true;
            }
            else if (linkHoverIndicator != null)
            {
                linkHoverIndicator.enabled = false;
            }

            RefreshLinkHoverCursor();
        }

        private void EnsureLinkHoverIndicator()
        {
            if (linkHoverIndicator != null)
            {
                return;
            }

            var indicatorObject = new GameObject("Milestro link hover",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextBoxLinkHoverIndicator));
            indicatorObject.hideFlags = HideFlags.HideAndDontSave;
            var indicatorTransform = indicatorObject.GetComponent<RectTransform>();
            indicatorTransform.SetParent(rectTransform, false);
            indicatorTransform.anchorMin = Vector2.zero;
            indicatorTransform.anchorMax = Vector2.one;
            indicatorTransform.pivot = rectTransform.pivot;
            indicatorTransform.offsetMin = Vector2.zero;
            indicatorTransform.offsetMax = Vector2.zero;
            indicatorTransform.SetAsFirstSibling();
            linkHoverIndicator = indicatorObject.GetComponent<TextBoxLinkHoverIndicator>();
            linkHoverIndicator.raycastTarget = false;
        }

        private void HideLinkHoverFeedback()
        {
            if (linkHoverIndicator != null)
            {
                linkHoverIndicator.enabled = false;
            }

            ClearLinkHoverCursor();
        }

        private void RefreshLinkHoverCursor()
        {
            if (!m_linkHoverCursorEnabled)
            {
                ClearLinkHoverCursor();
                return;
            }

            var cursor = m_linkHoverCursor;
            var hotspot = m_linkHoverCursorHotspot;
            if (cursor == null)
            {
                if (defaultLinkHoverCursor == null)
                {
                    defaultLinkHoverCursor = TextBoxLinkHoverCursor.Create();
                }

                cursor = defaultLinkHoverCursor;
                hotspot = TextBoxLinkHoverCursor.Hotspot;
            }

            hotspot = new Vector2(Mathf.Clamp(hotspot.x, 0f, cursor.width - 1f),
                Mathf.Clamp(hotspot.y, 0f, cursor.height - 1f));
            if (linkHoverCursorOwner == this && appliedLinkHoverCursor == cursor &&
                appliedLinkHoverCursorHotspot == hotspot && appliedLinkHoverCursorMode == m_linkHoverCursorMode)
            {
                return;
            }

            Cursor.SetCursor(cursor, hotspot, m_linkHoverCursorMode);
            linkHoverCursorOwner = this;
            appliedLinkHoverCursor = cursor;
            appliedLinkHoverCursorHotspot = hotspot;
            appliedLinkHoverCursorMode = m_linkHoverCursorMode;
        }

        private void ClearLinkHoverCursor()
        {
            if (linkHoverCursorOwner != this)
            {
                return;
            }

            linkHoverCursorOwner = null;
            appliedLinkHoverCursor = null;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        private ulong ClearHoveredLink(bool notify)
        {
            var revision = ++linkHoverRevision;
            var wasHovered = hasHoveredLink;
            var previous = hoveredLink;
            hasHoveredLink = false;
            hoveredLink = default;
            hoveredContentVersion = 0;
            linkHoverRects.Clear();
            HideLinkHoverFeedback();
            if (notify && wasHovered)
            {
                onLinkHoverChanged.Invoke(new LinkHoverEventArgs(previous.Href, previous.Id, hovered: false));
            }

            return revision;
        }

        private void ResetLinkHover(bool notify)
        {
            linkHoverPointer = null;
            ClearHoveredLink(notify);
        }

        private void DestroyLinkHoverFeedback()
        {
            ResetLinkHover(notify: false);
            if (linkHoverIndicator != null)
            {
                DestroyImmediate(linkHoverIndicator.gameObject);
                linkHoverIndicator = null;
            }

            if (defaultLinkHoverCursor != null)
            {
                DestroyImmediate(defaultLinkHoverCursor);
                defaultLinkHoverCursor = null;
            }
        }

        private void ValidateLinkHoverSettings()
        {
            m_linkHoverUnderlineThickness = NormalizeLinkHoverThickness(m_linkHoverUnderlineThickness);
            m_linkHoverCursorHotspot = NormalizeCursorHotspot(m_linkHoverCursorHotspot);
        }

        private static float NormalizeLinkHoverThickness(float value)
        {
            return FloatUtil.IsFinite(value) ? Mathf.Max(0f, value) : 0f;
        }

        private static Vector2 NormalizeCursorHotspot(Vector2 value)
        {
            return new Vector2(FloatUtil.IsFinite(value.x) ? Mathf.Max(0f, value.x) : 0f,
                FloatUtil.IsFinite(value.y) ? Mathf.Max(0f, value.y) : 0f);
        }
    }
}
