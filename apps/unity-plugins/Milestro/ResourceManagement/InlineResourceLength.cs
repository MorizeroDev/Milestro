using System;

namespace Milestro.ResourceManagement
{
    public readonly struct InlineResourceLength
    {
        private InlineResourceLength(float value, bool isEm)
        {
            if (!float.IsFinite(value) || value <= 0f) throw new ArgumentOutOfRangeException(nameof(value));
            Value = value;
            IsEm = isEm;
        }

        public float Value { get; }
        public bool IsEm { get; }
        public static InlineResourceLength Pixels(float value) => new InlineResourceLength(value, false);
        public static InlineResourceLength Em(float value) => new InlineResourceLength(value, true);
        internal float Resolve(float fontSize) => IsEm ? Value * fontSize : Value;
    }
}
