namespace Milestro.Model
{
    public readonly struct MarginResolveContext
    {
        public MarginResolveContext(float containerWidth, float containerHeight, float fontSize,
            float viewportWidth = float.NaN, float viewportHeight = float.NaN,
            bool hasContainerWidth = true, bool hasViewport = false)
        {
            ContainerWidth = IsFinite(containerWidth) ? containerWidth : 0f;
            ContainerHeight = IsFinite(containerHeight) ? containerHeight : 0f;
            FontSize = IsFinite(fontSize) ? fontSize : 0f;
            ViewportWidth = IsFinite(viewportWidth) ? viewportWidth : 0f;
            ViewportHeight = IsFinite(viewportHeight) ? viewportHeight : 0f;
            HasContainerWidth = hasContainerWidth && IsFinite(containerWidth);
            HasViewport = hasViewport && IsFinite(viewportWidth) && IsFinite(viewportHeight);
        }

        public float ContainerWidth { get; }
        public float ContainerHeight { get; }
        public float FontSize { get; }
        public float ViewportWidth { get; }
        public float ViewportHeight { get; }
        public bool HasContainerWidth { get; }
        public bool HasViewport { get; }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
