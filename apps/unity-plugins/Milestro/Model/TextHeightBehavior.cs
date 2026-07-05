using System;

namespace Milestro.Model
{
    /// <summary>
    /// Maps to Skia's <c>skia::textlayout::TextHeightBehavior</c> in <c>modules/skparagraph/include/DartTypes.h</c>.
    /// </summary>
    [Flags]
    public enum TextHeightBehavior
    {
        All = 0x0,
        DisableFirstAscent = 0x1,
        DisableLastDescent = 0x2,
        DisableAll = DisableFirstAscent | DisableLastDescent,
    }
}
