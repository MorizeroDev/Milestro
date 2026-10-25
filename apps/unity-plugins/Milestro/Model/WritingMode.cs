namespace Milestro.Model
{
    /// <summary>
    /// Maps to Skia's <c>skia::textlayout::WritingMode</c>. Text direction remains a
    /// separate concern and continues to describe resolved LTR/RTL runs.
    /// </summary>
    public enum WritingMode
    {
        HorizontalTb = 0,
        VerticalRl = 1,
    }
}
