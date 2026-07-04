using System;
using System.Collections.Generic;
using Milestro.Model;
using Milestro.Skia;
using Milestro.Util;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Milestro.Components.Internal
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Milestro/Internal/Text Box Render Texture Producer")]
    public class TextBoxRenderTextureProducer : RenderTextureProducer
    {
        [TextArea(3, 10)]
        [SerializeField]
        private string m_content = "";

        [SerializeField]
        private RectOffset m_margin = new RectOffset();

        [SerializeField]
        private List<string> m_fontFamilies = new List<string>() { "Source Han Sans VF" };

        [SerializeField]
        private TextAlign m_textAlign = TextAlign.Start;

        [SerializeField]
        private TextDirection m_textDirection = TextDirection.Ltr;

        [SerializeField]
        private TextBoxWrapMode m_wrapMode = TextBoxWrapMode.Wrap;

        [SerializeField]
        private bool m_singleLine;

        [SerializeField]
        private TextOverflow m_textOverflow = TextOverflow.Clip;

        [SerializeField]
        private string m_ellipsisString = "\u2026";

        [SerializeField]
        private float m_size = 36;

        [SerializeField]
        [Range(0, 1000)]
        private int m_weight = 400;

        [SerializeField]
        private Color m_textColor = Color.white;

        [SerializeField]
        private string m_locale = "zh-Hans";

        [NonSerialized] private RectTransform? rectTransformCache;
        [NonSerialized] private TextBoxRenderTarget? renderTarget;
        [NonSerialized] private ColorSpace? m_colorSpaceOverride;
        [NonSerialized] private float m_scrollX;
        [NonSerialized] private float m_scrollY;
#if UNITY_EDITOR
        [NonSerialized] private bool m_editorRebuildQueued;
#endif

        public override Texture? OutputTexture => renderTarget?.OutputTexture;
        public override Rect OutputUvRect => renderTarget?.OutputUvRect ?? new Rect(0f, 0f, 1f, 1f);
        public override int OutputWidth => renderTarget?.OutputWidth ?? 0;
        public override int OutputHeight => renderTarget?.OutputHeight ?? 0;
        public override bool HasOutput => renderTarget?.HasOutput ?? false;
        public override long OutputVersion => renderTarget?.OutputVersion ?? 0;
        public float contentWidth => renderTarget?.ContentSize.x ?? 0f;
        public float contentHeight => renderTarget?.ContentSize.y ?? 0f;
        public float viewportWidth => renderTarget?.ViewportSize.x ?? 0f;
        public float viewportHeight => renderTarget?.ViewportSize.y ?? 0f;
        public float maxScrollX => renderTarget?.MaxScrollOffset.x ?? 0f;
        public float maxScrollY => renderTarget?.MaxScrollOffset.y ?? 0f;

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

        public TextBoxWrapMode wrapMode
        {
            get => m_wrapMode;
            set
            {
                m_wrapMode = value;
                MarkPropertiesChanged();
            }
        }

        public bool singleLine
        {
            get => m_singleLine;
            set
            {
                m_singleLine = value;
                MarkPropertiesChanged();
            }
        }

        public TextOverflow textOverflow
        {
            get => m_textOverflow;
            set
            {
                m_textOverflow = value;
                MarkPropertiesChanged();
            }
        }

        public string ellipsisString
        {
            get => m_ellipsisString;
            set
            {
                m_ellipsisString = value ?? "";
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

        public float scrollX
        {
            get => m_scrollX;
            set => SetScrollX(value);
        }

        public float scrollY
        {
            get => m_scrollY;
            set => SetScrollY(value);
        }

        public void ScrollByX(float delta)
        {
            if (Mathf.Approximately(delta, 0f))
            {
                return;
            }

            SetScrollX(m_scrollX + delta);
        }

        public void ScrollByY(float delta)
        {
            if (Mathf.Approximately(delta, 0f))
            {
                return;
            }

            SetScrollY(m_scrollY + delta);
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
            m_scrollX = FloatUtil.IsFinite(m_scrollX) ? Mathf.Max(0f, m_scrollX) : 0f;
            m_scrollY = FloatUtil.IsFinite(m_scrollY) ? Mathf.Max(0f, m_scrollY) : 0f;
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
            RenderTarget.Rebuild(CurrentSize(),
                SurfaceColorSpace(),
                CurrentSettings(),
                new Vector2(m_scrollX, m_scrollY),
                forceText,
                this);
            var nextScrollX = RenderTarget.ScrollOffset.x;
            if (!Mathf.Approximately(m_scrollX, nextScrollX))
            {
                m_scrollX = nextScrollX;
            }

            var nextScrollY = RenderTarget.ScrollOffset.y;
            if (!Mathf.Approximately(m_scrollY, nextScrollY))
            {
                m_scrollY = nextScrollY;
            }
        }

        public void RebuildOutput(bool forceText)
        {
            RebuildResources(forceText);
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
                m_wrapMode,
                m_singleLine,
                m_textOverflow,
                m_ellipsisString,
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

        private void SetScrollX(float value)
        {
            var nextScrollX = FloatUtil.IsFinite(value) ? Mathf.Max(0f, value) : 0f;
            if (Mathf.Approximately(m_scrollX, nextScrollX))
            {
                return;
            }

            m_scrollX = nextScrollX;
            RenderTarget.MarkPaintChanged();
        }

        private void SetScrollY(float value)
        {
            var nextScrollY = FloatUtil.IsFinite(value) ? Mathf.Max(0f, value) : 0f;
            if (Mathf.Approximately(m_scrollY, nextScrollY))
            {
                return;
            }

            m_scrollY = nextScrollY;
            RenderTarget.MarkPaintChanged();
        }

    }
}
