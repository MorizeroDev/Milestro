using UnityEngine;
using UnityEngine.EventSystems;

namespace Milestro.Util
{
    internal static class ScrollEventUtil
    {
        internal static bool IsHorizontalScrollModifierDown()
        {
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }

        internal static void PassScrollToParent(Transform sourceTransform,
            PointerEventData eventData,
            Vector2 scrollDelta)
        {
            if (eventData.used || sourceTransform.parent == null || !HasScrollDelta(scrollDelta))
            {
                return;
            }

            var originalDelta = eventData.scrollDelta;
            eventData.scrollDelta = scrollDelta;

            try
            {
                ExecuteEvents.ExecuteHierarchy(sourceTransform.parent.gameObject,
                    eventData,
                    ExecuteEvents.scrollHandler);
            }
            finally
            {
                eventData.scrollDelta = originalDelta;
            }
        }

        internal static bool HasScrollDelta(Vector2 scrollDelta)
        {
            return !Mathf.Approximately(scrollDelta.x, 0f) ||
                   !Mathf.Approximately(scrollDelta.y, 0f);
        }
    }
}
