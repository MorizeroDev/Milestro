namespace Milestro.Components
{
    internal readonly struct TextBoxLayoutMeasurement
    {
        public TextBoxLayoutMeasurement(float preferredWidth,
            float preferredHeight,
            bool hasContentPreferredWidth,
            float contentWidth,
            float contentHeight,
            float viewportWidth,
            float viewportHeight)
        {
            PreferredWidth = preferredWidth;
            PreferredHeight = preferredHeight;
            HasContentPreferredWidth = hasContentPreferredWidth;
            ContentWidth = contentWidth;
            ContentHeight = contentHeight;
            ViewportWidth = viewportWidth;
            ViewportHeight = viewportHeight;
        }

        public float PreferredWidth { get; }
        public float PreferredHeight { get; }
        public bool HasContentPreferredWidth { get; }
        public float ContentWidth { get; }
        public float ContentHeight { get; }
        public float ViewportWidth { get; }
        public float ViewportHeight { get; }
    }
}
