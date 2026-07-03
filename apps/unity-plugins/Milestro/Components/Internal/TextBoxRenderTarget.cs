using System;
using Milestro.Extensions;
using Milestro.Model;
using Milestro.Skia;
using Milestro.Skia.TextLayout;
using Milestro.Util;
using UnityEngine;

namespace Milestro.Components.Internal
{
    internal sealed class TextBoxRenderTarget : IDisposable
    {
        private static readonly Rect DefaultUvRect = new Rect(0f, 0f, 1f, 1f);
        private const float NoWrapProbeLayoutWidth = 1048576f;
        private const float NoWrapLayoutPadding = 1f;

        private UnityAutoRenderTextureSurface? surface;
        private Paragraph? paragraph;
        private bool layoutChanged = true;
        private bool paintChanged = true;
        private int layoutWidth = 640;
        private float paragraphHeight;
        private float paragraphContentWidth;
        private long outputVersion;
        private Vector2 scrollOffset;
        private Vector2 contentSize;
        private Vector2 viewportSize;
        private Vector2 maxScrollOffset;

        public Texture? OutputTexture => surface?.Texture;
        public Rect OutputUvRect => surface?.DisplayUvRect ?? DefaultUvRect;
        public int OutputWidth => surface?.Width ?? 0;
        public int OutputHeight => surface?.Height ?? 0;
        public bool HasOutput => surface?.Texture != null;
        public long OutputVersion => outputVersion;
        public Vector2 ScrollOffset => scrollOffset;
        public Vector2 ContentSize => contentSize;
        public Vector2 ViewportSize => viewportSize;
        public Vector2 MaxScrollOffset => maxScrollOffset;

        public void MarkPropertiesChanged()
        {
            layoutChanged = true;
            paintChanged = true;
        }

        public void MarkPaintChanged()
        {
            paintChanged = true;
        }

        public bool Rebuild(Vector2Int sizePixels,
            ColorSpace colorSpace,
            TextBoxRenderTargetSettings settings,
            Vector2 requestedScrollOffset,
            bool forceText,
            UnityEngine.Object? logContext)
        {
            ValidateMargin(settings.Margin);

            var needsDraw = EnsureSurface(sizePixels, colorSpace);
            if (forceText || paragraph == null || layoutChanged)
            {
                paragraph = BuildParagraph(settings);
                needsDraw = true;
            }

            needsDraw |= ResizeParagraph(paragraph, settings);
            needsDraw |= UpdateScrollMetrics(settings, requestedScrollOffset);
            needsDraw |= paintChanged;
            if (!needsDraw)
            {
                return true;
            }

            if (!TrySubmit(BuildRenderCommands(settings, logContext)))
            {
                paintChanged = true;
                return false;
            }

            layoutChanged = false;
            paintChanged = false;
            return true;
        }

        public void Dispose()
        {
            surface?.Dispose();
            surface = null;
            paragraph = null;
            layoutChanged = true;
            paintChanged = true;
            paragraphHeight = 0f;
            paragraphContentWidth = 0f;
            scrollOffset = Vector2.zero;
            contentSize = Vector2.zero;
            viewportSize = Vector2.zero;
            maxScrollOffset = Vector2.zero;
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
            if (settings.Weight > 0 && settings.Weight <= 1000)
            {
                textStyle.GetFontStyle(out int weight, out int width, out int slant);
                textStyle.SetFontStyle(settings.Weight, width, slant);
            }

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

        private bool ResizeParagraph(Paragraph? targetParagraph,
            TextBoxRenderTargetSettings settings,
            bool force = false)
        {
            if (targetParagraph == null)
            {
                return false;
            }

            if (settings.WrapMode == TextBoxWrapMode.NoWrap)
            {
                return ResizeNoWrapParagraph(targetParagraph, settings, force);
            }

            var newLayoutWidth = ContentViewportWidth(settings);
            if (newLayoutWidth == layoutWidth && !force)
            {
                return false;
            }

            layoutWidth = newLayoutWidth;
            paragraphContentWidth = layoutWidth;
            targetParagraph.Layout(layoutWidth);
            paragraphHeight = Mathf.Max(0f, targetParagraph.Height);
            return true;
        }

        private bool ResizeNoWrapParagraph(Paragraph targetParagraph,
            TextBoxRenderTargetSettings settings,
            bool force)
        {
            var viewportWidth = ContentViewportWidth(settings);
            if (force)
            {
                targetParagraph.Layout(NoWrapProbeLayoutWidth);
                paragraphContentWidth = MeasureNoWrapContentWidth(targetParagraph);
            }

            var newLayoutWidth = ResolveNoWrapLayoutWidth(viewportWidth, paragraphContentWidth);
            if (newLayoutWidth == layoutWidth && !force)
            {
                return false;
            }

            layoutWidth = newLayoutWidth;
            targetParagraph.Layout(layoutWidth);
            paragraphHeight = Mathf.Max(0f, targetParagraph.Height);
            return true;
        }

        private bool UpdateScrollMetrics(TextBoxRenderTargetSettings settings, Vector2 requestedScrollOffset)
        {
            var nextViewportSize = new Vector2(ContentViewportWidth(settings), ContentViewportHeight(settings));
            var nextContentWidth = settings.WrapMode == TextBoxWrapMode.NoWrap
                ? Mathf.Max(nextViewportSize.x, paragraph != null ? layoutWidth : 0f)
                : nextViewportSize.x;
            var nextContentSize = new Vector2(nextContentWidth, paragraph != null ? paragraphHeight : 0f);
            var nextMaxScrollOffset = new Vector2(
                Mathf.Max(0f, nextContentSize.x - nextViewportSize.x),
                Mathf.Max(0f, nextContentSize.y - nextViewportSize.y));
            var nextScrollOffset = new Vector2(
                Mathf.Clamp(FloatUtil.IsFinite(requestedScrollOffset.x) ? requestedScrollOffset.x : 0f, 0f, nextMaxScrollOffset.x),
                Mathf.Clamp(FloatUtil.IsFinite(requestedScrollOffset.y) ? requestedScrollOffset.y : 0f, 0f, nextMaxScrollOffset.y));

            var changed = viewportSize != nextViewportSize ||
                          contentSize != nextContentSize ||
                          maxScrollOffset != nextMaxScrollOffset ||
                          scrollOffset != nextScrollOffset;

            viewportSize = nextViewportSize;
            contentSize = nextContentSize;
            maxScrollOffset = nextMaxScrollOffset;
            scrollOffset = nextScrollOffset;
            return changed;
        }

        private int ContentViewportWidth(TextBoxRenderTargetSettings settings)
        {
            return Math.Max(1, OutputWidth - settings.Margin.horizontal);
        }

        private int ContentViewportHeight(TextBoxRenderTargetSettings settings)
        {
            return Math.Max(1, OutputHeight - settings.Margin.vertical);
        }

        private static float MeasureNoWrapContentWidth(Paragraph targetParagraph)
        {
            var longestLine = targetParagraph.LongestLine;
            var maxIntrinsicWidth = targetParagraph.MaxIntrinsicWidth;
            var width = Mathf.Max(FloatUtil.IsFinite(longestLine) ? longestLine : 0f,
                FloatUtil.IsFinite(maxIntrinsicWidth) ? maxIntrinsicWidth : 0f);
            return Mathf.Max(0f, Mathf.Ceil(width));
        }

        private static int ResolveNoWrapLayoutWidth(int viewportWidth, float contentWidth)
        {
            var paddedContentWidth = FloatUtil.IsFinite(contentWidth) && contentWidth > 0f
                ? contentWidth + NoWrapLayoutPadding
                : 0f;
            return Mathf.Max(viewportWidth, CeilToPositiveInt(paddedContentWidth));
        }

        private static int CeilToPositiveInt(float value)
        {
            if (!FloatUtil.IsFinite(value) || value <= 0f)
            {
                return 1;
            }

            return value >= int.MaxValue ? int.MaxValue : Mathf.CeilToInt(value);
        }

        private UnitySkiaRenderCommandList BuildRenderCommands(TextBoxRenderTargetSettings settings,
            UnityEngine.Object? logContext)
        {
            var commands = new UnitySkiaRenderCommandList();
            if (paragraph != null)
            {
                var clipRect = new Rect(settings.Margin.left,
                    settings.Margin.top,
                    viewportSize.x,
                    viewportSize.y);
                var paintPosition = new Vector2(settings.Margin.left - scrollOffset.x,
                    settings.Margin.top - scrollOffset.y);
                commands.DrawParagraph(paragraph, paintPosition, clipRect);
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
