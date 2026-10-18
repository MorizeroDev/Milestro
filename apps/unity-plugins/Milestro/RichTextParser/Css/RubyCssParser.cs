using System;
using System.Globalization;
using Milestro.Skia.TextLayout;

namespace Milestro.RichTextParser.Css
{
    internal static class RubyCssParser
    {
        internal static RubyStyle Parse(string declarations)
        {
            if (declarations == null) throw new ArgumentNullException(nameof(declarations));
            var style = new RubyStyle
            {
                Alignment = RubyAlignment.SpaceAround,
                Overhang = RubyOverhang.Allow,
            };
            var parser = new CssDeclarationParser(declarations);
            while (parser.TryRead(out var declaration))
            {
                if (!declaration.IsValid)
                {
                    throw new InvalidCastException($"invalid Ruby CSS declaration: {declaration.Raw}");
                }

                ParseDeclaration(style, declaration);
            }

            return style;
        }

        private static void ParseDeclaration(RubyStyle style, CssDeclaration declaration)
        {
            var property = declaration.Property;
            var value = declaration.Value;
            var isCustomProperty = property.StartsWith("--", StringComparison.Ordinal);
            if (!isCustomProperty && property.Equals("ruby-position", StringComparison.OrdinalIgnoreCase))
            {
                if (value.Equals("over", StringComparison.OrdinalIgnoreCase)) style.Position = RubyPosition.Above;
                else if (value.Equals("under", StringComparison.OrdinalIgnoreCase)) style.Position = RubyPosition.Below;
                else throw InvalidProperty(property, value);
            }
            else if (!isCustomProperty && property.Equals("ruby-align", StringComparison.OrdinalIgnoreCase))
            {
                if (value.Equals("start", StringComparison.OrdinalIgnoreCase)) style.Alignment = RubyAlignment.Start;
                else if (value.Equals("center", StringComparison.OrdinalIgnoreCase)) style.Alignment = RubyAlignment.Center;
                else if (value.Equals("space-around", StringComparison.OrdinalIgnoreCase))
                {
                    style.Alignment = RubyAlignment.SpaceAround;
                }
                else if (value.Equals("space-between", StringComparison.OrdinalIgnoreCase))
                {
                    style.Alignment = RubyAlignment.SpaceBetween;
                }
                else throw InvalidProperty(property, value);
            }
            else if (!isCustomProperty && property.Equals("ruby-overhang", StringComparison.OrdinalIgnoreCase))
            {
                if (value.Equals("none", StringComparison.OrdinalIgnoreCase)) style.Overhang = RubyOverhang.None;
                else if (value.Equals("auto", StringComparison.OrdinalIgnoreCase)) style.Overhang = RubyOverhang.Allow;
                else throw InvalidProperty(property, value);
            }
            else if (isCustomProperty && property.Equals("--milestro-ruby-overflow", StringComparison.Ordinal))
            {
                if (value.Equals("preserve-base", StringComparison.Ordinal)) style.Overflow = RubyOverflow.PreserveBase;
                else if (value.Equals("inline", StringComparison.Ordinal)) style.Overflow = RubyOverflow.Inline;
                else throw InvalidProperty(property, value);
            }
            else if (isCustomProperty && property.Equals("--milestro-ruby-size", StringComparison.Ordinal))
            {
                if (!TryParseFiniteFloat(value, out var fontSizeMultiplier) || fontSizeMultiplier <= 0)
                {
                    throw InvalidProperty(property, value);
                }

                style.FontSizeMultiplier = fontSizeMultiplier;
            }
            else if (isCustomProperty && property.Equals("--milestro-ruby-gap", StringComparison.Ordinal))
            {
                if (!TryParseFiniteFloat(value, out var gap) || gap < 0) throw InvalidProperty(property, value);
                style.Gap = gap;
            }
            else
            {
                throw new InvalidCastException($"unsupported Ruby CSS property: {property}");
            }
        }

        private static InvalidCastException InvalidProperty(CssSlice property, CssSlice value)
        {
            return new InvalidCastException($"invalid Ruby CSS value for {property}: {value}");
        }

        private static bool TryParseFiniteFloat(CssSlice value, out float result)
        {
            return float.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out result) &&
                   !float.IsNaN(result) && !float.IsInfinity(result);
        }
    }
}
