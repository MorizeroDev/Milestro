namespace Milestro.Skia.TextLayout
{
    public readonly struct InputBoxCaret
    {
        public readonly ulong Utf8Offset;
        public readonly ulong Utf16Offset;
        public readonly int Affinity;

        public InputBoxCaret(ulong utf8Offset, ulong utf16Offset, int affinity)
        {
            Utf8Offset = utf8Offset;
            Utf16Offset = utf16Offset;
            Affinity = affinity;
        }
    }
}
