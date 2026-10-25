#nullable enable
namespace Milestro.RichTextParser
{
    public readonly struct PlaceholderAnnotation
    {
        internal PlaceholderAnnotation(int startUtf16, float width, float height, string key,
            TextStyleState textStyle)
        {
            StartUtf16 = startUtf16;
            Width = width;
            Height = height;
            Key = key;
            TextStyle = textStyle;
        }

        public int StartUtf16 { get; }
        public int EndUtf16 => StartUtf16 + 1;
        public float Width { get; }
        public float Height { get; }
        public string Key { get; }
        internal TextStyleState TextStyle { get; }
    }
}
