namespace Milestro.RichTextParser
{
    public class TextSegment
    {
        public TextStyleState TextStyle { get; set; } = new TextStyleState();

        public string Content { get; set; }

        public static TextSegment MakeText(string text)
        {
            var ret = new TextSegment();
            ret.Content = text;
            return ret;
        }

        public static TextSegment MakeText(TextStyleState style, string text)
        {
            var ret = new TextSegment();
            ret.TextStyle = style;
            ret.Content = text;
            return ret;
        }
    }
}
