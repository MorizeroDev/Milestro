using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Milestro.Components.Internal;
using Milestro.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Milestro.Components
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Milestro/Dropdown")]
    public sealed class MilestroDropdown : Selectable,
        IPointerClickHandler,
        ISubmitHandler,
        ICancelHandler
    {
        private static readonly Dictionary<Canvas, MilestroDropdown> ExpandedByCanvas =
            new Dictionary<Canvas, MilestroDropdown>();

        [SerializeField] private CanvasSlimText? m_CaptionText;
        [SerializeField] private CanvasSlimText? m_PlaceholderText;
        [SerializeField] private RectTransform? m_Template;
        [SerializeField] private string m_Placeholder = string.Empty;
        [SerializeField, Min(1)] private int m_MaxVisibleItems = 8;
        [SerializeField] private List<string> m_Options = new List<string>();
        [SerializeField] private int m_Value = -1;
        [SerializeField] private MilestroDropdownChangedEvent? m_OnValueChanged = new();

        [NonSerialized] private ReadOnlyCollection<string>? readOnlyOptions;
        [NonSerialized] private readonly List<MilestroDropdownItem> items = new();
        [NonSerialized] private readonly List<CanvasGroup> canvasGroups = new();
        [NonSerialized] private readonly Vector3[] controlCorners = new Vector3[4];
        [NonSerialized] private GameObject? popupRoot;
        [NonSerialized] private Canvas? popupCanvas;
        [NonSerialized] private Canvas? rootCanvas;
        [NonSerialized] private RectTransform? popupRect;
        [NonSerialized] private ScrollRect? popupScrollRect;
        [NonSerialized] private RectTransform? popupContent;
        [NonSerialized] private float popupItemHeight;
        [NonSerialized] private int candidateIndex = -1;
        [NonSerialized] private int nextPopupId;
        [NonSerialized] private int activePopupId;
        [NonSerialized] private Matrix4x4 observedControlMatrix;
        [NonSerialized] private Matrix4x4 observedCanvasMatrix;
        [NonSerialized] private Rect observedSafeArea;
        [NonSerialized] private Rect observedCanvasPixelRect;

        public IReadOnlyList<string> options => readOnlyOptions ??= new ReadOnlyCollection<string>(m_Options);

        public int optionCount => m_Options.Count;

        public bool isExpanded => popupRoot != null;

        public MilestroDropdownChangedEvent onValueChanged => m_OnValueChanged ??= new();

        public CanvasSlimText? captionText
        {
            get => m_CaptionText;
            set
            {
                m_CaptionText = value;
                RefreshShownValue();
            }
        }

        public CanvasSlimText? placeholderText
        {
            get => m_PlaceholderText;
            set
            {
                m_PlaceholderText = value;
                RefreshShownValue();
            }
        }

        public RectTransform? template
        {
            get => m_Template;
            set
            {
                if (m_Template == value)
                {
                    return;
                }

                ClosePopup(restoreSelection: false);
                m_Template = value;
                if (m_Template != null)
                {
                    m_Template.gameObject.SetActive(false);
                }
            }
        }

        public string placeholder
        {
            get => m_Placeholder;
            set
            {
                var nextValue = value ?? string.Empty;
                if (m_Placeholder == nextValue)
                {
                    return;
                }

                m_Placeholder = nextValue;
                RefreshShownValue();
            }
        }

        public int maxVisibleItems
        {
            get => m_MaxVisibleItems;
            set
            {
                m_MaxVisibleItems = Mathf.Max(1, value);
                if (isExpanded)
                {
                    PositionPopup();
                    EnsureCandidateVisible();
                }
            }
        }

        public int value
        {
            get => m_Value;
            set => SetValue(value, notify: true);
        }

        protected override void Awake()
        {
            base.Awake();
            NormalizeSerializedState();
            if (m_Template != null)
            {
                m_Template.gameObject.SetActive(false);
            }

            RefreshShownValue();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            NormalizeSerializedState();
            RefreshShownValue();
        }
#endif

        protected override void OnDisable()
        {
            ClosePopup(restoreSelection: false);
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            ClosePopup(restoreSelection: false);
            base.OnDestroy();
        }

        protected override void OnCanvasGroupChanged()
        {
            base.OnCanvasGroupChanged();
            if (isExpanded && !HasInteractionPermission())
            {
                ClosePopup(restoreSelection: false);
            }
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            ClosePopup(restoreSelection: false);
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            ClosePopup(restoreSelection: false);
        }

        private void LateUpdate()
        {
            if (!isExpanded)
            {
                return;
            }

            if (!HasInteractionPermission() || rootCanvas == null || !rootCanvas.isActiveAndEnabled)
            {
                ClosePopup(restoreSelection: false);
                return;
            }

            if (((RectTransform)transform).localToWorldMatrix != observedControlMatrix ||
                rootCanvas.transform.localToWorldMatrix != observedCanvasMatrix ||
                Screen.safeArea != observedSafeArea ||
                rootCanvas.pixelRect != observedCanvasPixelRect)
            {
                ClosePopup(restoreSelection: false);
            }
        }

        public void SetOptions(IEnumerable<string>? newOptions, int initialIndex = 0)
        {
            var copiedOptions = new List<string>();
            if (newOptions != null)
            {
                foreach (var option in newOptions)
                {
                    copiedOptions.Add(option ?? string.Empty);
                }
            }

            ClosePopup(restoreSelection: true);
            m_Options.Clear();
            m_Options.AddRange(copiedOptions);
            m_Value = NormalizeIndex(initialIndex);
            candidateIndex = -1;
            RefreshShownValue();
        }

        public void ClearOptions()
        {
            SetOptions(null, -1);
        }

        public string GetOptionText(int index)
        {
            if (index < 0 || index >= m_Options.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return m_Options[index];
        }

        public void SetOptionText(int index, string? text)
        {
            if (index < 0 || index >= m_Options.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            m_Options[index] = text ?? string.Empty;
            if (index < items.Count && items[index] != null)
            {
                items[index].SetText(m_Options[index]);
            }

            if (index == m_Value)
            {
                RefreshShownValue();
            }
        }

        public void SetValueWithoutNotify(int newValue)
        {
            SetValue(newValue, notify: false);
        }

        public void Show()
        {
            if (isExpanded || m_Options.Count == 0 || m_Template == null || !HasInteractionPermission())
            {
                return;
            }

            var parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null)
            {
                return;
            }

            var targetRootCanvas = parentCanvas.rootCanvas;
            if (targetRootCanvas == null || targetRootCanvas.renderMode == RenderMode.WorldSpace)
            {
                return;
            }

            var templateScrollRect = m_Template.GetComponentInChildren<ScrollRect>(true);
            var templateItem = m_Template.GetComponentInChildren<MilestroDropdownItem>(true);
            if (templateScrollRect == null || templateScrollRect.viewport == null ||
                templateScrollRect.content == null || templateItem == null)
            {
                Debug.LogWarning("MilestroDropdown requires a template with a ScrollRect and an item prototype.", this);
                return;
            }

            if (ExpandedByCanvas.TryGetValue(targetRootCanvas, out var other) && other != null && other != this)
            {
                other.ClosePopup(restoreSelection: false);
            }

            activePopupId = ++nextPopupId;
            candidateIndex = m_Value >= 0 ? m_Value : 0;
            rootCanvas = targetRootCanvas;

            popupRoot = new GameObject("Milestro Dropdown Popup", typeof(RectTransform), typeof(Canvas));
            popupRoot.layer = targetRootCanvas.gameObject.layer;
            popupRoot.SetActive(false);
            popupRect = popupRoot.GetComponent<RectTransform>();
            popupRect.SetParent(targetRootCanvas.transform, false);
            SetFullStretch(popupRect);

            popupCanvas = popupRoot.GetComponent<Canvas>();
            popupCanvas.overrideSorting = true;
            popupCanvas.sortingLayerID = targetRootCanvas.sortingLayerID;
            popupCanvas.sortingOrder = Math.Min(targetRootCanvas.sortingOrder + 1000, short.MaxValue);
            popupRoot.AddComponent<GraphicRaycaster>();

            var blockerObject = new GameObject("Blocker", typeof(RectTransform), typeof(Image),
                typeof(MilestroDropdownBlocker));
            blockerObject.layer = targetRootCanvas.gameObject.layer;
            blockerObject.transform.SetParent(popupRoot.transform, false);
            SetFullStretch((RectTransform)blockerObject.transform);
            var blockerImage = blockerObject.GetComponent<Image>();
            blockerImage.color = Color.clear;
            blockerImage.raycastTarget = true;
            blockerObject.GetComponent<MilestroDropdownBlocker>().Configure(this, activePopupId);

            var listObject = Instantiate(m_Template.gameObject, popupRoot.transform, false);
            listObject.name = "Dropdown List";
            listObject.SetActive(false);
            var listRect = (RectTransform)listObject.transform;

            popupScrollRect = listObject.GetComponentInChildren<ScrollRect>(true);
            var itemPrototype = listObject.GetComponentInChildren<MilestroDropdownItem>(true);
            popupContent = popupScrollRect.content;
            popupItemHeight = Mathf.Max(1f, itemPrototype.RectTransform.rect.height);

            itemPrototype.gameObject.SetActive(false);
            items.Clear();
            for (var index = 0; index < m_Options.Count; ++index)
            {
                var item = Instantiate(itemPrototype, popupContent, false);
                item.name = "Option " + index;
                item.Configure(this, index, activePopupId);
                item.SetText(m_Options[index]);
                item.RectTransform.anchorMin = new Vector2(0f, 1f);
                item.RectTransform.anchorMax = new Vector2(1f, 1f);
                item.RectTransform.pivot = new Vector2(0.5f, 1f);
                item.RectTransform.sizeDelta = new Vector2(0f, popupItemHeight);
                item.RectTransform.anchoredPosition = new Vector2(0f, -index * popupItemHeight);
                item.gameObject.SetActive(true);
                items.Add(item);
            }

            popupContent.anchorMin = new Vector2(0f, 1f);
            popupContent.anchorMax = new Vector2(1f, 1f);
            popupContent.pivot = new Vector2(0.5f, 1f);
            popupContent.sizeDelta = new Vector2(0f, popupItemHeight * items.Count);
            popupContent.anchoredPosition = Vector2.zero;

            PositionPopup();
            RefreshItems();
            popupRoot.SetActive(true);
            listObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            popupScrollRect.verticalNormalizedPosition = 1f;
            EnsureCandidateVisible();

            ExpandedByCanvas[targetRootCanvas] = this;
            observedControlMatrix = ((RectTransform)transform).localToWorldMatrix;
            observedCanvasMatrix = targetRootCanvas.transform.localToWorldMatrix;
            observedSafeArea = Screen.safeArea;
            observedCanvasPixelRect = targetRootCanvas.pixelRect;
        }

        public void Hide()
        {
            ClosePopup(restoreSelection: true);
        }

        public override void OnMove(AxisEventData eventData)
        {
            if (!isExpanded)
            {
                base.OnMove(eventData);
                return;
            }

            if (eventData.moveDir == MoveDirection.Up)
            {
                MoveCandidate(-1);
            }
            else if (eventData.moveDir == MoveDirection.Down)
            {
                MoveCandidate(1);
            }

            eventData.Use();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (!HasInteractionPermission())
            {
                return;
            }

            if (isExpanded)
            {
                CommitCandidate(activePopupId);
            }
            else
            {
                Show();
            }

            eventData.Use();
        }

        public void OnCancel(BaseEventData eventData)
        {
            if (!isExpanded)
            {
                return;
            }

            ClosePopup(restoreSelection: true);
            eventData.Use();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !HasInteractionPermission())
            {
                return;
            }

            if (isExpanded)
            {
                ClosePopup(restoreSelection: true);
            }
            else
            {
                Show();
            }

            eventData.Use();
        }

        internal void HighlightItem(int popupId, int index)
        {
            if (!isExpanded || popupId != activePopupId)
            {
                return;
            }

            SetCandidate(index, ensureVisible: false);
        }

        internal void SelectItem(int popupId, int index)
        {
            if (!isExpanded || popupId != activePopupId)
            {
                return;
            }

            candidateIndex = Mathf.Clamp(index, 0, m_Options.Count - 1);
            CommitCandidate(popupId);
        }

        internal void CloseFromBlocker(int popupId)
        {
            if (isExpanded && popupId == activePopupId)
            {
                ClosePopup(restoreSelection: true);
            }
        }

        private void CommitCandidate(int popupId)
        {
            if (!isExpanded || popupId != activePopupId || candidateIndex < 0 ||
                candidateIndex >= m_Options.Count)
            {
                return;
            }

            var selectedIndex = candidateIndex;
            ClosePopup(restoreSelection: true);
            SetValue(selectedIndex, notify: true);
        }

        private void SetValue(int newValue, bool notify)
        {
            var normalizedValue = NormalizeIndex(newValue);
            if (normalizedValue == m_Value)
            {
                return;
            }

            m_Value = normalizedValue;
            RefreshShownValue();
            if (isExpanded)
            {
                SetCandidate(m_Value >= 0 ? m_Value : 0, ensureVisible: true);
            }

            if (notify)
            {
                onValueChanged.Invoke(m_Value);
            }
        }

        private int NormalizeIndex(int index)
        {
            if (m_Options.Count == 0 || index < 0)
            {
                return -1;
            }

            return index >= m_Options.Count ? m_Options.Count - 1 : index;
        }

        private void NormalizeSerializedState()
        {
            m_MaxVisibleItems = Mathf.Max(1, m_MaxVisibleItems);
            m_Placeholder ??= string.Empty;
            m_Options ??= new List<string>();
            for (var index = 0; index < m_Options.Count; ++index)
            {
                m_Options[index] ??= string.Empty;
            }

            m_Value = NormalizeIndex(m_Value);
        }

        private void RefreshShownValue()
        {
            var hasValue = m_Value >= 0 && m_Value < m_Options.Count;
            if (m_CaptionText != null)
            {
                m_CaptionText.gameObject.SetActive(hasValue);
                m_CaptionText.Text = hasValue ? m_Options[m_Value] : string.Empty;
            }

            if (m_PlaceholderText != null)
            {
                m_PlaceholderText.gameObject.SetActive(!hasValue);
                m_PlaceholderText.Text = hasValue ? string.Empty : m_Placeholder;
            }
        }

        private void MoveCandidate(int delta)
        {
            if (m_Options.Count == 0)
            {
                return;
            }

            SetCandidate(candidateIndex + delta, ensureVisible: true);
        }

        private void SetCandidate(int index, bool ensureVisible)
        {
            var nextCandidate = Mathf.Clamp(index, 0, m_Options.Count - 1);
            if (nextCandidate == candidateIndex)
            {
                if (ensureVisible)
                {
                    EnsureCandidateVisible();
                }

                return;
            }

            candidateIndex = nextCandidate;
            RefreshItems();
            if (ensureVisible)
            {
                EnsureCandidateVisible();
            }
        }

        private void RefreshItems()
        {
            for (var index = 0; index < items.Count; ++index)
            {
                items[index].SetVisual(index == m_Value, index == candidateIndex);
            }
        }

        private void EnsureCandidateVisible()
        {
            if (popupScrollRect == null || popupContent == null || candidateIndex < 0)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            var viewportHeight = popupScrollRect.viewport.rect.height;
            var contentHeight = popupItemHeight * items.Count;
            var hiddenHeight = Mathf.Max(0f, contentHeight - viewportHeight);
            if (hiddenHeight <= 0.001f || viewportHeight <= 0f)
            {
                popupScrollRect.verticalNormalizedPosition = 1f;
                return;
            }

            var scrollTop = (1f - popupScrollRect.verticalNormalizedPosition) * hiddenHeight;
            var itemTop = candidateIndex * popupItemHeight;
            var itemBottom = itemTop + popupItemHeight;
            if (itemTop < scrollTop)
            {
                scrollTop = itemTop;
            }
            else if (itemBottom > scrollTop + viewportHeight)
            {
                scrollTop = itemBottom - viewportHeight;
            }

            scrollTop = Mathf.Clamp(scrollTop, 0f, hiddenHeight);
            popupScrollRect.verticalNormalizedPosition = 1f - scrollTop / hiddenHeight;
        }

        private void PositionPopup()
        {
            if (popupRect == null || popupCanvas == null || rootCanvas == null || m_Template == null)
            {
                return;
            }

            var rootRect = rootCanvas.transform as RectTransform;
            var listRect = popupScrollRect != null ? popupScrollRect.transform as RectTransform : null;
            if (rootRect == null || listRect == null)
            {
                ClosePopup(restoreSelection: false);
                return;
            }

            var displayRect = ResolveDisplayRect(rootCanvas, rootRect);
            if (displayRect.width <= 0f || displayRect.height <= 0f)
            {
                ClosePopup(restoreSelection: false);
                return;
            }

            ((RectTransform)transform).GetWorldCorners(controlCorners);
            var left = float.PositiveInfinity;
            var right = float.NegativeInfinity;
            var bottom = float.PositiveInfinity;
            var top = float.NegativeInfinity;
            for (var index = 0; index < controlCorners.Length; ++index)
            {
                var point = (Vector2)rootRect.InverseTransformPoint(controlCorners[index]);
                left = Mathf.Min(left, point.x);
                right = Mathf.Max(right, point.x);
                bottom = Mathf.Min(bottom, point.y);
                top = Mathf.Max(top, point.y);
            }

            const float gap = 2f;
            const float verticalChrome = 4f;
            var contentHeight = popupItemHeight * items.Count;
            var desiredWidth = m_Template.rect.width > 0f ? m_Template.rect.width : right - left;
            var width = Mathf.Min(desiredWidth, displayRect.width);
            var maximumHeight = Mathf.Min(m_Template.rect.height,
                m_MaxVisibleItems * popupItemHeight + verticalChrome);
            var desiredHeight = Mathf.Min(contentHeight + verticalChrome, maximumHeight);
            var downSpace = Mathf.Max(0f, bottom - displayRect.yMin - gap);
            var upSpace = Mathf.Max(0f, displayRect.yMax - top - gap);
            var openUp = downSpace < desiredHeight && upSpace > downSpace;
            var height = Mathf.Min(desiredHeight, openUp ? upSpace : downSpace);
            if (height <= 0f)
            {
                openUp = upSpace >= downSpace;
                height = Mathf.Min(desiredHeight, Mathf.Max(upSpace, downSpace));
            }

            if (height <= 0f)
            {
                ClosePopup(restoreSelection: false);
                return;
            }

            var xMin = Mathf.Clamp((left + right) * 0.5f - width * 0.5f,
                displayRect.xMin, displayRect.xMax - width);
            var yMin = openUp ? top + gap : bottom - gap - height;
            yMin = Mathf.Clamp(yMin, displayRect.yMin, displayRect.yMax - height);

            listRect.anchorMin = Vector2.zero;
            listRect.anchorMax = Vector2.zero;
            listRect.pivot = Vector2.zero;
            listRect.sizeDelta = new Vector2(width, height);
            listRect.anchoredPosition = new Vector2(xMin - rootRect.rect.xMin, yMin - rootRect.rect.yMin);
        }

        private static Rect ResolveDisplayRect(Canvas canvas, RectTransform canvasRect)
        {
            var displayRect = canvasRect.rect;
            var eventCamera = canvas.renderMode == RenderMode.ScreenSpaceCamera ? canvas.worldCamera : null;
            var safeArea = Screen.safeArea;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, safeArea.min,
                    eventCamera, out var safeMin) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, safeArea.max,
                    eventCamera, out var safeMax))
            {
                return displayRect;
            }

            var safeRect = Rect.MinMaxRect(Mathf.Min(safeMin.x, safeMax.x), Mathf.Min(safeMin.y, safeMax.y),
                Mathf.Max(safeMin.x, safeMax.x), Mathf.Max(safeMin.y, safeMax.y));
            var xMin = Mathf.Max(displayRect.xMin, safeRect.xMin);
            var yMin = Mathf.Max(displayRect.yMin, safeRect.yMin);
            var xMax = Mathf.Min(displayRect.xMax, safeRect.xMax);
            var yMax = Mathf.Min(displayRect.yMax, safeRect.yMax);
            return xMax > xMin && yMax > yMin
                ? Rect.MinMaxRect(xMin, yMin, xMax, yMax)
                : new Rect(xMin, yMin, 0f, 0f);
        }

        private bool HasInteractionPermission()
        {
            if (!IsActive() || !IsInteractable())
            {
                return false;
            }

            GetComponentsInParent(false, canvasGroups);
            for (var index = 0; index < canvasGroups.Count; ++index)
            {
                var group = canvasGroups[index];
                if (!group.blocksRaycasts)
                {
                    return false;
                }

                if (group.ignoreParentGroups)
                {
                    break;
                }
            }

            return true;
        }

        private void ClosePopup(bool restoreSelection)
        {
            UnregisterPopup();
            if (!isExpanded)
            {
                return;
            }

            var eventSystem = EventSystem.current;
            var selectedObject = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            var ownsFocus = selectedObject == null || selectedObject == gameObject ||
                            (selectedObject != null && popupRoot != null &&
                             selectedObject.transform.IsChildOf(popupRoot.transform));
            var closingPopup = popupRoot;

            activePopupId = 0;
            popupRoot = null;
            popupCanvas = null;
            rootCanvas = null;
            popupRect = null;
            popupScrollRect = null;
            popupContent = null;
            candidateIndex = -1;
            items.Clear();

            if (closingPopup != null)
            {
                closingPopup.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(closingPopup);
                }
                else
                {
                    DestroyImmediate(closingPopup);
                }
            }

            if (restoreSelection && ownsFocus && eventSystem != null && selectedObject != gameObject &&
                HasInteractionPermission())
            {
                eventSystem.SetSelectedGameObject(gameObject);
            }
        }

        private void UnregisterPopup()
        {
            Canvas? registeredCanvas = null;
            var found = false;
            foreach (var pair in ExpandedByCanvas)
            {
                if (!ReferenceEquals(pair.Value, this))
                {
                    continue;
                }

                registeredCanvas = pair.Key;
                found = true;
                break;
            }

            if (found)
            {
                ExpandedByCanvas.Remove(registeredCanvas!);
            }
        }

        private static void SetFullStretch(RectTransform target)
        {
            target.anchorMin = Vector2.zero;
            target.anchorMax = Vector2.one;
            target.offsetMin = Vector2.zero;
            target.offsetMax = Vector2.zero;
        }
    }
}
