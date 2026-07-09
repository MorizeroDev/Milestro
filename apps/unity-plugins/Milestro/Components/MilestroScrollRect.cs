using System;
using Milestro.Util;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Milestro.Components
{
    [AddComponentMenu("Milestro/Milestro Scroll Rect")]
    public class MilestroScrollRect : ScrollRect
    {
        private const float DefaultScrollWheelStepPixels = 48f;
        private const float DefaultScrollTweenDurationSeconds = 0.14f;

        [SerializeField] private float m_scrollWheelStepPixels = DefaultScrollWheelStepPixels;
        [SerializeField] private bool m_smoothScroll = true;
        [SerializeField][Min(0f)] private float m_scrollTweenDurationSeconds = DefaultScrollTweenDurationSeconds;

        [NonSerialized] private readonly ScrollAxisLock scrollAxisLock = new ScrollAxisLock();
        [NonSerialized] private readonly ScrollTween scrollTweenX = new ScrollTween();
        [NonSerialized] private readonly ScrollTween scrollTweenY = new ScrollTween();

        public float scrollWheelStepPixels
        {
            get => m_scrollWheelStepPixels;
            set => m_scrollWheelStepPixels = ResolveScrollWheelStepPixels(value);
        }

        public bool smoothScroll
        {
            get => m_smoothScroll;
            set => m_smoothScroll = value;
        }

        public float scrollTweenDurationSeconds
        {
            get => m_scrollTweenDurationSeconds;
            set => m_scrollTweenDurationSeconds = ResolveScrollTweenDurationSeconds(value);
        }

        public override void OnScroll(PointerEventData eventData)
        {
            if (eventData == null)
            {
                return;
            }

            if (!IsActive())
            {
                CancelScrollState();
                ScrollEventUtil.PassScrollToParent(transform, eventData, eventData.scrollDelta);
                return;
            }

            UpdateBounds();
            CancelTweensIfExternallyMoved();

            var axis = scrollAxisLock.Resolve(eventData.scrollDelta,
                ScrollEventUtil.IsHorizontalScrollModifierDown(),
                out var contentOffsetDelta,
                out var lockedScrollDelta);
            if (axis == ScrollAxis.None)
            {
                ScrollEventUtil.PassScrollToParent(transform, eventData, lockedScrollDelta);
                return;
            }

            var stepPixels = ScrollWheelStepPixels();
            var shouldTweenPointerScroll = ShouldTweenPointerScroll(eventData.scrollDelta);
            var unusedScrollDelta = lockedScrollDelta;
            var consumed = false;
            if (axis == ScrollAxis.Horizontal || axis == ScrollAxis.Free)
            {
                var result = TryScrollX(contentOffsetDelta.x, stepPixels, shouldTweenPointerScroll);
                consumed |= result.Consumed;
                unusedScrollDelta = ApplyUnusedX(unusedScrollDelta, lockedScrollDelta, result.UnusedContentOffsetDelta,
                    contentOffsetDelta.x);
            }

            if (axis == ScrollAxis.Vertical || axis == ScrollAxis.Free)
            {
                var result = TryScrollY(contentOffsetDelta.y, stepPixels, shouldTweenPointerScroll);
                consumed |= result.Consumed;
                unusedScrollDelta = ApplyUnusedY(unusedScrollDelta, lockedScrollDelta, result.UnusedContentOffsetDelta,
                    contentOffsetDelta.y);
            }

            if (!consumed)
            {
                ScrollEventUtil.PassScrollToParent(transform, eventData, lockedScrollDelta);
                return;
            }

            StopMovement();
            ScrollEventUtil.PassScrollToParent(transform, eventData, unusedScrollDelta);
            eventData.Use();
        }

        public override void OnInitializePotentialDrag(PointerEventData eventData)
        {
            CancelScrollState();
            base.OnInitializePotentialDrag(eventData);
        }

        public override void OnBeginDrag(PointerEventData eventData)
        {
            CancelScrollState();
            base.OnBeginDrag(eventData);
        }

        public override void OnEndDrag(PointerEventData eventData)
        {
            scrollAxisLock.Reset();
            base.OnEndDrag(eventData);
        }

        protected override void OnDisable()
        {
            CancelScrollState();
            base.OnDisable();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            CancelScrollState();
        }

        protected override void LateUpdate()
        {
            TickScrollTweens();
            base.LateUpdate();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            m_scrollWheelStepPixels = ResolveScrollWheelStepPixels(m_scrollWheelStepPixels);
            m_scrollTweenDurationSeconds = ResolveScrollTweenDurationSeconds(m_scrollTweenDurationSeconds);
        }
#endif

        private ScrollResult TryScrollX(float contentOffsetDelta, float stepPixels, bool tweenScroll)
        {
            if (!horizontal || Mathf.Approximately(contentOffsetDelta, 0f))
            {
                return ScrollResult.Unhandled(contentOffsetDelta);
            }

            var maxScrollX = MaxScrollX();
            var currentScrollX = CurrentScrollX(maxScrollX);
            scrollTweenX.CancelIfExternallyMoved(currentScrollX);
            var deltaPixels = contentOffsetDelta * stepPixels;
            if (tweenScroll)
            {
                var consumption = ScrollDeltaUtil.ConsumeOffsetDelta(scrollTweenX.IsActive()
                        ? currentScrollX + scrollTweenX.PendingDeltaFrom(currentScrollX)
                        : currentScrollX,
                    deltaPixels,
                    maxScrollX);
                if (!consumption.Consumed)
                {
                    return ScrollResult.Unhandled(contentOffsetDelta);
                }

                scrollTweenX.ScrollBy(currentScrollX, consumption.ConsumedDelta, maxScrollX);
                return ScrollResult.Handled(consumption.UnusedDelta / stepPixels);
            }

            scrollTweenX.Cancel();
            var immediateConsumption = ScrollDeltaUtil.ConsumeOffsetDelta(currentScrollX, deltaPixels, maxScrollX);
            if (!immediateConsumption.Consumed)
            {
                return ScrollResult.Unhandled(contentOffsetDelta);
            }

            SetScrollX(immediateConsumption.NextOffset, maxScrollX);
            return ScrollResult.Handled(immediateConsumption.UnusedDelta / stepPixels);
        }

        private ScrollResult TryScrollY(float contentOffsetDelta, float stepPixels, bool tweenScroll)
        {
            if (!vertical || Mathf.Approximately(contentOffsetDelta, 0f))
            {
                return ScrollResult.Unhandled(contentOffsetDelta);
            }

            var maxScrollY = MaxScrollY();
            var currentScrollY = CurrentScrollY(maxScrollY);
            scrollTweenY.CancelIfExternallyMoved(currentScrollY);
            var deltaPixels = contentOffsetDelta * stepPixels;
            if (tweenScroll)
            {
                var consumption = ScrollDeltaUtil.ConsumeOffsetDelta(scrollTweenY.IsActive()
                        ? currentScrollY + scrollTweenY.PendingDeltaFrom(currentScrollY)
                        : currentScrollY,
                    deltaPixels,
                    maxScrollY);
                if (!consumption.Consumed)
                {
                    return ScrollResult.Unhandled(contentOffsetDelta);
                }

                scrollTweenY.ScrollBy(currentScrollY, consumption.ConsumedDelta, maxScrollY);
                return ScrollResult.Handled(consumption.UnusedDelta / stepPixels);
            }

            scrollTweenY.Cancel();
            var immediateConsumption = ScrollDeltaUtil.ConsumeOffsetDelta(currentScrollY, deltaPixels, maxScrollY);
            if (!immediateConsumption.Consumed)
            {
                return ScrollResult.Unhandled(contentOffsetDelta);
            }

            SetScrollY(immediateConsumption.NextOffset, maxScrollY);
            return ScrollResult.Handled(immediateConsumption.UnusedDelta / stepPixels);
        }

        private void TickScrollTweens()
        {
            if (!IsActive() || (!scrollTweenX.IsActive() && !scrollTweenY.IsActive()))
            {
                return;
            }

            UpdateBounds();
            var changed = false;
            var maxScrollX = MaxScrollX();
            var currentScrollX = CurrentScrollX(maxScrollX);
            if (TickScrollTween(scrollTweenX, currentScrollX, maxScrollX, out var nextScrollX))
            {
                SetScrollX(nextScrollX, maxScrollX);
                changed = true;
            }

            var maxScrollY = MaxScrollY();
            var currentScrollY = CurrentScrollY(maxScrollY);
            if (TickScrollTween(scrollTweenY, currentScrollY, maxScrollY, out var nextScrollY))
            {
                SetScrollY(nextScrollY, maxScrollY);
                changed = true;
            }

            if (changed)
            {
                StopMovement();
            }
        }

        private bool TickScrollTween(ScrollTween tween, float currentValue, float maxValue, out float nextValue)
        {
            return tween.Tick(currentValue,
                maxValue,
                Time.deltaTime,
                out nextValue);
        }

        private void CancelTweensIfExternallyMoved()
        {
            scrollTweenX.CancelIfExternallyMoved(CurrentScrollX(MaxScrollX()));
            scrollTweenY.CancelIfExternallyMoved(CurrentScrollY(MaxScrollY()));
        }

        private void CancelScrollState()
        {
            scrollAxisLock.Reset();
            scrollTweenX.Cancel();
            scrollTweenY.Cancel();
        }

        private float CurrentScrollX(float maxScrollX)
        {
            return FloatUtil.PercentToScrollOffset(horizontalNormalizedPosition, maxScrollX);
        }

        private float CurrentScrollY(float maxScrollY)
        {
            return FloatUtil.PercentToScrollOffset(1f - verticalNormalizedPosition, maxScrollY);
        }

        private void SetScrollX(float scrollOffset, float maxScrollX)
        {
            SetNormalizedPosition(FloatUtil.ScrollOffsetToPercent(scrollOffset, maxScrollX), 0);
        }

        private void SetScrollY(float scrollOffset, float maxScrollY)
        {
            SetNormalizedPosition(1f - FloatUtil.ScrollOffsetToPercent(scrollOffset, maxScrollY), 1);
        }

        private float MaxScrollX()
        {
            if (content == null)
            {
                return 0f;
            }

            UpdateBounds();
            return Mathf.Max(0f, m_ContentBounds.size.x - viewRect.rect.width);
        }

        private float MaxScrollY()
        {
            if (content == null)
            {
                return 0f;
            }

            UpdateBounds();
            return Mathf.Max(0f, m_ContentBounds.size.y - viewRect.rect.height);
        }

        private bool ShouldTweenPointerScroll(Vector2 scrollDelta)
        {
            return ShouldTweenScroll() &&
                   !ScrollEventUtil.ShouldBypassTweenForPointerScroll(scrollDelta);
        }

        private bool ShouldTweenScroll()
        {
            return Application.isPlaying && m_smoothScroll && ScrollTweenDurationSeconds() > 0f;
        }

        private float ScrollWheelStepPixels()
        {
            return ResolveScrollWheelStepPixels(m_scrollWheelStepPixels);
        }

        private float ScrollTweenDurationSeconds()
        {
            return ResolveScrollTweenDurationSeconds(m_scrollTweenDurationSeconds);
        }

        private static float ResolveScrollWheelStepPixels(float stepPixels)
        {
            return FloatUtil.IsFinite(stepPixels) ? Mathf.Max(1f, stepPixels) : DefaultScrollWheelStepPixels;
        }

        private static float ResolveScrollTweenDurationSeconds(float durationSeconds)
        {
            return FloatUtil.IsFinite(durationSeconds) ? Mathf.Max(0f, durationSeconds) : DefaultScrollTweenDurationSeconds;
        }

        private static Vector2 ApplyUnusedX(Vector2 unusedScrollDelta,
            Vector2 lockedScrollDelta,
            float unusedContentOffsetDelta,
            float originalContentOffsetDelta)
        {
            if (Mathf.Approximately(originalContentOffsetDelta, 0f))
            {
                return unusedScrollDelta;
            }

            var ratio = Mathf.Clamp01(unusedContentOffsetDelta / originalContentOffsetDelta);
            if (!Mathf.Approximately(lockedScrollDelta.x, 0f))
            {
                unusedScrollDelta.x = lockedScrollDelta.x * ratio;
            }
            else
            {
                unusedScrollDelta.y = lockedScrollDelta.y * ratio;
            }

            return unusedScrollDelta;
        }

        private static Vector2 ApplyUnusedY(Vector2 unusedScrollDelta,
            Vector2 lockedScrollDelta,
            float unusedContentOffsetDelta,
            float originalContentOffsetDelta)
        {
            if (Mathf.Approximately(originalContentOffsetDelta, 0f))
            {
                return unusedScrollDelta;
            }

            var ratio = Mathf.Clamp01(unusedContentOffsetDelta / originalContentOffsetDelta);
            unusedScrollDelta.y = lockedScrollDelta.y * ratio;
            return unusedScrollDelta;
        }

        private readonly struct ScrollResult
        {
            private ScrollResult(bool consumed, float unusedContentOffsetDelta)
            {
                Consumed = consumed;
                UnusedContentOffsetDelta = unusedContentOffsetDelta;
            }

            public bool Consumed { get; }

            public float UnusedContentOffsetDelta { get; }

            public static ScrollResult Handled(float unusedContentOffsetDelta)
            {
                return new ScrollResult(true, unusedContentOffsetDelta);
            }

            public static ScrollResult Unhandled(float unusedContentOffsetDelta)
            {
                return new ScrollResult(false, unusedContentOffsetDelta);
            }
        }
    }
}
