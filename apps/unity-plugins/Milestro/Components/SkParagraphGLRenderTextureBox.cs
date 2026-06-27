using System;
using System.Collections.Generic;
using Milestro.Extensions;
using Milestro.Skia;
using Milestro.Skia.TextLayout;
using UnityEngine;
using UnityEngine.UI;

namespace Milestro.Components
{
    [RequireComponent(typeof(RawImage))]
    public class SkParagraphGLRenderTextureBox : MonoBehaviour
    {
        [TextArea(3, 10)] [SerializeField] public string content = "";
        [SerializeField] public TextAsset imageAsset;
        [SerializeField] public Rect imageRect = new Rect(0, 0, 128, 128);
        [SerializeField] public Vector2 paragraphPosition = new Vector2(0, 144);
        [SerializeField] public int layoutWidth = 640;
        [SerializeField] public List<string> fontFamilies = new List<string>() { "Source Han Sans VF" };
        [SerializeField] public float size = 36;
        [SerializeField] public Color color = Color.white;
        [SerializeField] public string locale = "zh-Hans";
        [SerializeField] public bool srgb = true;

        [NonSerialized] private RawImage rawImage;
        [NonSerialized] private RectTransform rectTransform;
        [NonSerialized] private UnityAutoRenderTextureSurface surface;
        [NonSerialized] private Paragraph paragraph;
        [NonSerialized] private MilestroImage image;
        [NonSerialized] private string cachedContent;
        [NonSerialized] private TextAsset cachedImageAsset;
        [NonSerialized] private Vector2Int cachedSize;

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
            cachedContent = null;
            cachedImageAsset = null;
        }

        private void OnRectTransformDimensionsChange()
        {
            if (isActiveAndEnabled)
            {
                RebuildResources(forceText: false, forceImage: false);
            }
        }

        private void RebuildResources(bool forceText, bool forceImage)
        {
            var sizePixels = CurrentSize();
            if (surface == null)
            {
                surface = new UnityAutoRenderTextureSurface(sizePixels.x, sizePixels.y, srgb);
                rawImage.texture = surface.RenderTexture;
                cachedSize = sizePixels;
            }
            else if (cachedSize != sizePixels)
            {
                surface.Resize(sizePixels.x, sizePixels.y);
                rawImage.texture = surface.RenderTexture;
                cachedSize = sizePixels;
            }

            if (forceText || cachedContent != content || paragraph == null)
            {
                cachedContent = content;
                paragraph = BuildParagraph(cachedContent);
            }

            if (forceImage || cachedImageAsset != imageAsset)
            {
                RetireImage();
                cachedImageAsset = imageAsset;
                image = imageAsset != null ? MilestroImage.MakeFromTextAsset(imageAsset) : null;
            }

            surface.Draw(paragraph, image, paragraphPosition, imageRect);
        }

        private Vector2Int CurrentSize()
        {
            var rect = rectTransform.rect;
            return new Vector2Int(Mathf.Max(1, Mathf.CeilToInt(rect.width)),
                Mathf.Max(1, Mathf.CeilToInt(rect.height)));
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
