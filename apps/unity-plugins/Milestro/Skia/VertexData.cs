using System;
using Milestro.Binding;
using Paraparty.UnityNative.Base;

namespace Milestro.Skia.TextLayout
{
    public class VertexData : DisposableNativeObject
    {
        internal VertexData(IntPtr data)
            : base(data)
        {
        }

        protected override void DisposeUnmanaged()
        {
            if (ptr != IntPtr.Zero)
            {
                var nativePtr = ptr;
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaVertexDataDestroy(out nativePtr));
                ptr = nativePtr;
            }

            base.DisposeUnmanaged();
        }

        public ulong VertexCount
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaVertexDataGetVertexCount(NativePtr, out var ret)
                );
                return ret;
            }
        }

        public ulong VertexSize
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaVertexDataGetVertexSize(NativePtr, out var ret)
                );
                return ret;
            }
        }

        public unsafe float[] GetVertices()
        {
            var ret = new float[VertexCount * VertexSize / sizeof(float)];
            fixed (void* ptr = ret)
            {
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaVertexDataFillData(NativePtr, ptr));
            }

            return ret;
        }
    }
}
