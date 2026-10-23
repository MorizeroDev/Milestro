namespace Milestro.Model
{
    public readonly struct TextSizingRequest
    {
        public TextSizingRequest(TextWidthMode widthMode, TextHeightMode heightMode,
            float fixedWidth, float fixedHeight, float availableWidth, bool wrap,
            float designFontSize, bool autoFontSize = false, float minFontSize = 12f,
            float maxFontSize = 72f, float widthCap = float.PositiveInfinity,
            float heightCap = float.PositiveInfinity)
        {
            WidthMode = widthMode;
            HeightMode = heightMode;
            FixedWidth = fixedWidth;
            FixedHeight = fixedHeight;
            AvailableWidth = availableWidth;
            Wrap = wrap;
            DesignFontSize = designFontSize;
            AutoFontSize = autoFontSize;
            MinFontSize = minFontSize;
            MaxFontSize = maxFontSize;
            WidthCap = widthCap;
            HeightCap = heightCap;
        }

        public TextWidthMode WidthMode { get; }
        public TextHeightMode HeightMode { get; }
        public float FixedWidth { get; }
        public float FixedHeight { get; }
        public float AvailableWidth { get; }
        public bool Wrap { get; }
        public float DesignFontSize { get; }
        public bool AutoFontSize { get; }
        public float MinFontSize { get; }
        public float MaxFontSize { get; }
        public float WidthCap { get; }
        public float HeightCap { get; }
        public bool HasContentSizing => WidthMode != TextWidthMode.Fixed || HeightMode != TextHeightMode.Fixed;
    }
}
