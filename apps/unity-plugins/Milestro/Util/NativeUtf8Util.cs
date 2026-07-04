using System;
using System.Runtime.InteropServices;

namespace Milestro.Util
{
    internal static class NativeUtf8Util
    {
        internal static string ReadBorrowed(IntPtr ptr, ulong size)
        {
            if (ptr == IntPtr.Zero || size == 0)
            {
                return string.Empty;
            }

            if (size > int.MaxValue)
            {
                throw new Exception("Native UTF-8 string is too large.");
            }

            return Marshal.PtrToStringUTF8(ptr, (int)size);
        }
    }
}
