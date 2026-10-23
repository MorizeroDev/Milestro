namespace Milestro.Model
{
    public readonly struct TextSizingResult
    {
        public TextSizingResult(TextSizingRequest request,
            float width,
            float height,
            float fontSize,
            ResolvedMargin margin,
            TextIntrinsicMetrics metrics,
            float minWidth,
            float preferredWidth)
        {
            UsedWidth = width;
            UsedHeight = height;
            EffectiveFontSize = fontSize;
            Margin = margin;
            Metrics = metrics;
            MinWidth = request.WidthMode == TextWidthMode.Fixed ? -1f : minWidth;
            PreferredWidth = request.WidthMode == TextWidthMode.Fixed ? -1f : preferredWidth;
            PreferredHeight = request.HeightMode == TextHeightMode.Fixed ? -1f : height;
        }

        public float UsedWidth { get; }
        public float UsedHeight { get; }
        public float EffectiveFontSize { get; }
        public ResolvedMargin Margin { get; }
        public TextIntrinsicMetrics Metrics { get; }
        public float ContentWidth => Metrics.ContentWidth;
        public float ContentHeight => Metrics.ContentHeight;
        public float MinWidth { get; }
        public float PreferredWidth { get; }
        public float PreferredHeight { get; }
    }
}
