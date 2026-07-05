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
        private bool noAllocMode;
        private bool noAllocCapacityConfigured;
        private bool noAllocTextChanged;
        private int noAllocTextLength;
        private int noAllocCapacity;
        private Color32 noAllocTextColor;
        private UnitySkiaRenderTextureSurface.SlimTextNoAllocSubmission? retiredNoAllocSubmission;
        private UnitySkiaRenderTextureSurface.SlimTextNoAllocSubmission? noAllocSubmission;
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

        public bool UseManagedStringText()
        {
            var wasNoAllocMode = noAllocMode || noAllocCapacityConfigured || noAllocSubmission != null ||
                                 retiredNoAllocSubmission != null;
            noAllocMode = false;
            noAllocCapacityConfigured = false;
            noAllocTextChanged = false;
            noAllocTextLength = 0;
            noAllocCapacity = 0;
            DisposeNoAllocSubmission();
            DisposeNoAllocSubmission(ref retiredNoAllocSubmission);
            return wasNoAllocMode;
        }

        public void EnsureNoAllocCapacity(int capacity)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            noAllocMode = true;
            noAllocCapacityConfigured = true;
            if (capacity <= noAllocCapacity)
            {
                if (noAllocSubmission == null)
                {
                    paintChanged = true;
                }

                return;
            }

            noAllocCapacity = capacity;
            paintChanged = true;
        }

        public bool SetTextUtf8NoAlloc(Vector2Int sizePixels,
            ColorSpace colorSpace,
            SlimTextRenderTargetSettings settings,
            byte[] buffer,
            int offset,
            int length)
        {
            ThrowIfNoAllocRangeInvalid(buffer, offset, length);
            noAllocMode = true;
            EnsureNoAllocCapacityForText(buffer, offset, length);
            noAllocTextLength = length;
            noAllocTextChanged = true;

            var needsDraw = EnsureSurface(sizePixels, colorSpace);
            needsDraw |= EnsureFont(settings);
            needsDraw |= paintChanged;
            needsDraw |= noAllocTextChanged;

            if (font != null)
            {
                EnsureNoAllocSubmission(settings.TextColor);
                noAllocSubmission!.UpdateText(buffer, offset, length);
            }

            return RebuildNoAlloc(settings, needsDraw);
        }

        public bool Rebuild(Vector2Int sizePixels,
            ColorSpace colorSpace,
            SlimTextRenderTargetSettings settings)
        {
            var needsDraw = EnsureSurface(sizePixels, colorSpace);
            needsDraw |= EnsureFont(settings);
            needsDraw |= paintChanged;
            needsDraw |= noAllocMode && noAllocTextChanged;
            if (!needsDraw)
            {
                return true;
            }

            if (noAllocMode)
            {
                return RebuildNoAlloc(settings, needsDraw);
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
            DisposeFont();
            DisposeNoAllocSubmission();
            DisposeNoAllocSubmission(ref retiredNoAllocSubmission);
            surface?.Dispose();
            surface = null;
            styleChanged = true;
            paintChanged = true;
            noAllocMode = false;
            noAllocTextChanged = false;
            noAllocTextLength = 0;
            noAllocCapacity = 0;
            noAllocCapacityConfigured = false;
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
            DisposeNoAllocSubmission(ref retiredNoAllocSubmission);
            retiredNoAllocSubmission = noAllocSubmission;
            noAllocSubmission = null;
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

        private bool RebuildNoAlloc(SlimTextRenderTargetSettings settings, bool needsDraw)
        {
            if (font == null)
            {
                if (!TrySubmit(new UnitySkiaRenderCommandList()))
                {
                    paintChanged = true;
                    return false;
                }

                styleChanged = false;
                paintChanged = false;
                noAllocTextChanged = false;
                return true;
            }

            EnsureNoAllocSubmission(settings.TextColor);
            if (!needsDraw && !noAllocTextChanged)
            {
                return true;
            }

            if (noAllocTextLength == 0)
            {
                if (!TrySubmitNoAlloc(noAllocSubmission!, Vector2.zero, false))
                {
                    paintChanged = true;
                    return false;
                }

                styleChanged = false;
                paintChanged = false;
                noAllocTextChanged = false;
                return true;
            }

            var bounds = noAllocSubmission!.MeasureBounds();
            var padding = NormalizePadding(settings.Padding);
            var baseline = new Vector2(padding.x - bounds.xMin,
                padding.y - bounds.yMin);

            if (!TrySubmitNoAlloc(noAllocSubmission, baseline))
            {
                paintChanged = true;
                return false;
            }

            styleChanged = false;
            paintChanged = false;
            noAllocTextChanged = false;
            return true;
        }

        private void EnsureNoAllocSubmission(Color32 textColor)
        {
            if (font == null)
            {
                return;
            }

            if (noAllocSubmission != null &&
                noAllocSubmission.Capacity == noAllocCapacity &&
                ColorsEqual(noAllocTextColor, textColor))
            {
                return;
            }

            var oldSubmission = noAllocSubmission ?? retiredNoAllocSubmission;
            noAllocSubmission = new UnitySkiaRenderTextureSurface.SlimTextNoAllocSubmission(font,
                noAllocCapacity,
                textColor);
            noAllocTextColor = textColor;
            if (oldSubmission != null)
            {
                noAllocSubmission.CopyTextFrom(oldSubmission);
                RetireNoAllocSubmission(oldSubmission);
                if (oldSubmission == retiredNoAllocSubmission)
                {
                    retiredNoAllocSubmission = null;
                }
            }
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

        private bool TrySubmitNoAlloc(UnitySkiaRenderTextureSurface.SlimTextNoAllocSubmission submission,
            Vector2 baseline,
            bool drawText = true)
        {
            if (surface == null)
            {
                throw new InvalidOperationException("Milestro slim text render target has no surface.");
            }

            if (!surface.TrySubmitSlimTextNoAlloc(submission, baseline, drawText))
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

        private void DisposeNoAllocSubmission()
        {
            DisposeNoAllocSubmission(ref noAllocSubmission);
        }

        private void DisposeNoAllocSubmission(
            ref UnitySkiaRenderTextureSurface.SlimTextNoAllocSubmission? submission)
        {
            RetireNoAllocSubmission(submission);
            submission = null;
        }

        private void RetireNoAllocSubmission(UnitySkiaRenderTextureSurface.SlimTextNoAllocSubmission? submission)
        {
            if (submission == null)
            {
                return;
            }

            if (!submission.TryBeginRetire())
            {
                return;
            }

            if (surface != null)
            {
                surface.DisposeResourceAfterPendingDraws(submission);
            }
            else
            {
                submission.Dispose();
            }
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

        private void EnsureNoAllocCapacityForText(byte[] buffer, int offset, int length)
        {
            if (noAllocCapacityConfigured)
            {
                if (length > noAllocCapacity)
                {
                    throw new ArgumentOutOfRangeException(nameof(length),
                        "Text length exceeds the prepared WorldSpaceSlimText no-GC UTF-8 capacity.");
                }

                return;
            }

            noAllocCapacityConfigured = true;
            noAllocCapacity = Math.Max(length, buffer.Length - offset);
            DisposeNoAllocSubmission();
            paintChanged = true;
        }

        private static bool ColorsEqual(Color32 a, Color32 b)
        {
            return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
        }

        private static void ThrowIfNoAllocRangeInvalid(byte[] buffer, int offset, int length)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (offset < 0 || offset > buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            if (length < 0 || length > buffer.Length - offset)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }
        }
    }
}
