using System;
using System.Buffers;
using Milestro.Binding;
using Milestro.Components.Internal;
using Paraparty.UnityNative.Base;
using UnityEngine;
using Paragraph = Milestro.Skia.TextLayout.Paragraph;

namespace Milestro.Skia
{
    internal sealed class GlyphLayoutSnapshot : IDisposable
    {
        private GlyphLayoutDescriptor[]? glyphs;
        private GlyphPlacement[]? placements;
        private ReusableTextDrawSnapshot? owner;
        private ulong ownerVersion;
        private GlyphFontDescriptor fontDescriptor;
        private bool disposed;
        private IntPtr ptr;
        private IntPtr NativePtr => ptr;

        internal GlyphLayoutSnapshot() { }

        internal unsafe bool RefreshAtlasView(ReusableTextDrawSnapshot source)
        {
            using var measurement = TextPipelineDiagnostics.Measure(TextPipelineStage.GlyphSnapshotExport);
            TextPipelineDiagnostics.Record(TextPipelineCounter.GlyphSnapshotExports);
            if (disposed) { throw new ObjectDisposedException(nameof(GlyphLayoutSnapshot)); }
            if (ptr != IntPtr.Zero || glyphs != null)
            {
                throw new InvalidOperationException("An owned snapshot cannot become an atlas view.");
            }
            owner = null;
            GlyphCount = 0;
            if (!source.IsAlive) { return false; }
            if (placements == null || placements.Length < source.TextLength)
            {
                var nextPlacements = ArrayPool<GlyphPlacement>.Shared.Rent(source.TextLength);
                if (placements != null) { ArrayPool<GlyphPlacement>.Shared.Return(placements); }
                placements = nextPlacements;
            }
            GlyphFontDescriptor nextFont = default;
            fixed (GlyphPlacement* destination = placements)
            {
                var status = BindingC.SkiaReusableTextDrawSnapshotCopyGlyphs(source.NativePtr,
                    (byte*)&nextFont, (byte*)destination, (ulong)placements.Length * 12, out var count);
                if (status < 0) { return false; }
                GlyphCount = checked((int)count);
            }
            fontDescriptor = nextFont;
            ownerVersion = source.LayoutVersion;
            owner = source;
            IsWhiteSource = (fontDescriptor.PackedColor & 0x00ffffff) == 0x00ffffff;
            return true;
        }

        private GlyphLayoutSnapshot(IntPtr nativePtr)
        {
            ptr = nativePtr;
            try
            {
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaGlyphLayoutSnapshotGetGlyphCount(NativePtr, out var count));
                if (count > int.MaxValue / 52)
                {
                    throw new InvalidOperationException("Glyph layout exceeds supported capacity.");
                }

                GlyphCount = (int)count;
                glyphs = ArrayPool<GlyphLayoutDescriptor>.Shared.Rent(GlyphCount);
                CopyGlyphs();
                IsWhiteSource = true;
                for (var index = 0; index < GlyphCount; ++index)
                {
                    IsWhiteSource &= (glyphs[index].PackedColor & 0x00ffffff) == 0x00ffffff;
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal int GlyphCount { get; private set; }
        internal bool IsValid => glyphs != null ||
            (owner != null && owner.IsAlive && owner.LayoutVersion == ownerVersion);
        internal bool IsWhiteSource { get; private set; }

        internal GlyphLayoutDescriptor GetGlyph(int index)
        {
            ValidateIndex(index);
            if (owner != null)
            {
                throw new InvalidOperationException("An atlas view does not export glyph ink bounds.");
            }
            return glyphs![index];
        }

        internal AtlasGlyphDescriptor GetAtlasGlyph(int index)
        {
            ValidateIndex(index);
            return owner != null
                ? new AtlasGlyphDescriptor(fontDescriptor, placements![index])
                : new AtlasGlyphDescriptor(glyphs![index]);
        }

        private void ValidateIndex(int index)
        {
            if (!IsValid)
            {
                throw new ObjectDisposedException(nameof(GlyphLayoutSnapshot));
            }
            if ((uint)index >= (uint)GlyphCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        internal static GlyphLayoutSnapshot? TryCreate(ReusableTextDrawSnapshot source)
        {
            using var measurement = TextPipelineDiagnostics.Measure(TextPipelineStage.GlyphSnapshotExport);
            TextPipelineDiagnostics.Record(TextPipelineCounter.GlyphSnapshotExports);
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaGlyphLayoutSnapshotCreateFromText(source.NativePtr,
                out var nativePtr));
            return nativePtr == IntPtr.Zero ? null : new GlyphLayoutSnapshot(nativePtr);
        }

        internal static GlyphLayoutSnapshot? TryCreate(Paragraph source)
        {
            using var measurement = TextPipelineDiagnostics.Measure(TextPipelineStage.GlyphSnapshotExport);
            TextPipelineDiagnostics.Record(TextPipelineCounter.GlyphSnapshotExports);
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaGlyphLayoutSnapshotCreateFromParagraph(source.NativePtr,
                out var nativePtr));
            return nativePtr == IntPtr.Zero ? null : new GlyphLayoutSnapshot(nativePtr);
        }

        internal bool TryGetRasterBounds(int index, float scale, float phaseX, float phaseY,
            out RectInt bounds)
        {
            if (!IsValid) { bounds = default; return false; }
            if (owner != null)
            {
                var ownerStatus = BindingC.SkiaReusableTextDrawSnapshotGetRasterBounds(owner.NativePtr,
                    (ulong)index, scale, phaseX, phaseY, out var ownerLeft, out var ownerTop,
                    out var ownerWidth, out var ownerHeight);
                bounds = new RectInt(ownerLeft, ownerTop, ownerWidth, ownerHeight);
                return ownerStatus >= 0;
            }
            var status = BindingC.SkiaGlyphLayoutSnapshotGetRasterBounds(NativePtr,
                (ulong)index, scale, phaseX, phaseY, out var left, out var top, out var width, out var height);
            bounds = new RectInt(left, top, width, height);
            return status >= 0;
        }

        internal unsafe byte[] Rasterize(int index, float scale, float phaseX, float phaseY, int byteCount)
        {
            if (!IsValid) { throw new ObjectDisposedException(nameof(GlyphLayoutSnapshot)); }
            using var measurement = TextPipelineDiagnostics.Measure(TextPipelineStage.GlyphRasterization);
            var pixels = new byte[byteCount];
            fixed (byte* destination = pixels)
            {
                var status = owner != null
                    ? BindingC.SkiaReusableTextDrawSnapshotRasterize(owner.NativePtr,
                        (ulong)index, scale, phaseX, phaseY, destination, (ulong)pixels.Length)
                    : BindingC.SkiaGlyphLayoutSnapshotRasterize(NativePtr,
                        (ulong)index, scale, phaseX, phaseY, destination, (ulong)pixels.Length);
                ExitCodeUtil.ThrowIfFailed(status);
            }
            TextPipelineDiagnostics.Record(TextPipelineCounter.GlyphRasterizations);
            return pixels;
        }

        private unsafe void CopyGlyphs()
        {
            fixed (GlyphLayoutDescriptor* destination = glyphs)
            {
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaGlyphLayoutSnapshotCopyGlyphs(NativePtr,
                    (byte*)destination, (ulong)(GlyphCount * sizeof(GlyphLayoutDescriptor))));
            }
        }

        ~GlyphLayoutSnapshot()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposed) { return; }
            disposed = true;
            owner = null;
            if (placements != null)
            {
                ArrayPool<GlyphPlacement>.Shared.Return(placements);
                placements = null;
            }
            var buffer = glyphs;
            glyphs = null;
            GlyphCount = 0;
            if (buffer != null)
            {
                ArrayPool<GlyphLayoutDescriptor>.Shared.Return(buffer);
            }

            if (ptr != IntPtr.Zero)
            {
                var status = BindingC.SkiaGlyphLayoutSnapshotDestroy(ref ptr);
                if (disposing) { ExitCodeUtil.ThrowIfFailed(status); }
            }
        }
    }
}
