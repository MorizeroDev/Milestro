namespace Milestro.Configuration
{
    public class TextInputConfiguration
    {
        public float ScrollWheelStepPixels { get; set; } = 48f;
        public float KeyRepeatInitialDelay { get; set; } = 0.42f;
        public float KeyRepeatInterval { get; set; } = 0.045f;
        public float KeyboardScrollInterlockTimeoutSeconds { get; set; } = 0.03f;
        public float SurrogatePairTimeout { get; set; } = 2.5f;
    }
}
