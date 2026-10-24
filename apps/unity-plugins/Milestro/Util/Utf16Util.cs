using System.Globalization;
using System.Text;

namespace Milestro.Util
{
    internal static class Utf16Util
    {
        internal static int CountUnicodeScalars(string text)
        {
            var count = 0;
            for (var offset = 0; offset < text.Length; ++offset)
            {
                if (char.IsHighSurrogate(text[offset]) && offset + 1 < text.Length &&
                    char.IsLowSurrogate(text[offset + 1]))
                {
                    ++offset;
                }
                ++count;
            }
            return count;
        }

        internal static int CountGraphemeClusters(string text)
        {
            return string.IsNullOrEmpty(text) ? 0 : StringInfo.ParseCombiningCharacters(text).Length;
        }

        internal static int Utf8ToUtf16Offset(string text, ulong utf8Offset)
        {
            ulong byteOffset = 0;
            var offset = 0;
            while (offset < text.Length && byteOffset < utf8Offset)
            {
                var codePoint = char.ConvertToUtf32(text, offset);
                var byteCount = codePoint <= 0x7f ? 1 : codePoint <= 0x7ff ? 2 : codePoint <= 0xffff ? 3 : 4;
                if (byteOffset + (ulong)byteCount > utf8Offset) break;
                byteOffset += (ulong)byteCount;
                offset += codePoint > 0xffff ? 2 : 1;
            }
            return offset;
        }

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
