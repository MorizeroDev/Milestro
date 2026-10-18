using UnityEngine;

namespace Milestro.Components.Internal
{
    internal readonly struct ScreenSpaceRasterMeasurement
    {
        internal ScreenSpaceRasterMeasurement(Vector2Int logicalSize,
            float desiredScale,
            Camera? eventCamera,
            int targetDisplay)
        {
            LogicalSize = logicalSize;
            DesiredScale = desiredScale;
            EventCamera = eventCamera;
            TargetDisplay = targetDisplay;
        }

        internal Vector2Int LogicalSize { get; }
        internal float DesiredScale { get; }
        internal Camera? EventCamera { get; }
        internal int TargetDisplay { get; }
    }
}
