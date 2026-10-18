namespace Milestro.Components
{
    /// <summary>
    /// Validates one Unicode scalar value. Multi-scalar grapheme clusters, including ZWJ emoji,
    /// are not delivered atomically.
    /// </summary>
    public delegate int TextInputValidateInput(string text, int charIndex, int codePoint);
}
