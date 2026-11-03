using UnityEngine;
using UnityEngine.EventSystems;

namespace Milestro.Components.Internal
{
    internal sealed class MilestroDropdownBlocker : MonoBehaviour, IPointerClickHandler
    {
        private MilestroDropdown? owner;
        private int popupId;

        internal void Configure(MilestroDropdown dropdown, int expandedPopupId)
        {
            owner = dropdown;
            popupId = expandedPopupId;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || owner == null)
            {
                return;
            }

            owner.CloseFromBlocker(popupId);
            eventData.Use();
        }
    }
}
