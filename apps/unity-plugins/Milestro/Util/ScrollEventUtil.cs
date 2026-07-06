using Milestro.Configuration;
using Milestro.InputManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Milestro.Util
{
    internal static class ScrollEventUtil
    {
        internal static bool IsHorizontalScrollModifierDown()
        {
            return HybridInput.GetKey(KeyCode.LeftShift) || HybridInput.GetKey(KeyCode.RightShift);
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

        internal static bool ShouldBypassTweenForPointerScroll(Vector2 scrollDelta)
        {
            var configuration = MilestroConfiguration.Configuration?.ScrollTween;
            var mode = configuration?.PointerScrollTweenMode ?? PointerScrollTweenMode.Auto;
            switch (mode)
            {
                case PointerScrollTweenMode.AlwaysTween:
                    return false;
                case PointerScrollTweenMode.BypassFractional:
                    return HasFractionalScrollDelta(scrollDelta, ResolveFractionalDeltaTolerance(configuration));
                case PointerScrollTweenMode.Auto:
                default:
                    return IsPlatformAutoBypassScroll(scrollDelta, ResolveFractionalDeltaTolerance(configuration));
            }
        }

        private static bool IsPlatformAutoBypassScroll(Vector2 scrollDelta, float fractionalDeltaTolerance)
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            return HasFractionalScrollDelta(scrollDelta, fractionalDeltaTolerance);
#else
            return false;
#endif
        }

        private static bool HasFractionalScrollDelta(Vector2 scrollDelta, float fractionalDeltaTolerance)
        {
            return HasFractionalValue(scrollDelta.x, fractionalDeltaTolerance) ||
                   HasFractionalValue(scrollDelta.y, fractionalDeltaTolerance);
        }

        private static bool HasFractionalValue(float value, float fractionalDeltaTolerance)
        {
            if (!FloatUtil.IsFinite(value))
            {
                return false;
            }

            return Mathf.Abs(value - Mathf.Round(value)) > fractionalDeltaTolerance;
        }

        private static float ResolveFractionalDeltaTolerance(ScrollTweenConfiguration? configuration)
        {
            var tolerance = configuration?.FractionalDeltaTolerance ??
                            ScrollTweenConfiguration.DefaultFractionalDeltaTolerance;
            if (!FloatUtil.IsFinite(tolerance))
            {
                return ScrollTweenConfiguration.DefaultFractionalDeltaTolerance;
            }

            return Mathf.Clamp(tolerance, 0f, 0.5f);
        }
    }
}
