using System;
using Milestro.Skia.TextLayout;
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
                default:
                    throw new NotSupportedException(
                        "Milestro automatic RenderTexture surface supports only Metal and Direct3D12. Current Unity graphics device is " +
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
