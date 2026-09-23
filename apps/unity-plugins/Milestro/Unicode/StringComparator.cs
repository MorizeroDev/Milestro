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
            fixed (byte* collationPtr = s)
            {
                ExitCodeUtil.ThrowIfFailed(BindingC.StringComparatorCreate(out ptr, collationPtr));
            }
        }

        protected override void DisposeUnmanaged()
        {
            if (ptr != IntPtr.Zero)
            {
                ExitCodeUtil.ThrowIfFailed(BindingC.StringComparatorDestroy(ref ptr));
            }

            base.DisposeUnmanaged();
        }

        public unsafe int Compare(string a, string b)
        {
            var ap = a.CStr();
            var bp = b.CStr();

            fixed (byte* firstPtr = ap)
            fixed (byte* secondPtr = bp)
            {
                ExitCodeUtil.ThrowIfFailed(BindingC.StringComparatorCompare(NativePtr, out var result, firstPtr, secondPtr));
                return result;
            }
        }
    }
}
