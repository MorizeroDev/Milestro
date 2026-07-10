using System.Collections.Generic;
using System.Text;

namespace Milestro.Skia
{
    internal static class FontFamilyLexer
    {
        internal static List<string> Lex(string declaration)
        {
            var slices = new List<string>();
            var token = new StringBuilder();
            var inJsonString = false;
            var escaped = false;

            foreach (var c in declaration ?? "")
            {
                if (inJsonString)
                {
                    token.Append(c);
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (c == '"')
                    {
                        inJsonString = false;
                    }

                    continue;
                }

                if (c == '"' && IsWhiteSpaceOnly(token))
                {
                    token.Clear();
                    token.Append(c);
                    inJsonString = true;
                    continue;
                }

                if (c == ',')
                {
                    slices.Add(token.ToString());
                    token.Clear();
                    continue;
                }

                token.Append(c);
            }

            slices.Add(token.ToString());
            return slices;
        }

        private static bool IsWhiteSpaceOnly(StringBuilder value)
        {
            for (var i = 0; i < value.Length; i++)
            {
                if (!char.IsWhiteSpace(value[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
