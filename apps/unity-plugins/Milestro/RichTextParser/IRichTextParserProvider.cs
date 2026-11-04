namespace Milestro.RichTextParser
{
    /// <summary>
    /// Provides one named rich text parser to <see cref="RichTextParserService"/>: the stable
    /// <see cref="Id"/> stored in serialized data, the <see cref="DisplayName"/> shown in the
    /// inspector, and the factory for parser instances. Implementations must be stateless;
    /// one instance is shared for the whole registration.
    /// </summary>
    public interface IRichTextParserProvider
    {
        /// <summary>
        /// Stable identifier matching <c>[A-Za-z_][A-Za-z0-9_-]*</c>. Ids live in serialized
        /// data, so never rename a shipped id — register an alias instead.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Name shown in the inspector. Presentation-only and may change freely.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Creates the parser. Called once per registration; the result is shared with all
        /// later callers.
        /// </summary>
        IRichTextParser CreateParser();
    }
}
