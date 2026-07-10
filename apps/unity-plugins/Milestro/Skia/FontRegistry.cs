using System;
using System.Collections.Generic;
using System.Text;
using Milestro.Binding;
using Milestro.Configuration;
using Milestro.Model;
using Milestro.Skia.TextLayout;
using Milestro.Util;
using Paraparty.UnityNative;

namespace Milestro.Skia
{
    public static class FontRegistry
    {
        private static readonly object FontFamilyConfigurationSyncLock = new object();

        public static unsafe Font ResolveFont(string familyName,
                                              int weight = FontWeight.Normal,
                                              float size = 16f,
                                              bool fallbackToSystem = true)
        {
            return ResolveFontFromFamilies(new[] { familyName ?? "" }, weight, size, fallbackToSystem);
        }

        public static Font ResolveFontFromFamilies(IEnumerable<string> familyNames,
                                                   int weight = FontWeight.Normal,
                                                   float size = 16f,
                                                   bool fallbackToSystem = true)
        {
            return ResolveFontFamilyTokens(FontFamilyDeclaration.ToBareTokens(familyNames),
                weight,
                size,
                fallbackToSystem);
        }

        public static Font ResolveFontFromFamilyTokens(IEnumerable<FontFamilyToken> familyTokens,
                                                       int weight = FontWeight.Normal,
                                                       float size = 16f,
                                                       bool fallbackToSystem = true)
        {
            return ResolveFontFamilyTokens(familyTokens,
                weight,
                size,
                fallbackToSystem);
        }

        internal static void ApplyTextStyleFontFamilyTokens(IntPtr textStyle, IEnumerable<FontFamilyToken> familyTokens)
        {
            SyncFontFamilyConfiguration();
            unsafe
            {
                var payload = BuildFontFamilyPayload(familyTokens);
                using var families = new UnmanagedStringArray(payload.Names);
                fixed (int* kinds = payload.Kinds)
                {
                    ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutTextStyleSetFontFamilyTokens(textStyle,
                        families.Ptr,
                        kinds,
                        families.Length));
                }
            }
        }

        internal static void ApplyStrutStyleFontFamilyTokens(IntPtr strutStyle, IEnumerable<FontFamilyToken> familyTokens)
        {
            SyncFontFamilyConfiguration();
            unsafe
            {
                var payload = BuildFontFamilyPayload(familyTokens);
                using var families = new UnmanagedStringArray(payload.Names);
                fixed (int* kinds = payload.Kinds)
                {
                    ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutStrutStyleSetFontFamilyTokens(strutStyle,
                        families.Ptr,
                        kinds,
                        families.Length));
                }
            }
        }

        private static unsafe Font ResolveFontFamilyTokens(IEnumerable<FontFamilyToken> familyTokens,
                                                          int weight,
                                                          float size,
                                                          bool fallbackToSystem)
        {
            SyncFontFamilyConfiguration();
            var payload = BuildFontFamilyPayload(familyTokens);
            using var families = new UnmanagedStringArray(payload.Names);
            fixed (int* kinds = payload.Kinds)
            {
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaFontRegistryResolveTypefaceFromFamilyTokens(out var font,
                    families.Ptr,
                    kinds,
                    families.Length,
                    weight,
                    size,
                    fallbackToSystem ? 1 : 0));
                return new Font(font);
            }
        }

        private static void SyncFontFamilyConfiguration()
        {
            lock (FontFamilyConfigurationSyncLock)
            {
                unsafe
                {
                    ExitCodeUtil.ThrowIfFailed(BindingC.SkiaFontRegistryResetFontFamilyKeywordMappings());
                }

                var configuration = MilestroConfiguration.Configuration?.FontFamily;
                if (configuration == null)
                {
                    return;
                }

                foreach (var mapping in configuration.GetKeywordMappings())
                {
                    SyncFontFamilyKeywordMapping(mapping.Key, mapping.Value);
                }
            }
        }

        private static unsafe void SyncFontFamilyKeywordMapping(string keyword, IEnumerable<FontFamilyToken> familyTokens)
        {
            var keywordBytes = Encoding.UTF8.GetBytes(keyword ?? "");
            var payload = BuildFontFamilyPayload(familyTokens);
            using var families = new UnmanagedStringArray(payload.Names);
            fixed (byte* keywordPtr = keywordBytes)
            fixed (int* kinds = payload.Kinds)
            {
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaFontRegistrySetFontFamilyKeywordMapping(keywordPtr,
                    (ulong)keywordBytes.Length,
                    families.Ptr,
                    kinds,
                    families.Length));
            }
        }

        private static FontFamilyPayload BuildFontFamilyPayload(IEnumerable<FontFamilyToken> familyTokens)
        {
            var names = new List<string>();
            var kinds = new List<int>();
            if (familyTokens != null)
            {
                foreach (var token in familyTokens)
                {
                    if (token.Value.Length == 0)
                    {
                        continue;
                    }

                    names.Add(token.Value);
                    kinds.Add((int)token.Kind);
                }
            }

            return new FontFamilyPayload(names, kinds.ToArray());
        }

        private sealed class FontFamilyPayload
        {
            public FontFamilyPayload(List<string> names, int[] kinds)
            {
                Names = names;
                Kinds = kinds;
            }

            public List<string> Names { get; }

            public int[] Kinds { get; }
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
