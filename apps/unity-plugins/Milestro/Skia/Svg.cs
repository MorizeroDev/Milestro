using System;
using Milestro.Binding;
using Paraparty.UnityNative;
using Paraparty.UnityNative.Base;
using UnityEngine;

namespace Milestro.Skia
{
    public class Svg : DisposableNativeObject
    {
        internal Svg(IntPtr ptr)
            : base(ptr)
        {
        }

        public unsafe Svg(void* data, ulong size)
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaSvgCreate(out ptr, data, size));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="asset">SVG 数据，会被复制</param>
        /// <returns></returns>
        public static unsafe Svg MakeFromTextAsset(TextAsset asset)
        {
            fixed (byte* bptr = asset.bytes)
            {
                return new Svg(bptr, (ulong)asset.dataSize);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="data">SVG 数据，会被复制</param>
        /// <returns></returns>
        public static unsafe Svg MakeFromBytes(byte[] data)
        {
            fixed (byte* bptr = data)
            {
                return new Svg(bptr, (ulong)data.Length);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="data">SVG 数据，会被复制</param>
        /// <returns></returns>
        public static Svg MakeFromString(string data)
        {
            return MakeFromBytes(data.CStr());
        }

        protected override void DisposeUnmanaged()
        {
            if (ptr != IntPtr.Zero)
            {
                var nativePtr = ptr;
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaSvgDestroy(out nativePtr));
                ptr = nativePtr;
            }

            base.DisposeUnmanaged();
        }

        public void Render(Canvas canvas)
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaSvgRender(NativePtr, canvas.NativePtr));
        }
    }
}
