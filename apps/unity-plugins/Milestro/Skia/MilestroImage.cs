using System;
using Milestro.Binding;
using Paraparty.UnityNative.Base;
using UnityEngine;

namespace Milestro.Skia
{
    public class MilestroImage : DisposableNativeObject
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="data">图像数据，会被复制</param>
        /// <param name="size"></param>
        public unsafe MilestroImage(void* data, ulong size)
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaImageCreate(out ptr, data, size));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="asset">图像数据，会被复制</param>
        /// <returns></returns>
        public static unsafe MilestroImage MakeFromTextAsset(TextAsset asset)
        {
            fixed (byte* bptr = asset.bytes)
            {
                return new MilestroImage(bptr, (ulong)asset.dataSize);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="data">图像数据，会被复制</param>
        /// <returns></returns>
        public static unsafe MilestroImage MakeFromBytes(byte[] data)
        {
            fixed (byte* bptr = data)
            {
                return new MilestroImage(bptr, (ulong)data.Length);
            }
        }


        /// <summary>
        /// 如果发生了错误，那么这个 Image 对象直接失效
        ///
        /// <see href="https://source.chromium.org/chromium/chromium/src/+/main:third_party/skia/include/core/SkColorType.h">这里一定要传一个 kRGBA_8888_SkColorType，不然没法用</see>
        /// </summary>
        /// <param name="targetColorType"></param>
        public void SetColorType(int targetColorType)
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaImageSetColorType(NativePtr, targetColorType));
        }

        public int Width
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaImageGetWidth(NativePtr, out var ret)
                );
                return ret;
            }
        }

        public int Height
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaImageGetHeight(NativePtr, out var ret)
                );
                return ret;
            }
        }

        protected override void DisposeUnmanaged()
        {
            if (ptr != IntPtr.Zero)
            {
                var nativePtr = ptr;
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaImageDestroy(out nativePtr));
                ptr = nativePtr;
            }

            base.DisposeUnmanaged();
        }
    }
}
