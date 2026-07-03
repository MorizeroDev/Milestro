using System;
using Milestro.Binding;

namespace Milestro.Skia.TextLayout
{
    public class Font
    {
        public IntPtr Ptr { get; private set; }

        // Borrowed native pointer; Font does not own or destroy it.
        public Font(IntPtr font)
        {
            Ptr = font;
        }

        public Path GetPath(ushort glyphId)
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaFontGetPath(Ptr, out var path, glyphId));
            return new Path(path);
        }
    }
}
