using UnityEngine;

namespace Milestro.Skia
{
    public readonly struct FontTextMeasurement
    {
        public FontTextMeasurement(Rect bounds, float advanceX, FontMetrics metrics)
        {
            Bounds = bounds;
            AdvanceX = advanceX;
            Metrics = metrics;
        }

        public Rect Bounds { get; }
        public float AdvanceX { get; }
        public FontMetrics Metrics { get; }
    }
}
