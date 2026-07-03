using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Milestro.Binding;
using Newtonsoft.Json;
using Paraparty.UnityNative.Base;
using UnityEngine;

namespace Milestro.Skia.TextLayout
{
    public class Paragraph : DisposableNativeObject
    {
        internal Paragraph(IntPtr ptr)
            : base(ptr)
        {
        }

        protected override void DisposeUnmanaged()
        {
            if (ptr != IntPtr.Zero)
            {
                var nativePtr = ptr;
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutParagraphDestroy(out nativePtr));
                ptr = nativePtr;
            }

            base.DisposeUnmanaged();
        }

        public void Layout(float width)
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutParagraphLayout(NativePtr, width));
        }

        public float Height
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutParagraphGetHeight(NativePtr, out var ret));
                return ret;
            }
        }

        public float LongestLine
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutParagraphGetLongestLine(NativePtr, out var ret));
                return ret;
            }
        }

        public float MaxIntrinsicWidth
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphGetMaxIntrinsicWidth(NativePtr, out var ret));
                return ret;
            }
        }

        public void Paint(Canvas canvas, Vector2 position)
        {
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutParagraphPaint(NativePtr, canvas.NativePtr, position.x, position.y)
            );
        }

        public void SplitGlypl(IntPtr context, Vector2 position,
            MilestroCTypes.SkiaTextlayoutParagraphSplitGlyphCallback callback)
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutParagraphSplitGlyph(NativePtr,
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
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutParagraphToSDF(NativePtr,
                sdfWidth, sdfHeight, sdfScale,
                x, y,
                distanceField
            ));
        }

        public Path ToPath(float x, float y)
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutParagraphToPath(NativePtr, out var pathPtr, x, y));
            return new Path(pathPtr);
        }
    }
}
