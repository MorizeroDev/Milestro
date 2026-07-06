using System;
using Milestro.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Milestro.Components
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Milestro/Text Box Scrollbar")]
    public class TextBoxScrollbar : MonoBehaviour
    {
        private const float OverflowEpsilon = 0.001f;

        [SerializeField] private TextBox? m_textBox;
        [SerializeField] private TextInput? m_textInput;
        [SerializeField] private Scrollbar? m_horizontalScrollbar;
        [SerializeField] private Scrollbar? m_verticalScrollbar;

        private bool syncingScrollbars;
        private Scrollbar? registeredHorizontalScrollbar;
        private Scrollbar? registeredVerticalScrollbar;

        private void OnEnable()
        {
            RefreshScrollbarListeners();
            SyncScrollbars();
        }

        private void OnDisable()
        {
            ClearScrollbarListeners();
        }

        private void LateUpdate()
        {
            RefreshScrollbarListeners();
            SyncScrollbars();
        }

        private void RefreshScrollbarListeners()
        {
            if (registeredHorizontalScrollbar != m_horizontalScrollbar)
            {
                if (registeredHorizontalScrollbar != null)
                {
                    registeredHorizontalScrollbar.onValueChanged.RemoveListener(OnHorizontalScrollbarValueChanged);
                }

                registeredHorizontalScrollbar = m_horizontalScrollbar;
                if (registeredHorizontalScrollbar != null)
                {
                    registeredHorizontalScrollbar.onValueChanged.AddListener(OnHorizontalScrollbarValueChanged);
                }
            }

            if (registeredVerticalScrollbar != m_verticalScrollbar)
            {
                if (registeredVerticalScrollbar != null)
                {
                    registeredVerticalScrollbar.onValueChanged.RemoveListener(OnVerticalScrollbarValueChanged);
                }

                registeredVerticalScrollbar = m_verticalScrollbar;
                if (registeredVerticalScrollbar != null)
                {
                    registeredVerticalScrollbar.onValueChanged.AddListener(OnVerticalScrollbarValueChanged);
                }
            }
        }

        private void ClearScrollbarListeners()
        {
            if (registeredHorizontalScrollbar != null)
            {
                registeredHorizontalScrollbar.onValueChanged.RemoveListener(OnHorizontalScrollbarValueChanged);
                registeredHorizontalScrollbar = null;
            }

            if (registeredVerticalScrollbar != null)
            {
                registeredVerticalScrollbar.onValueChanged.RemoveListener(OnVerticalScrollbarValueChanged);
                registeredVerticalScrollbar = null;
            }
        }

        private void SyncScrollbars()
        {
            if (!TryGetScrollState(out var state))
            {
                SetScrollbarVisible(m_horizontalScrollbar, false);
                SetScrollbarVisible(m_verticalScrollbar, false);
                return;
            }

            syncingScrollbars = true;
            try
            {
                SyncHorizontalScrollbar(state);
                SyncVerticalScrollbar(state);
            }
            finally
            {
                syncingScrollbars = false;
            }
        }

        private void SyncHorizontalScrollbar(TextBoxScrollState state)
        {
            if (m_horizontalScrollbar == null)
            {
                return;
            }

            var hasOverflow = HasOverflow(state.ViewportWidth, state.ContentWidth);
            m_horizontalScrollbar.size = hasOverflow ? ThumbSize(state.ViewportWidth, state.ContentWidth) : 1f;
            m_horizontalScrollbar.SetValueWithoutNotify(hasOverflow
                ? ScrollOffsetToPercent(state.ScrollX, state.ViewportWidth, state.ContentWidth)
                : 0f);
            SetScrollbarVisible(m_horizontalScrollbar, hasOverflow);
        }

        private void SyncVerticalScrollbar(TextBoxScrollState state)
        {
            if (m_verticalScrollbar == null)
            {
                return;
            }

            var hasOverflow = HasOverflow(state.ViewportHeight, state.ContentHeight);
            m_verticalScrollbar.size = hasOverflow ? ThumbSize(state.ViewportHeight, state.ContentHeight) : 1f;
            m_verticalScrollbar.SetValueWithoutNotify(hasOverflow
                ? 1f - ScrollOffsetToPercent(state.ScrollY, state.ViewportHeight, state.ContentHeight)
                : 1f);
            SetScrollbarVisible(m_verticalScrollbar, hasOverflow);
        }

        private bool TryGetScrollState(out TextBoxScrollState state)
        {
            return Target.TryGetScrollState(out state);
        }

        private void OnHorizontalScrollbarValueChanged(float value)
        {
            if (syncingScrollbars)
            {
                return;
            }

            Target.ScrollToPercentX(value);
        }

        private void OnVerticalScrollbarValueChanged(float value)
        {
            if (syncingScrollbars)
            {
                return;
            }

            var scrollPercent = 1f - value;
            Target.ScrollToPercentY(scrollPercent);
        }

        private ITextBoxScrollTarget Target
        {
            get
            {
                if (m_textBox != null && m_textInput != null)
                {
                    throw new InvalidOperationException(
                        "TextBoxScrollbar must target either a TextBox or a TextInput, not both.");
                }

                if (m_textBox != null)
                {
                    return m_textBox;
                }

                if (m_textInput != null)
                {
                    return m_textInput;
                }

                throw new InvalidOperationException("TextBoxScrollbar must target a TextBox or a TextInput.");
            }
        }

        private static bool HasOverflow(float viewportSize, float contentSize)
        {
            return FloatUtil.IsFinite(viewportSize) &&
                   FloatUtil.IsFinite(contentSize) &&
                   viewportSize > 0f &&
                   contentSize - viewportSize > OverflowEpsilon;
        }

        private static float ThumbSize(float viewportSize, float contentSize)
        {
            if (!FloatUtil.IsFinite(viewportSize) || !FloatUtil.IsFinite(contentSize) || contentSize <= 0f)
            {
                return 1f;
            }

            return Mathf.Clamp01(viewportSize / contentSize);
        }

        private static float ScrollOffsetToPercent(float scrollOffset, float viewportSize, float contentSize)
        {
            return FloatUtil.ScrollOffsetToPercent(scrollOffset, contentSize - viewportSize);
        }

        private static void SetScrollbarVisible(Scrollbar? scrollbar, bool visible)
        {
            if (scrollbar == null || scrollbar.gameObject.activeSelf == visible)
            {
                return;
            }

            scrollbar.gameObject.SetActive(visible);
        }
    }
}
