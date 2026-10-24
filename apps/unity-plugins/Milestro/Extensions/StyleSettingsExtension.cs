using System.Collections.Generic;
using Milestro.Model;
using Milestro.Skia.TextLayout;

namespace Milestro.Extensions
{
    public static class StyleSettingsExtension
    {
        public static ParagraphStyle ToParagraphStyle(this ParagraphStyleSettings settings, bool ellipsize)
        {
            var ret = new ParagraphStyle();
            ret.TextAlign = settings.textAlign;
            ret.TextDirection = settings.textDirection;
            if (settings.singleLine)
            {
                ret.MaxLines = 1;
                if (ellipsize)
                {
                    ret.SetEllipsis(settings.ellipsisString);
                }
            }

            return ret;
        }

        public static TextStyle ToTextStyle(this TextStyleSettings settings)
        {
            var ret = new TextStyle();
            ret.SetFontFamilies(new List<string>(settings.fontFamilies));
            ret.FontSize = settings.size;
            ret.Locale = settings.locale;
            ret.Color = settings.textColor;
            ret.LetterSpacing = settings.letterSpacing;
            ret.WordSpacing = settings.wordSpacing;
            if (settings.lineHeight > 0f)
            {
                ret.Height = settings.lineHeight;
                ret.HeightOverride = true;
            }

            if (settings.weight > FontWeight.Invisible && settings.weight <= FontWeight.ExtraBlack)
            {
                ret.SetFontStyle(settings.weight, settings.width, settings.slant);
            }

            return ret;
        }
    }
}
