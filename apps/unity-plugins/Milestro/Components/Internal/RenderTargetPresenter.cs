using System;
using UnityEngine;

namespace Milestro.Components.Internal
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("Milestro/Internal/Render Target Presenter")]
    public class RenderTargetPresenter : RenderTextureGraphic
    {
        [SerializeField] private RenderTargetProducer? m_producer;

        [NonSerialized] private long observedOutputVersion = long.MinValue;

        public RenderTargetProducer? Producer
        {
            get => m_producer;
            set
            {
                if (m_producer == value)
                {
                    return;
                }

                m_producer = value;
                observedOutputVersion = long.MinValue;
                ApplyProducerOutput(force: true);
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            observedOutputVersion = long.MinValue;
            ApplyProducerOutput(force: true);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Texture = null;
            observedOutputVersion = long.MinValue;
        }

        protected override void Reset()
        {
            base.Reset();
            m_producer = GetComponent<RenderTargetProducer>();
        }

        private void Update()
        {
            ApplyProducerOutput(force: false);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            observedOutputVersion = long.MinValue;
            if (isActiveAndEnabled)
            {
                ApplyProducerOutput(force: true);
            }
        }
#endif

        private void ApplyProducerOutput(bool force)
        {
            var producer = ResolveProducer();
            if (producer == null || !producer.HasOutput)
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

        private RenderTargetProducer? ResolveProducer()
        {
            if (m_producer != null)
            {
                return m_producer;
            }

            return GetComponent<RenderTargetProducer>();
        }
    }
}
