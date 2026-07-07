using System;
using Milestro.Components.Internal;
using Milestro.Util;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif
using UnityEngine;
using UnityEngine.EventSystems;

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

        [NonSerialized] private TextBoxRenderTextureProducer? producerCache;
        [NonSerialized] private readonly ScrollTween scrollTweenX = new ScrollTween();
        [NonSerialized] private readonly ScrollTween scrollTweenY = new ScrollTween();
        [NonSerialized] private readonly ScrollAxisLock scrollAxisLock = new ScrollAxisLock();
        [NonSerialized] private long observedOutputVersion = long.MinValue;
        [NonSerialized] private TextBoxRenderTextureProducer? observedProducer;
#if UNITY_EDITOR
        [NonSerialized] private bool m_editorApplyQueued;
#endif

        protected override void OnEnable()
        {
            base.OnEnable();
            EnsureConfigured(forceText: true, forceApply: true);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            CancelScrollTweens();
            scrollAxisLock.Reset();
            SetObservedProducer(null);
            Texture = null;
            observedOutputVersion = long.MinValue;
        }

        private void Update()
        {
            var producer = ProducerComponent();
            producer.RebuildOutput(forceText: false);
            TickScrollTweens(producer);
            ApplyProducerOutput(producer, force: false);
        }

        protected override void Reset()
        {
            base.Reset();
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

            var producer = ProducerComponent();
            producer.RebuildOutput(forceText: false);
            ApplyProducerOutput(producer, force: false);
            scrollTweenX.CancelIfExternallyMoved(producer.scrollX);
            scrollTweenY.CancelIfExternallyMoved(producer.scrollY);
            var stepPixels = FloatUtil.IsFinite(m_scrollWheelStepPixels)
                ? Mathf.Max(1f, m_scrollWheelStepPixels)
                : DefaultScrollWheelStepPixels;

            var axis = scrollAxisLock.Resolve(eventData.scrollDelta,
                ScrollEventUtil.IsHorizontalScrollModifierDown(),
                out var contentOffsetDelta,
                out var lockedScrollDelta);
            if (axis == ScrollAxis.None)
            {
                ScrollEventUtil.PassScrollToParent(transform, eventData, lockedScrollDelta);
                return;
            }

            var shouldTweenPointerScroll = ShouldTweenPointerScroll(eventData.scrollDelta);
            var unusedScrollDelta = Vector2.zero;
            var consumed = false;
            if (axis == ScrollAxis.Horizontal || axis == ScrollAxis.Free)
            {
                if (TryScrollX(producer, contentOffsetDelta.x, stepPixels, shouldTweenPointerScroll))
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
                if (TryScrollY(producer, contentOffsetDelta.y, stepPixels, shouldTweenPointerScroll))
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
            eventData.Use();
        }

        public Vector2 GetScrollPercent()
        {
            var producer = ProducerComponent();
            producer.RebuildOutput(forceText: false);
            ApplyProducerOutput(producer, force: false);
            return new Vector2(ResolveScrollPercentX(producer), ResolveScrollPercentY(producer));
        }

        public float GetScrollPercentX()
        {
            var producer = ProducerComponent();
            producer.RebuildOutput(forceText: false);
            ApplyProducerOutput(producer, force: false);
            return ResolveScrollPercentX(producer);
        }

        public float GetScrollPercentY()
        {
            var producer = ProducerComponent();
            producer.RebuildOutput(forceText: false);
            ApplyProducerOutput(producer, force: false);
            return ResolveScrollPercentY(producer);
        }

        public void ScrollToPercent(Vector2 percent, bool animated = false)
        {
            var producer = ProducerComponent();
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
            var producer = ProducerComponent();
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
            var producer = ProducerComponent();
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
            if (!isActiveAndEnabled)
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

        private bool TryScrollX(TextBoxRenderTextureProducer producer,
            float contentOffsetDelta,
            float stepPixels,
            bool tweenScroll)
        {
            if (Mathf.Approximately(contentOffsetDelta, 0f))
            {
                return false;
            }

            var deltaPixels = contentOffsetDelta * stepPixels;
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
            bool tweenScroll)
        {
            if (Mathf.Approximately(contentOffsetDelta, 0f))
            {
                return false;
            }

            var deltaPixels = contentOffsetDelta * stepPixels;
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

        private void SetObservedProducer(TextBoxRenderTextureProducer? producer)
        {
            if (observedProducer == producer)
            {
                return;
            }

            if (observedProducer != null)
            {
                observedProducer.OutputChanged -= OnProducerOutputChanged;
            }

            observedProducer = producer;
            if (observedProducer != null)
            {
                observedProducer.OutputChanged += OnProducerOutputChanged;
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
    }
}
