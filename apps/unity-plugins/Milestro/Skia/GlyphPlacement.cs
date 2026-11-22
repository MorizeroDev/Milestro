using System.Runtime.InteropServices;

namespace Milestro.Skia
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct GlyphPlacement
    {
        internal uint GlyphId;
        internal float X, Y;
    }
}
