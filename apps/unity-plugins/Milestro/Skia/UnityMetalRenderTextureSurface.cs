using System;
using System.Runtime.InteropServices;
using Milestro.Binding;
using Milestro.Skia.TextLayout;
using UnityEngine;
using UnityEngine.Rendering;

namespace Milestro.Skia
{
    public sealed class UnityMetalRenderTextureSurface : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct RenderPayload
        {
            public IntPtr ColorRenderBufferHandle;
            public int Width;
            public int Height;
            public int Srgb;
            public int ClearBeforeDraw;

            public IntPtr Paragraph;
            public float ParagraphX;
            public float ParagraphY;

            public IntPtr Image;
            public float ImageX;
            public float ImageY;
            public float ImageWidth;
            public float ImageHeight;
        }

        private readonly bool srgb;
        private IntPtr payloadPtr;
        private IntPtr renderEventFunc;
        private int renderEventId;

        public RenderTexture RenderTexture { get; private set; }

        public int Width { get; private set; }
        public int Height { get; private set; }

        public UnityMetalRenderTextureSurface(int width, int height, bool srgb = true)
        {
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Metal)
            {
                throw new NotSupportedException("Milestro UnityMetalRenderTextureSurface requires Unity Metal backend.");
            }

            this.srgb = srgb;
            payloadPtr = Marshal.AllocHGlobal(Marshal.SizeOf<RenderPayload>());
            renderEventFunc = BindingC.UnityRenderGetRenderEventAndDataFunc();
            ExitCodeUtil.ThrowIfFailed(BindingC.UnityRenderGetMetalRenderEventId(out renderEventId));
            Resize(width, height);
        }

        public void Resize(int width, int height)
        {
            width = Math.Max(1, width);
            height = Math.Max(1, height);
            if (RenderTexture != null && Width == width && Height == height && RenderTexture.IsCreated())
            {
                return;
            }

            ReleaseRenderTexture();
            Width = width;
            Height = height;

            var descriptor = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 0)
            {
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false,
                sRGB = srgb
            };
            RenderTexture = new RenderTexture(descriptor)
            {
                name = "Milestro Metal RenderTexture PoC"
            };
            RenderTexture.Create();
        }

        public void Draw(Paragraph paragraph,
            MilestroImage image,
            Vector2 paragraphPosition,
            Rect imageRect,
            bool clearBeforeDraw = true)
        {
            if (renderEventFunc == IntPtr.Zero)
            {
                throw new InvalidOperationException("Milestro Unity render event callback is unavailable.");
            }

            if (RenderTexture == null || !RenderTexture.IsCreated())
            {
                Resize(Width, Height);
            }

            var payload = new RenderPayload
            {
                ColorRenderBufferHandle = RenderTexture.colorBuffer.GetNativeRenderBufferPtr(),
                Width = Width,
                Height = Height,
                Srgb = srgb ? 1 : 0,
                ClearBeforeDraw = clearBeforeDraw ? 1 : 0,
                Paragraph = paragraph?.Ptr ?? IntPtr.Zero,
                ParagraphX = paragraphPosition.x,
                ParagraphY = paragraphPosition.y,
                Image = image?.Ptr ?? IntPtr.Zero,
                ImageX = imageRect.x,
                ImageY = imageRect.y,
                ImageWidth = imageRect.width,
                ImageHeight = imageRect.height
            };
            Marshal.StructureToPtr(payload, payloadPtr, false);
            GL.IssuePluginEventAndData(renderEventFunc, renderEventId, payloadPtr);
        }

        public void Dispose()
        {
            ReleaseRenderTexture();
            if (payloadPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(payloadPtr);
                payloadPtr = IntPtr.Zero;
            }
        }

        private void ReleaseRenderTexture()
        {
            if (RenderTexture == null)
            {
                return;
            }

            RenderTexture.Release();
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(RenderTexture);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(RenderTexture);
            }
            RenderTexture = null;
        }
    }
}
