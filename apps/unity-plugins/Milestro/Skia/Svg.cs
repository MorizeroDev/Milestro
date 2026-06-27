using System;
using Milestro.Binding;
using Milestro.Native;
using UnityEngine;

namespace Milestro.Skia.TextLayout
{
    public class Svg
    {
        public IntPtr Ptr { get; private set; }

        internal Svg(IntPtr ptr)
        {
            Ptr = ptr;
        }

        public unsafe Svg(void* data, ulong size)
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaSvgCreate(out var ptr, data, size));
            Ptr = ptr;
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

        ~Svg()
        {
            var ptr = Ptr;
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaSvgDestroy(out ptr));
            Ptr = ptr;
        }

        public void Render(Canvas canvas)
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaSvgRender(Ptr, canvas.Ptr));
        }
    }
}