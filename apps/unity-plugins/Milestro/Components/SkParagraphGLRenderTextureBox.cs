using System;
using System.Collections.Generic;
using Milestro.Extensions;
using Milestro.Skia;
using Milestro.Skia.TextLayout;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Milestro.Components
{
    [RequireComponent(typeof(RawImage))]
    public class SkParagraphGLRenderTextureBox : MonoBehaviour
    {
        [TextArea(3, 10)]
        [SerializeField]
        [FormerlySerializedAs("content")]
        private string m_content = "";

        [SerializeField]
        [FormerlySerializedAs("imageAsset")]
        private TextAsset m_imageAsset;

        [SerializeField]
        [FormerlySerializedAs("imageRect")]
        private Rect m_imageRect = new Rect(0, 0, 128, 128);

        [SerializeField]
        [FormerlySerializedAs("paragraphPosition")]
        private Vector2 m_paragraphPosition = new Vector2(0, 144);

        [SerializeField]
        [FormerlySerializedAs("layoutWidth")]
        private int m_layoutWidth = 640;

        [SerializeField]
        [FormerlySerializedAs("fontFamilies")]
        private List<string> m_fontFamilies = new List<string>() { "Source Han Sans VF" };

        [SerializeField]
        [FormerlySerializedAs("size")]
        private float m_size = 36;

        [SerializeField]
        [FormerlySerializedAs("color")]
        private Color m_color = Color.white;

        [SerializeField]
        [FormerlySerializedAs("locale")]
        private string m_locale = "zh-Hans";

        [NonSerialized] private RawImage rawImage;
        [NonSerialized] private RectTransform rectTransform;
        [NonSerialized] private UnityAutoRenderTextureSurface surface;
        [NonSerialized] private Paragraph paragraph;
        [NonSerialized] private MilestroImage image;
        [NonSerialized] private bool? m_srgbOverride;
        [NonSerialized] private bool m_havePropertiesChanged = true;

        public string content
        {
            get => m_content;
            set
            {
                m_content = value;
                m_havePropertiesChanged = true;
            }
        }

        public TextAsset imageAsset
        {
            get => m_imageAsset;
            set
            {
                m_imageAsset = value;
                m_havePropertiesChanged = true;
            }
        }

        public Rect imageRect
        {
            get => m_imageRect;
            set
            {
                m_imageRect = value;
                m_havePropertiesChanged = true;
            }
        }

        public Vector2 paragraphPosition
        {
            get => m_paragraphPosition;
            set
            {
                m_paragraphPosition = value;
                m_havePropertiesChanged = true;
            }
        }

        public int layoutWidth
        {
            get => m_layoutWidth;
            set
            {
                m_layoutWidth = value;
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

        public float size
        {
            get => m_size;
            set
            {
                m_size = value;
                m_havePropertiesChanged = true;
            }
        }

        public Color color
        {
            get => m_color;
            set
            {
                m_color = value;
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
            get => SurfaceSrgb();
            set
            {
                m_srgbOverride = value;
                m_havePropertiesChanged = true;
            }
        }

        private void OnEnable()
        {
            rawImage = GetComponent<RawImage>();
            rectTransform = GetComponent<RectTransform>();
            RebuildResources(forceText: true, forceImage: true);
        }

        private void Update()
        {
            RebuildResources(forceText: false, forceImage: false);
        }

        private void OnDisable()
        {
            if (rawImage != null)
            {
                rawImage.texture = null;
            }

            RetireImage();
            surface?.Dispose();
            surface = null;
            paragraph = null;
            m_havePropertiesChanged = true;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            m_havePropertiesChanged = true;
        }
#endif

        private void OnRectTransformDimensionsChange()
        {
            if (isActiveAndEnabled)
            {
                RebuildResources(forceText: false, forceImage: false);
            }
        }

        private void RebuildResources(bool forceText, bool forceImage)
        {
            var needsDraw = false;
            var sizePixels = CurrentSize();
            var propertiesChanged = m_havePropertiesChanged;
            var surfaceSrgb = SurfaceSrgb();
            if (surface == null || surface.Srgb != surfaceSrgb)
            {
                if (surface != null)
                {
                    RetireImage();
                    surface.Dispose();
                    forceImage = true;
                }

                surface = new UnityAutoRenderTextureSurface(sizePixels.x, sizePixels.y, surfaceSrgb);
                rawImage.texture = surface.Texture;
                needsDraw = true;
            }
            else if (surface.Width != sizePixels.x || surface.Height != sizePixels.y)
            {
                surface.Resize(sizePixels.x, sizePixels.y);
                rawImage.texture = surface.Texture;
                needsDraw = true;
            }

            if (forceText || paragraph == null || propertiesChanged)
            {
                paragraph = BuildParagraph(content);
                needsDraw = true;
            }

            if (forceImage || propertiesChanged)
            {
                RetireImage();
                image = imageAsset != null ? MilestroImage.MakeFromTextAsset(imageAsset) : null;
                needsDraw = true;
            }

            if (propertiesChanged)
            {
                needsDraw = true;
            }

            if (needsDraw)
            {
                surface.Submit(BuildRenderCommands());
            }

            m_havePropertiesChanged = false;
        }

        private Vector2Int CurrentSize()
        {
            var rect = rectTransform.rect;
            return new Vector2Int(Mathf.Max(1, Mathf.CeilToInt(rect.width)),
                Mathf.Max(1, Mathf.CeilToInt(rect.height)));
        }

        private bool SurfaceSrgb()
        {
            return m_srgbOverride ?? UnitySkiaRenderTextureDescriptor.DefaultSrgb;
        }

        private Paragraph BuildParagraph(string text)
        {
            ParagraphStyle paragraphStyle = new ParagraphStyle();

            TextStyle textStyle = new TextStyle();
            textStyle.SetFontFamilies(fontFamilies);
            textStyle.FontSize = size;
            textStyle.Locale = locale;
            textStyle.Color = color;

            var parser = new RichTextParser.RichTextParser();
            parser.ParseText(text ?? "");
            var segments = parser.ConvertToSegments();
            var result = segments.ToParagraph(paragraphStyle, textStyle);
            result.Layout(layoutWidth);
            return result;
        }

        private UnitySkiaRenderCommandList BuildRenderCommands()
        {
            var commands = new UnitySkiaRenderCommandList();
            commands.DrawImage(image, imageRect);
            commands.DrawParagraph(paragraph, paragraphPosition);
            return commands;
        }

        private void RetireImage()
        {
            if (image == null)
            {
                return;
            }

            if (surface != null)
            {
                surface.DisposeResourceAfterPendingDraws(image);
            }
            else
            {
                image.Dispose();
            }

            image = null;
        }
    }
}
