using System;
using System.Runtime.InteropServices;
using AOT;
using Milestro.Binding;
using Milestro.Components;

namespace Milestro.Util
{
    internal class SplitGlyphCallback
    {
        [MonoPInvokeCallback(typeof(MilestroCTypes.SkiaTextlayoutParagraphSplitGlyphCallback))]
        internal static ulong SkiaTextlayoutParagraphSplitGlyphCallback(IntPtr context, UInt16 glyphId, IntPtr font,
            float boundLeft, float boundTop,
            float boundRight, float boundBottom,
            float advanceWidth, float advanceHeight)
        {
            GCHandle handle = (GCHandle)context;
            var contextTarget = handle.Target as SkParagraphBitmapTextBox;
            contextTarget?.SplitGlyphInfoCallback(glyphId, font, boundLeft, boundTop, boundRight, boundBottom,
                advanceWidth, advanceHeight);
            return 0;
        }
    }
}
