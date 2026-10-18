namespace Milestro.RichTextParser.Css
{
    internal readonly struct CssDeclaration
    {
        internal CssDeclaration(CssSlice raw)
        {
            Raw = raw;
            Property = default;
            Value = default;
            IsValid = false;
        }

        internal CssDeclaration(CssSlice raw, CssSlice property, CssSlice value)
        {
            Raw = raw;
            Property = property;
            Value = value;
            IsValid = true;
        }

        internal CssSlice Raw { get; }
        internal CssSlice Property { get; }
        internal CssSlice Value { get; }
        internal bool IsValid { get; }
    }
}
