using System;
using System.Collections.Generic;
using Milestro.Model;
using Milestro.Skia;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Serialization;

namespace Milestro.Components.Internal
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Milestro/Internal/Text Box Render Target Producer")]
    public class TextBoxRenderTargetProducer : RenderTargetProducer
    {
        [TextArea(3, 10)]
        [SerializeField]
        [FormerlySerializedAs("content")]
        private string m_content = "";

        [SerializeField]
        [FormerlySerializedAs("margin")]
        private RectOffset m_margin = new RectOffset();

        [SerializeField]
        [FormerlySerializedAs("fontFamilies")]
        private List<string> m_fontFamilies = new List<string>() { "Source Han Sans VF" };

        [SerializeField]
        [FormerlySerializedAs("textAlign")]
        private TextAlign m_textAlign = TextAlign.Start;

        [SerializeField]
        private TextDirection m_textDirection = TextDirection.Ltr;

        [SerializeField]
        [FormerlySerializedAs("size")]
        private float m_size = 36;

        [SerializeField]
        [FormerlySerializedAs("weight")]
        [Range(0, 1000)]
        private int m_weight = 400;

        [SerializeField]
        [FormerlySerializedAs("color")]
        private Color m_textColor = Color.white;

        [SerializeField]
        [FormerlySerializedAs("locale")]
        private string m_locale = "zh-Hans";

        [NonSerialized] private RectTransform? rectTransformCache;
        [NonSerialized] private TextBoxRenderTarget? renderTarget;
        [NonSerialized] private ColorSpace? m_colorSpaceOverride;
#if UNITY_EDITOR
        [NonSerialized] private bool m_editorRebuildQueued;
#endif

        public override Texture? OutputTexture => renderTarget?.OutputTexture;
        public override Rect OutputUvRect => renderTarget?.OutputUvRect ?? new Rect(0f, 0f, 1f, 1f);
        public override int OutputWidth => renderTarget?.OutputWidth ?? 0;
        public override int OutputHeight => renderTarget?.OutputHeight ?? 0;
        public override bool HasOutput => renderTarget?.HasOutput ?? false;
        public override long OutputVersion => renderTarget?.OutputVersion ?? 0;

        public string content
        {
            get => m_content;
            set
            {
                m_content = value ?? "";
                MarkPropertiesChanged();
            }
        }

        public List<string> fontFamilies
        {
            get => m_fontFamilies;
            set
            {
                m_fontFamilies = value ?? new List<string>();
                MarkPropertiesChanged();
            }
        }

        public TextAlign textAlign
        {
            get => m_textAlign;
            set
            {
                m_textAlign = value;
                MarkPropertiesChanged();
            }
        }

        public TextDirection textDirection
        {
            get => m_textDirection;
            set
            {
                m_textDirection = value;
                MarkPropertiesChanged();
            }
        }

        public float size
        {
            get => m_size;
            set
            {
                m_size = value;
                MarkPropertiesChanged();
            }
        }

        public int weight
        {
            get => m_weight;
            set
            {
                m_weight = value;
                MarkPropertiesChanged();
            }
        }

        public Color textColor
        {
            get => m_textColor;
            set
            {
                m_textColor = value;
                MarkPropertiesChanged();
            }
        }

        public string locale
        {
            get => m_locale;
            set
            {
                m_locale = value ?? "";
                MarkPropertiesChanged();
            }
        }

        public RectOffset margin
        {
            get => m_margin;
            set
            {
                m_margin = value ?? new RectOffset();
                MarkPropertiesChanged();
            }
        }

        public bool srgb
        {
            get => SurfaceColorSpace() == ColorSpace.Linear;
            set
            {
                m_colorSpaceOverride = value ? ColorSpace.Linear : ColorSpace.Gamma;
                MarkPropertiesChanged();
            }
        }

        protected virtual void OnEnable()
        {
            rectTransformCache = GetComponent<RectTransform>();
            RebuildResources(forceText: true);
        }

        protected virtual void OnDisable()
        {
            renderTarget?.Dispose();
            renderTarget = null;
        }

        private void Update()
        {
            RebuildResources(forceText: false);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            MarkPropertiesChanged();
            if (isActiveAndEnabled)
            {
                QueueEditorRebuild();
            }
        }
#endif

        private void OnRectTransformDimensionsChange()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            MarkPropertiesChanged();
#if UNITY_EDITOR
            QueueEditorRebuild();
#endif
        }

#if UNITY_EDITOR
        private void QueueEditorRebuild()
        {
            if (Application.isPlaying || m_editorRebuildQueued)
            {
                return;
            }

            m_editorRebuildQueued = true;
            EditorApplication.delayCall += RebuildResourcesFromEditorDelayCall;
        }

        private void RebuildResourcesFromEditorDelayCall()
        {
            m_editorRebuildQueued = false;
            if (!this || !isActiveAndEnabled)
            {
                return;
            }

            RebuildResources(forceText: false);
        }
#endif

        private void RebuildResources(bool forceText)
        {
            RenderTarget.Rebuild(CurrentSize(), SurfaceColorSpace(), CurrentSettings(), forceText, this);
        }

        private Vector2Int CurrentSize()
        {
            var rectTransform = RectTransformComponent();
            if (rectTransform == null)
            {
                return Vector2Int.one;
            }

            var rect = rectTransform.rect;
            return new Vector2Int(Mathf.Max(1, Mathf.CeilToInt(rect.width)),
                Mathf.Max(1, Mathf.CeilToInt(rect.height)));
        }

        private ColorSpace SurfaceColorSpace()
        {
            return m_colorSpaceOverride ?? UnitySkiaRenderTextureDescriptor.DefaultColorSpace;
        }

        private TextBoxRenderTargetSettings CurrentSettings()
        {
            return new TextBoxRenderTargetSettings(m_content,
                m_margin,
                m_fontFamilies,
                m_textAlign,
                m_textDirection,
                m_size,
                m_weight,
                m_textColor,
                m_locale);
        }

        private RectTransform? RectTransformComponent()
        {
            if (rectTransformCache == null)
            {
                rectTransformCache = GetComponent<RectTransform>();
            }

            return rectTransformCache;
        }

        private TextBoxRenderTarget RenderTarget
        {
            get
            {
                if (renderTarget == null)
                {
                    renderTarget = new TextBoxRenderTarget();
                }

                return renderTarget;
            }
        }

        private void MarkPropertiesChanged()
        {
            RenderTarget.MarkPropertiesChanged();
        }
    }
}
