namespace Milestro.Unicode
{
    /// <summary>
    /// Specifies the direction of transliteration.
    /// </summary>
    public enum UTransDirection
    {
        /// <summary>
        /// Specifies the forward direction, from <source> to <target> for a transliterator 
        /// with ID <source>-<target>. When a transliterator is opened using a rule, 
        /// it refers to forward direction rules, e.g., "A > B".
        /// </summary>
        UTRANS_FORWARD,

        /// <summary>
        /// Specifies the reverse direction, from <target> to <source> for a transliterator 
        /// with ID <source>-<target>. When a transliterator is opened using a rule, 
        /// it refers to reverse direction rules, e.g., "A < B".
        /// </summary>
        UTRANS_REVERSE
    }
}