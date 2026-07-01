using System;
using System.Runtime.InteropServices;
using Milestro.Model;
using Milestro.Skia.TextLayout;
using Milestro.Util;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Milestro.Components
{
    [RequireComponent(typeof(Image))]
    public abstract class SkParagraphBitmapTextBox : UIBehaviour
    {
        public Paragraph Paragraph { get; set; }

        [SerializeField] public Vector2 offsetPosition;

        [SerializeField] public VerticalAlign verticalAlign;

        [NonSerialized] protected RectTransform rect;

        [NonSerialized] private Texture2D texture;

        [NonSerialized] private Sprite sprite;

        [NonSerialized] private Image img;

        protected bool Inited { get; private set; } = false;

        protected override void OnEnable()
        {
            base.OnEnable();
            Inited = true;
            rect = GetComponent<RectTransform>();
            img = GetComponent<Image>();
        }

        public void RenderParagraph()
        {
            if (Paragraph == null)
            {
                return;
            }

            if (sprite)
            {
                DestroyImmediate(sprite);
            }

            var width = (int)(rect.rect.width);
            var height = (int)(rect.rect.height);

            if (texture)
            {
                if (width != texture.width || height != texture.height)
                {
                    DestroyImmediate(texture);
                    texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                }
                else
                {
                    // reuse
                }
            }
            else
            {
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            }

            using var canvas = new Milestro.Skia.Canvas(texture, verticalFlip: false, clearPixels: true);

            var offset = CalculateOffset(height);
            Paragraph.Paint(canvas, offset);
            texture.Apply();
            sprite = Sprite.Create(texture, new Rect(0, height, width, -height), Vector2.zero);
            img.color = Color.white;
            img.sprite = sprite;
        }

        private Vector2 CalculateOffset(float canvasHeight)
        {
            if (verticalAlign == VerticalAlign.Top)
            {
                return offsetPosition;
            }

            var alignOffset = Vector2.zero;


            SplitGlyphInfo();
            if (firstGlyphFlags)
            {
                Debug.LogError("fail to split glyph info");
                return offsetPosition;
            }

            var glyphHeight = glyphBound.height;

            alignOffset = verticalAlign switch
            {
                VerticalAlign.Center => new Vector2(0, (canvasHeight - glyphHeight) / 2),
                VerticalAlign.Bottom => new Vector2(0, canvasHeight - glyphHeight),
                _ => alignOffset
            };

            return alignOffset + offsetPosition;
        }

        private void SplitGlyphInfo()
        {
            var handle = GCHandle.Alloc(this);
            var pThis = GCHandle.ToIntPtr(handle);
            firstGlyphFlags = true;
            Paragraph.SplitGlypl(pThis, Vector2.zero, SplitGlyphCallback.SkiaTextlayoutParagraphSplitGlyphCallback);
            handle.Free();
        }

        [NonSerialized] private Rect glyphBound = Rect.zero;
        [NonSerialized] private bool firstGlyphFlags = true;

        internal void SplitGlyphInfoCallback(UInt16 glyphId, IntPtr font,
            float boundLeft, float boundTop,
            float boundRight, float boundBottom,
            float advanceWidth, float advanceHeight)
        {
            if (firstGlyphFlags)
            {
                firstGlyphFlags = false;
                glyphBound.xMin = boundLeft;
                glyphBound.xMax = boundRight;
                glyphBound.yMin = boundTop;
                glyphBound.yMax = boundBottom;
                return;
            }

            glyphBound.xMin = Math.Min(boundLeft, glyphBound.xMin);
            glyphBound.xMax = Math.Max(boundRight, glyphBound.xMax);
            glyphBound.yMin = Math.Min(boundTop, glyphBound.yMin);
            glyphBound.yMax = Math.Max(boundBottom, glyphBound.yMax);
        }

        protected override void OnRectTransformDimensionsChange()
        {
            OnRectTransformDimensionsChangeInternal();
        }

        protected abstract void OnRectTransformDimensionsChangeInternal();
    }
}
