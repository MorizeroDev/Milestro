using System;
using Milestro.Binding;
using Paraparty.UnityNative.Base;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Milestro.Skia
{
    public class Canvas : DisposableNativeObject
    {
        public int Width { get; }
        public int Height { get; }

        /// <summary>
        /// 在当前对象中申请 Bitmap。Canvas 拥有 Texture 的所有权。
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        public unsafe Canvas(int width, int height)
        {
            Width = width;
            Height = height;
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaCanvasCreate(out ptr, width, height));
        }

        /// <summary>
        /// 从外部的 Texture2D 中的 Bitmap 创建 Canvas。
        /// </summary>
        /// <param name="tex"></param>
        /// <param name="verticalFlip"></param>
        /// <param name="clearPixels"></param>
        public unsafe Canvas(Texture2D tex, bool verticalFlip, bool clearPixels)
        {
            Width = tex.width;
            Height = tex.height;
            var pixel = tex.GetPixelData<Color32>(0);
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaCanvasCreateWithMemory(out ptr, Width, Height,
                pixel.GetUnsafePtr(), verticalFlip ? 1 : 0, clearPixels ? 1 : 0)
            );
        }

        protected override void DisposeUnmanaged()
        {
            if (ptr != IntPtr.Zero)
            {
                var nativePtr = ptr;
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaCanvasDestroy(out nativePtr));
                ptr = nativePtr;
            }

            base.DisposeUnmanaged();
        }

        public void DrawImageSimple(MilestroImage image, Vector2 position)
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaCanvasDrawImageSimple(NativePtr, image.NativePtr, position.x, position.y));
        }

        public void DrawImage(MilestroImage image, Rect src, Rect dst)
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaCanvasDrawImage(NativePtr, image.NativePtr,
                src.xMin, src.yMin, src.xMax, src.yMax,
                dst.xMin, dst.yMin, dst.xMax, dst.yMax
            ));
        }


        /// <summary>
        /// 把 Canvas 里的数据 dump 到一个 Texture2D 中
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public unsafe Texture2D ToTexture()
        {
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            var pixel = tex.GetPixelData<Color32>(0);
            if (BindingC.SkiaCanvasGetTexture(NativePtr, pixel.GetUnsafePtr()) < 0)
            {
                Object.DestroyImmediate(tex);
                throw new Exception("get texture failed");
            }

            tex.Apply();
            return tex;
        }
    }
}
