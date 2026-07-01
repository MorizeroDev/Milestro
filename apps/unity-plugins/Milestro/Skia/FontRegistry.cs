using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Milestro.Binding;
using Paraparty.UnityNative;

namespace Milestro.Skia
{
    public static class FontRegistry
    {
        public static void RegisterFontFromPath(string path)
        {
            var dat = path.CStr();
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaFontRegistryRegisterFontFromFile(dat));
        }

        public static List<string> GetRegisteredFontFamilyNames()
        {
            var list = IntPtr.Zero;
            try
            {
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaFontRegistryGetRegisteredFontFamilyList(out list));
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaFontFamilyListGetSize(list, out var size));

                var fontFamilyNames = new List<string>(ToListCapacity(size));
                for (ulong i = 0; i < size; i++)
                {
                    ExitCodeUtil.ThrowIfFailed(BindingC.SkiaFontFamilyListRefElementAt(list, out var familyInfo, i));
                    ExitCodeUtil.ThrowIfFailed(BindingC.SkiaFontFamilyInfoGetName(familyInfo,
                        out var namePtr,
                        out var nameSize));
                    fontFamilyNames.Add(ReadNativeUtf8(namePtr, nameSize));
                }

                return fontFamilyNames;
            }
            finally
            {
                if (list != IntPtr.Zero)
                {
                    ExitCodeUtil.ThrowIfFailed(BindingC.SkiaFontFamilyListDestroy(ref list));
                }
            }
        }

        public static List<FontFaceInfo> GetRegisteredFontFaces()
        {
            var list = IntPtr.Zero;
            try
            {
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaFontRegistryGetRegisteredFontFaceList(out list));
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaFontFaceListGetSize(list, out var size));

                var fontFaces = new List<FontFaceInfo>(ToListCapacity(size));
                for (ulong i = 0; i < size; i++)
                {
                    ExitCodeUtil.ThrowIfFailed(BindingC.SkiaFontFaceListRefElementAt(list, out var faceInfo, i));
                    fontFaces.Add(ReadFontFaceInfo(faceInfo));
                }

                return fontFaces;
            }
            finally
            {
                if (list != IntPtr.Zero)
                {
                    ExitCodeUtil.ThrowIfFailed(BindingC.SkiaFontFaceListDestroy(ref list));
                }
            }
        }

        private static FontFaceInfo ReadFontFaceInfo(IntPtr faceInfo)
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaFontFaceInfoGetSourcePath(faceInfo,
                out var sourcePathPtr,
                out var sourcePathSize));
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaFontFaceInfoGetFamilyName(faceInfo,
                out var familyNamePtr,
                out var familyNameSize));
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaFontFaceInfoGetFaceIndex(faceInfo, out var faceIndex));
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaFontFaceInfoGetInstanceIndex(faceInfo, out var instanceIndex));
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaFontFaceInfoGetPackedIndex(faceInfo, out var packedIndex));
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaFontFaceInfoGetWeight(faceInfo, out var weight));
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaFontFaceInfoGetWidth(faceInfo, out var width));
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaFontFaceInfoGetSlant(faceInfo, out var slant));
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaFontFaceInfoGetFixedPitch(faceInfo, out var fixedPitch));

            return new FontFaceInfo
            {
                SourcePath = ReadNativeUtf8(sourcePathPtr, sourcePathSize),
                FamilyName = ReadNativeUtf8(familyNamePtr, familyNameSize),
                FaceIndex = faceIndex,
                InstanceIndex = instanceIndex,
                PackedIndex = packedIndex,
                Weight = weight,
                Width = width,
                Slant = slant,
                FixedPitch = fixedPitch != 0,
            };
        }

        private static string ReadNativeUtf8(IntPtr ptr, ulong size)
        {
            if (ptr == IntPtr.Zero || size == 0)
            {
                return string.Empty;
            }

            if (size > int.MaxValue)
            {
                throw new Exception("Native UTF-8 string is too large.");
            }

            var bytes = new byte[(int)size];
            Marshal.Copy(ptr, bytes, 0, bytes.Length);
            return Encoding.UTF8.GetString(bytes);
        }

        private static int ToListCapacity(ulong size)
        {
            return size > int.MaxValue ? int.MaxValue : (int)size;
        }
    }
}
