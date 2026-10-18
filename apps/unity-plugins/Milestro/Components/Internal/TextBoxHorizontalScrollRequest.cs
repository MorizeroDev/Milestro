namespace Milestro.Components.Internal
{
    internal readonly struct TextBoxHorizontalScrollRequest
    {
        private TextBoxHorizontalScrollRequest(float value)
        {
            HasValue = true;
            Value = value;
        }

        internal bool HasValue { get; }
        internal float Value { get; }

        internal static TextBoxHorizontalScrollRequest None => default;

        internal static TextBoxHorizontalScrollRequest FromValue(float value)
        {
            return new TextBoxHorizontalScrollRequest(value);
        }
    }
}
