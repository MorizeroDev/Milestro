using System;
using UnityEngine;

namespace Milestro.Skia
{
    public sealed class UnityMetalRenderTextureSurface : IDisposable
    {
        private readonly UnitySkiaRenderTextureSurface surface;

        public bool Srgb => surface.Srgb;
        public Rect DisplayUvRect => surface.DisplayUvRect;
        public Texture Texture => surface.Texture;
        public RenderTexture RenderTexture => surface.RenderTexture;

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
