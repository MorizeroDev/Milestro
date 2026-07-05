using System.Text;
using Paraparty.Colors;
using UnityEngine;

namespace Milestro.RichTextParser
{
    /// <summary>
    /// 这玩意儿必须是 Struct
    /// </summary>
    public class TextStyleState
    {
        public bool Underline { get; set; } = false;

        public bool Strikethrough { get; set; } = false;

        public bool Italic { get; set; } = false;

        public int? FontWeight { get; set; } = null;

        public Color? Color { get; set; } = null;

        public float FontSize { get; set; } = -1;

        public TextStyleState Clone()
        {
            var ret = new TextStyleState();

            ret.Underline = Underline;
            ret.Strikethrough = Strikethrough;

            ret.Italic = Italic;
            ret.FontWeight = FontWeight;
            ret.Color = Color;
            ret.FontSize = FontSize;


            return ret;
        }

#if UNITY_EDITOR
        public string GenerateRichText()
        {
            var sb = new StringBuilder();
            if (FontWeight.HasValue)
            {
                sb.Append("<font weight=\"").Append(FontWeight.Value).Append("\">");
            }

            if (Italic)
            {
                sb.Append("<i>");
            }

            if (Underline)
            {
                sb.Append("<u>");
            }

            if (Strikethrough)
            {
                sb.Append("<s>");
            }

            if (Color.HasValue)
            {
                sb.Append("<color=").Append(ColorUtils.SerializeColor(Color.Value)).Append('>');
            }

            if (FontSize >= 0)
            {
                sb.Append("<size=").Append(FontSize).Append('>');
            }

            return sb.ToString();
        }

        public override string ToString()
        {
            return GenerateRichText();
        }
#endif
    }
}
