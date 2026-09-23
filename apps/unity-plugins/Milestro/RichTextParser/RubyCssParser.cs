using System;
using System.Globalization;
using Milestro.Skia.TextLayout;

namespace Milestro.RichTextParser
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
            foreach (var rawDeclaration in declarations.Split(';'))
            {
                var declaration = rawDeclaration.Trim();
                if (declaration.Length == 0)
                {
                    continue;
                }

                ParseDeclaration(style, declaration);
            }

            return style;
        }

        private static void ParseDeclaration(RubyStyle style, string declaration)
        {
            var separator = declaration.IndexOf(':');
            if (separator <= 0 || separator == declaration.Length - 1)
            {
                throw new InvalidCastException($"invalid Ruby CSS declaration: {declaration}");
            }

            var property = declaration.Substring(0, separator).Trim();
            var value = declaration.Substring(separator + 1).Trim();
            var isCustomProperty = property.StartsWith("--", StringComparison.Ordinal);
            var normalizedProperty = isCustomProperty ? property : property.ToLowerInvariant();
            var normalizedValue = isCustomProperty ? value : value.ToLowerInvariant();
            switch (normalizedProperty)
            {
                case "ruby-position":
                    style.Position = normalizedValue switch
                    {
                        "over" => RubyPosition.Above,
                        "under" => RubyPosition.Below,
                        _ => throw InvalidProperty(property, value),
                    };
                    break;
                case "ruby-align":
                    style.Alignment = normalizedValue switch
                    {
                        "start" => RubyAlignment.Start,
                        "center" => RubyAlignment.Center,
                        "space-around" => RubyAlignment.SpaceAround,
                        "space-between" => RubyAlignment.SpaceBetween,
                        _ => throw InvalidProperty(property, value),
                    };
                    break;
                case "ruby-overhang":
                    style.Overhang = normalizedValue switch
                    {
                        "none" => RubyOverhang.None,
                        "auto" => RubyOverhang.Allow,
                        _ => throw InvalidProperty(property, value),
                    };
                    break;
                case "--milestro-ruby-overflow":
                    style.Overflow = value switch
                    {
                        "preserve-base" => RubyOverflow.PreserveBase,
                        "inline" => RubyOverflow.Inline,
                        _ => throw InvalidProperty(property, value),
                    };
                    break;
                case "--milestro-ruby-size":
                    if (!TryParseFiniteFloat(value, out var fontSizeMultiplier) || fontSizeMultiplier <= 0)
                    {
                        throw InvalidProperty(property, value);
                    }

                    style.FontSizeMultiplier = fontSizeMultiplier;
                    break;
                case "--milestro-ruby-gap":
                    if (!TryParseFiniteFloat(value, out var gap) || gap < 0)
                    {
                        throw InvalidProperty(property, value);
                    }

                    style.Gap = gap;
                    break;
                default:
                    throw new InvalidCastException($"unsupported Ruby CSS property: {property}");
            }
        }

        private static InvalidCastException InvalidProperty(string property, string value)
        {
            return new InvalidCastException($"invalid Ruby CSS value for {property}: {value}");
        }

        private static bool TryParseFiniteFloat(string value, out float result)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) &&
                   !float.IsNaN(result) && !float.IsInfinity(result);
        }
    }
}
