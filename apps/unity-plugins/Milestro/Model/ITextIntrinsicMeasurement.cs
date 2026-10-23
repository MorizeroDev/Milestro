namespace Milestro.Model
{
    public interface ITextIntrinsicMeasurement
    {
        TextIntrinsicMetrics Measure(float fontSize, float contentWidth);
    }

    public readonly struct TextIntrinsicMetrics
    {
        public TextIntrinsicMetrics(float minContentWidth, float maxContentWidth,
            float naturalHeight, float contentWidth, float contentHeight)
        {
            MinContentWidth = minContentWidth;
            MaxContentWidth = maxContentWidth;
            NaturalHeight = naturalHeight;
            ContentWidth = contentWidth;
            ContentHeight = contentHeight;
        }

        public float MinContentWidth { get; }
        public float MaxContentWidth { get; }
        public float NaturalHeight { get; }
        public float ContentWidth { get; }
        public float ContentHeight { get; }
    }
}
