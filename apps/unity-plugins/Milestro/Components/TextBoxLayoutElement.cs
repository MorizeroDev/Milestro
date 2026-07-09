using System;
using Milestro.Components.Internal;
using Milestro.Util;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Milestro.Components
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(TextBox))]
    [AddComponentMenu("Milestro/Text Box Layout Element")]
    public class TextBoxLayoutElement : UIBehaviour, ILayoutElement
    {
        [SerializeField] private bool m_providePreferredWidth;
        [SerializeField] private bool m_providePreferredHeight = true;
        [SerializeField] private bool m_forwardPointerScrollToParent = true;
        [SerializeField] private int m_layoutPriority = 1;
        [SerializeField] private RectTransform? m_viewport;
#if MILESTRO_FLOW_TEXTBOX_DEBUG
        [SerializeField] private bool m_debugFlowVisibleRange;
#endif

        [NonSerialized] private TextBox? textBoxCache;
        [NonSerialized] private bool subscribed;
        [NonSerialized] private bool hasCachedMeasurement;
        [NonSerialized] private TextBoxLayoutMeasurement cachedMeasurement;
        [NonSerialized] private ScrollRect? scrollRectCache;
        [NonSerialized] private readonly Vector3[] worldCorners = new Vector3[4];

        public bool providePreferredWidth
        {
            get => m_providePreferredWidth;
            set
            {
                if (m_providePreferredWidth == value)
                {
                    return;
                }

                m_providePreferredWidth = value;
                SetLayoutDirty();
            }
        }

        public bool providePreferredHeight
        {
            get => m_providePreferredHeight;
            set
            {
                if (m_providePreferredHeight == value)
                {
                    return;
                }

                m_providePreferredHeight = value;
                SetLayoutDirty();
            }
        }

        public bool forwardPointerScrollToParent
        {
            get => m_forwardPointerScrollToParent;
            set => m_forwardPointerScrollToParent = value;
        }

        public int layoutPriority
        {
            get => m_layoutPriority;
            set
            {
                if (m_layoutPriority == value)
                {
                    return;
                }

                m_layoutPriority = value;
                SetLayoutDirty();
            }
        }

#if MILESTRO_FLOW_TEXTBOX_DEBUG
        public bool debugFlowVisibleRange
        {
            get => m_debugFlowVisibleRange;
            set
            {
                if (m_debugFlowVisibleRange == value)
                {
                    return;
                }

                m_debugFlowVisibleRange = value;
                TextBoxComponent()?.SetFlowDiagnosticsEnabled(value);
            }
        }
#endif

        public float minWidth => -1f;
        public float preferredWidth => m_providePreferredWidth &&
                                       TryGetMeasurement(out var measurement) &&
                                       measurement.HasContentPreferredWidth
            ? measurement.PreferredWidth
            : -1f;
        public float flexibleWidth => -1f;
        public float minHeight => -1f;
        public float preferredHeight => m_providePreferredHeight && TryGetMeasurement(out var measurement)
            ? measurement.PreferredHeight
            : -1f;
        public float flexibleHeight => -1f;

        protected override void OnEnable()
        {
            base.OnEnable();
            Subscribe();
            var textBox = TextBoxComponent();
            textBox?.EnsureLayoutProducerObserved();
#if MILESTRO_FLOW_TEXTBOX_DEBUG
            textBox?.SetFlowDiagnosticsEnabled(m_debugFlowVisibleRange);
#endif
            textBox?.SetFlowModeActive(true);
            SetLayoutDirty();
            UpdateVisibleRange();
        }

        protected override void OnDisable()
        {
            TextBoxComponent()?.SetFlowModeActive(false);
            Unsubscribe();
            SetLayoutDirty(requireActive: false);
            base.OnDisable();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetLayoutDirty();
        }
#endif

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            // Rect changes are usually the result of the current layout pass.
            // The producer will mark layout dirty after updated metrics change
            // an exposed preferred size.
            hasCachedMeasurement = false;
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            scrollRectCache = null;
            SetLayoutDirty();
        }

        private void LateUpdate()
        {
            UpdateVisibleRange();
        }

        public void CalculateLayoutInputHorizontal()
        {
            RefreshMeasurement();
        }

        public void CalculateLayoutInputVertical()
        {
            RefreshMeasurement();
        }

        internal bool ShouldForwardPointerScrollToParent()
        {
            return isActiveAndEnabled && m_forwardPointerScrollToParent;
        }

        private bool TryGetMeasurement(out TextBoxLayoutMeasurement measurement)
        {
            if (!hasCachedMeasurement)
            {
                RefreshMeasurement();
            }

            measurement = cachedMeasurement;
            return hasCachedMeasurement;
        }

        private void RefreshMeasurement()
        {
            var textBox = TextBoxComponent();
            hasCachedMeasurement = textBox != null &&
                                   textBox.TryGetLayoutMeasurement(out cachedMeasurement);
            if (!hasCachedMeasurement)
            {
                cachedMeasurement = default;
            }
        }

        private TextBox? TextBoxComponent()
        {
            if (textBoxCache == null)
            {
                textBoxCache = GetComponent<TextBox>();
            }

            return textBoxCache;
        }

        private void Subscribe()
        {
            if (subscribed)
            {
                return;
            }

            var textBox = TextBoxComponent();
            if (textBox == null)
            {
                return;
            }

            textBox.LayoutChanged += OnTextBoxLayoutChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            var textBox = TextBoxComponent();
            if (textBox != null)
            {
                textBox.LayoutChanged -= OnTextBoxLayoutChanged;
            }

            subscribed = false;
        }

        private void OnTextBoxLayoutChanged()
        {
            SetLayoutDirty();
        }

        private void UpdateVisibleRange()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            var textBox = TextBoxComponent();
            if (textBox == null)
            {
                return;
            }

#if MILESTRO_FLOW_TEXTBOX_DEBUG
            textBox.SetFlowDiagnosticsEnabled(m_debugFlowVisibleRange);
#endif
            if (!TryResolveVisibleRange(out var visible,
                    out var localStartY,
                    out var localEndY,
                    out var visibleCapacityHeight))
            {
                DebugFlow("resolve failed: no RectTransform");
                textBox.SetFlowVisibleRange(false, 0f, 0f, 0f);
                return;
            }

            textBox.SetFlowVisibleRange(visible, localStartY, localEndY, visibleCapacityHeight);
        }

        private bool TryResolveVisibleRange(out bool visible,
            out float localStartY,
            out float localEndY,
            out float visibleCapacityHeight)
        {
            visible = false;
            localStartY = 0f;
            localEndY = 0f;
            visibleCapacityHeight = 0f;

            var itemRect = transform as RectTransform;
            if (itemRect == null)
            {
                return false;
            }

            var itemHeight = Mathf.Max(0f, itemRect.rect.height);
            if (itemHeight <= 0f)
            {
                DebugFlow($"resolve zero item height rect={FormatRect(itemRect.rect)}");
                return true;
            }

            var viewport = ResolveViewport();
            if (viewport == null)
            {
                visible = true;
                localEndY = itemHeight;
                visibleCapacityHeight = itemHeight;
                DebugFlow($"resolve no viewport itemHeight={itemHeight:F3} range=[0,{itemHeight:F3}] capacity={visibleCapacityHeight:F3}");
                return true;
            }

            viewport.GetWorldCorners(worldCorners);
            var viewportTopInItem = float.NegativeInfinity;
            var viewportBottomInItem = float.PositiveInfinity;
            for (var i = 0; i < worldCorners.Length; ++i)
            {
                var itemPoint = itemRect.InverseTransformPoint(worldCorners[i]);
                viewportTopInItem = Mathf.Max(viewportTopInItem, itemPoint.y);
                viewportBottomInItem = Mathf.Min(viewportBottomInItem, itemPoint.y);
            }

            var rawLocalStartY = itemRect.rect.yMax - viewportTopInItem;
            var rawLocalEndY = itemRect.rect.yMax - viewportBottomInItem;
            var rawCapacityHeight = rawLocalEndY - rawLocalStartY;
            visibleCapacityHeight = FloatUtil.IsFinite(rawCapacityHeight) && rawCapacityHeight > 0f
                ? Mathf.Min(itemHeight, rawCapacityHeight)
                : itemHeight;
            visible = TextBoxFlowVisibleRange.TryNormalize(rawLocalStartY,
                rawLocalEndY,
                itemHeight,
                out localStartY,
                out localEndY);
            DebugFlow("resolve " +
                      $"itemHeight={itemHeight:F3} " +
                      $"raw=[{rawLocalStartY:F3},{rawLocalEndY:F3}] " +
                      $"normalized=[{localStartY:F3},{localEndY:F3}] " +
                      $"capacity={visibleCapacityHeight:F3} " +
                      $"visible={visible} " +
                      $"viewportTopBottomInItem=[{viewportTopInItem:F3},{viewportBottomInItem:F3}] " +
                      $"itemRect={FormatRect(itemRect.rect)} viewportRect={FormatRect(viewport.rect)}");
            return true;
        }

        private RectTransform? ResolveViewport()
        {
            if (m_viewport != null)
            {
                return m_viewport;
            }

            var scrollRect = ScrollRectComponent();
            if (scrollRect != null)
            {
                if (scrollRect.viewport != null)
                {
                    return scrollRect.viewport;
                }

                return scrollRect.transform as RectTransform;
            }

            return transform.parent as RectTransform;
        }

        private ScrollRect? ScrollRectComponent()
        {
            if (scrollRectCache == null)
            {
                scrollRectCache = GetComponentInParent<ScrollRect>();
            }

            return scrollRectCache;
        }

        private void SetLayoutDirty(bool requireActive = true)
        {
            hasCachedMeasurement = false;
            var targetRectTransform = transform as RectTransform;
            if ((requireActive && !IsActive()) || targetRectTransform == null)
            {
                return;
            }

            LayoutRebuilder.MarkLayoutForRebuild(targetRectTransform);
        }

        [System.Diagnostics.Conditional("MILESTRO_FLOW_TEXTBOX_DEBUG")]
        private void DebugFlow(string message)
        {
#if MILESTRO_FLOW_TEXTBOX_DEBUG
            if (!m_debugFlowVisibleRange)
            {
                return;
            }

            Debug.Log($"[Milestro FlowTextBox][LayoutElement:{name}] {message}", this);
#endif
        }

        private static string FormatRect(Rect rect)
        {
            return $"({rect.xMin:F3},{rect.yMin:F3},{rect.width:F3},{rect.height:F3})";
        }
    }
}
