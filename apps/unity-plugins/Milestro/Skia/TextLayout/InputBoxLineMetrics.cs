namespace Milestro.Skia.TextLayout
{
    public readonly struct InputBoxLineMetrics
    {
        public readonly ulong StartUtf8;
        public readonly ulong EndUtf8;
        public readonly float Ascent;
        public readonly float Descent;
        public readonly float UnscaledAscent;
        public readonly float Height;
        public readonly float Width;
        public readonly float Left;
        public readonly float Baseline;

        public InputBoxLineMetrics(ulong startUtf8,
            ulong endUtf8,
            float ascent,
            float descent,
            float unscaledAscent,
            float height,
            float width,
            float left,
            float baseline)
        {
            StartUtf8 = startUtf8;
            EndUtf8 = endUtf8;
            Ascent = ascent;
            Descent = descent;
            UnscaledAscent = unscaledAscent;
            Height = height;
            Width = width;
            Left = left;
            Baseline = baseline;
        }
    }
}