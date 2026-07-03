namespace Milestro.Skia.TextLayout
{
    public readonly struct InputBoxSelection
    {
        public readonly ulong AnchorUtf8;
        public readonly ulong FocusUtf8;
        public readonly ulong StartUtf8;
        public readonly ulong EndUtf8;
        public readonly int AnchorAffinity;
        public readonly int FocusAffinity;
        public readonly bool HasSelection;

        public InputBoxSelection(ulong anchorUtf8,
            ulong focusUtf8,
            ulong startUtf8,
            ulong endUtf8,
            int anchorAffinity,
            int focusAffinity,
            bool hasSelection)
        {
            AnchorUtf8 = anchorUtf8;
            FocusUtf8 = focusUtf8;
            StartUtf8 = startUtf8;
            EndUtf8 = endUtf8;
            AnchorAffinity = anchorAffinity;
            FocusAffinity = focusAffinity;
            HasSelection = hasSelection;
        }
    }
}