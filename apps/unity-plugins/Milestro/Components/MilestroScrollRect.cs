using System;
using System.Collections.Generic;
using Milestro.Input;
using Milestro.Model;
using Milestro.Util;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Milestro.Components
{
    [AddComponentMenu("Milestro/Milestro Scroll Rect")]
    public class MilestroScrollRect : ScrollRect, ICancelHandler
    {
        private const float DefaultScrollWheelStepPixels = 48f;
        private const float DefaultScrollTweenDurationSeconds = 0.14f;

        [SerializeField] private float m_scrollWheelStepPixels = DefaultScrollWheelStepPixels;
        [SerializeField] private bool m_smoothScroll = true;
        [SerializeField][Min(0f)] private float m_scrollTweenDurationSeconds = DefaultScrollTweenDurationSeconds;

        [SerializeField]
        private ScrollElasticSettings? m_scrollElastic = new ScrollElasticSettings();

        public ScrollElasticSettings scrollElastic
        {
            get => ScrollElasticSettings.Resolve(ref m_scrollElastic);
            set
            {
                m_scrollElastic = value;
                ScrollElasticSettings.Resolve(ref m_scrollElastic);
            }
        }

        [NonSerialized] private readonly ScrollAxisLock scrollAxisLock = new ScrollAxisLock();
        [NonSerialized] private readonly ScrollTween scrollTweenX = new ScrollTween();
        [NonSerialized] private readonly ScrollTween scrollTweenY = new ScrollTween();
        [NonSerialized] private readonly ScrollElasticAxis scrollElasticX = new ScrollElasticAxis();
        [NonSerialized] private readonly ScrollElasticAxis scrollElasticY = new ScrollElasticAxis();
        [NonSerialized]
        private readonly List<MonoBehaviour> parentScrollHandlerScratch = new List<MonoBehaviour>(8);
        [NonSerialized]
        private readonly ScrollElasticReleasePolicy scrollElasticReleaseX =
            new ScrollElasticReleasePolicy();
        [NonSerialized]
        private readonly ScrollElasticReleasePolicy scrollElasticReleaseY =
            new ScrollElasticReleasePolicy();
        [NonSerialized] private RectTransform? observedContent;
        [NonSerialized] private Vector2 observedLogicalContentPosition;
        [NonSerialized] private bool hasObservedLogicalContentPosition;
        [NonSerialized] private RectTransform? appliedVisualContent;
        [NonSerialized] private Vector2 appliedVisualDelta;
        [NonSerialized] private Vector2 appliedVisualBasePosition;
        [NonSerialized] private bool movementTypeOverridden;
        [NonSerialized] private MovementType savedMovementType;
        [NonSerialized] private bool customDragActive;
        [NonSerialized] private PointerEventData? activeDragEventData;

        public new RectTransform content
        {
            get
            {
                RemoveVisualOffset();
                return base.content;
            }
            set
            {
                RemoveVisualOffset();
                CancelScrollState();
                base.content = value;
                observedContent = value;
                RecordLogicalContentPosition();
            }
        }

        public Vector2 GetLogicalNormalizedPosition()
        {
            RemoveVisualOffset();
            return base.normalizedPosition;
        }

        public void SetLogicalNormalizedPosition(Vector2 value)
        {
            RemoveVisualOffset();
            CancelScrollState();
            base.normalizedPosition = value;
            RecordLogicalContentPosition();
        }

        public float GetLogicalHorizontalNormalizedPosition()
        {
            return GetLogicalNormalizedPosition().x;
        }

        public float GetLogicalVerticalNormalizedPosition()
        {
            return GetLogicalNormalizedPosition().y;
        }

        public void SetLogicalHorizontalNormalizedPosition(float value)
        {
            var logical = GetLogicalNormalizedPosition();
            SetLogicalNormalizedPosition(new Vector2(value, logical.y));
        }

        public void SetLogicalVerticalNormalizedPosition(float value)
        {
            var logical = GetLogicalNormalizedPosition();
            SetLogicalNormalizedPosition(new Vector2(logical.x, value));
        }

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

        protected override void OnEnable()
        {
            base.OnEnable();
            observedContent = base.content;
            RecordLogicalContentPosition();
            ElasticSettings();
            EnsureSingleMotionOwner();
            Canvas.willRenderCanvases -= ApplyVisualOffset;
            Canvas.willRenderCanvases += ApplyVisualOffset;
        }

        private void Update()
        {
            RemoveVisualOffset();
        }

        public override void OnScroll(PointerEventData eventData)
        {
            RemoveVisualOffset();
            try
            {
                HandleScroll(eventData);
            }
            finally
            {
                RemoveVisualOffset();
            }
        }

        private void HandleScroll(PointerEventData eventData)
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

            var scrollInput = HybridInputRuntime.ResolveScrollInput(eventData);
            var axis = scrollAxisLock.Resolve(scrollInput.Delta,
                ScrollEventUtil.IsHorizontalScrollModifierDown(),
                out var contentOffsetDelta,
                out var lockedScrollDelta);
            if (axis == ScrollAxis.None)
            {
                ScrollEventUtil.PassScrollToParent(transform, eventData, lockedScrollDelta);
                return;
            }

            var stepPixels = ScrollWheelStepPixels();
            var shouldTweenPointerScroll = ShouldTweenPointerScroll(scrollInput.Delta);
            var elasticSettings = ElasticSettings();
            EnsureSingleMotionOwner();
            var allowElastic = scrollInput.Metadata.Capability != HybridScrollCapability.Unsupported &&
                               elasticSettings.Enabled &&
                               !ScrollEventUtil.HasActiveParentScrollHandler(transform, parentScrollHandlerScratch);
            if (!allowElastic)
            {
                SettleElastic();
            }
            SettleUnavailableElasticAxes();
            var unusedScrollDelta = lockedScrollDelta;
            var consumed = false;
            if (axis == ScrollAxis.Horizontal || axis == ScrollAxis.Free)
            {
                var result = TryScrollX(contentOffsetDelta.x,
                    stepPixels,
                    shouldTweenPointerScroll,
                    allowElastic,
                    elasticSettings);
                consumed |= result.Consumed;
                unusedScrollDelta = ApplyUnusedX(unusedScrollDelta, lockedScrollDelta, result.UnusedContentOffsetDelta,
                    contentOffsetDelta.x);
            }

            if (axis == ScrollAxis.Vertical || axis == ScrollAxis.Free)
            {
                var result = TryScrollY(contentOffsetDelta.y,
                    stepPixels,
                    shouldTweenPointerScroll,
                    allowElastic,
                    elasticSettings);
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
            ObserveElasticRelease(axis, scrollInput.Metadata, elasticSettings.ReleaseDelaySeconds);
            ScrollEventUtil.PassScrollToParent(transform, eventData, unusedScrollDelta);
            eventData.Use();
        }

        public override void OnInitializePotentialDrag(PointerEventData eventData)
        {
            RemoveVisualOffset();
            if (eventData == null)
            {
                return;
            }
            if (eventData.button != PointerEventData.InputButton.Left)
            {
                base.OnInitializePotentialDrag(eventData);
                return;
            }

            EndBaseDragIfActive();
            CancelScrollState();
            customDragActive = false;
            EnsureSingleMotionOwner();
            base.OnInitializePotentialDrag(eventData);
        }

        public override void OnBeginDrag(PointerEventData eventData)
        {
            RemoveVisualOffset();
            if (eventData == null)
            {
                return;
            }
            if (eventData.button != PointerEventData.InputButton.Left || !IsActive())
            {
                base.OnBeginDrag(eventData);
                return;
            }

            EndBaseDragIfActive();
            CancelScrollState();
            EnsureSingleMotionOwner();
            base.OnBeginDrag(eventData);
            activeDragEventData = eventData;
            customDragActive = CanUseCustomElastic();
        }

        public override void OnDrag(PointerEventData eventData)
        {
            RemoveVisualOffset();
            if (eventData == null)
            {
                return;
            }
            if (eventData.button != PointerEventData.InputButton.Left || activeDragEventData == null)
            {
                base.OnDrag(eventData);
                return;
            }

            if (!customDragActive || !CanUseCustomElastic())
            {
                customDragActive = false;
                SettleElastic();
                base.OnDrag(eventData);
                RecordLogicalContentPosition();
                return;
            }

            UpdateBounds();
            SettleUnavailableElasticAxes();
            var localDelta = ResolveDragDelta(eventData);
            var contentOffsetDelta = DragDeltaToContentOffset(localDelta);
            var settings = ElasticSettings();
            var changed = false;
            if (horizontal)
            {
                var maxScrollX = MaxScrollX();
                var currentScrollX = CurrentScrollX(maxScrollX);
                if (scrollElasticX.Apply(currentScrollX,
                        maxScrollX,
                        contentOffsetDelta.x,
                        settings,
                        out var nextScrollX))
                {
                    SetScrollX(nextScrollX, maxScrollX);
                    changed = true;
                }
            }
            if (vertical)
            {
                var maxScrollY = MaxScrollY();
                var currentScrollY = CurrentScrollY(maxScrollY);
                if (scrollElasticY.Apply(currentScrollY,
                        maxScrollY,
                        contentOffsetDelta.y,
                        settings,
                        out var nextScrollY))
                {
                    SetScrollY(nextScrollY, maxScrollY);
                    changed = true;
                }
            }

            if (changed)
            {
                StopMovement();
            }
        }

        public override void OnEndDrag(PointerEventData eventData)
        {
            RemoveVisualOffset();
            if (eventData == null)
            {
                return;
            }
            if (eventData.button != PointerEventData.InputButton.Left)
            {
                base.OnEndDrag(eventData);
                return;
            }

            scrollAxisLock.Reset();
            base.OnEndDrag(eventData);
            var hadActiveDrag = activeDragEventData != null;
            activeDragEventData = null;
            if (!hadActiveDrag)
            {
                customDragActive = false;
                return;
            }
            ReleaseElasticImmediately();
            if (HasElasticState())
            {
                StopMovement();
            }
            customDragActive = false;
        }

        public void OnCancel(BaseEventData eventData)
        {
            RemoveVisualOffset();
            var dragEventData = activeDragEventData;
            if (dragEventData == null)
            {
                return;
            }

            base.OnEndDrag(dragEventData);
            activeDragEventData = null;
            scrollAxisLock.Reset();
            ReleaseElasticImmediately();
            if (HasElasticState())
            {
                StopMovement();
            }
            customDragActive = false;
        }

        protected override void OnDisable()
        {
            Canvas.willRenderCanvases -= ApplyVisualOffset;
            RemoveVisualOffset();
            CancelScrollState();
            customDragActive = false;
            activeDragEventData = null;
            RestoreMovementType();
            base.OnDisable();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            RemoveVisualOffset();
            base.OnRectTransformDimensionsChange();
            CancelScrollState();
            customDragActive = false;
        }

        protected override void LateUpdate()
        {
            RemoveVisualOffset();
            EnsureSingleMotionOwner();
            try
            {
                TickScrollTweens();
                base.LateUpdate();
                TickElastic();
            }
            finally
            {
                RemoveVisualOffset();
            }
        }

        protected override void OnDestroy()
        {
            Canvas.willRenderCanvases -= ApplyVisualOffset;
            RemoveVisualOffset();
            SettleElastic();
            customDragActive = false;
            activeDragEventData = null;
            RestoreMovementType();
            base.OnDestroy();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            m_scrollWheelStepPixels = ResolveScrollWheelStepPixels(m_scrollWheelStepPixels);
            m_scrollTweenDurationSeconds = ResolveScrollTweenDurationSeconds(m_scrollTweenDurationSeconds);
            ElasticSettings().Validate();
        }
#endif

        private ScrollResult TryScrollX(float contentOffsetDelta,
            float stepPixels,
            bool tweenScroll,
            bool allowElastic,
            ScrollElasticSettings elasticSettings)
        {
            if (!horizontal || Mathf.Approximately(contentOffsetDelta, 0f))
            {
                return ScrollResult.Unhandled(contentOffsetDelta);
            }

            var maxScrollX = MaxScrollX();
            var currentScrollX = CurrentScrollX(maxScrollX);
            scrollTweenX.CancelIfExternallyMoved(currentScrollX);
            var deltaPixels = contentOffsetDelta * stepPixels;
            var effectiveScrollX = tweenScroll && scrollTweenX.IsActive()
                ? currentScrollX + scrollTweenX.PendingDeltaFrom(currentScrollX)
                : currentScrollX;
            if (allowElastic && (scrollElasticX.IsActive ||
                                 effectiveScrollX + deltaPixels < 0f ||
                                 effectiveScrollX + deltaPixels > maxScrollX))
            {
                if (!scrollElasticX.Apply(effectiveScrollX,
                        maxScrollX,
                        deltaPixels,
                        elasticSettings,
                        out var nextEffectiveScrollX))
                {
                    return ScrollResult.Unhandled(contentOffsetDelta);
                }

                ApplyElasticLogicalOffset(scrollTweenX,
                    currentScrollX,
                    effectiveScrollX,
                    nextEffectiveScrollX,
                    maxScrollX,
                    tweenScroll,
                    horizontal: true);
                return ScrollResult.Handled(0f);
            }

            if (tweenScroll)
            {
                var consumption = ScrollDeltaUtil.ConsumeOffsetDelta(effectiveScrollX,
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

        private ScrollResult TryScrollY(float contentOffsetDelta,
            float stepPixels,
            bool tweenScroll,
            bool allowElastic,
            ScrollElasticSettings elasticSettings)
        {
            if (!vertical || Mathf.Approximately(contentOffsetDelta, 0f))
            {
                return ScrollResult.Unhandled(contentOffsetDelta);
            }

            var maxScrollY = MaxScrollY();
            var currentScrollY = CurrentScrollY(maxScrollY);
            scrollTweenY.CancelIfExternallyMoved(currentScrollY);
            var deltaPixels = contentOffsetDelta * stepPixels;
            var effectiveScrollY = tweenScroll && scrollTweenY.IsActive()
                ? currentScrollY + scrollTweenY.PendingDeltaFrom(currentScrollY)
                : currentScrollY;
            if (allowElastic && (scrollElasticY.IsActive ||
                                 effectiveScrollY + deltaPixels < 0f ||
                                 effectiveScrollY + deltaPixels > maxScrollY))
            {
                if (!scrollElasticY.Apply(effectiveScrollY,
                        maxScrollY,
                        deltaPixels,
                        elasticSettings,
                        out var nextEffectiveScrollY))
                {
                    return ScrollResult.Unhandled(contentOffsetDelta);
                }

                ApplyElasticLogicalOffset(scrollTweenY,
                    currentScrollY,
                    effectiveScrollY,
                    nextEffectiveScrollY,
                    maxScrollY,
                    tweenScroll,
                    horizontal: false);
                return ScrollResult.Handled(0f);
            }

            if (tweenScroll)
            {
                var consumption = ScrollDeltaUtil.ConsumeOffsetDelta(effectiveScrollY,
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
            SettleElastic();
        }

        private void ApplyElasticLogicalOffset(ScrollTween tween,
            float currentOffset,
            float effectiveOffset,
            float nextEffectiveOffset,
            float maxOffset,
            bool tweenScroll,
            bool horizontal)
        {
            if (tweenScroll)
            {
                tween.ScrollBy(currentOffset, nextEffectiveOffset - effectiveOffset, maxOffset);
                return;
            }

            tween.Cancel();
            if (horizontal)
            {
                SetScrollX(nextEffectiveOffset, maxOffset);
            }
            else
            {
                SetScrollY(nextEffectiveOffset, maxOffset);
            }
        }

        private void ObserveElasticRelease(ScrollAxis axis,
            HybridScrollMetadata metadata,
            float releaseDelaySeconds)
        {
            var eventTime = Time.unscaledTimeAsDouble;
            if (axis == ScrollAxis.Horizontal || axis == ScrollAxis.Free)
            {
                ObserveElasticRelease(scrollElasticX,
                    scrollElasticReleaseX,
                    metadata,
                    releaseDelaySeconds,
                    eventTime);
            }
            if (axis == ScrollAxis.Vertical || axis == ScrollAxis.Free)
            {
                ObserveElasticRelease(scrollElasticY,
                    scrollElasticReleaseY,
                    metadata,
                    releaseDelaySeconds,
                    eventTime);
            }
        }

        private static void ObserveElasticRelease(ScrollElasticAxis elasticAxis,
            ScrollElasticReleasePolicy releasePolicy,
            HybridScrollMetadata metadata,
            float releaseDelaySeconds,
            double eventTime)
        {
            if (elasticAxis.IsActive)
            {
                releasePolicy.Observe(metadata, eventTime, releaseDelaySeconds);
            }
            else
            {
                releasePolicy.Cancel();
            }
        }

        private void TickElastic()
        {
            if (!HasElasticState())
            {
                return;
            }

            var settings = ElasticSettings();
            if (HybridInputRuntime.Diagnostics.ScrollCapability == HybridScrollCapability.Unsupported ||
                !settings.Enabled ||
                ScrollEventUtil.HasActiveParentScrollHandler(transform, parentScrollHandlerScratch))
            {
                SettleElastic();
                return;
            }

            SettleUnavailableElasticAxes();

            if (scrollElasticReleaseX.TryBeginReturn(Time.unscaledTimeAsDouble))
            {
                scrollElasticX.BeginReturn(settings);
            }
            if (scrollElasticReleaseY.TryBeginReturn(Time.unscaledTimeAsDouble))
            {
                scrollElasticY.BeginReturn(settings);
            }

            scrollElasticX.TickReturn(Time.unscaledDeltaTime, settings);
            scrollElasticY.TickReturn(Time.unscaledDeltaTime, settings);
        }

        private void ReleaseElasticImmediately()
        {
            var now = Time.unscaledTimeAsDouble;
            if (scrollElasticX.IsActive)
            {
                scrollElasticReleaseX.ReleaseImmediately(now);
            }
            if (scrollElasticY.IsActive)
            {
                scrollElasticReleaseY.ReleaseImmediately(now);
            }
        }

        private void SettleElastic()
        {
            scrollElasticReleaseX.Cancel();
            scrollElasticReleaseY.Cancel();
            scrollElasticX.Settle();
            scrollElasticY.Settle();
        }

        private bool HasElasticState()
        {
            return scrollElasticX.IsActive ||
                   scrollElasticY.IsActive ||
                   scrollElasticReleaseX.IsPending ||
                   scrollElasticReleaseY.IsPending;
        }

        private void SettleUnavailableElasticAxes()
        {
            scrollElasticX.SettleIfUnavailable(horizontal ? MaxScrollX() : 0f, scrollElasticReleaseX);
            scrollElasticY.SettleIfUnavailable(vertical ? MaxScrollY() : 0f, scrollElasticReleaseY);
        }

        private void RemoveVisualOffset()
        {
            var invalidated = false;
            if (appliedVisualContent != null && appliedVisualDelta != Vector2.zero)
            {
                var currentPosition = appliedVisualContent.anchoredPosition;
                var expectedPosition = appliedVisualBasePosition + appliedVisualDelta;
                var externallyMoved = currentPosition != expectedPosition;
                var contentReplaced = base.content != appliedVisualContent;
                appliedVisualContent.anchoredPosition = ResolveLogicalPositionAfterVisualOffset(currentPosition,
                    appliedVisualBasePosition,
                    appliedVisualDelta);
                invalidated = externallyMoved || contentReplaced;
            }

            appliedVisualContent = null;
            appliedVisualDelta = Vector2.zero;
            appliedVisualBasePosition = Vector2.zero;

            if (observedContent != base.content)
            {
                observedContent = base.content;
                hasObservedLogicalContentPosition = false;
                invalidated = true;
            }

            var target = base.content;
            if (target != null)
            {
                var currentLogicalPosition = target.anchoredPosition;
                if (hasObservedLogicalContentPosition &&
                    (scrollElasticX.IsActive || scrollElasticY.IsActive) &&
                    currentLogicalPosition != observedLogicalContentPosition)
                {
                    invalidated = true;
                }
                observedLogicalContentPosition = currentLogicalPosition;
                hasObservedLogicalContentPosition = true;
            }
            else
            {
                hasObservedLogicalContentPosition = false;
            }

            if (invalidated)
            {
                SettleElastic();
            }
        }

        internal static Vector2 ResolveLogicalPositionAfterVisualOffset(Vector2 currentPosition,
            Vector2 logicalPosition,
            Vector2 visualDelta)
        {
            return currentPosition == logicalPosition + visualDelta
                ? logicalPosition
                : currentPosition - visualDelta;
        }

        private void ApplyVisualOffset()
        {
            RemoveVisualOffset();
            if (!HasElasticState())
            {
                return;
            }

            if (!CustomElasticAvailable() ||
                ScrollEventUtil.HasActiveParentScrollHandler(transform, parentScrollHandlerScratch))
            {
                SettleElastic();
                return;
            }

            SettleUnavailableElasticAxes();
            if (!HasElasticState())
            {
                return;
            }

            var target = base.content;
            var delta = new Vector2(-scrollElasticX.Offset, scrollElasticY.Offset);
            if (target == null || delta == Vector2.zero)
            {
                return;
            }

            appliedVisualBasePosition = target.anchoredPosition;
            target.anchoredPosition = appliedVisualBasePosition + delta;
            appliedVisualContent = target;
            appliedVisualDelta = delta;
        }

        private ScrollElasticSettings ElasticSettings()
        {
            return scrollElastic;
        }

        private bool CustomElasticAvailable()
        {
            return ElasticSettings().Enabled &&
                   HybridInputRuntime.Diagnostics.ScrollCapability != HybridScrollCapability.Unsupported;
        }

        private bool CanUseCustomElastic()
        {
            return CustomElasticAvailable() &&
                   !ScrollEventUtil.HasActiveParentScrollHandler(transform, parentScrollHandlerScratch);
        }

        private Vector2 ResolveDragDelta(PointerEventData eventData)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(viewRect,
                    eventData.position,
                    eventData.pressEventCamera,
                    out var currentPoint) &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(viewRect,
                    eventData.position - eventData.delta,
                    eventData.pressEventCamera,
                    out var previousPoint))
            {
                return currentPoint - previousPoint;
            }

            return eventData.delta;
        }

        internal static Vector2 DragDeltaToContentOffset(Vector2 localDelta)
        {
            return new Vector2(-localDelta.x, localDelta.y);
        }

        private void EnsureSingleMotionOwner()
        {
            if (!ElasticSettings().Enabled)
            {
                RestoreMovementType();
                return;
            }

            if (!movementTypeOverridden)
            {
                savedMovementType = movementType;
                movementTypeOverridden = true;
            }
            else if (movementType != MovementType.Clamped)
            {
                savedMovementType = movementType;
            }

            movementType = MovementType.Clamped;
        }

        private void RestoreMovementType()
        {
            if (!movementTypeOverridden)
            {
                return;
            }

            movementType = savedMovementType;
            movementTypeOverridden = false;
        }

        private void EndBaseDragIfActive()
        {
            var dragEventData = activeDragEventData;
            if (dragEventData == null)
            {
                return;
            }

            base.OnEndDrag(dragEventData);
            activeDragEventData = null;
        }

        private float CurrentScrollX(float maxScrollX)
        {
            return FloatUtil.PercentToScrollOffset(base.horizontalNormalizedPosition, maxScrollX);
        }

        private float CurrentScrollY(float maxScrollY)
        {
            return FloatUtil.PercentToScrollOffset(1f - base.verticalNormalizedPosition, maxScrollY);
        }

        private void SetScrollX(float scrollOffset, float maxScrollX)
        {
            SetNormalizedPosition(FloatUtil.ScrollOffsetToPercent(scrollOffset, maxScrollX), 0);
            RecordLogicalContentPosition();
        }

        private void SetScrollY(float scrollOffset, float maxScrollY)
        {
            SetNormalizedPosition(1f - FloatUtil.ScrollOffsetToPercent(scrollOffset, maxScrollY), 1);
            RecordLogicalContentPosition();
        }

        private void RecordLogicalContentPosition()
        {
            var target = base.content;
            if (target == null)
            {
                hasObservedLogicalContentPosition = false;
                return;
            }

            observedLogicalContentPosition = target.anchoredPosition;
            hasObservedLogicalContentPosition = true;
        }

        private float MaxScrollX()
        {
            if (base.content == null)
            {
                return 0f;
            }

            UpdateBounds();
            return Mathf.Max(0f, m_ContentBounds.size.x - viewRect.rect.width);
        }

        private float MaxScrollY()
        {
            if (base.content == null)
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
