using System;
using Milestro.Skia;
using UnityEngine;

namespace Milestro.Components
{
    public abstract class SkiaRenderTargetProducer : MonoBehaviour
    {
        private static readonly Rect DefaultUvRect = new Rect(0f, 0f, 1f, 1f);

        [NonSerialized] private UnityAutoRenderTextureSurface? surface;
        [NonSerialized] private long outputVersion;

        public Texture? OutputTexture => surface?.Texture;
        public Rect OutputUvRect => surface?.DisplayUvRect ?? DefaultUvRect;
        public int OutputWidth => surface?.Width ?? 0;
        public int OutputHeight => surface?.Height ?? 0;
        public bool HasOutput => surface?.Texture != null;
        public long OutputVersion => outputVersion;

        protected bool HasRenderTarget => surface != null;

        protected bool EnsureRenderTarget(Vector2Int sizePixels, ColorSpace colorSpace)
        {
            sizePixels = NormalizeSize(sizePixels);
            if (surface == null || surface.ColorSpace != colorSpace)
            {
                surface?.Dispose();
                surface = null;
                surface = new UnityAutoRenderTextureSurface(sizePixels.x, sizePixels.y, colorSpace);
                MarkOutputChanged();
                return true;
            }

            if (surface.Width == sizePixels.x && surface.Height == sizePixels.y)
            {
                return false;
            }

            surface.Resize(sizePixels.x, sizePixels.y);
            MarkOutputChanged();
            return true;
        }

        protected bool TrySubmitRenderCommands(UnitySkiaRenderCommandList commands, bool clearBeforeDraw = true)
        {
            if (surface == null)
            {
                throw new InvalidOperationException("Milestro render target producer has no render target.");
            }

            if (!surface.TrySubmit(commands, clearBeforeDraw))
            {
                return false;
            }

            MarkOutputChanged();
            return true;
        }

        protected void DisposeResourceAfterPendingDraws(IDisposable resource)
        {
            if (resource == null)
            {
                return;
            }

            if (surface != null)
            {
                surface.DisposeResourceAfterPendingDraws(resource);
                return;
            }

            resource.Dispose();
        }

        protected void ReleaseRenderTarget()
        {
            if (surface == null)
            {
                return;
            }

            surface.Dispose();
            surface = null;
            MarkOutputChanged();
        }

        protected virtual void OnDisable()
        {
            ReleaseRenderTarget();
        }

        protected virtual void OnDestroy()
        {
            ReleaseRenderTarget();
        }

        protected void MarkOutputChanged()
        {
            unchecked
            {
                ++outputVersion;
            }
        }

        private static Vector2Int NormalizeSize(Vector2Int sizePixels)
        {
            return new Vector2Int(Mathf.Max(1, sizePixels.x), Mathf.Max(1, sizePixels.y));
        }
    }
}
