namespace Milestro.RichTextParser
{
    internal readonly struct LinkAnnotation
    {
        internal LinkAnnotation(string href,
            string id,
            int startUtf16,
            int endUtf16,
            int occurrence)
        {
            Href = href;
            Id = id;
            StartUtf16 = startUtf16;
            EndUtf16 = endUtf16;
            Occurrence = occurrence;
        }

        internal string Href { get; }
        internal string Id { get; }
        internal int StartUtf16 { get; }
        internal int EndUtf16 { get; }
        internal int Occurrence { get; }
    }
}
