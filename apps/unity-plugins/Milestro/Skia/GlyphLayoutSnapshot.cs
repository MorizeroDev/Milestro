using System;
using System.Runtime.InteropServices;
using Milestro.Binding;
using Paraparty.UnityNative.Base;
using UnityEngine;
using Paragraph = Milestro.Skia.TextLayout.Paragraph;

namespace Milestro.Skia
{
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct GlyphLayoutDescriptor
    {
        internal readonly uint TypefaceId;
        internal readonly uint GlyphId;
        internal readonly uint FontFlags;
        internal readonly uint PackedColor;
        internal readonly float FontSize;
        internal readonly float FontScaleX;
        internal readonly float FontSkewX;
        internal readonly float X;
        internal readonly float Y;
        internal readonly float Left;
        internal readonly float Top;
        internal readonly float Right;
        internal readonly float Bottom;

        internal Color32 Color => new Color32((byte)(PackedColor >> 16), (byte)(PackedColor >> 8),
            (byte)PackedColor, (byte)(PackedColor >> 24));
    }

    internal sealed class GlyphLayoutSnapshot : DisposableNativeObject
    {
        private GlyphLayoutSnapshot(IntPtr nativePtr) : base(nativePtr)
        {
            try
            {
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaGlyphLayoutSnapshotGetGlyphCount(NativePtr, out var count));
                if (count > int.MaxValue / 52)
                {
                    throw new InvalidOperationException("Glyph layout exceeds supported capacity.");
                }

                Glyphs = new GlyphLayoutDescriptor[(int)count];
                CopyGlyphs();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal GlyphLayoutDescriptor[] Glyphs { get; }

        internal static GlyphLayoutSnapshot? TryCreate(ReusableTextDrawSnapshot source)
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaGlyphLayoutSnapshotCreateFromText(source.NativePtr,
                out var nativePtr));
            return nativePtr == IntPtr.Zero ? null : new GlyphLayoutSnapshot(nativePtr);
        }

        internal static GlyphLayoutSnapshot? TryCreate(Paragraph source)
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaGlyphLayoutSnapshotCreateFromParagraph(source.NativePtr,
                out var nativePtr));
            return nativePtr == IntPtr.Zero ? null : new GlyphLayoutSnapshot(nativePtr);
        }

        internal bool TryGetRasterBounds(int index, float scale, out RectInt bounds)
        {
            var status = BindingC.SkiaGlyphLayoutSnapshotGetRasterBounds(NativePtr,
                (ulong)index, scale, out var left, out var top, out var width, out var height);
            bounds = new RectInt(left, top, width, height);
            return status >= 0;
        }

        internal unsafe byte[] Rasterize(int index, float scale, int byteCount)
        {
            var pixels = new byte[byteCount];
            fixed (byte* destination = pixels)
            {
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaGlyphLayoutSnapshotRasterize(NativePtr,
                    (ulong)index, scale, destination, (ulong)pixels.Length));
            }

            return pixels;
        }

        private unsafe void CopyGlyphs()
        {
            fixed (GlyphLayoutDescriptor* destination = Glyphs)
            {
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaGlyphLayoutSnapshotCopyGlyphs(NativePtr,
                    (byte*)destination, (ulong)(Glyphs.Length * sizeof(GlyphLayoutDescriptor))));
            }
        }

        protected override void DisposeUnmanaged()
        {
            if (ptr != IntPtr.Zero)
            {
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaGlyphLayoutSnapshotDestroy(ref ptr));
            }

            base.DisposeUnmanaged();
        }
    }
}
