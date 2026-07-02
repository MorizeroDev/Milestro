using System;
using Milestro.Components.Internal;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Milestro.Components
{
    [AddComponentMenu("Milestro/Text Box")]
    public class TextBox : RenderTextureGraphic
    {
        [NonSerialized] private TextBoxRenderTextureProducer? producerCache;
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
            Texture = null;
            observedOutputVersion = long.MinValue;
        }

        private void Update()
        {
            EnsureConfigured(forceText: false, forceApply: false);
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
    }
}
