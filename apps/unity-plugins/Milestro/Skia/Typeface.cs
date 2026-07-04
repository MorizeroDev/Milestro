using System;
using System.Collections.Generic;
using Milestro.Binding;
using Milestro.Util;
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
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTypefaceDestroy(ref nativePtr));
                ptr = nativePtr;
            }

            base.DisposeUnmanaged();
        }

        public List<FontFamilyName> GetFontFamilyNames()
        {
            var list = IntPtr.Zero;
            try
            {
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTypefaceGetFamilyNameList(NativePtr, out list));
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTypefaceFamilyNameListGetSize(list, out var size));

                var familyNames = new List<FontFamilyName>(ToListCapacity(size));
                for (ulong i = 0; i < size; i++)
                {
                    ExitCodeUtil.ThrowIfFailed(
                        BindingC.SkiaTypefaceFamilyNameListRefElementAt(list, out var familyName, i));
                    familyNames.Add(ReadFontFamilyName(familyName));
                }

                return familyNames;
            }
            finally
            {
                if (list != IntPtr.Zero)
                {
                    ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTypefaceFamilyNameListDestroy(ref list));
                }
            }
        }

        private static FontFamilyName ReadFontFamilyName(IntPtr familyName)
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTypefaceFamilyNameGetName(familyName, out var namePtr,
                out var nameSize));
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTypefaceFamilyNameGetLanguage(familyName, out var languagePtr,
                out var languageSize));

            return new FontFamilyName
            {
                Name = NativeUtf8Util.ReadBorrowed(namePtr, nameSize),
                Language = NativeUtf8Util.ReadBorrowed(languagePtr, languageSize),
            };
        }

        private static int ToListCapacity(ulong size)
        {
            return size > int.MaxValue ? int.MaxValue : (int)size;
        }
    }
}
