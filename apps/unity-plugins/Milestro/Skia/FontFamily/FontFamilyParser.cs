using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Milestro.Skia
{
    public static class FontFamilyParser
    {
        public static string NormalizeKeyword(string keyword)
        {
            return (keyword ?? "").Trim().ToLowerInvariant();
        }

        public static FontFamilyToken ParseFontFamilyToken(string familyName)
        {
            var tokenText = (familyName ?? "").Trim();
            return TryParseJsonStringToken(tokenText, out var exactName)
                ? FontFamilyToken.Exact(exactName)
                : FontFamilyToken.Bare(tokenText);
        }

        public static bool TryParseConfiguredFamilyToken(string value, out FontFamilyToken token)
        {
            token = ParseFontFamilyToken(value);
            return token.Value.Length > 0;
        }

        public static List<FontFamilyToken> ParseFontFamilyTokens(IEnumerable<string> familyNames)
        {
            var tokens = new List<FontFamilyToken>();
            if (familyNames == null)
            {
                return tokens;
            }

            foreach (var familyName in familyNames)
            {
                AddToken(tokens, ParseFontFamilyToken(familyName));
            }

            return tokens;
        }

        public static bool TryParseFontFamilyList(string value, out List<FontFamilyToken> fontFamilies)
        {
            fontFamilies = new List<FontFamilyToken>();
            foreach (var token in FontFamilyLexer.Lex(value))
            {
                AddToken(fontFamilies, ParseFontFamilyToken(token));
            }

            return fontFamilies.Count > 0;
        }

        public static List<string> ToSourceFamilyList(IEnumerable<FontFamilyToken> familyTokens)
        {
            var families = new List<string>();
            if (familyTokens == null)
            {
                return families;
            }

            foreach (var token in familyTokens)
            {
                families.Add(token.Value);
            }

            return families;
        }

        public static string FormatFontFamilyList(IEnumerable<FontFamilyToken> familyTokens)
        {
            return FontFamilyFormatter.Format(familyTokens);
        }

        private static void AddToken(List<FontFamilyToken> tokens, FontFamilyToken token)
        {
            if (token.Value.Length > 0)
            {
                tokens.Add(token);
            }
        }

        private static bool TryParseJsonStringToken(string value, out string parsedValue)
        {
            parsedValue = "";
            if (value == null || value.Length < 2 || value[0] != '"' || value[value.Length - 1] != '"')
            {
                return false;
            }

            var sb = new StringBuilder();
            for (var i = 1; i < value.Length - 1; i++)
            {
                var c = value[i];
                if (c != '\\')
                {
                    if (c == '"' || char.IsControl(c))
                    {
                        return false;
                    }

                    sb.Append(c);
                    continue;
                }

                if (i + 1 >= value.Length - 1)
                {
                    return false;
                }

                var escaped = value[++i];
                switch (escaped)
                {
                    case '"':
                    case '\\':
                    case '/':
                        sb.Append(escaped);
                        break;
                    case 'b':
                        sb.Append('\b');
                        break;
                    case 'f':
                        sb.Append('\f');
                        break;
                    case 'n':
                        sb.Append('\n');
                        break;
                    case 'r':
                        sb.Append('\r');
                        break;
                    case 't':
                        sb.Append('\t');
                        break;
                    case 'u':
                        if (i + 4 >= value.Length)
                        {
                            return false;
                        }

                        var hex = value.Substring(i + 1, 4);
                        if (!ushort.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var codeUnit))
                        {
                            return false;
                        }

                        sb.Append((char)codeUnit);
                        i += 4;
                        break;
                    default:
                        return false;
                }
            }

            parsedValue = sb.ToString();
            return true;
        }
    }
}
