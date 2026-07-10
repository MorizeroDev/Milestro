using System.Collections.Generic;

namespace Milestro.Skia
{
    internal static class FontFamilyDeclaration
    {
        internal static List<FontFamilyToken> ToBareTokens(IEnumerable<string> familyNames)
        {
            var tokens = new List<FontFamilyToken>();
            if (familyNames == null)
            {
                return tokens;
            }

            foreach (var familyName in familyNames)
            {
                var token = FontFamilyToken.Bare(familyName);
                if (token.Value.Length > 0)
                {
                    tokens.Add(token);
                }
            }

            return tokens;
        }
    }
}
