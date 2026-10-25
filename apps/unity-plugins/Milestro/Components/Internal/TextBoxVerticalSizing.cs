using System;
using System.Collections.Generic;
using System.Linq;
using Milestro.Extensions;
using Milestro.Model;
using Milestro.RichTextParser;
using Milestro.Skia.TextLayout;
using Milestro.Util;

namespace Milestro.Components.Internal
{
    internal readonly struct TextBoxVerticalMeasurement
    {
        public TextBoxVerticalMeasurement(float physicalWidth, float physicalHeight)
        {
            PhysicalWidth = physicalWidth;
            PhysicalHeight = physicalHeight;
        }

        public float PhysicalWidth { get; }
        public float PhysicalHeight { get; }
    }

    internal sealed class TextBoxVerticalIntrinsicMeasurement : IDisposable
    {
        private readonly TextBoxRenderTargetSettings settings;
        private readonly ParagraphPayload payload;
        private readonly List<Paragraph> paragraphs;
        private readonly Dictionary<float, TextBoxVerticalMeasurement> cache =
            new Dictionary<float, TextBoxVerticalMeasurement>();

        internal bool MatchesMarginContext(TextBoxRenderTargetSettings next)
        {
            return Nullable.Equals(settings.VerticalMarginContext, next.VerticalMarginContext);
        }

        public TextBoxVerticalIntrinsicMeasurement(TextBoxRenderTargetSettings settings)
        {
            this.settings = settings;
            var parser = new RichTextParser.RichTextParser();
            parser.ParseText(settings.Content);
            payload = parser.ConvertToSegments(allowViewportBlockMargins: true);
            ZeroPercentageTextIndent(payload);
            using var paragraphStyle = settings.ParagraphStyle.ToParagraphStyle(
                settings.TextOverflow == TextOverflow.Ellipsis);
            using var textStyle = settings.TextStyle.ToTextStyle();
            using var indentScope = TextBoxVerticalLayoutSupport.UseRepresentableVerticalIndent(
                payload,
                float.PositiveInfinity,
                settings.TextStyle.size,
                out _);
            paragraphs = payload.ToParagraphs(paragraphStyle, textStyle);
        }

        private static void ZeroPercentageTextIndent(ParagraphPayload payload)
        {
            ZeroPercentageTextIndent(payload.ParagraphStyle);
            foreach (var block in payload.Blocks)
            {
                ZeroPercentageTextIndent(block.Style);
            }
        }

        private static void ZeroPercentageTextIndent(ParagraphStyleState style)
        {
            if (style?.TextIndent is { IsPercentage: true })
            {
                style.TextIndent = TextIndent.Pixels(0f);
            }
        }

        public TextBoxVerticalMeasurement Measure(float contentHeight)
        {
            contentHeight = FloatUtil.IsFinite(contentHeight) ? Math.Max(0f, contentHeight) : 0f;
            if (cache.TryGetValue(contentHeight, out var cached))
            {
                return cached;
            }

            if (paragraphs.Count == 0)
            {
                return new TextBoxVerticalMeasurement(0f, contentHeight);
            }

            var wraps = TextBoxVerticalBlockLayout.UsesFiniteInlineExtent(settings);
            var margins = TextBoxVerticalBlockLayout.ResolveMargins(
                payload.Blocks.Select(block => block.Style.MarginTop).ToArray(),
                payload.Blocks.Select(block => block.Style.MarginBottom).ToArray(),
                contentHeight, settings);
            var layout = TextBoxVerticalBlockLayout.Layout(paragraphs,
                wraps ? contentHeight : float.PositiveInfinity,
                settings.ParagraphStyle.paragraphSpacing, margins);
            var result = new TextBoxVerticalMeasurement(layout.PhysicalSize.x,
                layout.PhysicalSize.y);
            if (cache.Count >= 32)
            {
                cache.Clear();
            }
            cache[contentHeight] = result;
            return result;
        }

        public void Dispose()
        {
            if (paragraphs == null)
            {
                return;
            }
            foreach (var paragraph in paragraphs)
            {
                paragraph.Dispose();
            }
            paragraphs.Clear();
            cache.Clear();
        }
    }

    internal static class TextBoxVerticalSizingResolver
    {
        public static TextSizingResult Resolve(TextBoxVerticalIntrinsicMeasurement measurement,
            TextSizingRequest request,
            ResolvedMargin margin,
            float assignedHeight,
            float? assignedWidth)
        {
            if (measurement == null)
            {
                throw new ArgumentNullException(nameof(measurement));
            }
            if (request.HeightMode != TextHeightMode.Fixed)
            {
                throw new ArgumentException("Vertical content sizing requires a fixed height.",
                    nameof(request));
            }

            var usedHeight = NonNegative(assignedHeight);
            var contentHeight = Math.Max(0f,
                usedHeight - margin.FixedVerticalSize);
            var measured = measurement.Measure(contentHeight);
            var contentWidth = measured.PhysicalWidth;
            var contribution = Math.Max(0f,
                contentWidth + margin.FixedHorizontalSize);
            var usedWidth = assignedWidth.HasValue
                ? NonNegative(assignedWidth.Value)
                : request.WidthMode == TextWidthMode.Fixed
                    ? NonNegative(request.FixedWidth)
                    : contribution;
            var metrics = new TextIntrinsicMetrics(contentWidth,
                contentWidth,
                measured.PhysicalHeight,
                contentWidth,
                contentHeight);
            return new TextSizingResult(request,
                usedWidth,
                usedHeight,
                Positive(request.DesignFontSize),
                margin,
                metrics,
                contribution,
                contribution);
        }

        private static float NonNegative(float value)
        {
            return FloatUtil.IsFinite(value) ? Math.Max(0f, value) : 0f;
        }

        private static float Positive(float value)
        {
            return Math.Max(0.1f, NonNegative(value));
        }
    }
}
