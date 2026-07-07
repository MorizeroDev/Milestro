namespace Milestro.Model
{
    public readonly struct ResolvedMargin
    {
        public ResolvedMargin(float left, float top, float right, float bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public float Left { get; }
        public float Top { get; }
        public float Right { get; }
        public float Bottom { get; }
        public float FixedHorizontalSize => Left + Right;
        public float FixedVerticalSize => Top + Bottom;
    }
}
