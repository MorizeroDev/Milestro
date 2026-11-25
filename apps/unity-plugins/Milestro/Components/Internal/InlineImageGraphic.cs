using Milestro.RichTextParser;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace Milestro.Components.Internal
{
    internal sealed class InlineImageGraphic : MaskableGraphic
    {
        private Sprite? sprite;
        private Rect imageRect;
        private Rect clippedRect;
        public override Texture mainTexture => sprite ? sprite.texture : Texture2D.whiteTexture;

        internal void Present(Sprite value, Rect box, Rect clip, InlineObjectStyle style, float pixelsToLocal)
        {
            var natural = value.rect.size * pixelsToLocal;
            var size = box.size;
            if (style.Fit != InlineObjectFit.Fill)
            {
                var contain = Mathf.Min(box.width / natural.x, box.height / natural.y);
                var scale = style.Fit switch
                {
                    InlineObjectFit.Cover => Mathf.Max(box.width / natural.x, box.height / natural.y),
                    InlineObjectFit.None => 1f,
                    InlineObjectFit.ScaleDown => Mathf.Min(1f, contain),
                    _ => contain
                };
                size = natural * scale;
            }
            var nextImage = new Rect(box.xMin + (box.width - size.x) * style.Position.x,
                box.yMax - (box.height - size.y) * style.Position.y - size.y, size.x, size.y);
            var nextClip = Intersect(Intersect(box, clip), nextImage);
            var nextColor = new Color(1f, 1f, 1f, style.Opacity);
            if (sprite != value)
            {
                sprite = value;
                SetMaterialDirty();
                SetVerticesDirty();
            }
            if (imageRect != nextImage || clippedRect != nextClip || color != nextColor)
            {
                imageRect = nextImage;
                clippedRect = nextClip;
                color = nextColor;
                SetVerticesDirty();
            }
        }

        internal void Clear()
        {
            sprite = null;
            SetMaterialDirty();
            SetVerticesDirty();
        }

        private static Rect Intersect(Rect left, Rect right)
        {
            var min = Vector2.Max(left.min, right.min);
            var max = Vector2.Max(min, Vector2.Min(left.max, right.max));
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (!sprite || clippedRect.width <= 0 || clippedRect.height <= 0 || imageRect.width <= 0 || imageRect.height <= 0) return;
            var uv = DataUtility.GetOuterUV(sprite);
            var left = Mathf.Lerp(uv.x, uv.z, (clippedRect.xMin - imageRect.xMin) / imageRect.width);
            var right = Mathf.Lerp(uv.x, uv.z, (clippedRect.xMax - imageRect.xMin) / imageRect.width);
            var bottom = Mathf.Lerp(uv.y, uv.w, (clippedRect.yMin - imageRect.yMin) / imageRect.height);
            var top = Mathf.Lerp(uv.y, uv.w, (clippedRect.yMax - imageRect.yMin) / imageRect.height);
            helper.AddVert(new Vector3(clippedRect.xMin, clippedRect.yMin), color, new Vector2(left, bottom));
            helper.AddVert(new Vector3(clippedRect.xMin, clippedRect.yMax), color, new Vector2(left, top));
            helper.AddVert(new Vector3(clippedRect.xMax, clippedRect.yMax), color, new Vector2(right, top));
            helper.AddVert(new Vector3(clippedRect.xMax, clippedRect.yMin), color, new Vector2(right, bottom));
            helper.AddTriangle(0, 1, 2);
            helper.AddTriangle(2, 3, 0);
        }
    }
}
