using System;
using System.Collections.Generic;
using Milestro.Extensions;
using Milestro.Model;
using Milestro.Skia;
using Milestro.Skia.TextLayout;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Serialization;

namespace Milestro.Components
{
    [DisallowMultipleComponent]
    public class SkParagraphTextBoxRenderTargetProducer : SkiaRenderTargetProducer
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
        [FormerlySerializedAs("color")]
        private Color m_textColor = Color.white;

        [SerializeField]
        [FormerlySerializedAs("locale")]
        private string m_locale = "zh-Hans";

        [NonSerialized] private RectTransform? rectTransformCache;
        [NonSerialized] private Paragraph? paragraph;
        [NonSerialized] private ColorSpace? m_colorSpaceOverride;
        [NonSerialized] private bool m_havePropertiesChanged = true;
#if UNITY_EDITOR
        [NonSerialized] private bool m_editorRebuildQueued;
#endif
        [NonSerialized] private int layoutWidth = 640;

        public string content
        {
            get => m_content;
            set
            {
                m_content = value ?? "";
                m_havePropertiesChanged = true;
            }
        }

        public List<string> fontFamilies
        {
            get => m_fontFamilies;
            set
            {
                m_fontFamilies = value ?? new List<string>();
                m_havePropertiesChanged = true;
            }
        }

        public TextAlign textAlign
        {
            get => m_textAlign;
            set
            {
                m_textAlign = value;
                m_havePropertiesChanged = true;
            }
        }

        public TextDirection textDirection
        {
            get => m_textDirection;
            set
            {
                m_textDirection = value;
                m_havePropertiesChanged = true;
            }
        }

        public float size
        {
            get => m_size;
            set
            {
                m_size = value;
                m_havePropertiesChanged = true;
            }
        }

        public Color textColor
        {
            get => m_textColor;
            set
            {
                m_textColor = value;
                m_havePropertiesChanged = true;
            }
        }

        public string locale
        {
            get => m_locale;
            set
            {
                m_locale = value ?? "";
                m_havePropertiesChanged = true;
            }
        }

        public bool srgb
        {
            get => SurfaceColorSpace() == ColorSpace.Linear;
            set
            {
                m_colorSpaceOverride = value ? ColorSpace.Linear : ColorSpace.Gamma;
                m_havePropertiesChanged = true;
            }
        }

        protected virtual void OnEnable()
        {
            rectTransformCache = GetComponent<RectTransform>();
            RebuildResources(forceText: true);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            paragraph = null;
            m_havePropertiesChanged = true;
        }

        private void Update()
        {
            RebuildResources(forceText: false);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            m_havePropertiesChanged = true;
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

            m_havePropertiesChanged = true;
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
            var needsDraw = false;
            var sizePixels = CurrentSize();
            var propertiesChanged = m_havePropertiesChanged;
            if (EnsureRenderTarget(sizePixels, SurfaceColorSpace()))
            {
                needsDraw = true;
            }

            if (forceText || paragraph == null || propertiesChanged)
            {
                paragraph = BuildParagraph(content);
                needsDraw = true;
            }

            if (needsDraw)
            {
                ValidateMargin();
                ResizeParagraph(paragraph);
                if (!TrySubmitRenderCommands(BuildRenderCommands()))
                {
                    m_havePropertiesChanged = true;
                    return;
                }
            }

            m_havePropertiesChanged = false;
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

        protected virtual Paragraph BuildParagraph(string text)
        {
            ParagraphStyle paragraphStyle = new ParagraphStyle();
            paragraphStyle.TextAlign = (int)textAlign;
            paragraphStyle.TextDirection = (int)textDirection;

            TextStyle textStyle = new TextStyle();
            textStyle.SetFontFamilies(fontFamilies);
            textStyle.FontSize = size;
            textStyle.Locale = locale;
            textStyle.Color = textColor;

            var parser = new RichTextParser.RichTextParser();
            parser.ParseText(text ?? "");
            var segments = parser.ConvertToSegments();
            var result = segments.ToParagraph(paragraphStyle, textStyle);
            ResizeParagraph(result, true);
            return result;
        }

        private void ValidateMargin()
        {
            if (m_margin.left < 0) m_margin.left = 0;
            if (m_margin.top < 0) m_margin.top = 0;
            if (m_margin.right < 0) m_margin.right = 0;
            if (m_margin.bottom < 0) m_margin.bottom = 0;
        }

        private void ResizeParagraph(Paragraph? targetParagraph, bool force = false)
        {
            if (targetParagraph == null)
            {
                return;
            }

            var rectTransform = RectTransformComponent();
            var rectWidth = rectTransform != null ? rectTransform.rect.width : 1f;
            var newLayoutWidth = Math.Max(1, Mathf.CeilToInt(rectWidth) - m_margin.horizontal);
            if (newLayoutWidth == layoutWidth && !force)
            {
                return;
            }

            layoutWidth = newLayoutWidth;
            targetParagraph.Layout(layoutWidth);
        }

        private RectTransform? RectTransformComponent()
        {
            if (rectTransformCache == null)
            {
                rectTransformCache = GetComponent<RectTransform>();
            }

            return rectTransformCache;
        }

        private UnitySkiaRenderCommandList BuildRenderCommands()
        {
            var commands = new UnitySkiaRenderCommandList();
            if (paragraph != null)
            {
                commands.DrawParagraph(paragraph, new Vector2(m_margin.left, m_margin.top));
            }
            else
            {
                Debug.LogWarning("No paragraph selected", this);
            }

            return commands;
        }
    }
}
