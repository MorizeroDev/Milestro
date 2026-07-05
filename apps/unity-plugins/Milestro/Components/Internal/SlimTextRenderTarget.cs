using System;
using Milestro.Model;
using Milestro.Skia;
using Milestro.Util;
using UnityEngine;
using SkFont = Milestro.Skia.Font;

namespace Milestro.Components.Internal
{
    internal sealed class SlimTextRenderTarget : IDisposable
    {
        private static readonly Rect DefaultUvRect = new Rect(0f, 0f, 1f, 1f);

        private UnityAutoRenderTextureSurface? surface;
        private SkFont? font;
        private string resolvedFontFamily = "";
        private int resolvedFontWeight = FontWeight.Normal;
        private float resolvedFontSize = 16f;
        private bool resolvedFallbackToSystemFont = true;
        private bool styleChanged = true;
        private bool paintChanged = true;
        private long outputVersion;

        public Texture? OutputTexture => surface?.Texture;
        public Rect OutputUvRect => surface?.DisplayUvRect ?? DefaultUvRect;
        public int OutputWidth => surface?.Width ?? 0;
        public int OutputHeight => surface?.Height ?? 0;
        public bool HasOutput => surface?.Texture != null;
        public long OutputVersion => outputVersion;

        public void MarkPropertiesChanged()
        {
            styleChanged = true;
            paintChanged = true;
        }

        public void MarkPaintChanged()
        {
            paintChanged = true;
        }

        public bool Rebuild(Vector2Int sizePixels,
            ColorSpace colorSpace,
            SlimTextRenderTargetSettings settings)
        {
            var needsDraw = EnsureSurface(sizePixels, colorSpace);
            needsDraw |= EnsureFont(settings);
            needsDraw |= paintChanged;
            if (!needsDraw)
            {
                return true;
            }

            if (!TrySubmit(BuildRenderCommands(settings)))
            {
                paintChanged = true;
                return false;
            }

            styleChanged = false;
            paintChanged = false;
            return true;
        }

        public void Dispose()
        {
            surface?.Dispose();
            surface = null;
            DisposeFont();
            styleChanged = true;
            paintChanged = true;
            MarkOutputChanged();
        }

        private bool EnsureSurface(Vector2Int sizePixels, ColorSpace colorSpace)
        {
            sizePixels = NormalizeSize(sizePixels);
            if (surface == null || surface.ColorSpace != colorSpace)
            {
                surface?.Dispose();
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

        private bool EnsureFont(SlimTextRenderTargetSettings settings)
        {
            var nextFontSize = NormalizeFontSize(settings.FontSize);
            var needsResolve = font == null ||
                               styleChanged ||
                               resolvedFontFamily != settings.FontFamily ||
                               resolvedFontWeight != settings.FontWeight ||
                               !Mathf.Approximately(resolvedFontSize, nextFontSize) ||
                               resolvedFallbackToSystemFont != settings.FallbackToSystemFont;
            if (!needsResolve)
            {
                return false;
            }

            DisposeFont();
            font = FontRegistry.ResolveFont(settings.FontFamily,
                settings.FontWeight,
                nextFontSize,
                settings.FallbackToSystemFont);
            resolvedFontFamily = settings.FontFamily;
            resolvedFontWeight = settings.FontWeight;
            resolvedFontSize = nextFontSize;
            resolvedFallbackToSystemFont = settings.FallbackToSystemFont;
            return true;
        }

        private UnitySkiaRenderCommandList BuildRenderCommands(SlimTextRenderTargetSettings settings)
        {
            var commands = new UnitySkiaRenderCommandList();
            if (font == null || string.IsNullOrEmpty(settings.Text))
            {
                return commands;
            }

            var measurement = font.MeasureText(settings.Text);
            var padding = NormalizePadding(settings.Padding);
            var baseline = new Vector2(padding.x - measurement.Bounds.xMin,
                padding.y - measurement.Bounds.yMin);
            commands.DrawString(settings.Text, font, baseline, settings.TextColor);
            return commands;
        }

        private bool TrySubmit(UnitySkiaRenderCommandList commands)
        {
            if (surface == null)
            {
                throw new InvalidOperationException("Milestro slim text render target has no surface.");
            }

            if (!surface.TrySubmit(commands))
            {
                return false;
            }

            MarkOutputChanged();
            return true;
        }

        private void DisposeFont()
        {
            font?.Dispose();
            font = null;
        }

        private void MarkOutputChanged()
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

        private static Vector2 NormalizePadding(Vector2 padding)
        {
            return new Vector2(FloatUtil.IsFinite(padding.x) ? Mathf.Max(0f, padding.x) : 0f,
                FloatUtil.IsFinite(padding.y) ? Mathf.Max(0f, padding.y) : 0f);
        }

        private static float NormalizeFontSize(float fontSize)
        {
            return FloatUtil.IsFinite(fontSize) ? Mathf.Max(1f, fontSize) : 1f;
        }
    }
}
