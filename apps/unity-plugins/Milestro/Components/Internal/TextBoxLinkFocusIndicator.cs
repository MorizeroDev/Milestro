using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Milestro.Components.Internal
{
    internal sealed class TextBoxLinkFocusIndicator : MaskableGraphic
    {
        private readonly List<Rect> rects = new List<Rect>();

        internal float Thickness { get; set; } = 2f;

        internal void SetRects(IReadOnlyList<Rect> source)
        {
            rects.Clear();
            for (var index = 0; index < source.Count; ++index)
            {
                rects.Add(source[index]);
            }

            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var thickness = Mathf.Max(0f, Thickness);
            if (thickness <= 0f)
            {
                return;
            }

            foreach (var rect in rects)
            {
                AddQuad(vh, Rect.MinMaxRect(rect.xMin, rect.yMin, rect.xMax, rect.yMin + thickness));
                AddQuad(vh, Rect.MinMaxRect(rect.xMin, rect.yMax - thickness, rect.xMax, rect.yMax));
                AddQuad(vh, Rect.MinMaxRect(rect.xMin, rect.yMin + thickness, rect.xMin + thickness, rect.yMax - thickness));
                AddQuad(vh, Rect.MinMaxRect(rect.xMax - thickness, rect.yMin + thickness, rect.xMax, rect.yMax - thickness));
            }
        }

        private void AddQuad(VertexHelper vh, Rect rect)
        {
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            var start = vh.currentVertCount;
            var vertexColor = color;
            vh.AddVert(new Vector3(rect.xMin, rect.yMin), vertexColor, Vector2.zero);
            vh.AddVert(new Vector3(rect.xMin, rect.yMax), vertexColor, Vector2.zero);
            vh.AddVert(new Vector3(rect.xMax, rect.yMax), vertexColor, Vector2.zero);
            vh.AddVert(new Vector3(rect.xMax, rect.yMin), vertexColor, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }
    }
}
