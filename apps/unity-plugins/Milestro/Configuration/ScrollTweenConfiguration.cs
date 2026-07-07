namespace Milestro.Configuration
{
    public class ScrollTweenConfiguration
    {
        public const float DefaultFractionalDeltaTolerance = 0.001f;

        public PointerScrollTweenMode PointerScrollTweenMode { get; set; } = PointerScrollTweenMode.Auto;
        public float FractionalDeltaTolerance { get; set; } = DefaultFractionalDeltaTolerance;
    }
}
