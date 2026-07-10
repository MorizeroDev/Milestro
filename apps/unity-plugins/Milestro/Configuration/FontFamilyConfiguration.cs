using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Milestro.Skia;

namespace Milestro.Configuration
{
    public class FontFamilyConfiguration
    {
        private readonly Dictionary<string, List<FontFamilyToken>> keywordMappings =
            new Dictionary<string, List<FontFamilyToken>>(System.StringComparer.OrdinalIgnoreCase);

        public void SetKeywordMapping(string keyword, params string[] families)
        {
            SetKeywordMapping(keyword, (IEnumerable<string>)families);
        }

        public void SetKeywordMapping(string keyword, IEnumerable<string> families)
        {
            var normalizedKeyword = FontFamilyParser.NormalizeKeyword(keyword);
            if (normalizedKeyword.Length == 0)
            {
                return;
            }

            keywordMappings[normalizedKeyword] = NormalizeFamilies(families);
        }

        public void SetKeywordMappings<TFamilies>(
            IEnumerable<KeyValuePair<string, TFamilies>> mappings)
            where TFamilies : IEnumerable<string>
        {
            keywordMappings.Clear();
            if (mappings == null)
            {
                return;
            }

            foreach (var mapping in mappings)
            {
                SetKeywordMapping(mapping.Key, mapping.Value);
            }
        }

        public void ResetKeywordMapping(string keyword)
        {
            keywordMappings.Remove(FontFamilyParser.NormalizeKeyword(keyword));
        }

        public void ResetKeywordMappings()
        {
            keywordMappings.Clear();
        }

        public bool TryGetKeywordMapping(
            string keyword,
            [NotNullWhen(true)] out IReadOnlyList<FontFamilyToken>? families
        )
        {
            if (keywordMappings.TryGetValue(FontFamilyParser.NormalizeKeyword(keyword), out var configuredFamilies))
            {
                families = configuredFamilies;
                return true;
            }

            families = null;
            return false;
        }

        internal IEnumerable<KeyValuePair<string, IReadOnlyList<FontFamilyToken>>> GetKeywordMappings()
        {
            foreach (var mapping in keywordMappings)
            {
                yield return new KeyValuePair<string, IReadOnlyList<FontFamilyToken>>(mapping.Key, mapping.Value);
            }
        }

        private static List<FontFamilyToken> NormalizeFamilies(IEnumerable<string> families)
        {
            var normalizedFamilies = new List<FontFamilyToken>();
            if (families == null)
            {
                return normalizedFamilies;
            }

            foreach (var family in families)
            {
                if (FontFamilyParser.TryParseConfiguredFamilyToken(family, out var token))
                {
                    normalizedFamilies.Add(token);
                }
            }

            return normalizedFamilies;
        }
    }
}
