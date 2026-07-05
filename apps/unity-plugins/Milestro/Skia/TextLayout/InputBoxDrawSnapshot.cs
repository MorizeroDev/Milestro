using System;
using Milestro.Binding;
using Paraparty.UnityNative.Base;

namespace Milestro.Skia.TextLayout
{
    internal sealed class InputBoxDrawSnapshot : DisposableNativeObject
    {
        internal InputBoxDrawSnapshot(IntPtr ptr)
            : base(ptr)
        {
        }

        internal IntPtr DetachNativePtr()
        {
            var nativePtr = NativePtr;
            ptr = IntPtr.Zero;
            return nativePtr;
        }

        protected override void DisposeUnmanaged()
        {
            if (ptr != IntPtr.Zero)
            {
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxDrawSnapshotDestroy(ref ptr));
            }

            base.DisposeUnmanaged();
        }
    }
}
