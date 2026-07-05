using System;

namespace Milestro.Model
{
    /// <summary>
    /// Maps to Skia's <c>skia::textlayout::TextDecoration</c> in <c>modules/skparagraph/include/TextStyle.h</c>.
    /// </summary>
    [Flags]
    public enum TextDecoration
    {
        NoDecoration = 0x0,
        Underline = 0x1,
        Overline = 0x2,
        LineThrough = 0x4,
    }
}
