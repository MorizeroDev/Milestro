using System;
using UnityEngine;

namespace Milestro.Skia
{
    public sealed class UnityMetalRenderTextureSurface : IDisposable
    {
        private readonly UnitySkiaRenderTextureSurface surface;

        public UnityEngine.ColorSpace ColorSpace => surface.ColorSpace;
        public bool UseSrgbStorage => surface.UseSrgbStorage;

        public Rect DisplayUvRect => surface.DisplayUvRect;
        public Texture? Texture => surface.Texture;
        public RenderTexture? RenderTexture => surface.RenderTexture;

        public int Width => surface.Width;
        public int Height => surface.Height;

        public UnityMetalRenderTextureSurface(int width, int height)
        {
            surface = new UnitySkiaRenderTextureSurface(UnitySkiaGraphicsBackend.Metal, width, height);
        }

        public UnityMetalRenderTextureSurface(int width, int height, bool srgb)
        {
            surface = new UnitySkiaRenderTextureSurface(UnitySkiaGraphicsBackend.Metal, width, height, srgb);
        }

        public UnityMetalRenderTextureSurface(int width, int height, UnityEngine.ColorSpace colorSpace)
        {
            surface = new UnitySkiaRenderTextureSurface(UnitySkiaGraphicsBackend.Metal, width, height, colorSpace);
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

        public bool TrySubmit(UnitySkiaRenderCommandList commands, bool clearBeforeDraw = true)
        {
            return surface.TrySubmit(commands, clearBeforeDraw);
        }

        public void Dispose()
        {
            surface.Dispose();
        }
    }
}
