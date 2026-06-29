using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Milestro.Skia
{
    public sealed class UnityAutoRenderTextureSurface : IDisposable
    {
        private readonly UnitySkiaRenderTextureSurface surface;

        public UnitySkiaGraphicsBackend Backend => surface.Backend;
        public bool Srgb => surface.Srgb;
        public Texture Texture => surface.Texture;
        public RenderTexture RenderTexture => surface.RenderTexture;

        public int Width => surface.Width;
        public int Height => surface.Height;

        public UnityAutoRenderTextureSurface(int width, int height, bool srgb = true)
        {
            surface = new UnitySkiaRenderTextureSurface(SelectBackendForCurrentGraphicsDevice(), width, height, srgb);
        }

        public static UnitySkiaGraphicsBackend SelectBackendForCurrentGraphicsDevice()
        {
            switch (SystemInfo.graphicsDeviceType)
            {
                case GraphicsDeviceType.Metal:
                    return UnitySkiaGraphicsBackend.Metal;
                case GraphicsDeviceType.Direct3D12:
                    return UnitySkiaGraphicsBackend.Direct3D12;
                case GraphicsDeviceType.OpenGLES3:
                    return UnitySkiaGraphicsBackend.OpenGLES;
                case GraphicsDeviceType.OpenGLCore:
                    return UnitySkiaGraphicsBackend.OpenGL;
                default:
                    throw new NotSupportedException(
                        "Milestro automatic RenderTexture surface supports Metal, Direct3D12, OpenGLES3, and OpenGLCore. Current Unity graphics device is " +
                        SystemInfo.graphicsDeviceType + ".");
            }
        }

        public void Resize(int width, int height)
        {
            surface.Resize(width, height);
        }

        public void DisposeResourceAfterPendingDraws(IDisposable resource)
        {
            surface.DisposeResourceAfterPendingDraws(resource);
        }

        public void Submit(UnitySkiaRenderCommandList commands, bool clearBeforeDraw = true)
        {
            surface.Submit(commands, clearBeforeDraw);
        }

        public void Dispose()
        {
            surface.Dispose();
        }
    }
}
