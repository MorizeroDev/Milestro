using System;
using Milestro.Binding;
using Milestro.Model;
using Paraparty.UnityNative;
using Paraparty.UnityNative.Base;

namespace Milestro.Unicode
{
    public class Segmenter : DisposableNativeObject
    {
        public Segmenter(string locale, string text) : base(true)
        {
            var localeCStr = locale.CStr();
            var textCStr = text.CStr();
            unsafe
            {
                fixed (byte* localeC = localeCStr)
                {
                    fixed (byte* textC = textCStr)
                    {
                        GC.KeepAlive(this);
                        ExitCodeUtil.ThrowIfFailed(BindingC.UnicodeSegmenterCreate(out ptr, localeC, textC));
                    }
                }
            }
        }

        protected override void DisposeUnmanaged()
        {
            if (ptr != IntPtr.Zero)
            {
                GC.KeepAlive(this);
                ExitCodeUtil.ThrowIfFailed(BindingC.UnicodeSegmenterDestroy(ref ptr));
            }

            base.DisposeUnmanaged();
        }

        public int First()
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.UnicodeSegmenterFirst(ptr, out var ret));
            return ret;
        }

        public int Next()
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.UnicodeSegmenterNext(ptr, out var ret));
            return ret;
        }

        public int Current()
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.UnicodeSegmenterCurrent(ptr, out var ret));
            return ret;
        }

        public int Previous()
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.UnicodeSegmenterPrevious(ptr, out var ret));
            return ret;
        }

        public string CurrentAndNext(int start, int len)
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.UnicodeSegmenterSubString(ptr, out var value, start, len));

            using var ret = new BytesWrapper(value);
            return ret.GetString();
        }
    }
}
