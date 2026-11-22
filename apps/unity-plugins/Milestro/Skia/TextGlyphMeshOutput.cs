using System;
using System.Buffers;
using Milestro.Components.Internal;
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
        private TextGlyphQuad[]? quads;
        private readonly bool pooled;

        internal TextGlyphMeshOutput(TextGlyphAtlas.Page page, TextGlyphQuad[] quads,
            Vector2 logicalSize, Rect clipRect)
            : this(page, quads, quads.Length, logicalSize, clipRect, false)
        {
        }

        internal TextGlyphMeshOutput(TextGlyphAtlas.Page page, TextGlyphQuad[] quads, int glyphCount,
            Vector2 logicalSize, Rect clipRect, bool pooled)
        {
            this.page = page;
            this.quads = quads;
            this.pooled = pooled;
            GlyphCount = glyphCount;
            LogicalSize = logicalSize;
            ClipRect = clipRect;
            page.Retain();
        }

        internal bool IsValid => page != null && page.Texture != null;
        internal Texture2D? Texture => page?.Texture;
        internal Vector2 LogicalSize { get; }
        internal Rect ClipRect { get; }
        internal int GlyphCount { get; }

        internal void Populate(VertexHelper vertices, Rect displayRect, Color tint)
        {
            using var measurement = TextPipelineDiagnostics.Measure(TextPipelineStage.GlyphMeshPopulate);
            TextPipelineDiagnostics.Record(TextPipelineCounter.GlyphMeshPopulates);
            vertices.Clear();
            if (!IsValid || LogicalSize.x <= 0f || LogicalSize.y <= 0f)
            {
                return;
            }

            var scaleX = displayRect.width / LogicalSize.x;
            var scaleY = displayRect.height / LogicalSize.y;
            var clipLeft = Mathf.Max(0f, ClipRect.xMin);
            var clipTop = Mathf.Max(0f, ClipRect.yMin);
            var clipRight = Mathf.Min(LogicalSize.x, ClipRect.xMax);
            var clipBottom = Mathf.Min(LogicalSize.y, ClipRect.yMax);
            var whiteTint = tint.r == 1f && tint.g == 1f && tint.b == 1f && tint.a == 1f;
            for (var index = 0; index < GlyphCount; ++index)
            {
                var quad = quads![index];
                var left = Mathf.Max(quad.Bounds.xMin, clipLeft);
                var top = Mathf.Max(quad.Bounds.yMin, clipTop);
                var right = Mathf.Min(quad.Bounds.xMax, clipRight);
                var bottom = Mathf.Min(quad.Bounds.yMax, clipBottom);
                if (right <= left || bottom <= top)
                {
                    continue;
                }

                var uvLeft = quad.Uv.xMin;
                var uvRight = quad.Uv.xMax;
                var uvTop = quad.Uv.yMax;
                var uvBottom = quad.Uv.yMin;
                if (left != quad.Bounds.xMin || right != quad.Bounds.xMax ||
                    top != quad.Bounds.yMin || bottom != quad.Bounds.yMax)
                {
                    uvLeft = Mathf.Lerp(quad.Uv.xMin, quad.Uv.xMax,
                        (left - quad.Bounds.xMin) / quad.Bounds.width);
                    uvRight = Mathf.Lerp(quad.Uv.xMin, quad.Uv.xMax,
                        (right - quad.Bounds.xMin) / quad.Bounds.width);
                    uvTop = Mathf.Lerp(quad.Uv.yMax, quad.Uv.yMin,
                        (top - quad.Bounds.yMin) / quad.Bounds.height);
                    uvBottom = Mathf.Lerp(quad.Uv.yMax, quad.Uv.yMin,
                        (bottom - quad.Bounds.yMin) / quad.Bounds.height);
                }
                var vertexColor = whiteTint ? quad.Color : (Color32)((Color)quad.Color * tint);
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
            var buffer = quads;
            quads = null;
            if (pooled && buffer != null)
            {
                ArrayPool<TextGlyphQuad>.Shared.Return(buffer);
            }
        }
    }
}
