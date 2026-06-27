using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Milestro.Binding;
using Newtonsoft.Json;
using UnityEngine;

namespace Milestro.Skia.TextLayout
{
    public class Paragraph
    {
        public IntPtr Ptr { get; private set; }

        internal Paragraph(IntPtr ptr)
        {
            Ptr = ptr;
        }

        ~Paragraph()
        {
            var ptr = Ptr;
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutParagraphDestroy(out ptr));
            Ptr = ptr;
        }

        public void Layout(float width)
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutParagraphLayout(Ptr, width));
        }


        public void Paint(Canvas canvas, Vector2 position)
        {
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutParagraphPaint(Ptr, canvas.Ptr, position.x, position.y)
            );
        }

        public void SplitGlypl(IntPtr context, Vector2 position,
            MilestroCTypes.SkiaTextlayoutParagraphSplitGlyphCallback callback)
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutParagraphSplitGlyph(Ptr,
                context,
                position.x, position.y, callback
            ));
        }

        public unsafe void ToSDF(int sdfWidth,
            int sdfHeight,
            float sdfScale,
            float x, float y,
            void* distanceField)
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutParagraphToSDF(Ptr,
                sdfWidth, sdfHeight, sdfScale,
                x, y,
                distanceField
            ));
        }

        public Path ToPath(float x, float y)
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutParagraphToPath(Ptr, out var pathPtr, x, y));
            return new Path(pathPtr);
        }
    }
}