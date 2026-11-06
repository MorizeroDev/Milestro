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
        private bool vertical;

        internal void SetPresentation(IReadOnlyList<Rect> source,
            Color fill,
            Color underline,
            float thickness,
            bool isVerticalLayout)
        {
            var changed = fillColor != fill || underlineColor != underline ||
                          underlineThickness != thickness || vertical != isVerticalLayout ||
                          rects.Count != source.Count;
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
            vertical = isVerticalLayout;
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
                    AddQuad(vh, vertical ? VerticalFillRect(rect) : rect, fillColor);
                }
            }

            if (underlineThickness <= 0f)
            {
                return;
            }

            if (!vertical)
            {
                foreach (var rect in rects)
                {
                    var underline = Rect.MinMaxRect(rect.xMin,
                        rect.yMin,
                        rect.xMax,
                        Mathf.Min(rect.yMax, rect.yMin + underlineThickness));
                    AddQuad(vh, underline, underlineColor);
                }
                return;
            }

            Rect? mergedCells = null;
            Rect? mergedUnderline = null;

            void FlushUnderline()
            {
                if (mergedUnderline.HasValue)
                {
                    AddQuad(vh, mergedUnderline.Value, underlineColor);
                }
                mergedCells = null;
                mergedUnderline = null;
            }

            foreach (var rect in rects)
            {
                var underline = Rect.MinMaxRect(rect.xMin,
                    rect.yMin,
                    Mathf.Min(rect.xMax, rect.xMin + underlineThickness),
                    rect.yMax);
                if (mergedCells.HasValue)
                {
                    var cells = mergedCells.Value;
                    var sameColumn = cells.xMin < rect.xMax && rect.xMin < cells.xMax;
                    var adjacent = cells.yMin <= rect.yMax + 0.01f &&
                                   rect.yMin <= cells.yMax + 0.01f;
                    if (sameColumn && adjacent)
                    {
                        var previous = mergedUnderline!.Value;
                        var left = Mathf.Min(previous.xMin, underline.xMin);
                        mergedCells = Rect.MinMaxRect(Mathf.Min(cells.xMin, rect.xMin),
                            Mathf.Min(cells.yMin, rect.yMin),
                            Mathf.Max(cells.xMax, rect.xMax),
                            Mathf.Max(cells.yMax, rect.yMax));
                        mergedUnderline = Rect.MinMaxRect(left,
                            Mathf.Min(previous.yMin, underline.yMin),
                            Mathf.Min(mergedCells.Value.xMax, left + underlineThickness),
                            Mathf.Max(previous.yMax, underline.yMax));
                        continue;
                    }
                }

                FlushUnderline();
                mergedCells = rect;
                mergedUnderline = underline;
            }

            FlushUnderline();
        }

        private Rect VerticalFillRect(Rect rect)
        {
            var left = rect.xMin;
            var right = rect.xMax;
            foreach (var candidate in rects)
            {
                if (rect.xMin < candidate.xMax && candidate.xMin < rect.xMax)
                {
                    left = Mathf.Min(left, candidate.xMin);
                    right = Mathf.Max(right, candidate.xMax);
                }
            }
            return Rect.MinMaxRect(left, rect.yMin, right, rect.yMax);
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
