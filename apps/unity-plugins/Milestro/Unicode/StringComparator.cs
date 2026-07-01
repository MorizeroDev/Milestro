using System;
using Milestro.Binding;
using Paraparty.UnityNative;
using Paraparty.UnityNative.Base;

namespace Milestro.Unicode
{
    public class StringComparator : DisposableNativeObject
    {
        public IntPtr Ptr { get; private set; }

        internal StringComparator(IntPtr ptr)
        {
            Ptr = ptr;
        }

        public unsafe StringComparator(string collation)
        {
            var s = collation.CStr();
            ExitCodeUtil.ThrowIfFailed(BindingC.StringComparatorCreate(out var ptr, s));
            Ptr = ptr;
        }

        protected override void DisposeUnmanaged()
        {
            var tPtr = Ptr;
            ExitCodeUtil.ThrowIfFailed(BindingC.StringComparatorDestroy(ref tPtr));
            Ptr = tPtr;

            base.DisposeUnmanaged();
        }

        public int Compare(string a, string b)
        {
            var ap = a.CStr();
            var bp = b.CStr();

            ExitCodeUtil.ThrowIfFailed(BindingC.StringComparatorCompare(Ptr, out var result, ap, bp));
            return result;
        }
    }
}
