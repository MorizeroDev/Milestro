using System;
using UnityEngine;
using UnityEngine.UI;

namespace Milestro.Skia
{
    internal readonly struct TextGlyphMeshRun
    {
        internal TextGlyphMeshRun(GlyphLayoutSnapshot layout, Vector2 position, float scale)
            : this(layout, position, scale, new Color32(255, 255, 255, 255))
        {
        }

        internal TextGlyphMeshRun(GlyphLayoutSnapshot layout, Vector2 position, float scale, Color32 tint)
        {
            Layout = layout;
            Position = position;
            Scale = scale;
            Tint = tint;
        }

        internal GlyphLayoutSnapshot Layout { get; }
        internal Vector2 Position { get; }
        internal float Scale { get; }
        internal Color32 Tint { get; }
    }

    internal readonly struct TextGlyphQuad
    {
        internal TextGlyphQuad(Rect bounds, Rect uv, Color32 color)
        {
            Bounds = bounds;
            Uv = uv;
            Color = color;
        }

        internal Rect Bounds { get; }
        internal Rect Uv { get; }
        internal Color32 Color { get; }
    }

    internal sealed class TextGlyphMeshOutput : IDisposable
    {
        private TextGlyphAtlas.Page? page;
        private readonly TextGlyphQuad[] quads;

        internal TextGlyphMeshOutput(TextGlyphAtlas.Page page, TextGlyphQuad[] quads,
            Vector2 logicalSize, Rect clipRect)
        {
            this.page = page;
            this.quads = quads;
            LogicalSize = logicalSize;
            ClipRect = clipRect;
            page.Retain();
        }

        internal bool IsValid => page != null && page.Texture != null;
        internal Texture2D? Texture => page?.Texture;
        internal Vector2 LogicalSize { get; }
        internal Rect ClipRect { get; }
        internal int GlyphCount => quads.Length;

        internal void Populate(VertexHelper vertices, Rect displayRect, Color tint)
        {
            vertices.Clear();
            if (!IsValid || LogicalSize.x <= 0f || LogicalSize.y <= 0f)
            {
                return;
            }

            var scaleX = displayRect.width / LogicalSize.x;
            var scaleY = displayRect.height / LogicalSize.y;
            foreach (var quad in quads)
            {
                var left = Mathf.Max(0f, Mathf.Max(quad.Bounds.xMin, ClipRect.xMin));
                var top = Mathf.Max(0f, Mathf.Max(quad.Bounds.yMin, ClipRect.yMin));
                var right = Mathf.Min(LogicalSize.x, Mathf.Min(quad.Bounds.xMax, ClipRect.xMax));
                var bottom = Mathf.Min(LogicalSize.y, Mathf.Min(quad.Bounds.yMax, ClipRect.yMax));
                if (right <= left || bottom <= top)
                {
                    continue;
                }

                var uvLeft = Mathf.Lerp(quad.Uv.xMin, quad.Uv.xMax,
                    (left - quad.Bounds.xMin) / quad.Bounds.width);
                var uvRight = Mathf.Lerp(quad.Uv.xMin, quad.Uv.xMax,
                    (right - quad.Bounds.xMin) / quad.Bounds.width);
                var uvTop = Mathf.Lerp(quad.Uv.yMax, quad.Uv.yMin,
                    (top - quad.Bounds.yMin) / quad.Bounds.height);
                var uvBottom = Mathf.Lerp(quad.Uv.yMax, quad.Uv.yMin,
                    (bottom - quad.Bounds.yMin) / quad.Bounds.height);
                var vertexColor = (Color32)((Color)quad.Color * tint);
                var start = vertices.currentVertCount;
                var xMin = displayRect.xMin + left * scaleX;
                var xMax = displayRect.xMin + right * scaleX;
                var yMin = displayRect.yMax - bottom * scaleY;
                var yMax = displayRect.yMax - top * scaleY;
                vertices.AddVert(new Vector3(xMin, yMin), vertexColor, new Vector2(uvLeft, uvBottom));
                vertices.AddVert(new Vector3(xMin, yMax), vertexColor, new Vector2(uvLeft, uvTop));
                vertices.AddVert(new Vector3(xMax, yMax), vertexColor, new Vector2(uvRight, uvTop));
                vertices.AddVert(new Vector3(xMax, yMin), vertexColor, new Vector2(uvRight, uvBottom));
                vertices.AddTriangle(start, start + 1, start + 2);
                vertices.AddTriangle(start + 2, start + 3, start);
            }
        }

        public void Dispose()
        {
            var current = page;
            page = null;
            current?.Release();
        }
    }
}
