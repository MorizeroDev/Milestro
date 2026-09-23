using Milestro.Skia.TextLayout;

namespace Milestro.RichTextParser
{
    internal readonly struct RubyAnnotation
    {
        internal RubyAnnotation(int startUtf16, int endUtf16, string text, RubyStyle style)
        {
            StartUtf16 = startUtf16;
            EndUtf16 = endUtf16;
            Text = text;
            Style = style;
        }

        internal int StartUtf16 { get; }
        internal int EndUtf16 { get; }
        internal string Text { get; }
        internal RubyStyle Style { get; }
    }
}
