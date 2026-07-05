using System;
using System.Text;
using Milestro.Binding;
using Paraparty.UnityNative.Base;
using UnityEngine;
using SkFont = Milestro.Skia.Font;

namespace Milestro.Skia
{
    internal sealed class TextDrawSnapshot : DisposableNativeObject
    {
        internal unsafe TextDrawSnapshot(SkFont font, string text, Color32 color)
        {
            var payload = Encoding.UTF8.GetBytes(text ?? "");
            fixed (byte* textPtr = payload)
            {
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextDrawSnapshotCreate(out ptr,
                    font.NativePtr,
                    textPtr,
                    (ulong)payload.Length,
                    color.r,
                    color.g,
                    color.b,
                    color.a));
            }
        }

        protected override void DisposeUnmanaged()
        {
            if (ptr != IntPtr.Zero)
            {
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextDrawSnapshotDestroy(ref ptr));
            }

            base.DisposeUnmanaged();
        }
    }
}
