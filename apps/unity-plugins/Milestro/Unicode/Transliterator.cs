using System;
using Milestro.Binding;
using Milestro.Model;
using Paraparty.UnityNative;
using Paraparty.UnityNative.Base;

namespace Milestro.Unicode
{
    /// <summary>
    /// Specifies the direction of transliteration.
    /// </summary>
    public enum UTransDirection
    {
        /// <summary>
        /// Specifies the forward direction, from <source> to <target> for a transliterator 
        /// with ID <source>-<target>. When a transliterator is opened using a rule, 
        /// it refers to forward direction rules, e.g., "A > B".
        /// </summary>
        UTRANS_FORWARD,

        /// <summary>
        /// Specifies the reverse direction, from <target> to <source> for a transliterator 
        /// with ID <source>-<target>. When a transliterator is opened using a rule, 
        /// it refers to reverse direction rules, e.g., "A < B".
        /// </summary>
        UTRANS_REVERSE
    }

    public class Transliterator : DisposableNativeObject
    {
        public Transliterator(string id, UTransDirection direction = UTransDirection.UTRANS_FORWARD) : base(true)
        {
            var idCStr = id.CStr();
            unsafe
            {
                fixed (byte* pathC = idCStr)
                {
                    GC.KeepAlive(this);
                    ExitCodeUtil.ThrowIfFailed(BindingC.UnicodeTransliteratorCreate(out ptr, pathC, (int)direction));
                }
            }
        }

        protected override void DisposeUnmanaged()
        {
            if (ptr != IntPtr.Zero)
            {
                GC.KeepAlive(this);
                ExitCodeUtil.ThrowIfFailed(BindingC.UnicodeTransliteratorDestroy(ref ptr));
            }

            base.DisposeUnmanaged();
        }

        public string Transliterate(string input)
        {
            var inputCStr = input.CStr();
            unsafe
            {
                fixed (byte* textC = inputCStr)
                {
                    ExitCodeUtil.ThrowIfFailed(BindingC.UnicodeTransliteratorTransliterate(ptr, out var value, textC));

                    using var ret = new BytesWrapper(value);
                    return ret.GetString();
                }
            }
        }
    }
}
