using System;
using System.Buffers;
using System.Collections.Generic;
using Milestro.Components.Internal;
using Milestro.Configuration;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Milestro.Skia
{
    internal static class TextGlyphAtlas
    {
        private static readonly List<Page> Pages = new List<Page>();
        private static Material? material;
        private static bool registered;
        internal static long RasterizedGlyphs { get; private set; }
        internal static long Uploads { get; private set; }

        internal static Material? Material
        {
            get
            {
                if (material != null)
                {
                    return material;
                }

                var shader = Resources.Load<Shader>("Milestro/TextGlyphMesh");
                if (shader == null || !shader.isSupported)
                {
                    return null;
                }

                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                Register();
                return material;
            }
        }

        internal static bool TryBuild(IReadOnlyList<TextGlyphMeshRun> runs, float rasterScale,
            Vector2 logicalSize, Rect clipRect, out TextGlyphMeshOutput? output)
        {
            using var measurement = TextPipelineDiagnostics.Measure(TextPipelineStage.GlyphMeshBuild);
            TextPipelineDiagnostics.Record(TextPipelineCounter.GlyphMeshBuilds);
            output = null;
            var configuration = MilestroConfiguration.Configuration?.RenderSurface;
            if (configuration == null || !configuration.EnableTextGlyphMesh ||
                QualitySettings.activeColorSpace != ColorSpace.Gamma || Material == null ||
                !SystemInfo.SupportsTextureFormat(TextureFormat.Alpha8) ||
                !IsPositiveFinite(rasterScale) || !IsPositiveFinite(logicalSize.x) ||
                !IsPositiveFinite(logicalSize.y) || !IsFinite(clipRect.xMin) ||
                !IsFinite(clipRect.yMin) || !IsFinite(clipRect.xMax) || !IsFinite(clipRect.yMax))
            {
                return false;
            }

            var count = 0L;
            for (var runIndex = 0; runIndex < runs.Count; ++runIndex)
            {
                var run = runs[runIndex];
                if (run.Layout == null || !run.Layout.IsValid || !IsPositiveFinite(run.Scale) ||
                    !IsFinite(run.Position.x) || !IsFinite(run.Position.y) ||
                    run.Tint.r != 255 || run.Tint.g != 255 || run.Tint.b != 255)
                {
                    return false;
                }
                if (!run.Layout.IsWhiteSource)
                {
                    return false;
                }
                count += run.Layout.GlyphCount;
            }

            if (count > Math.Min(16000, configuration.MaxTextGlyphMeshGlyphs))
            {
                return false;
            }

            var size = configuration.GlyphAtlasPageSize;
            if (size < 32 || size > Math.Min(4096, SystemInfo.maxTextureSize) ||
                configuration.MaxGlyphAtlasPages < 1 || configuration.MaxGlyphAtlasPages > 8)
            {
                return false;
            }

            for (var index = Pages.Count - 1; index >= 0; --index)
            {
                var page = Pages[index];
                if (page.Size == size && page.Texture != null &&
                    TryBuildOnPage(page, runs, (int)count, rasterScale, logicalSize, clipRect, out output))
                {
                    return true;
                }
            }

            for (var index = Pages.Count - 1; index >= 0; --index)
            {
                if (Pages[index].References == 0)
                {
                    Pages[index].Dispose();
                    Pages.RemoveAt(index);
                }
            }

            if (Pages.Count >= configuration.MaxGlyphAtlasPages)
            {
                return false;
            }

            var next = new Page(size);
            Pages.Add(next);
            Register();
            return TryBuildOnPage(next, runs, (int)count, rasterScale, logicalSize, clipRect, out output);
        }

        private static void Register()
        {
            if (registered)
            {
                return;
            }
            registered = true;
            UnityEngine.Canvas.willRenderCanvases += Flush;
            Camera.onPreCull += BeforeCamera;
            Application.quitting += Reset;
#if UNITY_EDITOR
            AssemblyReloadEvents.beforeAssemblyReload += Reset;
#endif
        }

        private static void BeforeCamera(Camera camera) => Flush();

        private static bool TryBuildOnPage(Page page, IReadOnlyList<TextGlyphMeshRun> runs,
            int glyphCapacity, float rasterScale, Vector2 logicalSize, Rect clipRect,
            out TextGlyphMeshOutput? output)
        {
            output = null;
            var quads = ArrayPool<TextGlyphQuad>.Shared.Rent(glyphCapacity);
            var quadCount = 0;
            try
            {
                for (var runIndex = 0; runIndex < runs.Count; ++runIndex)
                {
                    var run = runs[runIndex];
                    var glyphScale = rasterScale * run.Scale;
                    if (!IsPositiveFinite(glyphScale) || glyphScale > 64f)
                    {
                        return false;
                    }
                    for (var index = 0; index < run.Layout.GlyphCount; ++index)
                    {
                        var glyph = run.Layout.GetAtlasGlyph(index);
                        if (!IsFinite(glyph.X) || !IsFinite(glyph.Y))
                        {
                            return false;
                        }
                        var origin = (run.Position + new Vector2(glyph.X, glyph.Y) * run.Scale) * rasterScale;
                        if (!TryResolveRasterPosition(origin, glyph.FontFlags, out var position) ||
                            !page.TryGet(run.Layout, index, glyph, glyphScale, position, out var entry))
                        {
                            return false;
                        }
                        if (entry.Bounds.width == 0 || entry.Bounds.height == 0)
                        {
                            continue;
                        }

                        var bounds = new Rect((position.Anchor.x + entry.Bounds.x) / rasterScale,
                            (position.Anchor.y + entry.Bounds.y) / rasterScale,
                            entry.Bounds.width / rasterScale, entry.Bounds.height / rasterScale);
                        quads[quadCount++] = new TextGlyphQuad(bounds, entry.Uv,
                            (Color32)((Color)glyph.Color * run.Tint));
                    }
                }

                output = new TextGlyphMeshOutput(page, quads, quadCount, logicalSize, clipRect, true);
                return true;
            }
            finally
            {
                if (output == null)
                {
                    ArrayPool<TextGlyphQuad>.Shared.Return(quads);
                }
            }
        }

        internal static void Flush()
        {
            foreach (var page in Pages)
            {
                page.Flush();
            }
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsPositiveFinite(float value) => IsFinite(value) && value > 0f;

        internal static bool TryResolveRasterPosition(Vector2 origin, uint fontFlags, out RasterPosition position)
        {
            position = default;
            if (!IsFinite(origin.x) || !IsFinite(origin.y))
            {
                return false;
            }

            var subpixel = (fontFlags & 4) != 0;
            var xSteps = subpixel ? 4 : 1;
            var ySteps = subpixel && (fontFlags & 32) == 0 ? 4 : 1;
            var quantized = new Vector2(Mathf.Floor(origin.x * xSteps + 0.5f) / xSteps,
                Mathf.Floor(origin.y * ySteps + 0.5f) / ySteps);
            if (!IsFinite(quantized.x) || !IsFinite(quantized.y))
            {
                return false;
            }

            var anchor = new Vector2(Mathf.Floor(quantized.x), Mathf.Floor(quantized.y));
            position = new RasterPosition(anchor, (byte)((quantized.x - anchor.x) * 4),
                (byte)((quantized.y - anchor.y) * 4));
            return true;
        }

        internal readonly struct RasterPosition
        {
            internal RasterPosition(Vector2 anchor, byte phaseXIndex, byte phaseYIndex)
            {
                Anchor = anchor;
                PhaseXIndex = phaseXIndex;
                PhaseYIndex = phaseYIndex;
            }

            internal Vector2 Anchor { get; }
            internal byte PhaseXIndex { get; }
            internal byte PhaseYIndex { get; }
            internal float PhaseX => PhaseXIndex * 0.25f;
            internal float PhaseY => PhaseYIndex * 0.25f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            UnityEngine.Canvas.willRenderCanvases -= Flush;
            Camera.onPreCull -= BeforeCamera;
            Application.quitting -= Reset;
#if UNITY_EDITOR
            AssemblyReloadEvents.beforeAssemblyReload -= Reset;
#endif
            registered = false;
            foreach (var page in Pages)
            {
                page.Dispose();
            }
            Pages.Clear();
            Destroy(material);
            material = null;
            RasterizedGlyphs = 0;
            Uploads = 0;
        }

        private static void Destroy(UnityEngine.Object? target)
        {
            if (target == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        internal readonly struct Entry
        {
            internal Entry(RectInt bounds, Rect uv)
            {
                Bounds = bounds;
                Uv = uv;
            }
            internal RectInt Bounds { get; }
            internal Rect Uv { get; }
        }

        private readonly struct Key : IEquatable<Key>
        {
            private readonly uint typeface;
            private readonly uint glyph;
            private readonly uint flags;
            private readonly float fontSize;
            private readonly float fontScale;
            private readonly float skew;
            private readonly float rasterScale;
            private readonly byte phaseX;
            private readonly byte phaseY;

            internal Key(AtlasGlyphDescriptor descriptor, float scale, RasterPosition position)
            {
                typeface = descriptor.TypefaceId;
                glyph = descriptor.GlyphId;
                flags = descriptor.FontFlags;
                fontSize = descriptor.FontSize;
                fontScale = descriptor.FontScaleX;
                skew = descriptor.FontSkewX;
                rasterScale = scale;
                phaseX = position.PhaseXIndex;
                phaseY = position.PhaseYIndex;
            }

            public bool Equals(Key other) => typeface == other.typeface && glyph == other.glyph &&
                flags == other.flags && fontSize.Equals(other.fontSize) && fontScale.Equals(other.fontScale) &&
                skew.Equals(other.skew) && rasterScale.Equals(other.rasterScale) &&
                phaseX == other.phaseX && phaseY == other.phaseY;
            public override bool Equals(object? other) => other is Key key && Equals(key);
            public override int GetHashCode() => HashCode.Combine(typeface, glyph, flags, fontSize,
                fontScale, skew, rasterScale, phaseX | phaseY << 8);
        }

        internal sealed class Page : IDisposable
        {
            private readonly RenderAtlasAllocator allocator;
            private readonly Dictionary<Key, Entry> entries = new Dictionary<Key, Entry>();
            private bool dirty;

            internal Page(int size)
            {
                Size = size;
                allocator = new RenderAtlasAllocator(size);
                Texture = new Texture2D(size, size, TextureFormat.Alpha8, false, true)
                {
                    name = "Milestro Glyph Atlas",
                    hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                var pixels = Texture.GetRawTextureData<byte>();
                for (var index = 0; index < pixels.Length; ++index)
                {
                    pixels[index] = 0;
                }
                dirty = true;
            }

            internal int Size { get; }
            internal Texture2D? Texture { get; private set; }
            internal int References { get; private set; }
            internal void Retain() => ++References;
            internal void Release() => --References;

            internal bool TryGet(GlyphLayoutSnapshot snapshot, int index, AtlasGlyphDescriptor descriptor, float scale,
                RasterPosition position, out Entry entry)
            {
                var key = new Key(descriptor, scale, position);
                if (entries.TryGetValue(key, out entry))
                {
                    TextPipelineDiagnostics.Record(TextPipelineCounter.GlyphCacheHits);
                    return true;
                }
                TextPipelineDiagnostics.Record(TextPipelineCounter.GlyphCacheMisses);
                if (!snapshot.TryGetRasterBounds(index, scale, position.PhaseX, position.PhaseY, out var bounds))
                {
                    entry = default;
                    return false;
                }
                if (bounds.width == 0 || bounds.height == 0)
                {
                    entry = new Entry(bounds, default);
                    return true;
                }
                if (!allocator.TryAllocate(bounds.width, bounds.height, out var allocation))
                {
                    return false;
                }

                byte[] source;
                try
                {
                    source = snapshot.Rasterize(index, scale, position.PhaseX, position.PhaseY,
                        checked(bounds.width * bounds.height));
                }
                catch
                {
                    allocator.Release(allocation);
                    throw;
                }
                var destination = Texture!.GetRawTextureData<byte>();
                var left = allocation.x + RenderAtlasAllocator.Padding;
                var bottom = allocation.y + RenderAtlasAllocator.Padding;
                for (var row = 0; row < bounds.height; ++row)
                {
                    var targetOffset = (bottom + bounds.height - row - 1) * Size + left;
                    var sourceOffset = row * bounds.width;
                    for (var column = 0; column < bounds.width; ++column)
                    {
                        destination[targetOffset + column] = source[sourceOffset + column];
                    }
                }
                entry = new Entry(bounds, new Rect((float)left / Size, (float)bottom / Size,
                    (float)bounds.width / Size, (float)bounds.height / Size));
                entries.Add(key, entry);
                ++RasterizedGlyphs;
                dirty = true;
                return true;
            }

            internal void Flush()
            {
                if (!dirty || Texture == null)
                {
                    return;
                }
                using var measurement = TextPipelineDiagnostics.Measure(TextPipelineStage.GlyphAtlasUpload);
                Texture.Apply(false, false);
                TextPipelineDiagnostics.Record(TextPipelineCounter.GlyphAtlasUploads);
                TextPipelineDiagnostics.Record(TextPipelineCounter.GlyphAtlasUploadBytes, (long)Size * Size);
                dirty = false;
                ++Uploads;
            }

            public void Dispose()
            {
                Destroy(Texture);
                Texture = null;
            }
        }
    }
}
