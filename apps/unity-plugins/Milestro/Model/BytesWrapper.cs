using System;
using System.Runtime.InteropServices;
using System.Text;
using Milestro.Binding;
using Milestro.Util;
using Paraparty.UnityNative.Base;

namespace Milestro.Model
{
    internal class BytesWrapper : DisposableNativeObject
    {
        internal unsafe BytesWrapper(string data, bool isEnabledDispose) : base(isEnabledDispose)
        {
            var bytes = Encoding.UTF8.GetBytes(data);
            fixed (byte* bytesP = bytes)
            {
                ExitCodeUtil.ThrowIfFailed(BindingC.GameModelBytesWrapperCreate(out ptr, bytesP, (ulong)bytes.Length));
            }
        }

        internal BytesWrapper(IntPtr tPtr)
        {
            ptr = tPtr;
        }

        protected override void DisposeUnmanaged()
        {
            if (ptr != IntPtr.Zero)
            {
                ExitCodeUtil.ThrowIfFailed(BindingC.GameModelDataEnvelopDestroy(ref ptr));
                GC.KeepAlive(this);
            }

            base.DisposeUnmanaged();
        }

        public string GetString()
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.GameModelBytesWrapperCStr(ptr, out var dataPtr, out var size));
            return NativeUtf8Util.ReadBorrowed(dataPtr, size);
        }

        public byte[] GetBytes()
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.GameModelBytesWrapperCStr(ptr, out var dataPtr, out var size));
            if (size == 0)
            {
                return Array.Empty<byte>();
            }

            var bytes = new byte[size];
            Marshal.Copy(dataPtr, bytes, 0, (int)size);
            return bytes;
        }

        public override string ToString()
        {
            return ptr != IntPtr.Zero ? GetString() : "";
        }
    }
}
