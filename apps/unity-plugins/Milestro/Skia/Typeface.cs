using System;
using System.Collections.Generic;
using System.Text;
using Milestro.Binding;
using Newtonsoft.Json;
using Paraparty.UnityNative.Base;

namespace Milestro.Skia
{
    public class TypeFace : DisposableNativeObject
    {
        internal TypeFace(IntPtr typeFace)
            : base(typeFace)
        {
        }

        protected override void DisposeUnmanaged()
        {
            if (ptr != IntPtr.Zero)
            {
                var nativePtr = ptr;
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTypefaceDestroy(out nativePtr));
                ptr = nativePtr;
            }

            base.DisposeUnmanaged();
        }

        public List<FontFamilyName> GetFontFamilyNames()
        {
            var ret = new byte[4096];
            ulong needed;
            var err = BindingC.SkiaTypefaceGetFamilyNames(NativePtr, ret, 4096, out needed);
            if (err < 0)
            {
                throw new Exception("SkiaTypeFaceGetFamilyNames Error");
            }

            var s = Encoding.UTF8.GetString(ret).TrimEnd('\0');
            return JsonConvert.DeserializeObject<List<FontFamilyName>>(s) ?? new List<FontFamilyName>();
        }
    }
}
