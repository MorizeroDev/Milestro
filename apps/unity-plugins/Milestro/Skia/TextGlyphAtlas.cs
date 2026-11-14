using System;
using System.Collections.Generic;
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
            output = null;
            var configuration = MilestroConfiguration.Configuration?.RenderSurface;
            if (configuration == null || !configuration.EnableTextGlyphMesh || Material == null ||
                !SystemInfo.SupportsTextureFormat(TextureFormat.Alpha8) ||
                !IsPositiveFinite(rasterScale) || !IsPositiveFinite(logicalSize.x) ||
                !IsPositiveFinite(logicalSize.y) || !IsFinite(clipRect.xMin) ||
                !IsFinite(clipRect.yMin) || !IsFinite(clipRect.xMax) || !IsFinite(clipRect.yMax))
            {
                return false;
            }

            var count = 0L;
            foreach (var run in runs)
            {
                if (run.Layout == null || !IsPositiveFinite(run.Scale) ||
                    !IsFinite(run.Position.x) || !IsFinite(run.Position.y))
                {
                    return false;
                }
                count += run.Layout.Glyphs.Length;
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
                    TryBuildOnPage(page, runs, rasterScale, logicalSize, clipRect, out output))
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
            return TryBuildOnPage(next, runs, rasterScale, logicalSize, clipRect, out output);
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
            float rasterScale, Vector2 logicalSize, Rect clipRect, out TextGlyphMeshOutput? output)
        {
            output = null;
            var quads = new List<TextGlyphQuad>();
            foreach (var run in runs)
            {
                var glyphScale = rasterScale * run.Scale;
                if (!IsPositiveFinite(glyphScale) || glyphScale > 64f)
                {
                    return false;
                }
                for (var index = 0; index < run.Layout.Glyphs.Length; ++index)
                {
                    var glyph = run.Layout.Glyphs[index];
                    if (!IsFinite(glyph.X) || !IsFinite(glyph.Y))
                    {
                        return false;
                    }
                    if (!page.TryGet(run.Layout, index, glyphScale, out var entry))
                    {
                        return false;
                    }
                    if (entry.Bounds.width == 0 || entry.Bounds.height == 0)
                    {
                        continue;
                    }

                    var bounds = new Rect(run.Position.x + glyph.X * run.Scale + entry.Bounds.x / rasterScale,
                        run.Position.y + glyph.Y * run.Scale + entry.Bounds.y / rasterScale,
                        entry.Bounds.width / rasterScale, entry.Bounds.height / rasterScale);
                    quads.Add(new TextGlyphQuad(bounds, entry.Uv, (Color32)((Color)glyph.Color * run.Tint)));
                }
            }

            output = new TextGlyphMeshOutput(page, quads.ToArray(), logicalSize, clipRect);
            return true;
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

            internal Key(GlyphLayoutDescriptor descriptor, float scale)
            {
                typeface = descriptor.TypefaceId;
                glyph = descriptor.GlyphId;
                flags = descriptor.FontFlags;
                fontSize = descriptor.FontSize;
                fontScale = descriptor.FontScaleX;
                skew = descriptor.FontSkewX;
                rasterScale = scale;
            }

            public bool Equals(Key other) => typeface == other.typeface && glyph == other.glyph &&
                flags == other.flags && fontSize.Equals(other.fontSize) && fontScale.Equals(other.fontScale) &&
                skew.Equals(other.skew) && rasterScale.Equals(other.rasterScale);
            public override bool Equals(object? other) => other is Key key && Equals(key);
            public override int GetHashCode() => HashCode.Combine(typeface, glyph, flags, fontSize,
                fontScale, skew, rasterScale);
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

            internal bool TryGet(GlyphLayoutSnapshot snapshot, int index, float scale, out Entry entry)
            {
                var key = new Key(snapshot.Glyphs[index], scale);
                if (entries.TryGetValue(key, out entry))
                {
                    return true;
                }

                if (!snapshot.TryGetRasterBounds(index, scale, out var bounds))
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
                    source = snapshot.Rasterize(index, scale, checked(bounds.width * bounds.height));
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
                Texture.Apply(false, false);
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
