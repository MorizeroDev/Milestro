using System;
using Milestro.Binding;
using Milestro.Model;
using Paraparty.UnityNative;
using Paraparty.UnityNative.Base;

namespace Milestro.Unicode
{
    public class Normalizer : DisposableNativeObject
    {
        public Normalizer(string name, int mode = 0) : base(true)
        {
            var nameCStr = name.CStr();
            unsafe
            {
                fixed (byte* pathC = nameCStr)
                {
                    GC.KeepAlive(this);
                    ExitCodeUtil.ThrowIfFailed(BindingC.UnicodeNormalizerCreate(out ptr, pathC, mode));
                }
            }
        }

        protected override void DisposeUnmanaged()
        {
            if (ptr != IntPtr.Zero)
            {
                GC.KeepAlive(this);
                ExitCodeUtil.ThrowIfFailed(BindingC.UnicodeNormalizerDestroy(ref ptr));
            }

            base.DisposeUnmanaged();
        }

        public string Normalize(string text)
        {
            var textCStr = text.CStr();
            unsafe
            {
                fixed (byte* textC = textCStr)
                {
                    ExitCodeUtil.ThrowIfFailed(BindingC.UnicodeNormalizerNormalize(ptr, out var value, textC));

                    using var ret = new BytesWrapper(value);
                    return ret.GetString();
                }
            }
        }
    }
}
