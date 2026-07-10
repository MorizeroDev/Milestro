using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Milestro.Skia
{
    internal static class FontFamilyFormatter
    {
        internal static string Format(IEnumerable<FontFamilyToken> familyTokens)
        {
            var families = new List<string>();
            if (familyTokens != null)
            {
                foreach (var token in familyTokens)
                {
                    families.Add(token.Kind == FontFamilyTokenKind.Exact
                        ? QuoteJsonString(token.Value)
                        : token.Value);
                }
            }

            return string.Join(", ", families);
        }

        private static string QuoteJsonString(string value)
        {
            var sb = new StringBuilder();
            sb.Append('"');
            foreach (var c in value ?? "")
            {
                switch (c)
                {
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        if (char.IsControl(c))
                        {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }

                        break;
                }
            }

            sb.Append('"');
            return sb.ToString();
        }
    }
}
