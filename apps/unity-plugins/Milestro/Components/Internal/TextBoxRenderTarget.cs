using System;
using Milestro.Extensions;
using Milestro.Model;
using Milestro.Skia;
using Milestro.Skia.TextLayout;
using UnityEngine;

namespace Milestro.Components.Internal
{
    internal sealed class TextBoxRenderTarget : IDisposable
    {
        private static readonly Rect DefaultUvRect = new Rect(0f, 0f, 1f, 1f);

        private UnityAutoRenderTextureSurface? surface;
        private Paragraph? paragraph;
        private bool propertiesChanged = true;
        private int layoutWidth = 640;
        private long outputVersion;

        public Texture? OutputTexture => surface?.Texture;
        public Rect OutputUvRect => surface?.DisplayUvRect ?? DefaultUvRect;
        public int OutputWidth => surface?.Width ?? 0;
        public int OutputHeight => surface?.Height ?? 0;
        public bool HasOutput => surface?.Texture != null;
        public long OutputVersion => outputVersion;

        public void MarkPropertiesChanged()
        {
            propertiesChanged = true;
        }

        public bool Rebuild(Vector2Int sizePixels,
            ColorSpace colorSpace,
            TextBoxRenderTargetSettings settings,
            bool forceText,
            UnityEngine.Object? logContext)
        {
            var needsDraw = EnsureSurface(sizePixels, colorSpace);
            if (forceText || paragraph == null || propertiesChanged)
            {
                paragraph = BuildParagraph(settings);
                needsDraw = true;
            }

            if (!needsDraw)
            {
                return true;
            }

            ValidateMargin(settings.Margin);
            ResizeParagraph(paragraph, settings);
            if (!TrySubmit(BuildRenderCommands(settings, logContext)))
            {
                propertiesChanged = true;
                return false;
            }

            propertiesChanged = false;
            return true;
        }

        public void Dispose()
        {
            surface?.Dispose();
            surface = null;
            paragraph = null;
            propertiesChanged = true;
            MarkOutputChanged();
        }

        private bool EnsureSurface(Vector2Int sizePixels, ColorSpace colorSpace)
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

        private bool TrySubmit(UnitySkiaRenderCommandList commands)
        {
            if (surface == null)
            {
                throw new InvalidOperationException("Milestro TextBox render target has no surface.");
            }

            if (!surface.TrySubmit(commands))
            {
                return false;
            }

            MarkOutputChanged();
            return true;
        }

        private Paragraph BuildParagraph(TextBoxRenderTargetSettings settings)
        {
            ParagraphStyle paragraphStyle = new ParagraphStyle();
            paragraphStyle.TextAlign = (int)settings.TextAlign;
            paragraphStyle.TextDirection = (int)settings.TextDirection;

            TextStyle textStyle = new TextStyle();
            textStyle.SetFontFamilies(settings.FontFamilies);
            textStyle.FontSize = settings.Size;
            textStyle.Locale = settings.Locale;
            textStyle.Color = settings.TextColor;

            var parser = new RichTextParser.RichTextParser();
            parser.ParseText(settings.Content);
            var segments = parser.ConvertToSegments();
            var result = segments.ToParagraph(paragraphStyle, textStyle);
            ResizeParagraph(result, settings, true);
            return result;
        }

        private static void ValidateMargin(RectOffset margin)
        {
            if (margin.left < 0) margin.left = 0;
            if (margin.top < 0) margin.top = 0;
            if (margin.right < 0) margin.right = 0;
            if (margin.bottom < 0) margin.bottom = 0;
        }

        private void ResizeParagraph(Paragraph? targetParagraph,
            TextBoxRenderTargetSettings settings,
            bool force = false)
        {
            if (targetParagraph == null)
            {
                return;
            }

            var newLayoutWidth = Math.Max(1, OutputWidth - settings.Margin.horizontal);
            if (newLayoutWidth == layoutWidth && !force)
            {
                return;
            }

            layoutWidth = newLayoutWidth;
            targetParagraph.Layout(layoutWidth);
        }

        private UnitySkiaRenderCommandList BuildRenderCommands(TextBoxRenderTargetSettings settings,
            UnityEngine.Object? logContext)
        {
            var commands = new UnitySkiaRenderCommandList();
            if (paragraph != null)
            {
                commands.DrawParagraph(paragraph, new Vector2(settings.Margin.left, settings.Margin.top));
            }
            else
            {
                Debug.LogWarning("No paragraph selected", logContext);
            }

            return commands;
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
    }
}
