using System.Runtime.InteropServices;

namespace Milestro.Skia
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct GlyphFontDescriptor
    {
        internal uint TypefaceId, FontFlags, PackedColor;
        internal float FontSize, FontScaleX, FontSkewX;
    }
}
