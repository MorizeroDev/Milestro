using System;
using System.Collections.Generic;
using System.Text;
using Milestro.Binding;
using Milestro.Model;
using Milestro.Skia.TextLayout;
using Milestro.Util;

namespace Milestro.Skia
{
    public static class FontRegistry
    {
        public static unsafe Font ResolveFont(string familyName,
                                              int weight = FontWeight.Normal,
                                              float size = 16f,
                                              bool fallbackToSystem = true)
        {
            var payload = Encoding.UTF8.GetBytes(familyName ?? "");
            fixed (byte* familyPtr = payload)
            {
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaFontRegistryResolveTypeface(out var font,
                    familyPtr,
                    (ulong)payload.Length,
                    weight,
                    size,
                    fallbackToSystem ? 1 : 0));
                return new Font(font);
            }
        }

        public static void RegisterFontFromPath(string path)
        {
            var payload = Encoding.UTF8.GetBytes(path ?? "");
            unsafe
            {
                fixed (byte* pathPtr = payload)
                {
                    ExitCodeUtil.ThrowIfFailed(BindingC.SkiaFontRegistryRegisterFontFromFile(pathPtr,
                        (ulong)payload.Length));
                }
            }
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
                    fontFamilyNames.Add(NativeUtf8Util.ReadBorrowed(namePtr, nameSize));
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
                SourcePath = NativeUtf8Util.ReadBorrowed(sourcePathPtr, sourcePathSize),
                FamilyName = NativeUtf8Util.ReadBorrowed(familyNamePtr, familyNameSize),
                FaceIndex = faceIndex,
                InstanceIndex = instanceIndex,
                PackedIndex = packedIndex,
                Weight = weight,
                Width = width,
                Slant = slant,
                FixedPitch = fixedPitch != 0,
            };
        }

        private static int ToListCapacity(ulong size)
        {
            return size > int.MaxValue ? int.MaxValue : (int)size;
        }
    }
}
