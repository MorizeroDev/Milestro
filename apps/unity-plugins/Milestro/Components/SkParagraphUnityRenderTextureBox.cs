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
    public class SkParagraphUnityRenderTextureBox : MonoBehaviour
    {
        [SerializeField] public UnitySkiaGraphicsBackend backend = UnitySkiaGraphicsBackend.Direct3D12;
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
        [NonSerialized] private UnitySkiaRenderTextureSurface surface;
        [NonSerialized] private Paragraph paragraph;
        [NonSerialized] private MilestroImage image;
        [NonSerialized] private string cachedContent;
        [NonSerialized] private TextAsset cachedImageAsset;
        [NonSerialized] private Vector2Int cachedSize;
        [NonSerialized] private UnitySkiaGraphicsBackend cachedBackend;
        [NonSerialized] private bool cachedSrgb;
        [NonSerialized] private int cachedLayoutWidth;
        [NonSerialized] private List<string> cachedFontFamilies;
        [NonSerialized] private float cachedFontSize;
        [NonSerialized] private Color cachedColor;
        [NonSerialized] private string cachedLocale;
        [NonSerialized] private Vector2 cachedParagraphPosition;
        [NonSerialized] private Rect cachedImageRect;

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
            cachedFontFamilies = null;
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
            var needsDraw = false;
            var sizePixels = CurrentSize();
            if (surface == null || cachedBackend != backend || cachedSrgb != srgb)
            {
                RetireImage();
                surface?.Dispose();
                surface = new UnitySkiaRenderTextureSurface(backend, sizePixels.x, sizePixels.y, srgb);
                rawImage.texture = surface.Texture;
                cachedSize = sizePixels;
                cachedBackend = backend;
                cachedSrgb = srgb;
                forceImage = true;
                needsDraw = true;
            }
            else if (cachedSize != sizePixels)
            {
                surface.Resize(sizePixels.x, sizePixels.y);
                rawImage.texture = surface.Texture;
                cachedSize = sizePixels;
                needsDraw = true;
            }

            if (forceText || TextInputsChanged())
            {
                cachedContent = content;
                cachedLayoutWidth = layoutWidth;
                cachedFontFamilies = CopyFontFamilies(fontFamilies);
                cachedFontSize = size;
                cachedColor = color;
                cachedLocale = locale;
                paragraph = BuildParagraph(cachedContent);
                needsDraw = true;
            }

            if (forceImage || cachedImageAsset != imageAsset)
            {
                RetireImage();
                cachedImageAsset = imageAsset;
                image = imageAsset != null ? MilestroImage.MakeFromTextAsset(imageAsset) : null;
                needsDraw = true;
            }

            if (cachedParagraphPosition != paragraphPosition || cachedImageRect != imageRect)
            {
                cachedParagraphPosition = paragraphPosition;
                cachedImageRect = imageRect;
                needsDraw = true;
            }

            if (needsDraw)
            {
                surface.Draw(paragraph, image, paragraphPosition, imageRect);
            }
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

        private bool TextInputsChanged()
        {
            return paragraph == null ||
                   cachedContent != content ||
                   cachedLayoutWidth != layoutWidth ||
                   cachedFontSize != size ||
                   cachedColor != color ||
                   cachedLocale != locale ||
                   !FontFamiliesEqual(cachedFontFamilies, fontFamilies);
        }

        private static bool FontFamiliesEqual(IReadOnlyList<string> left, IReadOnlyList<string> right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            for (var i = 0; i < left.Count; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static List<string> CopyFontFamilies(List<string> value)
        {
            return value != null ? new List<string>(value) : null;
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
