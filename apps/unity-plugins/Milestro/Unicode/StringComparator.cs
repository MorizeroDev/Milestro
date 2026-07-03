using System;
using Milestro.Binding;
using Paraparty.UnityNative;
using Paraparty.UnityNative.Base;

namespace Milestro.Unicode
{
    public class StringComparator : DisposableNativeObject
    {
        internal StringComparator(IntPtr ptr)
            : base(ptr)
        {
        }

        public unsafe StringComparator(string collation)
        {
            var s = collation.CStr();
            ExitCodeUtil.ThrowIfFailed(BindingC.StringComparatorCreate(out ptr, s));
        }

        protected override void DisposeUnmanaged()
        {
            if (ptr != IntPtr.Zero)
            {
                ExitCodeUtil.ThrowIfFailed(BindingC.StringComparatorDestroy(ref ptr));
            }

            base.DisposeUnmanaged();
        }

        public int Compare(string a, string b)
        {
            var ap = a.CStr();
            var bp = b.CStr();

            ExitCodeUtil.ThrowIfFailed(BindingC.StringComparatorCompare(NativePtr, out var result, ap, bp));
            return result;
        }
    }
}
