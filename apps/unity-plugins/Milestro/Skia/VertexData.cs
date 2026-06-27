using System;
using Milestro.Binding;

namespace Milestro.Skia.TextLayout
{
    public class VertexData : IDisposable
    {
        public IntPtr Ptr { get; private set; }

        internal VertexData(IntPtr data)
        {
            Ptr = data;
        }

        public void Dispose()
        {
            var ptr = Ptr;
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaVertexDataDestroy(out ptr));
            Ptr = ptr;
        }

        ~VertexData()
        {
        }

        public ulong VertexCount
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaVertexDataGetVertexCount(Ptr, out var ret)
                );
                return ret;
            }
        }

        public ulong VertexSize
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaVertexDataGetVertexSize(Ptr, out var ret)
                );
                return ret;
            }
        }

        public unsafe float[] GetVertices()
        {
            var ret = new float[VertexCount * VertexSize / sizeof(float)];
            fixed (void* ptr = ret)
            {
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaVertexDataFillData(Ptr, ptr));
            }

            return ret;
        }
    }
}