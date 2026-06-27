using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Milestro.Binding;
using Milestro.Native;
using Newtonsoft.Json;

namespace Milestro.Skia
{
    public static class FontManager
    {
        public static void RegisterFontFromPath(string path)
        {
            var dat = path.CStr();
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaFontManagerRegisterFontFromFile(dat));
        }

        private static bool GetFontFamilyNamesImpl(ulong bufferSize,
            [MaybeNullWhen(false)] out List<string> fontFamilyNames)
        {
            var ret = new byte[bufferSize];
            ulong needed;
            var err = BindingC.SkiaFontManagerGetFontFamilies(ret, bufferSize, out needed);
            if (err < 0)
            {
                throw new Exception("SkiaFontManagerGetFontFamilies Error");
            }

            bool enoughSpace = bufferSize >= needed;
            if (enoughSpace)
            {
                var s = Encoding.UTF8.GetString(ret).TrimEnd('\0');
                fontFamilyNames = JsonConvert.DeserializeObject<List<string>>(s) ?? new List<string>();
            }
            else
            {
                fontFamilyNames = new List<string>();
            }

            return enoughSpace;
        }

        public static List<string> GetFontFamilyNames()
        {
            ulong maxSize = 1024 * 1024; // 1 MB
            ulong size = 4096;

            while (size <= maxSize)
            {
                if (GetFontFamilyNamesImpl(size, out var fontFamilyNames))
                {
                    return fontFamilyNames;
                }

                size *= 2;
            }

            throw new Exception("Buffer size exceeded the maximum limit of 1 MB.");
        }
    }
}