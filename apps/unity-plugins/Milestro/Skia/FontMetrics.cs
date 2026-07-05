namespace Milestro.Skia
{
    public readonly struct FontMetrics
    {
        public FontMetrics(float ascent, float descent, float leading)
        {
            Ascent = ascent;
            Descent = descent;
            Leading = leading;
        }

        public float Ascent { get; }
        public float Descent { get; }
        public float Leading { get; }
    }
}
