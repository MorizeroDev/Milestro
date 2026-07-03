using System.Collections.Generic;
using System.Text;

namespace Milestro.RichTextParser.AST
{
    public class XmlElementNode : XmlNode
    {
        public IDictionary<string, string> Attributes { get; private set; } = new Dictionary<string, string>();

        public IList<XmlNode> Children { get; private set; } = new List<XmlNode>();

        public XmlElementNode(string tag, Dictionary<string, string> attributes) : base(tag)
        {
            Attributes = attributes;
        }
#if UNITY_EDITOR
        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.Append('<').Append(Tag);
            foreach (var (key, value) in Attributes)
            {
                // value 没转义
                sb.Append(' ').Append(key).Append("=\"").Append(value).Append('"');
            }

            if (Children.Count == 0)
            {
                sb.Append("/>");
            }
            else
            {
                sb.Append('>');

                foreach (var child in Children)
                {
                    sb.Append(child);
                }

                sb.Append("</").Append(Tag).Append('>');
            }

            return sb.ToString();
        }
#endif
    }
}