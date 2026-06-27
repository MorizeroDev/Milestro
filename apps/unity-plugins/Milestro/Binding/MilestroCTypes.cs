using System;

namespace Milestro.Binding
{
    public class MilestroCTypes
    {
        public delegate ulong SkiaTextlayoutParagraphSplitGlyphCallback(IntPtr context, UInt16 glyphId, IntPtr font,
            float boundLeft, float boundTop,
            float boundRight, float boundBottom,
            float advanceWidth, float advanceHeight);
    }
}