using System.Collections.Generic;

namespace Milestro.RichTextParser
{
    /// <summary>
    /// Per-parse inputs shared by all <see cref="IRichTextParser"/> implementations.
    /// </summary>
    public sealed class RichTextParseContext
    {
        public RichTextParseContext(bool allowViewportBlockMargins = false,
            ICollection<string>? issues = null)
        {
            AllowViewportBlockMargins = allowViewportBlockMargins;
            Issues = issues;
        }

        /// <summary>
        /// Whether viewport-relative block margin units (vw/vh) are allowed. Vertical
        /// layouts can resolve them against the viewport; other layouts reject them.
        /// Parsers without viewport-relative units may ignore this flag.
        /// </summary>
        public bool AllowViewportBlockMargins { get; }

        /// <summary>
        /// Optional sink for non-fatal parse diagnostics, in source order.
        /// </summary>
        public ICollection<string>? Issues { get; }
    }
}
