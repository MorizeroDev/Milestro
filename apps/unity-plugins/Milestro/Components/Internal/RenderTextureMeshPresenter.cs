using System;
#if UNITY_EDITOR
using UnityEditorInternal;
#endif
using UnityEngine;

namespace Milestro.Components.Internal
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshRenderer))]
    [AddComponentMenu("Milestro/Internal/Render Texture Mesh Presenter")]
    public class RenderTextureMeshPresenter : MonoBehaviour
    {
        private const string DefaultTexturePropertyName = "_MainTex";
        private static readonly Rect DefaultUvRect = new Rect(0f, 0f, 1f, 1f);

        [SerializeField] private RenderTextureProducer? m_producer;
        [SerializeField] private string m_texturePropertyName = DefaultTexturePropertyName;
        [SerializeField] private bool m_disableRendererWhenNoOutput = true;

        [NonSerialized] private MeshRenderer? meshRendererCache;
        [NonSerialized] private MaterialPropertyBlock? propertyBlock;
        [NonSerialized] private Texture? observedOutputTexture;
        [NonSerialized] private Rect observedOutputUvRect = DefaultUvRect;
        [NonSerialized] private long observedOutputVersion = long.MinValue;
        [NonSerialized] private long observedVisualVersion = long.MinValue;
        [NonSerialized] private string observedTexturePropertyName = DefaultTexturePropertyName;
        [NonSerialized] private bool rendererDisabledByPresenter;
        [NonSerialized] private RenderTextureProducer? observedProducer;
        [NonSerialized] private string cachedTexturePropertyName = "";
        [NonSerialized] private int cachedTexturePropertyId = -1;
        [NonSerialized] private int cachedTextureStPropertyId = -1;

        internal event Action<RenderTextureMeshPresenter>? VisualStateChanged;

        public RenderTextureProducer? Producer
        {
            get => m_producer;
            set
            {
                if (m_producer == value)
                {
                    return;
                }

                SetObservedProducer(value);
                m_producer = value;
                ResetObservedOutput();
                ApplyProducerOutput(force: true);
            }
        }

        public string TexturePropertyName
        {
            get => TexturePropertyNameOrDefault();
            set
            {
                var nextValue = string.IsNullOrEmpty(value) ? DefaultTexturePropertyName : value;
                if (m_texturePropertyName == nextValue)
                {
                    return;
                }

                m_texturePropertyName = nextValue;
                ResetObservedOutput();
                ApplyProducerOutput(force: true);
            }
        }

        public bool DisableRendererWhenNoOutput
        {
            get => m_disableRendererWhenNoOutput;
            set
            {
                if (m_disableRendererWhenNoOutput == value)
                {
                    return;
                }

                m_disableRendererWhenNoOutput = value;
                ApplyProducerOutput(force: true);
            }
        }

        private void OnEnable()
        {
            meshRendererCache = GetComponent<MeshRenderer>();
            ResetObservedOutput();
            SetObservedProducer(ResolveProducer());
            ApplyProducerOutput(force: true);
            VisualStateChanged?.Invoke(this);
        }

        private void OnDisable()
        {
            SetObservedProducer(null);
            var targetRenderer = MeshRendererComponent();
            if (targetRenderer != null)
            {
                targetRenderer.SetPropertyBlock(null);
                RestoreRendererIfDisabledByPresenter(targetRenderer);
            }

            ResetObservedOutput();
            VisualStateChanged?.Invoke(this);
        }

        private void Reset()
        {
            meshRendererCache = GetComponent<MeshRenderer>();
            m_producer = GetComponent<RenderTextureProducer>();
        }

        private void Update()
        {
            var profiling = SlimTextPipelineProfiler.Enabled;
            var phaseStart = profiling ? SlimTextPipelineProfiler.Timestamp : 0;
            if (profiling)
            {
                SlimTextPipelineProfiler.PresenterUpdateCalls++;
            }

            ApplyProducerOutput(force: false);

            if (profiling)
            {
                SlimTextPipelineProfiler.PresenterUpdateMs += SlimTextPipelineProfiler.ElapsedMs(phaseStart);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(m_texturePropertyName))
            {
                m_texturePropertyName = DefaultTexturePropertyName;
            }

            ResetObservedOutput();
            if (isActiveAndEnabled)
            {
                ApplyProducerOutput(force: true);
            }
        }
#endif

        private void ApplyProducerOutput(bool force)
        {
            var targetRenderer = MeshRendererComponent();
            if (targetRenderer == null)
            {
                ResetObservedOutput();
                return;
            }

            var producer = ResolveProducer();
            SetObservedProducer(producer);
            var outputTexture = producer != null && producer.HasOutput ? producer.OutputTexture : null;
            if (producer == null || outputTexture == null)
            {
                if (force || observedOutputTexture != null || observedOutputVersion != long.MinValue)
                {
                    ClearProducerOutput(targetRenderer);
                }

                return;
            }

            var outputVersion = producer.OutputVersion;
            var outputUvRect = producer.OutputUvRect;
            var texturePropertyName = TexturePropertyNameOrDefault();
            if (!force &&
                observedOutputTexture == outputTexture &&
                observedOutputUvRect == outputUvRect &&
                observedTexturePropertyName == texturePropertyName)
            {
                observedOutputVersion = outputVersion;
                return;
            }

            ApplyTextureOutput(targetRenderer, outputTexture, outputUvRect, texturePropertyName);
            observedOutputTexture = outputTexture;
            observedOutputUvRect = outputUvRect;
            observedOutputVersion = outputVersion;
            observedTexturePropertyName = texturePropertyName;
        }

        private void ApplyTextureOutput(MeshRenderer targetRenderer,
            Texture outputTexture,
            Rect outputUvRect,
            string texturePropertyName)
        {
            RestoreRendererIfDisabledByPresenter(targetRenderer);

            if (cachedTexturePropertyName != texturePropertyName)
            {
                cachedTexturePropertyName = texturePropertyName;
                cachedTexturePropertyId = Shader.PropertyToID(texturePropertyName);
                cachedTextureStPropertyId = Shader.PropertyToID(texturePropertyName + "_ST");
            }

            var block = PropertyBlock();
            targetRenderer.GetPropertyBlock(block);

            block.SetTexture(cachedTexturePropertyId, outputTexture);
            block.SetVector(cachedTextureStPropertyId,
                new Vector4(outputUvRect.width, outputUvRect.height, outputUvRect.x, outputUvRect.y));

            targetRenderer.SetPropertyBlock(block);
        }

        private void ClearProducerOutput(MeshRenderer targetRenderer)
        {
            targetRenderer.SetPropertyBlock(null);
            if (!m_disableRendererWhenNoOutput)
            {
                RestoreRendererIfDisabledByPresenter(targetRenderer);
                ResetObservedOutput();
                return;
            }

            if (targetRenderer.enabled)
            {
                targetRenderer.enabled = false;
                rendererDisabledByPresenter = true;
            }

            ResetObservedOutput();
        }

        private void RestoreRendererIfDisabledByPresenter(MeshRenderer targetRenderer)
        {
            if (!rendererDisabledByPresenter)
            {
                return;
            }

            targetRenderer.enabled = true;
            rendererDisabledByPresenter = false;
        }

        private RenderTextureProducer? ResolveProducer()
        {
            if (m_producer != null)
            {
                return m_producer;
            }

            return GetComponent<RenderTextureProducer>();
        }

        private void SetObservedProducer(RenderTextureProducer? producer)
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
            var producer = observedProducer ?? m_producer;
            var visualVersion = producer != null ? producer.VisualVersion : 0;
            if (visualVersion == observedVisualVersion)
            {
                return;
            }

            observedVisualVersion = visualVersion;
            VisualStateChanged?.Invoke(this);
#if UNITY_EDITOR
            if (!Application.isPlaying && this && isActiveAndEnabled)
            {
                ApplyProducerOutput(force: true);
                InternalEditorUtility.RepaintAllViews();
            }
#endif
        }

        private MeshRenderer? MeshRendererComponent()
        {
            if (meshRendererCache == null)
            {
                meshRendererCache = GetComponent<MeshRenderer>();
            }

            return meshRendererCache;
        }

        private MaterialPropertyBlock PropertyBlock()
        {
            var block = propertyBlock;
            if (block == null)
            {
                block = new MaterialPropertyBlock();
                propertyBlock = block;
            }

            return block;
        }

        private string TexturePropertyNameOrDefault()
        {
            return string.IsNullOrEmpty(m_texturePropertyName) ? DefaultTexturePropertyName : m_texturePropertyName;
        }

        private void ResetObservedOutput()
        {
            observedOutputTexture = null;
            observedOutputUvRect = DefaultUvRect;
            observedOutputVersion = long.MinValue;
            observedVisualVersion = long.MinValue;
            observedTexturePropertyName = TexturePropertyNameOrDefault();
        }
    }
}
