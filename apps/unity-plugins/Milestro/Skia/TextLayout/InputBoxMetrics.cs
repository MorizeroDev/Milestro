namespace Milestro.Skia.TextLayout
{
    public readonly struct InputBoxMetrics
    {
        public readonly float Height;
        public readonly float LongestLine;
        public readonly float MinIntrinsicWidth;
        public readonly float MaxIntrinsicWidth;
        public readonly float ContentWidth;
        public readonly float ScrollX;
        public readonly float ScrollY;
        public readonly float ViewportWidth;
        public readonly float ViewportHeight;

        public InputBoxMetrics(float height,
            float longestLine,
            float minIntrinsicWidth,
            float maxIntrinsicWidth,
            float contentWidth,
            float scrollX,
            float scrollY,
            float viewportWidth,
            float viewportHeight)
        {
            Height = height;
            LongestLine = longestLine;
            MinIntrinsicWidth = minIntrinsicWidth;
            MaxIntrinsicWidth = maxIntrinsicWidth;
            ContentWidth = contentWidth;
            ScrollX = scrollX;
            ScrollY = scrollY;
            ViewportWidth = viewportWidth;
            ViewportHeight = viewportHeight;
        }
    }
}
