namespace Milestro.RichTextParser.AST
{
    public abstract class XmlNode
    {
        public string Tag { get; private set; }

        public XmlNode(string tag)
        {
            Tag = tag;
        }
    }
}
