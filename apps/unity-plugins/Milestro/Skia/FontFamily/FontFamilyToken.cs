namespace Milestro.Skia
{
    public readonly struct FontFamilyToken
    {
        private FontFamilyToken(FontFamilyTokenKind kind, string value)
        {
            Kind = kind;
            Value = value ?? "";
        }

        public FontFamilyTokenKind Kind { get; }

        public string Value { get; }

        public static FontFamilyToken Bare(string familyName)
        {
            return new FontFamilyToken(FontFamilyTokenKind.Bare, (familyName ?? "").Trim());
        }

        public static FontFamilyToken Exact(string familyName)
        {
            return new FontFamilyToken(FontFamilyTokenKind.Exact, familyName ?? "");
        }

        public override string ToString()
        {
            return Value;
        }
    }
}
