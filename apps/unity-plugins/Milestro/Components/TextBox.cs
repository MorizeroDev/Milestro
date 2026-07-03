using System;
using Milestro.Components.Internal;
using Milestro.Util;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.EventSystems;

namespace Milestro.Components
{
    [AddComponentMenu("Milestro/Text Box")]
    public class TextBox : RenderTextureGraphic, IScrollHandler
    {
        private const float DefaultScrollWheelStepPixels = 48f;
        private const float DefaultScrollTweenDurationSeconds = 0.14f;

        [SerializeField] private float m_scrollWheelStepPixels = DefaultScrollWheelStepPixels;
        [SerializeField] private bool m_smoothScroll = true;
        [SerializeField][Min(0f)] private float m_scrollTweenDurationSeconds = DefaultScrollTweenDurationSeconds;

        [NonSerialized] private TextBoxRenderTextureProducer? producerCache;
        [NonSerialized] private readonly ScrollTween scrollTween = new ScrollTween();
        [NonSerialized] private long observedOutputVersion = long.MinValue;
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
            scrollTween.Cancel();
            Texture = null;
            observedOutputVersion = long.MinValue;
        }

        private void Update()
        {
            var producer = ProducerComponent();
            producer.RebuildOutput(forceText: false);
            TickScrollTween(producer);
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
            m_scrollWheelStepPixels = IsFinite(m_scrollWheelStepPixels)
                ? Mathf.Max(1f, m_scrollWheelStepPixels)
                : DefaultScrollWheelStepPixels;
            m_scrollTweenDurationSeconds = IsFinite(m_scrollTweenDurationSeconds)
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
            scrollTween.Cancel();
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
            if (eventData == null || Mathf.Approximately(eventData.scrollDelta.y, 0f))
            {
                return;
            }

            var producer = ProducerComponent();
            producer.RebuildOutput(forceText: false);
            ApplyProducerOutput(producer, force: false);
            scrollTween.CancelIfExternallyMoved(producer.scrollY);
            var previousScrollY = scrollTween.IsActive ? scrollTween.Target : producer.scrollY;
            var stepPixels = IsFinite(m_scrollWheelStepPixels)
                ? Mathf.Max(1f, m_scrollWheelStepPixels)
                : DefaultScrollWheelStepPixels;
            var nextScrollY = Mathf.Clamp(previousScrollY - eventData.scrollDelta.y * stepPixels,
                0f,
                producer.maxScrollY);
            if (Mathf.Approximately(previousScrollY, nextScrollY))
            {
                if (scrollTween.IsActive)
                {
                    eventData.Use();
                }

                return;
            }

            ScrollToY(producer, nextScrollY);
            eventData.Use();
        }

        private void ScrollToY(TextBoxRenderTextureProducer producer, float nextScrollY)
        {
            if (!Application.isPlaying || !m_smoothScroll || ScrollTweenDurationSeconds() <= 0f)
            {
                scrollTween.Cancel();
                producer.scrollY = nextScrollY;
                producer.RebuildOutput(forceText: false);
                ApplyProducerOutput(producer, force: true);
                return;
            }

            scrollTween.Start(producer.scrollY, nextScrollY);
        }

        private void TickScrollTween(TextBoxRenderTextureProducer producer)
        {
            if (!scrollTween.IsActive)
            {
                return;
            }

            if (!scrollTween.Tick(producer.scrollY,
                    producer.maxScrollY,
                    Time.deltaTime,
                    ScrollTweenDurationSeconds(),
                    out var nextScrollY))
            {
                return;
            }

            producer.scrollY = nextScrollY;
            producer.RebuildOutput(forceText: false);
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
            return producerCache;
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

            Texture = outputTexture;
            UvRect = outputUvRect;
            observedOutputVersion = outputVersion;
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

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private float ScrollTweenDurationSeconds()
        {
            return IsFinite(m_scrollTweenDurationSeconds)
                ? Mathf.Max(0f, m_scrollTweenDurationSeconds)
                : DefaultScrollTweenDurationSeconds;
        }
    }
}
