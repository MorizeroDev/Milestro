using System;
using System.Collections.Generic;
using Milestro.Extensions;
using Milestro.Model;
using Milestro.RichTextParser;
using Milestro.Skia.TextLayout;
using UnityEngine;

namespace Milestro.Components.Internal
{
    internal sealed class TextBoxIntrinsicMeasurement : ITextIntrinsicMeasurement, IDisposable
    {
        private readonly TextBoxRenderTargetSettings settings;
        private readonly ParagraphPayload payload;
        private readonly List<Paragraph> paragraphs;
        private readonly List<float> heights = new List<float>();
        private readonly List<float> marginTops = new List<float>();
        private readonly List<float> marginBottoms = new List<float>();
        private readonly List<float> tops = new List<float>();
        private readonly Dictionary<(float, float), TextIntrinsicMetrics> cache =
            new Dictionary<(float, float), TextIntrinsicMetrics>();
        private Vector2 availableSize;
        private readonly bool empty;
        private readonly float minWidth;
        private readonly float maxWidth;
        private readonly List<float> naturalHeights = new List<float>();

        public TextBoxIntrinsicMeasurement(TextBoxRenderTargetSettings settings)
        {
            this.settings = settings;
            var parser = new RichTextParser.RichTextParser();
            parser.ParseText(settings.Content);
            payload = parser.ConvertToSegments();
            using var paragraphStyle = settings.ParagraphStyle.ToParagraphStyle(false);
            paragraphStyle.ClearMaxLines();
            using var textStyle = settings.TextStyle.ToTextStyle();
            paragraphs = payload.ToParagraphs(paragraphStyle, textStyle);
            empty = true;
            foreach (var block in payload.Blocks)
            {
                foreach (var segment in block.Segments)
                {
                    empty &= string.IsNullOrEmpty(segment.Content);
                }
            }
            foreach (var paragraph in paragraphs)
            {
                paragraph.Layout(float.MaxValue);
                var paragraphMinWidth = paragraph.MinIntrinsicWidth;
                var paragraphMaxWidth = paragraph.MaxIntrinsicWidth;
                minWidth = Math.Max(minWidth, paragraphMinWidth);
                maxWidth = Math.Max(maxWidth, paragraphMaxWidth);
                paragraph.Layout(Paragraph.ResolveNoWrapLayoutWidth(0f, paragraphMaxWidth));
                naturalHeights.Add(paragraph.Height);
            }
            if (settings.ParagraphStyle.singleLine || settings.WrapMode == TextBoxWrapMode.NoWrap)
            {
                minWidth = maxWidth;
            }
        }

        public void SetAvailableSize(Vector2 value)
        {
            if (availableSize == value) return;
            availableSize = value;
            cache.Clear();
        }

        public TextIntrinsicMetrics Measure(float fontSize, float contentWidth)
        {
            if (empty) return default;
            var key = (fontSize, contentWidth);
            if (cache.TryGetValue(key, out var result)) return result;
            var scale = fontSize / settings.TextStyle.size;
            var wrap = settings.WrapMode == TextBoxWrapMode.Wrap && !settings.ParagraphStyle.singleLine;
            var width = wrap ? contentWidth / scale : float.PositiveInfinity;
            var measuredWidth = 0f;
            heights.Clear();
            for (var index = 0; index < paragraphs.Count; ++index)
            {
                var paragraph = paragraphs[index];
                var usesNaturalLayout = float.IsPositiveInfinity(width) ||
                                        width + 0.001f >= paragraph.MaxIntrinsicWidth;
                paragraph.Layout(usesNaturalLayout
                    ? Paragraph.ResolveNoWrapLayoutWidth(0f, paragraph.MaxIntrinsicWidth)
                    : Math.Max(1f, Mathf.Ceil(width)));
                measuredWidth = Math.Max(measuredWidth, paragraph.LongestLine * scale);
                heights.Add(paragraph.Height * scale);
            }
            ResolveMargins(fontSize);
            var height = TextBoxBlockStack.Layout(heights, marginTops, marginBottoms,
                settings.ParagraphStyle.paragraphSpacing * scale, tops);
            heights.Clear();
            foreach (var naturalHeight in naturalHeights) heights.Add(naturalHeight * scale);
            var natural = TextBoxBlockStack.Layout(heights, marginTops, marginBottoms,
                settings.ParagraphStyle.paragraphSpacing * scale, tops);
            result = new TextIntrinsicMetrics(minWidth * scale, maxWidth * scale,
                natural, measuredWidth, height);
            if (cache.Count >= 128) cache.Clear();
            cache[key] = result;
            return result;
        }

        private void ResolveMargins(float fontSize)
        {
            marginTops.Clear();
            marginBottoms.Clear();
            var context = new MarginResolveContext(availableSize.x, availableSize.y, fontSize);
            foreach (var block in payload.Blocks)
            {
                marginTops.Add(block.Style.MarginTop.Resolve(context, MarginAxis.Vertical));
                marginBottoms.Add(block.Style.MarginBottom.Resolve(context, MarginAxis.Vertical));
            }
        }

        public void Dispose()
        {
            foreach (var paragraph in paragraphs) paragraph.Dispose();
            cache.Clear();
        }
    }
}
