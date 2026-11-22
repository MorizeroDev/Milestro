using System.Runtime.InteropServices;
using UnityEngine;

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
}
