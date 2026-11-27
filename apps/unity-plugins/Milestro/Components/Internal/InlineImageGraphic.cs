using System;
using System.Collections.Generic;
using Milestro.RichTextParser;
using UnityEngine;
using UnityEngine.UI;

namespace Milestro.Components.Internal
{
    internal sealed class InlineImageGraphic : MaskableGraphic
    {
        private Sprite? sprite;
        private Rect imageRect;
        private Rect clippedRect;
        private Vector2[] positions = Array.Empty<Vector2>();
        private Vector2[] textureCoordinates = Array.Empty<Vector2>();
        private ushort[] triangles = Array.Empty<ushort>();
        private List<UIVertex> polygon = new List<UIVertex>(8);
        private List<UIVertex> clippedPolygon = new List<UIVertex>(8);
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
                positions = value.vertices;
                textureCoordinates = value.uv;
                triangles = value.triangles;
                for (var index = 0; index < positions.Length; ++index)
                {
                    var pixel = positions[index] * value.pixelsPerUnit + value.pivot;
                    positions[index] = new Vector2(pixel.x / value.rect.width, pixel.y / value.rect.height);
                }
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
            positions = Array.Empty<Vector2>();
            textureCoordinates = Array.Empty<Vector2>();
            triangles = Array.Empty<ushort>();
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
            for (var triangle = 0; triangle < triangles.Length; triangle += 3)
            {
                polygon.Clear();
                for (var corner = 0; corner < 3; ++corner)
                {
                    var index = triangles[triangle + corner];
                    var vertex = UIVertex.simpleVert;
                    vertex.position = imageRect.min + Vector2.Scale(positions[index], imageRect.size);
                    vertex.uv0 = textureCoordinates[index];
                    vertex.color = color;
                    polygon.Add(vertex);
                }
                Clip(0, clippedRect.xMin, true);
                Clip(0, clippedRect.xMax, false);
                Clip(1, clippedRect.yMin, true);
                Clip(1, clippedRect.yMax, false);
                if (polygon.Count < 3) continue;
                var start = helper.currentVertCount;
                foreach (var vertex in polygon) helper.AddVert(vertex);
                for (var corner = 1; corner + 1 < polygon.Count; ++corner)
                    helper.AddTriangle(start, start + corner, start + corner + 1);
            }
        }

        private void Clip(int axis, float edge, bool minimum)
        {
            clippedPolygon.Clear();
            if (polygon.Count == 0) return;
            var previous = polygon[polygon.Count - 1];
            var previousInside = minimum ? previous.position[axis] >= edge : previous.position[axis] <= edge;
            foreach (var current in polygon)
            {
                var currentInside = minimum ? current.position[axis] >= edge : current.position[axis] <= edge;
                if (previousInside != currentInside)
                {
                    var fraction = (edge - previous.position[axis]) / (current.position[axis] - previous.position[axis]);
                    var intersection = previous;
                    intersection.position = Vector3.LerpUnclamped(previous.position, current.position, fraction);
                    intersection.uv0 = Vector4.LerpUnclamped(previous.uv0, current.uv0, fraction);
                    clippedPolygon.Add(intersection);
                }
                if (currentInside) clippedPolygon.Add(current);
                previous = current;
                previousInside = currentInside;
            }
            (polygon, clippedPolygon) = (clippedPolygon, polygon);
        }
    }
}
