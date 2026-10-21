using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Milestro.Components.Internal
{
    internal sealed class TextBoxLinkHoverIndicator : MaskableGraphic
    {
        private readonly List<Rect> rects = new List<Rect>();

        private Color fillColor;
        private Color underlineColor;
        private float underlineThickness;

        internal void SetPresentation(IReadOnlyList<Rect> source,
            Color fill,
            Color underline,
            float thickness)
        {
            var changed = fillColor != fill || underlineColor != underline ||
                          underlineThickness != thickness || rects.Count != source.Count;
            for (var index = 0; !changed && index < source.Count; ++index)
            {
                changed = rects[index] != source[index];
            }

            if (!changed)
            {
                return;
            }

            fillColor = fill;
            underlineColor = underline;
            underlineThickness = thickness;
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
            foreach (var rect in rects)
            {
                if (fillColor.a > 0f)
                {
                    AddQuad(vh, rect, fillColor);
                }

                if (underlineThickness > 0f)
                {
                    AddQuad(vh,
                        Rect.MinMaxRect(rect.xMin,
                            rect.yMin,
                            rect.xMax,
                            Mathf.Min(rect.yMax, rect.yMin + underlineThickness)),
                        underlineColor);
                }
            }
        }

        private static void AddQuad(VertexHelper vh, Rect rect, Color vertexColor)
        {
            if (rect.width <= 0f || rect.height <= 0f || vertexColor.a <= 0f)
            {
                return;
            }

            var start = vh.currentVertCount;
            vh.AddVert(new Vector3(rect.xMin, rect.yMin), vertexColor, Vector2.zero);
            vh.AddVert(new Vector3(rect.xMin, rect.yMax), vertexColor, Vector2.zero);
            vh.AddVert(new Vector3(rect.xMax, rect.yMax), vertexColor, Vector2.zero);
            vh.AddVert(new Vector3(rect.xMax, rect.yMin), vertexColor, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }
    }
}
