using System.Text;

namespace Milestro.Util
{
    internal static class Utf16Util
    {
        internal static string RemoveUnpairedSurrogates(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(input.Length);
            var changed = false;
            for (var i = 0; i < input.Length; ++i)
            {
                var ch = input[i];
                if (char.IsHighSurrogate(ch))
                {
                    if (i + 1 < input.Length && char.IsLowSurrogate(input[i + 1]))
                    {
                        builder.Append(ch);
                        builder.Append(input[i + 1]);
                        ++i;
                        continue;
                    }

                    changed = true;
                    continue;
                }

                if (char.IsLowSurrogate(ch))
                {
                    changed = true;
                    continue;
                }

                builder.Append(ch);
            }

            return changed ? builder.ToString() : input;
        }
    }
}
