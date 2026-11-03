using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Milestro.Components.Internal
{
    [DisallowMultipleComponent]
    internal sealed class MilestroDropdownItem : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        [SerializeField] private CanvasSlimText? text;
        [SerializeField] private CanvasSlimText? checkmark;
        [SerializeField] private Image? background;
        [SerializeField] private Color normalColor = new(1f, 1f, 1f, 0f);
        [SerializeField] private Color candidateColor = new(0.35f, 0.55f, 0.9f, 0.35f);

        private MilestroDropdown? owner;
        private int popupId;
        private int optionIndex;

        internal RectTransform RectTransform => (RectTransform)transform;
        internal int OptionIndex => optionIndex;

        internal void ConfigureReferences(CanvasSlimText itemText, CanvasSlimText itemCheckmark, Image itemBackground)
        {
            text = itemText;
            checkmark = itemCheckmark;
            background = itemBackground;
        }

        internal void Configure(MilestroDropdown dropdown, int index, int expandedPopupId)
        {
            owner = dropdown;
            optionIndex = index;
            popupId = expandedPopupId;
        }

        internal void SetText(string value)
        {
            if (text != null)
            {
                text.Text = value;
            }
        }

        internal void SetVisual(bool committed, bool candidate)
        {
            if (checkmark != null)
            {
                checkmark.gameObject.SetActive(committed);
            }

            if (background != null)
            {
                background.color = candidate ? candidateColor : normalColor;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (owner != null)
            {
                owner.HighlightItem(popupId, optionIndex);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || eventData.dragging || owner == null)
            {
                return;
            }

            owner.SelectItem(popupId, optionIndex);
            eventData.Use();
        }
    }
}
