namespace Milestro.RichTextParser.Css
{
    internal readonly struct CssToken
    {
        internal CssToken(CssTokenType type, int start, int length)
        {
            Type = type;
            Start = start;
            Length = length;
        }

        internal CssTokenType Type { get; }
        internal int Start { get; }
        internal int Length { get; }
        internal int End => Start + Length;
    }
}
