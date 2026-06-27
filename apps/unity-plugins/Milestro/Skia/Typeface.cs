using System;
using System.Collections.Generic;
using System.Text;
using Milestro.Binding;
using Newtonsoft.Json;

namespace Milestro.Skia
{
    public class TypeFace
    {
        public IntPtr Ptr { get; private set; }

        internal TypeFace(IntPtr typeFace)
        {
            Ptr = typeFace;
        }

        ~TypeFace()
        {
            var ptr = Ptr;
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTypefaceDestroy(out ptr));
            Ptr = ptr;
        }

        public List<FontFamilyName> GetFontFamilyNames()
        {
            var ret = new byte[4096];
            ulong needed;
            var err = BindingC.SkiaTypefaceGetFamilyNames(Ptr, ret, 4096, out needed);
            if (err < 0)
            {
                throw new Exception("SkiaTypeFaceGetFamilyNames Error");
            }

            var s = Encoding.UTF8.GetString(ret).TrimEnd('\0');
            return JsonConvert.DeserializeObject<List<FontFamilyName>>(s);
        }
    }
}