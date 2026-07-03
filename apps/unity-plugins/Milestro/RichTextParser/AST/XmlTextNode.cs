using System.Text;

namespace Milestro.RichTextParser.AST
{
    public class XmlTextNode : XmlNode
    {
        public string Text { get; private set; }

        public XmlTextNode(string text) : base("text")
        {
            Text = text;
        }

#if UNITY_EDITOR
        public override string ToString()
        {
            var sb = new StringBuilder();
            // 没转义
            sb.Append(Text);
            return sb.ToString();
        }
#endif
    }
}