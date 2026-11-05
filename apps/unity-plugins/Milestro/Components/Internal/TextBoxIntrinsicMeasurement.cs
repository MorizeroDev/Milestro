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
        private readonly List<float> marginRights = new List<float>();
        private readonly List<float> marginBottoms = new List<float>();
        private readonly List<float> marginLefts = new List<float>();
        private readonly List<float> tops = new List<float>();
        private readonly Dictionary<(float, float), TextIntrinsicMetrics> cache =
            new Dictionary<(float, float), TextIntrinsicMetrics>();
        private Vector2 availableSize;
        private readonly bool empty;
        private readonly List<float> naturalHeights = new List<float>();

        public TextBoxIntrinsicMeasurement(TextBoxRenderTargetSettings settings)
        {
            if (TextBoxVerticalLayoutSupport.IsVertical(settings))
            {
                throw new NotSupportedException(
                    "Vertical-rl Canvas TextBox does not expose intrinsic horizontal text measurement.");
            }

            this.settings = settings;
            payload = settings.ParseContent();
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
                var paragraphMaxWidth = paragraph.MaxIntrinsicWidth;
                paragraph.Layout(Paragraph.ResolveNoWrapLayoutWidth(0f, paragraphMaxWidth));
                naturalHeights.Add(paragraph.Height);
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
            var measuredMinWidth = 0f;
            var measuredMaxWidth = 0f;
            heights.Clear();
            ResolveMargins(fontSize);
            for (var index = 0; index < paragraphs.Count; ++index)
            {
                var paragraph = paragraphs[index];
                var horizontalMargins = (marginLefts[index] + marginRights[index]) / scale;
                var availableWidth = width - horizontalMargins;
                var usesNaturalLayout = float.IsPositiveInfinity(width) ||
                                        availableWidth + 0.001f >= paragraph.MaxIntrinsicWidth;
                paragraph.Layout(usesNaturalLayout
                    ? Paragraph.ResolveNoWrapLayoutWidth(0f, paragraph.MaxIntrinsicWidth)
                    : Math.Max(1f, Mathf.Ceil(availableWidth)));
                measuredWidth = Math.Max(measuredWidth,
                    paragraph.LongestLine * scale + marginLefts[index] + marginRights[index]);
                measuredMinWidth = Math.Max(measuredMinWidth,
                    paragraph.MinIntrinsicWidth * scale + marginLefts[index] + marginRights[index]);
                measuredMaxWidth = Math.Max(measuredMaxWidth,
                    paragraph.MaxIntrinsicWidth * scale + marginLefts[index] + marginRights[index]);
                heights.Add(paragraph.Height * scale);
            }
            var height = TextBoxBlockStack.Layout(heights, marginTops, marginBottoms,
                settings.ParagraphStyle.paragraphSpacing * scale, tops);
            heights.Clear();
            foreach (var naturalHeight in naturalHeights) heights.Add(naturalHeight * scale);
            var natural = TextBoxBlockStack.Layout(heights, marginTops, marginBottoms,
                settings.ParagraphStyle.paragraphSpacing * scale, tops);
            if (settings.ParagraphStyle.singleLine || settings.WrapMode == TextBoxWrapMode.NoWrap)
            {
                measuredMinWidth = measuredMaxWidth;
            }
            result = new TextIntrinsicMetrics(measuredMinWidth, measuredMaxWidth,
                natural, measuredWidth, height);
            if (cache.Count >= 128) cache.Clear();
            cache[key] = result;
            return result;
        }

        private void ResolveMargins(float fontSize)
        {
            marginTops.Clear();
            marginRights.Clear();
            marginBottoms.Clear();
            marginLefts.Clear();
            var context = new MarginResolveContext(availableSize.x, availableSize.y, fontSize);
            foreach (var block in payload.Blocks)
            {
                marginTops.Add(block.Style.MarginTop.Resolve(context, MarginAxis.Vertical));
                marginRights.Add(block.Style.MarginRight.Resolve(context, MarginAxis.Horizontal));
                marginBottoms.Add(block.Style.MarginBottom.Resolve(context, MarginAxis.Vertical));
                marginLefts.Add(block.Style.MarginLeft.Resolve(context, MarginAxis.Horizontal));
            }
        }

        public void Dispose()
        {
            foreach (var paragraph in paragraphs) paragraph.Dispose();
            cache.Clear();
        }
    }
}
