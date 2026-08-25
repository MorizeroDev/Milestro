namespace Milestro.Configuration
{
    public class RenderSurfaceConfiguration
    {
        public const float DefaultMaxScreenSpaceRasterScale = 2f;
        public const float DefaultMinimumFallbackScale = 0.25f;
        public const float DefaultScaleQuantum = 0.25f;
        public const float DefaultScaleHysteresis = 0.05f;
        public const int DefaultConservativeMaxTextureEdge = 8192;
        public const long DefaultMaxPixelsPerSurface = 16L * 1024L * 1024L;
        public const long DefaultMaxBytesPerSurface = 64L * 1024L * 1024L;
        public const long DefaultMaxGlobalBytes = 256L * 1024L * 1024L;
        public const long DefaultMaxTransitionBytes = 128L * 1024L * 1024L;
        public const int DefaultMaxAttemptsPerRequestAndEpoch = 8;

        public float MaxScreenSpaceRasterScale { get; set; } = DefaultMaxScreenSpaceRasterScale;

        public float MinimumFallbackScale { get; set; } = DefaultMinimumFallbackScale;

        public float ScaleQuantum { get; set; } = DefaultScaleQuantum;

        public float ScaleHysteresis { get; set; } = DefaultScaleHysteresis;

        public int ConservativeMaxTextureEdge { get; set; } = DefaultConservativeMaxTextureEdge;

        public long MaxPixelsPerSurface { get; set; } = DefaultMaxPixelsPerSurface;

        public long MaxBytesPerSurface { get; set; } = DefaultMaxBytesPerSurface;

        public long MaxGlobalBytes { get; set; } = DefaultMaxGlobalBytes;

        public long MaxTransitionBytes { get; set; } = DefaultMaxTransitionBytes;

        public int MaxAttemptsPerRequestAndEpoch { get; set; } = DefaultMaxAttemptsPerRequestAndEpoch;
    }
}
