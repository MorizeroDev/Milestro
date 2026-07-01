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
    public class SkParagraphGLRenderTextureBox : SkiaRenderTextureGraphic
    {
        [TextArea(3, 10)]
        [SerializeField]
        [FormerlySerializedAs("content")]
        private string m_content = "";

        [SerializeField]
        [FormerlySerializedAs("margin")]
        private RectOffset m_margin = new RectOffset();

        // [SerializeField] [FormerlySerializedAs("paragraphPosition")]
        // private Vector2 m_paragraphPosition = new Vector2(0, 144);
        //
        // [SerializeField] [FormerlySerializedAs("layoutWidth")]
        // private int m_layoutWidth = 640;

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


        [NonSerialized] private RectTransform rectTransform;
        [NonSerialized] private UnityAutoRenderTextureSurface? surface;
        [NonSerialized] private Paragraph? paragraph;
        [NonSerialized] private MilestroImage image;
        [NonSerialized] private ColorSpace? m_colorSpaceOverride;
        [NonSerialized] private bool m_havePropertiesChanged = true;
#if UNITY_EDITOR
        [NonSerialized] private bool m_editorRebuildQueued;
#endif
        [NonSerialized] private int layoutWidth = 640;
        [NonSerialized] private Vector2 paragraphPosition = new Vector2(640, 640);

        public string content
        {
            get => m_content;
            set
            {
                m_content = value;
                m_havePropertiesChanged = true;
            }
        }

        public List<string> fontFamilies
        {
            get => m_fontFamilies;
            set
            {
                m_fontFamilies = value;
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
                m_locale = value;
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

        protected override void OnEnable()
        {
            base.OnEnable();
            rectTransform = GetComponent<RectTransform>();
            RebuildResources(forceText: true);
        }

        private void Update()
        {
            RebuildResources(forceText: false);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Texture = null;
            surface?.Dispose();
            surface = null;
            paragraph = null;
            m_havePropertiesChanged = true;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (surface != null) UvRect = surface.DisplayUvRect;
            m_havePropertiesChanged = true;
        }
#endif

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            if (isActiveAndEnabled)
            {
                m_havePropertiesChanged = true;
                SetVerticesDirty();
#if UNITY_EDITOR
                QueueEditorRebuild();
#endif
            }
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
            var surfaceColorSpace = SurfaceColorSpace();
            if (surface == null || surface.ColorSpace != surfaceColorSpace)
            {
                surface?.Dispose();

                surface = new UnityAutoRenderTextureSurface(sizePixels.x, sizePixels.y, surfaceColorSpace);
                ApplySurfaceToRawImage();
                needsDraw = true;
            }
            else if (surface.Width != sizePixels.x || surface.Height != sizePixels.y)
            {
                surface.Resize(sizePixels.x, sizePixels.y);
                ApplySurfaceToRawImage();
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
                if (!surface.TrySubmit(BuildRenderCommands()))
                {
                    m_havePropertiesChanged = true;
                    return;
                }
            }

            m_havePropertiesChanged = false;
        }

        private Vector2Int CurrentSize()
        {
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

        private void ResizeParagraph(Paragraph paragraph, bool force = false)
        {
            var newLayoutWidth =
                Math.Max(1, Mathf.CeilToInt(rectTransform.rect.width) - m_margin.horizontal);
            if (newLayoutWidth == layoutWidth && !force) return;
            layoutWidth = newLayoutWidth;
            paragraph.Layout(layoutWidth);
        }

        private UnitySkiaRenderCommandList BuildRenderCommands()
        {
            var commands = new UnitySkiaRenderCommandList();
            if (paragraph != null)
            {
                paragraphPosition = new Vector2(m_margin.left, m_margin.top);
                commands.DrawParagraph(paragraph, paragraphPosition);
            }
            else
            {
                Debug.LogWarning("No paragraph selected");
            }

            return commands;
        }

        private void ApplySurfaceToRawImage()
        {
            Texture = surface!.Texture;
            UvRect = surface.DisplayUvRect;
        }
    }
}
