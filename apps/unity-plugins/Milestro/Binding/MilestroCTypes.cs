using System;

namespace Milestro.Binding
{
    public class MilestroCTypes
    {
        public delegate ulong SkiaTextlayoutParagraphSplitGlyphCallback(IntPtr context, UInt16 glyphId, IntPtr font,
            float boundLeft, float boundTop,
            float boundRight, float boundBottom,
            float advanceWidth, float advanceHeight);

        public delegate ulong SkiaTextlayoutParagraphRectCallback(IntPtr context,
            float left, float top, float right, float bottom);
    }
}
