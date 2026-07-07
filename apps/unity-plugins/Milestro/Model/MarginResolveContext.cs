namespace Milestro.Model
{
    public readonly struct MarginResolveContext
    {
        public MarginResolveContext(float containerWidth, float containerHeight, float fontSize)
        {
            ContainerWidth = IsFinite(containerWidth) ? containerWidth : 0f;
            ContainerHeight = IsFinite(containerHeight) ? containerHeight : 0f;
            FontSize = IsFinite(fontSize) ? fontSize : 0f;
        }

        public float ContainerWidth { get; }
        public float ContainerHeight { get; }
        public float FontSize { get; }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
