namespace Milestro.Configuration
{
    public class ScrollAxisLockConfiguration
    {
        public float DefaultGestureTimeoutSeconds { get; set; } = 0.18f;
        public float DefaultDeadzone { get; set; } = 0.2f;
        public float DefaultDominanceRatio { get; set; } = 2f;
        public float DefaultDecisionDistance { get; set; } = 1f;
        public float DefaultInitialCorrectionSeconds { get; set; } = 0.08f;
    }
}
