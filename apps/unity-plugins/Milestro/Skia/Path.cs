using System;
using Milestro.Binding;
using Paraparty.UnityNative.Base;

namespace Milestro.Skia
{
    public class Path : DisposableNativeObject
    {
        internal Path(IntPtr path)
            : base(path)
        {
        }

        protected override void DisposeUnmanaged()
        {
            if (ptr != IntPtr.Zero)
            {
                var nativePtr = ptr;
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaPathDestroy(out nativePtr));
                ptr = nativePtr;
            }

            base.DisposeUnmanaged();
        }

        public VertexData ToAATriangles(float tolerance)
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaPathToAATriangles(NativePtr, out var vertexData, tolerance));
            return new VertexData(vertexData);
        }
    }
}
