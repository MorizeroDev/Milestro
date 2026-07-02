using System;
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
        [NonSerialized] private string observedTexturePropertyName = DefaultTexturePropertyName;
        [NonSerialized] private bool rendererDisabledByPresenter;

        public RenderTextureProducer? Producer
        {
            get => m_producer;
            set
            {
                if (m_producer == value)
                {
                    return;
                }

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
            ApplyProducerOutput(force: true);
        }

        private void OnDisable()
        {
            var targetRenderer = MeshRendererComponent();
            if (targetRenderer != null)
            {
                targetRenderer.SetPropertyBlock(null);
                RestoreRendererIfDisabledByPresenter(targetRenderer);
            }

            ResetObservedOutput();
        }

        private void Reset()
        {
            meshRendererCache = GetComponent<MeshRenderer>();
            m_producer = GetComponent<RenderTextureProducer>();
        }

        private void Update()
        {
            ApplyProducerOutput(force: false);
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
                observedOutputVersion == outputVersion &&
                observedOutputTexture == outputTexture &&
                observedOutputUvRect == outputUvRect &&
                observedTexturePropertyName == texturePropertyName)
            {
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

            var block = PropertyBlock();
            targetRenderer.GetPropertyBlock(block);

            block.SetTexture(Shader.PropertyToID(texturePropertyName), outputTexture);
            block.SetVector(Shader.PropertyToID(texturePropertyName + "_ST"),
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
            observedTexturePropertyName = TexturePropertyNameOrDefault();
        }
    }
}
