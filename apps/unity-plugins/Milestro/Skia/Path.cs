using System;
using Milestro.Binding;

namespace Milestro.Skia.TextLayout
{
    public class Path
    {
        public IntPtr Ptr { get; private set; }

        internal Path(IntPtr path)
        {
            Ptr = path;
        }

        ~Path()
        {
            var ptr = Ptr;
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaPathDestroy(out ptr));
            Ptr = ptr;
        }

        public VertexData ToAATriangles(float tolerance)
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaPathToAATriangles(Ptr, out var vertexData, tolerance));
            return new VertexData(vertexData);
        }
    }
}