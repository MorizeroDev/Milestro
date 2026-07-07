using System;
using System.Text;
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

        private UnityAutoRenderTextureSurface? surface;
        private Paragraph? paragraph;
        private bool layoutChanged = true;
        private bool paintChanged = true;
        private int layoutWidth = 640;
        private string paragraphPlainText = "";
        private float paragraphHeight;
        private float paragraphContentWidth;
        private float paragraphVisualLeft;
        private float paragraphVisualWidth;
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
                ReplaceParagraph(BuildParagraph(settings));
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
            RetireParagraph();
            surface?.Dispose();
            surface = null;
            paragraphPlainText = "";
            layoutChanged = true;
            paintChanged = true;
            paragraphHeight = 0f;
            paragraphContentWidth = 0f;
            paragraphVisualLeft = 0f;
            paragraphVisualWidth = 0f;
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
            var result = CreateParagraph(settings, out var plainText);
            paragraphPlainText = plainText;
            ResizeParagraph(result, settings, true);
            return result;
        }

        private Paragraph BuildPaintParagraph(TextBoxRenderTargetSettings settings)
        {
            var result = CreateParagraph(settings, out _);
            result.Layout(Math.Max(1, layoutWidth));
            return result;
        }

        private Paragraph CreateParagraph(TextBoxRenderTargetSettings settings,
            out string plainText)
        {
            using var paragraphStyle = new ParagraphStyle();
            paragraphStyle.TextAlign = settings.TextAlign;
            paragraphStyle.TextDirection = settings.TextDirection;
            if (settings.SingleLine)
            {
                paragraphStyle.MaxLines = 1;
                if (settings.TextOverflow == TextOverflow.Ellipsis)
                {
                    paragraphStyle.SetEllipsis(settings.EllipsisString);
                }
            }

            using var textStyle = new TextStyle();
            textStyle.SetFontFamilies(settings.FontFamilies);
            textStyle.FontSize = settings.Size;
            textStyle.Locale = settings.Locale;
            textStyle.Color = settings.TextColor;
            if (settings.Weight > FontWeight.Invisible && settings.Weight <= FontWeight.ExtraBlack)
            {
                textStyle.GetFontStyle(out int weight, out var width, out var slant);
                textStyle.SetFontStyle(settings.Weight, width, slant);
            }

            var parser = new RichTextParser.RichTextParser();
            parser.ParseText(settings.Content);
            var segments = parser.ConvertToSegments();
            plainText = PlainText(segments);
            return segments.ToParagraph(paragraphStyle, textStyle);
        }

        private static void ValidateMargin(Margin margin)
        {
            margin.Normalize();
        }

        private bool ResizeParagraph(Paragraph? targetParagraph,
            TextBoxRenderTargetSettings settings,
            bool force = false)
        {
            if (targetParagraph == null)
            {
                return false;
            }

            if (ShouldUseWideNoWrapLayout(settings))
            {
                return ResizeNoWrapParagraph(targetParagraph, settings, force);
            }

            var newLayoutWidth = ContentViewportWidth(settings);
            if (newLayoutWidth == layoutWidth && !force)
            {
                return false;
            }

            layoutWidth = newLayoutWidth;
            targetParagraph.Layout(layoutWidth);
            paragraphContentWidth = layoutWidth;
            paragraphVisualWidth = Mathf.Clamp(targetParagraph.LongestLine, 0f, layoutWidth);
            paragraphVisualLeft = ResolveParagraphVisualLeft(settings, paragraphVisualWidth, layoutWidth);
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
                targetParagraph.Layout(Paragraph.ResolveNoWrapProbeLayoutWidth(paragraphPlainText,
                    settings.Size,
                    viewportWidth));
                paragraphContentWidth = targetParagraph.ResolveNoWrapContentWidth(paragraphPlainText);
                paragraphVisualWidth = paragraphContentWidth;
                paragraphVisualLeft = ResolveParagraphVisualLeft(settings, paragraphVisualWidth, viewportWidth);
            }

            var newLayoutWidth = CeilToPositiveInt(Paragraph.ResolveNoWrapLayoutWidth(viewportWidth, paragraphContentWidth));
            if (newLayoutWidth == layoutWidth && !force)
            {
                return false;
            }

            layoutWidth = newLayoutWidth;
            targetParagraph.Layout(layoutWidth);
            paragraphVisualWidth = paragraphContentWidth;
            paragraphVisualLeft = ResolveParagraphVisualLeft(settings, paragraphVisualWidth, layoutWidth);
            paragraphHeight = Mathf.Max(0f, targetParagraph.Height);
            return true;
        }

        private bool UpdateScrollMetrics(TextBoxRenderTargetSettings settings, Vector2 requestedScrollOffset)
        {
            var nextViewportSize = new Vector2(ContentViewportWidth(settings), ContentViewportHeight(settings));
            var nextContentWidth = ShouldUseWideNoWrapLayout(settings)
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
            var margin = ResolveMargin(settings);
            return CeilToPositiveInt(OutputWidth - margin.FixedHorizontalSize);
        }

        private int ContentViewportHeight(TextBoxRenderTargetSettings settings)
        {
            var margin = ResolveMargin(settings);
            return CeilToPositiveInt(OutputHeight - margin.FixedVerticalSize);
        }

        private ResolvedMargin ResolveMargin(TextBoxRenderTargetSettings settings)
        {
            return settings.Margin.Resolve(new MarginResolveContext(OutputWidth, OutputHeight, settings.Size));
        }

        private static int CeilToPositiveInt(float value)
        {
            if (!FloatUtil.IsFinite(value) || value <= 0f)
            {
                return 1;
            }

            return value >= int.MaxValue ? int.MaxValue : Mathf.CeilToInt(value);
        }

        private static bool ShouldUseWideNoWrapLayout(TextBoxRenderTargetSettings settings)
        {
            if (settings.SingleLine)
            {
                return settings.TextOverflow != TextOverflow.Ellipsis;
            }

            return settings.WrapMode == TextBoxWrapMode.NoWrap;
        }

        private static float ResolveParagraphVisualLeft(TextBoxRenderTargetSettings settings,
            float visualWidth,
            float containingWidth)
        {
            if (!FloatUtil.IsFinite(visualWidth) ||
                !FloatUtil.IsFinite(containingWidth) ||
                visualWidth <= 0f ||
                visualWidth >= containingWidth)
            {
                return 0f;
            }

            switch (EffectiveAlign(settings))
            {
                case TextAlign.Center:
                    return (containingWidth - visualWidth) * 0.5f;
                case TextAlign.Right:
                    return containingWidth - visualWidth;
                default:
                    return 0f;
            }
        }

        private static TextAlign EffectiveAlign(TextBoxRenderTargetSettings settings)
        {
            switch (settings.TextAlign)
            {
                case TextAlign.Start:
                    return settings.TextDirection == TextDirection.Rtl ? TextAlign.Right : TextAlign.Left;
                case TextAlign.End:
                    return settings.TextDirection == TextDirection.Rtl ? TextAlign.Left : TextAlign.Right;
                default:
                    return settings.TextAlign;
            }
        }

        private static string PlainText(RichTextParser.ParagraphPayload payload)
        {
            var builder = new StringBuilder();
            foreach (var segment in payload.Body)
            {
                builder.Append(segment.Content ?? "");
            }

            return builder.ToString();
        }

        private UnitySkiaRenderCommandList BuildRenderCommands(TextBoxRenderTargetSettings settings,
            UnityEngine.Object? logContext)
        {
            var commands = new UnitySkiaRenderCommandList();
            if (paragraph != null)
            {
                var margin = ResolveMargin(settings);
                var autoOffset = AutoContentOffset(settings);
                var paintPosition = new Vector2(margin.Left + autoOffset.x - scrollOffset.x,
                    margin.Top + autoOffset.y - scrollOffset.y);
                var clipRect = settings.TextOverflow == TextOverflow.Overflow
                    ? new Rect(0f, 0f, OutputWidth, OutputHeight)
                    : new Rect(margin.Left,
                        margin.Top,
                        viewportSize.x,
                        viewportSize.y);
                commands.DrawParagraphSnapshot(() => BuildPaintParagraph(settings), paintPosition, clipRect);
            }
            else
            {
                Debug.LogWarning("No paragraph selected", logContext);
            }

            return commands;
        }

        private Vector2 AutoContentOffset(TextBoxRenderTargetSettings settings)
        {
            return new Vector2(ResolveAutoMarginOffset(settings.Margin.Left.Auto,
                    settings.Margin.Right.Auto,
                    viewportSize.x,
                    paragraphVisualLeft,
                    paragraphVisualWidth),
                ResolveAutoMarginOffset(settings.Margin.Top.Auto,
                    settings.Margin.Bottom.Auto,
                    viewportSize.y,
                    0f,
                    paragraphHeight));
        }

        private static float ResolveAutoMarginOffset(bool startAuto,
            bool endAuto,
            float viewportLength,
            float contentStart,
            float contentLength)
        {
            if ((!startAuto && !endAuto) ||
                !FloatUtil.IsFinite(viewportLength) ||
                !FloatUtil.IsFinite(contentStart) ||
                !FloatUtil.IsFinite(contentLength) ||
                contentLength <= 0f ||
                contentLength >= viewportLength)
            {
                return 0f;
            }

            var spare = viewportLength - contentLength;
            if (startAuto && endAuto)
            {
                return spare * 0.5f - contentStart;
            }

            return startAuto ? spare - contentStart : -contentStart;
        }

        private void MarkOutputChanged()
        {
            unchecked
            {
                ++outputVersion;
            }
        }

        private void ReplaceParagraph(Paragraph nextParagraph)
        {
            var oldParagraph = paragraph;
            paragraph = nextParagraph;
            DisposeParagraphAfterPendingDraws(oldParagraph);
        }

        private void RetireParagraph()
        {
            var oldParagraph = paragraph;
            paragraph = null;
            DisposeParagraphAfterPendingDraws(oldParagraph);
        }

        private void DisposeParagraphAfterPendingDraws(Paragraph? oldParagraph)
        {
            if (oldParagraph == null)
            {
                return;
            }

            if (surface != null)
            {
                surface.DisposeResourceAfterPendingDraws(oldParagraph);
                return;
            }

            oldParagraph.Dispose();
        }

        private static Vector2Int NormalizeSize(Vector2Int sizePixels)
        {
            return new Vector2Int(Mathf.Max(1, sizePixels.x), Mathf.Max(1, sizePixels.y));
        }
    }
}
