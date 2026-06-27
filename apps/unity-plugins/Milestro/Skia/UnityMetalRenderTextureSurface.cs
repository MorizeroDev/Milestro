using System;
using Milestro.Skia.TextLayout;
using UnityEngine;

namespace Milestro.Skia
{
    public sealed class UnityMetalRenderTextureSurface : IDisposable
    {
        private readonly UnitySkiaRenderTextureSurface surface;

        public RenderTexture RenderTexture => surface.RenderTexture;

        public int Width => surface.Width;
        public int Height => surface.Height;

        public UnityMetalRenderTextureSurface(int width, int height, bool srgb = true)
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

        public void Draw(Paragraph paragraph,
            MilestroImage image,
            Vector2 paragraphPosition,
            Rect imageRect,
            bool clearBeforeDraw = true)
        {
            surface.Draw(paragraph, image, paragraphPosition, imageRect, clearBeforeDraw);
        }

        public void Dispose()
        {
            surface.Dispose();
        }
    }
}
