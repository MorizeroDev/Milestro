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
        /*
         *  enum TextDecoration {
         *      kNoDecoration = 0x0,
         *      kUnderline = 0x1,
         *      kOverline = 0x2,
         *      kLineThrough = 0x4,
         *  };
         */
        public bool Underline { get; set; } = false;
        public bool Strikethrough { get; set; } = false;

        // enum Slant {
        //     kUpright_Slant,
        //     kItalic_Slant,
        //     kOblique_Slant,
        // };
        public bool Italic { get; set; } = false;

        // enum Weight {
        //     kInvisible_Weight   =    0,
        //     kThin_Weight        =  100,
        //     kExtraLight_Weight  =  200,
        //     kLight_Weight       =  300,
        //     kNormal_Weight      =  400,
        //     kMedium_Weight      =  500,
        //     kSemiBold_Weight    =  600,
        //     kBold_Weight        =  700,
        //     kExtraBold_Weight   =  800,
        //     kBlack_Weight       =  900,
        //     kExtraBlack_Weight  = 1000,
        // };
        public bool Bold { get; set; } = false;

        public Color? Color { get; set; } = null;
        public float FontSize { get; set; } = -1;


        public TextStyleState Clone()
        {
            var ret = new TextStyleState();

            ret.Underline = Underline;
            ret.Strikethrough = Strikethrough;

            ret.Italic = Italic;
            ret.Bold = Bold;
            ret.Color = Color;
            ret.FontSize = FontSize;


            return ret;
        }

#if UNITY_EDITOR
        public string GenerateRichText()
        {
            var sb = new StringBuilder();
            if (Bold)
            {
                sb.Append("<b>");
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
