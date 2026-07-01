using System;
using Milestro.Binding;
using Paraparty.UnityNative;

namespace Milestro.Unicode
{
    public static class Icu
    {
        public static bool IsLoaded()
        {
            var ret = BindingC.IsICULoaded(out var loaded);
            if (ret < 0)
            {
                throw new System.Exception("Failed to query ICU load state");
            }

            return loaded != 0;
        }

        public static unsafe void LoadIcuFronPath(string path)
        {
            var pathCStr = path.CStr();
            fixed (byte* pathPtr = pathCStr)
            {
                var ret = BindingC.LoadICU((byte*)IntPtr.Zero.ToPointer(), pathPtr);
                if (ret < 0)
                {
                    throw new System.Exception($"Failed to load ICU from {path}");
                }
            }
        }

        public static unsafe void LoadIcuFronMemory(byte[] dat)
        {
            fixed (byte* datPtr = dat)
            {
                var ret = BindingC.CopyAndLoadICU(datPtr, (ulong)dat.Length, (byte*)IntPtr.Zero.ToPointer());
                if (ret < 0)
                {
                    throw new System.Exception($"Failed to load ICU from memory");
                }
            }
        }
    }
}
