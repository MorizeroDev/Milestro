using System;
using System.Collections.Generic;
using Milestro.Components.Internal;
using Milestro.Input;
using Milestro.Model;
using Milestro.Util;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Milestro.Components
{
    [AddComponentMenu("Milestro/Text Box")]
    public class TextBox : RenderTextureGraphic, IScrollHandler, ITextBoxScrollTarget
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

        [NonSerialized] private TextBoxRenderTextureProducer? producerCache;
        [NonSerialized] private readonly ScrollTween scrollTweenX = new ScrollTween();
        [NonSerialized] private readonly ScrollTween scrollTweenY = new ScrollTween();
        [NonSerialized] private readonly ScrollAxisLock scrollAxisLock = new ScrollAxisLock();
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
        [NonSerialized] private long observedOutputVersion = long.MinValue;
        [NonSerialized] private TextBoxRenderTextureProducer? observedProducer;
        [NonSerialized] private bool flowModeActive;
        [NonSerialized] private bool flowVisible;
        [NonSerialized] private float flowVisibleStartY;
        [NonSerialized] private float flowVisibleEndY;
        [NonSerialized] private float flowVisibleCapacityHeight;
#if MILESTRO_FLOW_TEXTBOX_DEBUG
        [NonSerialized] private bool flowDiagnosticsEnabled;
#endif
#if UNITY_EDITOR
        [NonSerialized] private bool m_editorApplyQueued;
#endif

        internal event Action? LayoutChanged;

        protected override void OnEnable()
        {
            base.OnEnable();
            ElasticSettings();
            EnsureConfigured(forceText: true, forceApply: true);
            LayoutChanged?.Invoke();
        }

        protected override void OnDisable()
        {
            SettleElastic(producerCache, rebuild: false);
            base.OnDisable();
            CancelScrollTweens();
            scrollAxisLock.Reset();
            SetObservedProducer(null);
            Texture = null;
            observedOutputVersion = long.MinValue;
            LayoutChanged?.Invoke();
        }

        private void Update()
        {
            var producer = ProducerComponent();
            producer.RebuildOutput(forceText: false);
            if (!flowModeActive)
            {
                TickScrollTweens(producer);
                TickElastic(producer);
            }
            ApplyProducerOutput(producer, force: false);
        }

        protected
#if UNITY_EDITOR
            override
#endif
            void Reset()
        {
#if UNITY_EDITOR
            base.Reset();
#endif
            EnsureConfigured(forceText: true, forceApply: true);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            m_scrollWheelStepPixels = FloatUtil.IsFinite(m_scrollWheelStepPixels)
                ? Mathf.Max(1f, m_scrollWheelStepPixels)
                : DefaultScrollWheelStepPixels;
            m_scrollTweenDurationSeconds = FloatUtil.IsFinite(m_scrollTweenDurationSeconds)
                ? Mathf.Max(0f, m_scrollTweenDurationSeconds)
                : DefaultScrollTweenDurationSeconds;
            ElasticSettings().Validate();
            if (this && gameObject != null)
            {
                EnsureConfigured(forceText: true, forceApply: true);
            }
        }
#endif

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            if (!isActiveAndEnabled)
            {
                return;
            }

            EnsureConfigured(forceText: false, forceApply: true);
            SettleElastic(producerCache, rebuild: false);
            CancelScrollTweens();
            scrollAxisLock.Reset();
            SetVerticesDirty();
#if UNITY_EDITOR
            QueueEditorApply();
#endif
        }

#if UNITY_EDITOR
        private void QueueEditorApply()
        {
            if (Application.isPlaying || m_editorApplyQueued)
            {
                return;
            }

            m_editorApplyQueued = true;
            EditorApplication.delayCall += ApplyProducerOutputFromEditorDelayCall;
        }

        private void ApplyProducerOutputFromEditorDelayCall()
        {
            m_editorApplyQueued = false;
            if (!this || !isActiveAndEnabled)
            {
                return;
            }

            EnsureConfigured(forceText: false, forceApply: true);
            InternalEditorUtility.RepaintAllViews();
        }
#endif

        private void EnsureConfigured(bool forceText, bool forceApply)
        {
            var producer = ProducerComponent();
            producer.RebuildOutput(forceText);
            ApplyProducerOutput(producer, forceApply);
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (eventData == null)
            {
                return;
            }

            if (flowModeActive)
            {
                SettleElastic(producerCache, rebuild: false);
                CancelScrollTweens();
                scrollAxisLock.Reset();
                ScrollEventUtil.PassScrollToParent(transform, eventData, eventData.scrollDelta);
                return;
            }

            if (ShouldForwardPointerScrollToParent())
            {
                SettleElastic(producerCache, rebuild: false);
                CancelScrollTweens();
                scrollAxisLock.Reset();
                ScrollEventUtil.PassScrollToParent(transform, eventData, eventData.scrollDelta);
                return;
            }

            var scrollInput = HybridInputRuntime.ResolveScrollInput(eventData);
            var producer = ProducerComponent();
            producer.RebuildOutput(forceText: false);
            ApplyProducerOutput(producer, force: false);
            scrollTweenX.CancelIfExternallyMoved(producer.scrollX);
            scrollTweenY.CancelIfExternallyMoved(producer.scrollY);
            var stepPixels = FloatUtil.IsFinite(m_scrollWheelStepPixels)
                ? Mathf.Max(1f, m_scrollWheelStepPixels)
                : DefaultScrollWheelStepPixels;

            var axis = scrollAxisLock.Resolve(scrollInput.Delta,
                ScrollEventUtil.IsHorizontalScrollModifierDown(),
                out var contentOffsetDelta,
                out var lockedScrollDelta);
            if (axis == ScrollAxis.None)
            {
                ScrollEventUtil.PassScrollToParent(transform, eventData, lockedScrollDelta);
                return;
            }

            var shouldTweenPointerScroll = ShouldTweenPointerScroll(scrollInput.Delta);
            var elasticSettings = ElasticSettings();
            var allowElastic = scrollInput.Metadata.Capability != HybridScrollCapability.Unsupported &&
                               elasticSettings.Enabled &&
                               !ScrollEventUtil.HasActiveParentScrollHandler(transform, parentScrollHandlerScratch);
            if (!allowElastic)
            {
                SettleElastic(producer, rebuild: true);
            }
            SettleUnavailableElasticAxes(producer);
            var unusedScrollDelta = Vector2.zero;
            var consumed = false;
            if (axis == ScrollAxis.Horizontal || axis == ScrollAxis.Free)
            {
                if (TryScrollX(producer,
                        contentOffsetDelta.x,
                        stepPixels,
                        shouldTweenPointerScroll,
                        allowElastic,
                        elasticSettings))
                {
                    consumed = true;
                }
                else
                {
                    unusedScrollDelta.x = lockedScrollDelta.x;
                }
            }

            if (axis == ScrollAxis.Vertical || axis == ScrollAxis.Free)
            {
                if (TryScrollY(producer,
                        contentOffsetDelta.y,
                        stepPixels,
                        shouldTweenPointerScroll,
                        allowElastic,
                        elasticSettings))
                {
                    consumed = true;
                }
                else
                {
                    unusedScrollDelta.y = lockedScrollDelta.y;
                }
            }

            if (!consumed)
            {
                ScrollEventUtil.PassScrollToParent(transform, eventData, lockedScrollDelta);
                return;
            }

            ScrollEventUtil.PassScrollToParent(transform, eventData, unusedScrollDelta);
            ObserveElasticRelease(axis, scrollInput.Metadata, elasticSettings.ReleaseDelaySeconds);
            eventData.Use();
        }

        public Vector2 GetScrollPercent()
        {
            if (flowModeActive)
            {
                return Vector2.zero;
            }

            var producer = ProducerComponent();
            producer.RebuildOutput(forceText: false);
            ApplyProducerOutput(producer, force: false);
            return new Vector2(ResolveScrollPercentX(producer), ResolveScrollPercentY(producer));
        }

        public float GetScrollPercentX()
        {
            if (flowModeActive)
            {
                return 0f;
            }

            var producer = ProducerComponent();
            producer.RebuildOutput(forceText: false);
            ApplyProducerOutput(producer, force: false);
            return ResolveScrollPercentX(producer);
        }

        public float GetScrollPercentY()
        {
            if (flowModeActive)
            {
                return 0f;
            }

            var producer = ProducerComponent();
            producer.RebuildOutput(forceText: false);
            ApplyProducerOutput(producer, force: false);
            return ResolveScrollPercentY(producer);
        }

        public void ScrollToPercent(Vector2 percent, bool animated = false)
        {
            if (flowModeActive)
            {
                return;
            }

            var producer = ProducerComponent();
            SettleElastic(producer, rebuild: false);
            producer.RebuildOutput(forceText: false);
            ApplyProducerOutput(producer, force: false);

            var shouldAnimate = animated && ShouldTweenScroll();
            var changed = SetScrollPercentX(producer, percent.x, shouldAnimate);
            changed |= SetScrollPercentY(producer, percent.y, shouldAnimate);
            if (changed)
            {
                producer.RebuildOutput(forceText: false);
                ApplyProducerOutput(producer, force: true);
            }
        }

        public void ScrollToPercentX(float percent, bool animated = false)
        {
            if (flowModeActive)
            {
                return;
            }

            var producer = ProducerComponent();
            SettleElastic(producer, rebuild: false);
            producer.RebuildOutput(forceText: false);
            ApplyProducerOutput(producer, force: false);
            if (!SetScrollPercentX(producer, percent, animated && ShouldTweenScroll()))
            {
                return;
            }

            producer.RebuildOutput(forceText: false);
            ApplyProducerOutput(producer, force: true);
        }

        public void ScrollToPercentY(float percent, bool animated = false)
        {
            if (flowModeActive)
            {
                return;
            }

            var producer = ProducerComponent();
            SettleElastic(producer, rebuild: false);
            producer.RebuildOutput(forceText: false);
            ApplyProducerOutput(producer, force: false);
            if (!SetScrollPercentY(producer, percent, animated && ShouldTweenScroll()))
            {
                return;
            }

            producer.RebuildOutput(forceText: false);
            ApplyProducerOutput(producer, force: true);
        }

        public bool TryGetScrollState(out TextBoxScrollState state)
        {
            if (!isActiveAndEnabled || flowModeActive)
            {
                state = default;
                return false;
            }

            var producer = ProducerComponent();
            producer.RebuildOutput(forceText: false);
            ApplyProducerOutput(producer, force: false);
            state = new TextBoxScrollState(producer.scrollX,
                producer.scrollY,
                producer.viewportWidth,
                producer.viewportHeight,
                producer.contentWidth,
                producer.contentHeight);
            return true;
        }

        internal void EnsureLayoutProducerObserved()
        {
            ProducerComponent();
        }

#if MILESTRO_FLOW_TEXTBOX_DEBUG
        internal void SetFlowDiagnosticsEnabled(bool enabled)
        {
            flowDiagnosticsEnabled = enabled;
            if (producerCache != null)
            {
                producerCache.flowDiagnosticsEnabled = enabled;
            }
        }
#endif

        internal void SetFlowModeActive(bool active)
        {
            if (flowModeActive == active)
            {
                return;
            }

            flowModeActive = active;
            SettleElastic(producerCache, rebuild: false);
            flowVisible = false;
            flowVisibleStartY = 0f;
            flowVisibleEndY = 0f;
            flowVisibleCapacityHeight = 0f;
            CancelScrollTweens();
            scrollAxisLock.Reset();

            var producer = ProducerComponent();
            producer.flowMode = active;
#if MILESTRO_FLOW_TEXTBOX_DEBUG
            producer.flowDiagnosticsEnabled = flowDiagnosticsEnabled;
#endif
            if (!active)
            {
                producer.ClearVisibleRange();
            }

            producer.RebuildOutput(forceText: false);
            ApplyProducerOutput(producer, force: true);
            SetVerticesDirty();
            SetMaterialDirty();
        }

        internal void SetFlowVisibleRange(bool visible, float localStartY, float localEndY)
        {
            SetFlowVisibleRange(visible, localStartY, localEndY, localEndY - localStartY);
        }

        internal void SetFlowVisibleRange(bool visible,
            float localStartY,
            float localEndY,
            float visibleCapacityHeight)
        {
            if (!flowModeActive)
            {
                return;
            }

            var nextStartY = 0f;
            var nextEndY = 0f;
            var nextVisible = visible &&
                              TextBoxFlowVisibleRange.TryNormalize(localStartY,
                                  localEndY,
                                  out nextStartY,
                                  out nextEndY);
            if (!nextVisible)
            {
                nextStartY = 0f;
                nextEndY = 0f;
                visibleCapacityHeight = 0f;
            }

            var geometryChanged = flowVisible != nextVisible ||
                                  !Mathf.Approximately(flowVisibleStartY, nextStartY) ||
                                  !Mathf.Approximately(flowVisibleEndY, nextEndY);
            flowVisible = nextVisible;
            flowVisibleStartY = nextStartY;
            flowVisibleEndY = nextEndY;
            flowVisibleCapacityHeight = visibleCapacityHeight;

            var producer = ProducerComponent();
#if MILESTRO_FLOW_TEXTBOX_DEBUG
            producer.flowDiagnosticsEnabled = flowDiagnosticsEnabled;
#endif
            var producerChanged = false;
            if (nextVisible)
            {
                producerChanged = producer.SetVisibleRange(nextStartY, nextEndY, visibleCapacityHeight);
            }
            else
            {
                producerChanged = producer.ClearVisibleRange();
            }

            DebugFlow("set visible range " +
                      $"inputVisible={visible} input=[{localStartY:F3},{localEndY:F3}] " +
                      $"nextVisible={nextVisible} next=[{nextStartY:F3},{nextEndY:F3}] capacity={flowVisibleCapacityHeight:F3} " +
                      $"geometryChanged={geometryChanged} producerChanged={producerChanged} " +
                      $"producerHasOutput={producer.HasOutput} producerOutput={producer.OutputWidth}x{producer.OutputHeight}");

            if (!geometryChanged && !producerChanged)
            {
                return;
            }

            producer.RebuildOutput(forceText: false);
            ApplyProducerOutput(producer, force: true);
            DebugFlow("after rebuild " +
                      $"flowVisible={flowVisible} flowRange=[{flowVisibleStartY:F3},{flowVisibleEndY:F3}] capacity={flowVisibleCapacityHeight:F3} " +
                      $"texture={(Texture != null ? "set" : "null")} " +
                      $"producerHasOutput={producer.HasOutput} producerOutput={producer.OutputWidth}x{producer.OutputHeight}");
            if (geometryChanged)
            {
                SetVerticesDirty();
                SetMaterialDirty();
            }
        }

        internal bool TryGetLayoutMeasurement(out TextBoxLayoutMeasurement measurement)
        {
            if (!isActiveAndEnabled)
            {
                measurement = default;
                return false;
            }

            var producer = ProducerComponent();
            if (!producer.TryGetLayoutMeasurement(out measurement))
            {
                return false;
            }

            ApplyProducerOutput(producer, force: false);
            return true;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            if (!flowModeActive)
            {
                base.OnPopulateMesh(vh);
                return;
            }

            PopulateFlowMesh(vh);
        }

        public override bool Raycast(Vector2 sp, Camera eventCamera)
        {
            if (flowModeActive && !FlowVisibleRangeContainsScreenPoint(sp, eventCamera))
            {
                return false;
            }

            return base.Raycast(sp, eventCamera);
        }

        private bool TryScrollX(TextBoxRenderTextureProducer producer,
            float contentOffsetDelta,
            float stepPixels,
            bool tweenScroll,
            bool allowElastic,
            ScrollElasticSettings elasticSettings)
        {
            if (Mathf.Approximately(contentOffsetDelta, 0f))
            {
                return false;
            }

            var deltaPixels = contentOffsetDelta * stepPixels;
            var currentScrollX = producer.scrollX;
            var effectiveScrollX = tweenScroll && scrollTweenX.IsActive()
                ? currentScrollX + scrollTweenX.PendingDeltaFrom(currentScrollX)
                : currentScrollX;
            if (allowElastic && (scrollElasticX.IsActive ||
                                 effectiveScrollX + deltaPixels < 0f ||
                                 effectiveScrollX + deltaPixels > producer.maxScrollX))
            {
                if (!scrollElasticX.Apply(effectiveScrollX,
                        producer.maxScrollX,
                        deltaPixels,
                        elasticSettings,
                        out var nextEffectiveScrollX))
                {
                    return false;
                }

                ApplyElasticLogicalOffset(scrollTweenX,
                    producer,
                    currentScrollX,
                    effectiveScrollX,
                    nextEffectiveScrollX,
                    producer.maxScrollX,
                    tweenScroll,
                    horizontal: true);
                ApplyElasticOffset(producer);
                producer.RebuildOutput(forceText: false);
                ApplyProducerOutput(producer, force: true);
                return true;
            }

            if (tweenScroll)
            {
                return scrollTweenX.ScrollBy(producer.scrollX, deltaPixels, producer.maxScrollX) ||
                       scrollTweenX.IsActive();
            }

            scrollTweenX.Cancel();
            var nextScrollX = Mathf.Clamp(producer.scrollX + deltaPixels, 0f, producer.maxScrollX);
            if (Mathf.Approximately(producer.scrollX, nextScrollX))
            {
                return false;
            }

            producer.scrollX = nextScrollX;
            producer.RebuildOutput(forceText: false);
            ApplyProducerOutput(producer, force: true);
            return true;
        }

        private bool TryScrollY(TextBoxRenderTextureProducer producer,
            float contentOffsetDelta,
            float stepPixels,
            bool tweenScroll,
            bool allowElastic,
            ScrollElasticSettings elasticSettings)
        {
            if (Mathf.Approximately(contentOffsetDelta, 0f))
            {
                return false;
            }

            var deltaPixels = contentOffsetDelta * stepPixels;
            var currentScrollY = producer.scrollY;
            var effectiveScrollY = tweenScroll && scrollTweenY.IsActive()
                ? currentScrollY + scrollTweenY.PendingDeltaFrom(currentScrollY)
                : currentScrollY;
            if (allowElastic && (scrollElasticY.IsActive ||
                                 effectiveScrollY + deltaPixels < 0f ||
                                 effectiveScrollY + deltaPixels > producer.maxScrollY))
            {
                if (!scrollElasticY.Apply(effectiveScrollY,
                        producer.maxScrollY,
                        deltaPixels,
                        elasticSettings,
                        out var nextEffectiveScrollY))
                {
                    return false;
                }

                ApplyElasticLogicalOffset(scrollTweenY,
                    producer,
                    currentScrollY,
                    effectiveScrollY,
                    nextEffectiveScrollY,
                    producer.maxScrollY,
                    tweenScroll,
                    horizontal: false);
                ApplyElasticOffset(producer);
                producer.RebuildOutput(forceText: false);
                ApplyProducerOutput(producer, force: true);
                return true;
            }

            if (tweenScroll)
            {
                return scrollTweenY.ScrollBy(producer.scrollY, deltaPixels, producer.maxScrollY) ||
                       scrollTweenY.IsActive();
            }

            scrollTweenY.Cancel();
            var nextScrollY = Mathf.Clamp(producer.scrollY + deltaPixels, 0f, producer.maxScrollY);
            if (Mathf.Approximately(producer.scrollY, nextScrollY))
            {
                return false;
            }

            producer.scrollY = nextScrollY;
            producer.RebuildOutput(forceText: false);
            ApplyProducerOutput(producer, force: true);
            return true;
        }

        private static float ResolveScrollPercentX(TextBoxRenderTextureProducer producer)
        {
            return FloatUtil.ScrollOffsetToPercent(producer.scrollX, producer.maxScrollX);
        }

        private static float ResolveScrollPercentY(TextBoxRenderTextureProducer producer)
        {
            return FloatUtil.ScrollOffsetToPercent(producer.scrollY, producer.maxScrollY);
        }

        private bool SetScrollPercentX(TextBoxRenderTextureProducer producer, float percent, bool animated)
        {
            var nextScrollX = FloatUtil.PercentToScrollOffset(percent, producer.maxScrollX);
            if (!scrollTweenX.ScrollTo(producer.scrollX,
                    nextScrollX,
                    producer.maxScrollX,
                    out nextScrollX,
                    animated))
            {
                return false;
            }

            producer.scrollX = nextScrollX;
            return true;
        }

        private bool SetScrollPercentY(TextBoxRenderTextureProducer producer, float percent, bool animated)
        {
            var nextScrollY = FloatUtil.PercentToScrollOffset(percent, producer.maxScrollY);
            if (!scrollTweenY.ScrollTo(producer.scrollY,
                    nextScrollY,
                    producer.maxScrollY,
                    out nextScrollY,
                    animated))
            {
                return false;
            }

            producer.scrollY = nextScrollY;
            return true;
        }

        private void TickScrollTweens(TextBoxRenderTextureProducer producer)
        {
            var changed = false;
            if (TickScrollTween(scrollTweenX,
                    producer.scrollX,
                    producer.maxScrollX,
                    out var nextScrollX))
            {
                producer.scrollX = nextScrollX;
                changed = true;
            }

            if (TickScrollTween(scrollTweenY,
                    producer.scrollY,
                    producer.maxScrollY,
                    out var nextScrollY))
            {
                producer.scrollY = nextScrollY;
                changed = true;
            }

            if (changed)
            {
                producer.RebuildOutput(forceText: false);
            }
        }

        private bool TickScrollTween(ScrollTween tween, float currentValue, float maxValue, out float nextValue)
        {
            return tween.Tick(currentValue,
                    maxValue,
                    Time.deltaTime,
                    out nextValue);
        }

        private bool ShouldTweenScroll()
        {
            return Application.isPlaying && m_smoothScroll && ScrollTweenDurationSeconds() > 0f;
        }

        private bool ShouldTweenPointerScroll(Vector2 scrollDelta)
        {
            return ShouldTweenScroll() &&
                   !ScrollEventUtil.ShouldBypassTweenForPointerScroll(scrollDelta);
        }

        private void CancelScrollTweens()
        {
            scrollTweenX.Cancel();
            scrollTweenY.Cancel();
        }

        private static void ApplyElasticLogicalOffset(ScrollTween tween,
            TextBoxRenderTextureProducer producer,
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
                producer.scrollX = nextEffectiveOffset;
            }
            else
            {
                producer.scrollY = nextEffectiveOffset;
            }
        }

        private void ObserveElasticRelease(ScrollAxis axis,
            HybridScrollMetadata metadata,
            float releaseDelaySeconds)
        {
            if (axis == ScrollAxis.Horizontal || axis == ScrollAxis.Free)
            {
                ObserveElasticRelease(scrollElasticX,
                    scrollElasticReleaseX,
                    metadata,
                    releaseDelaySeconds,
                    Time.unscaledTimeAsDouble);
            }
            if (axis == ScrollAxis.Vertical || axis == ScrollAxis.Free)
            {
                ObserveElasticRelease(scrollElasticY,
                    scrollElasticReleaseY,
                    metadata,
                    releaseDelaySeconds,
                    Time.unscaledTimeAsDouble);
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

        private void TickElastic(TextBoxRenderTextureProducer producer)
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
                SettleElastic(producer, rebuild: true);
                return;
            }

            SettleUnavailableElasticAxes(producer);

            if (scrollElasticReleaseX.TryBeginReturn(Time.unscaledTimeAsDouble))
            {
                scrollElasticX.BeginReturn(settings);
            }
            if (scrollElasticReleaseY.TryBeginReturn(Time.unscaledTimeAsDouble))
            {
                scrollElasticY.BeginReturn(settings);
            }

            var changed = scrollElasticX.TickReturn(Time.unscaledDeltaTime, settings);
            changed |= scrollElasticY.TickReturn(Time.unscaledDeltaTime, settings);
            var nextVisualOffset = new Vector2(scrollElasticX.Offset, scrollElasticY.Offset);
            if (!changed && producer.visualScrollOffset == nextVisualOffset)
            {
                return;
            }

            producer.visualScrollOffset = nextVisualOffset;
            producer.RebuildOutput(forceText: false);
        }

        private ScrollElasticSettings ElasticSettings()
        {
            return scrollElastic;
        }

        private void SettleElastic(TextBoxRenderTextureProducer? producer, bool rebuild)
        {
            scrollElasticReleaseX.Cancel();
            scrollElasticReleaseY.Cancel();
            var changed = scrollElasticX.Settle();
            changed |= scrollElasticY.Settle();
            if (producer == null || (!changed && producer.visualScrollOffset == Vector2.zero))
            {
                return;
            }

            producer.visualScrollOffset = Vector2.zero;
            if (rebuild && producer.isActiveAndEnabled)
            {
                producer.RebuildOutput(forceText: false);
                ApplyProducerOutput(producer, force: true);
            }
        }

        private void ApplyElasticOffset(TextBoxRenderTextureProducer producer)
        {
            producer.visualScrollOffset = new Vector2(scrollElasticX.Offset, scrollElasticY.Offset);
        }

        private bool HasElasticState()
        {
            return scrollElasticX.IsActive ||
                   scrollElasticY.IsActive ||
                   scrollElasticReleaseX.IsPending ||
                   scrollElasticReleaseY.IsPending;
        }

        private void SettleUnavailableElasticAxes(TextBoxRenderTextureProducer producer)
        {
            if (!SettleElasticAxesForRange(producer.maxScrollX, producer.maxScrollY))
            {
                return;
            }

            ApplyElasticOffset(producer);
            producer.RebuildOutput(forceText: false);
            ApplyProducerOutput(producer, force: true);
        }

        internal bool SettleElasticAxesForRange(float maxScrollX, float maxScrollY)
        {
            var changed = scrollElasticX.SettleIfUnavailable(maxScrollX, scrollElasticReleaseX);
            changed |= scrollElasticY.SettleIfUnavailable(maxScrollY, scrollElasticReleaseY);
            return changed;
        }

        private TextBoxRenderTextureProducer ProducerComponent()
        {
            if (producerCache == null)
            {
                producerCache = GetComponent<TextBoxRenderTextureProducer>();
                if (producerCache == null)
                {
                    producerCache = gameObject.AddComponent<TextBoxRenderTextureProducer>();
                    MarkWrapperDirty();
                }
            }

            ShowConfigurationComponent(producerCache);
            SetObservedProducer(producerCache);
            return producerCache;
        }

        private bool ShouldForwardPointerScrollToParent()
        {
            var layoutElement = GetComponent<TextBoxLayoutElement>();
            return layoutElement != null && layoutElement.ShouldForwardPointerScrollToParent();
        }

        private void SetObservedProducer(TextBoxRenderTextureProducer? producer)
        {
            if (observedProducer == producer)
            {
                return;
            }

            if (observedProducer != null)
            {
                observedProducer.OutputChanged -= OnProducerOutputChanged;
                observedProducer.LayoutChanged -= OnProducerLayoutChanged;
            }

            observedProducer = producer;
            if (observedProducer != null)
            {
                observedProducer.OutputChanged += OnProducerOutputChanged;
                observedProducer.LayoutChanged += OnProducerLayoutChanged;
            }
        }

        private void OnProducerOutputChanged()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && this && isActiveAndEnabled)
            {
                QueueEditorApply();
            }
#endif
        }

        private void OnProducerLayoutChanged()
        {
            LayoutChanged?.Invoke();
        }

        private void ApplyProducerOutput(TextBoxRenderTextureProducer producer, bool force)
        {
            if (!producer.HasOutput)
            {
                if (force || Texture != null)
                {
                    Texture = null;
                    UvRect = new Rect(0f, 0f, 1f, 1f);
                }

                observedOutputVersion = long.MinValue;
                return;
            }

            var outputVersion = producer.OutputVersion;
            var outputTexture = producer.OutputTexture;
            var outputUvRect = producer.OutputUvRect;
            if (!force && observedOutputVersion == outputVersion && Texture == outputTexture && UvRect == outputUvRect)
            {
                return;
            }

            var textureAlreadyApplied = Texture == outputTexture;
            var uvAlreadyApplied = UvRect == outputUvRect;
            Texture = outputTexture;
            UvRect = outputUvRect;
            observedOutputVersion = outputVersion;
            if (textureAlreadyApplied && uvAlreadyApplied)
            {
                SetVerticesDirty();
                SetMaterialDirty();
            }
        }

        private static void ShowConfigurationComponent(Component configurationComponent)
        {
            var nextHideFlags = configurationComponent.hideFlags & ~HideFlags.HideInInspector;
            if (configurationComponent.hideFlags == nextHideFlags)
            {
                return;
            }

            configurationComponent.hideFlags = nextHideFlags;
            MarkProducerDirty(configurationComponent);
        }

        private void MarkWrapperDirty()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(this);
            }
#endif
        }

        private static void MarkProducerDirty(UnityEngine.Object target)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(target);
            }
#endif
        }

        private float ScrollTweenDurationSeconds()
        {
            return FloatUtil.IsFinite(m_scrollTweenDurationSeconds)
                ? Mathf.Max(0f, m_scrollTweenDurationSeconds)
                : DefaultScrollTweenDurationSeconds;
        }

        private void PopulateFlowMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!flowVisible || flowVisibleEndY <= flowVisibleStartY || Texture == null)
            {
                DebugFlow("mesh skip before texture " +
                          $"flowVisible={flowVisible} range=[{flowVisibleStartY:F3},{flowVisibleEndY:F3}] " +
                          $"texture={(Texture != null ? "set" : "null")}");
                return;
            }

            var targetTexture = mainTexture;
            if (targetTexture == null)
            {
                DebugFlow("mesh skip null main texture");
                return;
            }

            var rect = GetPixelAdjustedRect();
            var top = Mathf.Clamp(rect.yMax - flowVisibleStartY, rect.yMin, rect.yMax);
            var bottom = Mathf.Clamp(rect.yMax - flowVisibleEndY, rect.yMin, rect.yMax);
            if (bottom >= top)
            {
                DebugFlow("mesh skip empty bounds " +
                          $"range=[{flowVisibleStartY:F3},{flowVisibleEndY:F3}] " +
                          $"rect=({rect.xMin:F3},{rect.yMin:F3},{rect.width:F3},{rect.height:F3}) " +
                          $"top={top:F3} bottom={bottom:F3}");
                return;
            }

            var bounds = new Vector4(rect.xMin, bottom, rect.xMax, top);
            var texelSize = targetTexture.texelSize;
            var scaleX = targetTexture.width * texelSize.x;
            var scaleY = targetTexture.height * texelSize.y;
            var color32 = color;
            var uvRect = UvRect;
            DebugFlow("mesh submit " +
                      $"range=[{flowVisibleStartY:F3},{flowVisibleEndY:F3}] " +
                      $"bounds=({bounds.x:F3},{bounds.y:F3},{bounds.z:F3},{bounds.w:F3}) " +
                      $"texture={targetTexture.width}x{targetTexture.height} uv={uvRect}");

            vh.AddVert(new Vector3(bounds.x, bounds.y), color32,
                new Vector4(uvRect.xMin * scaleX, uvRect.yMin * scaleY));
            vh.AddVert(new Vector3(bounds.x, bounds.w), color32,
                new Vector4(uvRect.xMin * scaleX, uvRect.yMax * scaleY));
            vh.AddVert(new Vector3(bounds.z, bounds.w), color32,
                new Vector4(uvRect.xMax * scaleX, uvRect.yMax * scaleY));
            vh.AddVert(new Vector3(bounds.z, bounds.y), color32,
                new Vector4(uvRect.xMax * scaleX, uvRect.yMin * scaleY));

            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }

        private bool FlowVisibleRangeContainsScreenPoint(Vector2 screenPoint, Camera? eventCamera)
        {
            if (!flowVisible || flowVisibleEndY <= flowVisibleStartY)
            {
                return false;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,
                    screenPoint,
                    eventCamera,
                    out var localPoint))
            {
                return false;
            }

            var rect = rectTransform.rect;
            var top = Mathf.Clamp(rect.yMax - flowVisibleStartY, rect.yMin, rect.yMax);
            var bottom = Mathf.Clamp(rect.yMax - flowVisibleEndY, rect.yMin, rect.yMax);
            return localPoint.x >= rect.xMin &&
                   localPoint.x <= rect.xMax &&
                   localPoint.y >= bottom &&
                   localPoint.y <= top;
        }

        [System.Diagnostics.Conditional("MILESTRO_FLOW_TEXTBOX_DEBUG")]
        private void DebugFlow(string message)
        {
#if MILESTRO_FLOW_TEXTBOX_DEBUG
            if (!flowDiagnosticsEnabled)
            {
                return;
            }

            Debug.Log($"[Milestro FlowTextBox][TextBox:{name}] {message}", this);
#endif
        }

    }
}
