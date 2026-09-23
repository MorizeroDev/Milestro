using System;

namespace Milestro.Skia.TextLayout
{
    public sealed class RubyStyle
    {
        public TextStyle? TextStyle { get; set; }

        public float FontSizeMultiplier { get; set; } = 0.5f;

        public float Gap { get; set; } = 2;

        public RubyPosition Position { get; set; } = RubyPosition.Above;

        public RubyAlignment Alignment { get; set; } = RubyAlignment.Center;

        public RubyOverhang Overhang { get; set; } = RubyOverhang.None;

        public RubyOverflow Overflow { get; set; } = RubyOverflow.PreserveBase;

        internal void Validate()
        {
            if (float.IsNaN(FontSizeMultiplier) || float.IsInfinity(FontSizeMultiplier) ||
                FontSizeMultiplier <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(FontSizeMultiplier));
            }

            if (float.IsNaN(Gap) || float.IsInfinity(Gap) || Gap < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(Gap));
            }

            if (!Enum.IsDefined(typeof(RubyPosition), Position))
            {
                throw new ArgumentOutOfRangeException(nameof(Position));
            }

            if (!Enum.IsDefined(typeof(RubyAlignment), Alignment))
            {
                throw new ArgumentOutOfRangeException(nameof(Alignment));
            }

            if (!Enum.IsDefined(typeof(RubyOverhang), Overhang))
            {
                throw new ArgumentOutOfRangeException(nameof(Overhang));
            }

            if (!Enum.IsDefined(typeof(RubyOverflow), Overflow))
            {
                throw new ArgumentOutOfRangeException(nameof(Overflow));
            }
        }
    }
}
