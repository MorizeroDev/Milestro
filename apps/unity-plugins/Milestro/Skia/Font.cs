using System;
using System.Text;
using Milestro.Binding;
using Paraparty.UnityNative.Base;
using UnityEngine;

namespace Milestro.Skia
{
    public class Font : DisposableNativeObject
    {
        internal Font(IntPtr font)
            : this(font, true)
        {
        }

        internal Font(IntPtr font, bool isEnabledDispose)
            : base(font, isEnabledDispose)
        {
        }

        protected override void DisposeUnmanaged()
        {
            if (ptr != IntPtr.Zero)
            {
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaFontDestroy(ref ptr));
            }

            base.DisposeUnmanaged();
        }

        public Path GetPath(ushort glyphId)
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaFontGetPath(NativePtr, out var path, glyphId));
            return new Path(path);
        }

        public FontMetrics GetMetrics()
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaFontGetMetrics(NativePtr,
                out var ascent,
                out var descent,
                out var leading));
            return new FontMetrics(ascent, descent, leading);
        }

        public unsafe FontTextMeasurement MeasureText(string text)
        {
            var payload = Encoding.UTF8.GetBytes(text ?? "");
            fixed (byte* textPtr = payload)
            {
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaFontMeasureText(NativePtr,
                    textPtr,
                    (ulong)payload.Length,
                    out var boundsLeft,
                    out var boundsTop,
                    out var boundsRight,
                    out var boundsBottom,
                    out var advanceX));
                var metrics = GetMetrics();
                return new FontTextMeasurement(Rect.MinMaxRect(boundsLeft, boundsTop, boundsRight, boundsBottom),
                    advanceX,
                    metrics);
            }
        }
    }
}
