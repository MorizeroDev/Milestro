namespace Milestro.Components.Internal
{
    internal readonly struct TextBoxLinkHit
    {
        internal TextBoxLinkHit(string href, string id, int occurrence, long generation)
        {
            Href = href;
            Id = id;
            Occurrence = occurrence;
            Generation = generation;
        }

        internal string Href { get; }
        internal string Id { get; }
        internal int Occurrence { get; }
        internal long Generation { get; }

        internal bool IsSameOccurrence(TextBoxLinkHit other)
        {
            return Occurrence == other.Occurrence && Generation == other.Generation;
        }
    }
}
