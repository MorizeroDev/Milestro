#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Milestro.Model;
using Paraparty.Colors;
using UnityEngine;

namespace Milestro.RichTextParser
{
    /// <summary>
    /// Tracks the inline text style overrides collected while walking rich text markup.
    /// </summary>
    /// <remarks>
    /// This is parser state, not a complete text style. The parser mutates a freshly cloned
    /// instance while applying one element's attributes, then treats that instance as an
    /// immutable snapshot for the text segments and child elements produced from that element.
    /// Nullable properties mean that markup did not specify an override and the final
    /// <c>TextStyle</c> should inherit that value from the caller-provided base style.
    /// Non-null properties are explicit rich text overrides for the current segment.
    /// </remarks>
    public class TextStyleState
    {
        /// <summary>
        /// Whether underline decoration should be added to the inherited decoration flags.
        /// </summary>
        public bool Underline { get; set; } = false;

        /// <summary>
        /// Whether line-through decoration should be added to the inherited decoration flags.
        /// </summary>
        public bool Strikethrough { get; set; } = false;

        /// <summary>
        /// Optional numeric font weight override.
        /// </summary>
        public int? FontWeight { get; set; } = null;

        /// <summary>
        /// Optional font width override.
        /// </summary>
        public FontWidth? FontWidth { get; set; } = null;

        /// <summary>
        /// Optional font slant override.
        /// </summary>
        public FontSlant? FontSlant { get; set; } = null;

        /// <summary>
        /// Compatibility alias for italic markup. Setting this to <c>true</c> stores
        /// <see cref="Model.FontSlant.Italic"/> in <see cref="FontSlant"/>.
        /// </summary>
        public bool Italic
        {
            get => FontSlant == Milestro.Model.FontSlant.Italic;
            set
            {
                if (value)
                {
                    FontSlant = Milestro.Model.FontSlant.Italic;
                }
                else if (FontSlant == Milestro.Model.FontSlant.Italic)
                {
                    FontSlant = null;
                }
            }
        }

        /// <summary>
        /// Optional font family override, ordered by preferred fallback.
        /// </summary>
        public List<string>? FontFamilies { get; set; } = null;

        /// <summary>
        /// Optional glyph color override.
        /// </summary>
        public Color? Color { get; set; } = null;

        /// <summary>
        /// Optional font size override in paragraph text units.
        /// </summary>
        public float? FontSize { get; set; } = null;

        /// <summary>
        /// Optional letter spacing override.
        /// </summary>
        public float? LetterSpacing { get; set; } = null;

        /// <summary>
        /// Optional baseline shift override.
        /// </summary>
        public float? BaselineShift { get; set; } = null;

        /// <summary>
        /// Optional locale override used by text shaping and font fallback.
        /// </summary>
        public string? Locale { get; set; } = null;

        /// <summary>
        /// Creates an inherited snapshot for a rich text element so its attributes can be
        /// applied without mutating the parent or sibling segment state.
        /// </summary>
        public TextStyleState Clone()
        {
            var ret = new TextStyleState();

            ret.Underline = Underline;
            ret.Strikethrough = Strikethrough;

            ret.FontWeight = FontWeight;
            ret.FontWidth = FontWidth;
            ret.FontSlant = FontSlant;
            ret.FontFamilies = FontFamilies == null ? null : new List<string>(FontFamilies);
            ret.Color = Color;
            ret.FontSize = FontSize;
            ret.LetterSpacing = LetterSpacing;
            ret.BaselineShift = BaselineShift;
            ret.Locale = Locale;

            return ret;
        }

#if UNITY_EDITOR
        public string GenerateRichText()
        {
            var sb = new StringBuilder();
            if (FontWeight.HasValue || Color.HasValue || FontSize.HasValue || FontFamilies != null)
            {
                sb.Append("<font");
                if (FontWeight.HasValue)
                {
                    AppendAttribute(sb, "weight", FontWeight.Value.ToString(CultureInfo.InvariantCulture));
                }

                if (Color.HasValue)
                {
                    AppendAttribute(sb, "color", ColorUtils.SerializeColor(Color.Value));
                }

                if (FontSize.HasValue)
                {
                    AppendAttribute(sb, "size", FontSize.Value.ToString(CultureInfo.InvariantCulture));
                }

                if (FontFamilies != null)
                {
                    AppendAttribute(sb, "face", string.Join(", ", FontFamilies));
                }

                sb.Append('>');
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

            if (LetterSpacing.HasValue || BaselineShift.HasValue || Locale != null || FontWidth.HasValue ||
                (FontSlant.HasValue && FontSlant.Value != Milestro.Model.FontSlant.Italic))
            {
                sb.Append("<span");
                if (LetterSpacing.HasValue)
                {
                    AppendAttribute(sb, "cspace", LetterSpacing.Value.ToString(CultureInfo.InvariantCulture));
                }

                if (BaselineShift.HasValue)
                {
                    AppendAttribute(sb, "voffset", BaselineShift.Value.ToString(CultureInfo.InvariantCulture));
                }

                if (Locale != null)
                {
                    AppendAttribute(sb, "lang", Locale);
                }

                if (FontWidth.HasValue)
                {
                    AppendAttribute(sb, "width", FontWidth.Value.ToString());
                }

                if (FontSlant.HasValue && FontSlant.Value != Milestro.Model.FontSlant.Italic)
                {
                    AppendAttribute(sb, "slant", FontSlant.Value.ToString());
                }

                sb.Append('>');
            }

            return sb.ToString();
        }

        private static void AppendAttribute(StringBuilder sb, string name, string value)
        {
            sb.Append(' ')
                .Append(name)
                .Append("=\"")
                .Append(EscapeAttribute(value))
                .Append('"');
        }

        private static string EscapeAttribute(string value)
        {
            return value
                .Replace("&", "&amp;")
                .Replace("\"", "&quot;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }

        public override string ToString()
        {
            return GenerateRichText();
        }
#endif
    }
}
