using System;
using System.Globalization;
using System.Text;
using Milestro.Model;
using Milestro.Util;

namespace Milestro.Components.Internal
{
    internal static class TextInputValidation
    {
        internal static string FilterInsertion(string text, int start, int end, string input,
            int characterLimit, TextInputCharacterValidation validation, TextInputValidateInput? custom,
            TextInputLineMode lineMode, bool digitsOnly = false)
        {
            var remaining = text.Remove(start, end - start);
            var count = Utf16Util.CountUnicodeScalars(remaining);
            var accepted = new StringBuilder(input.Length);
            for (var offset = 0; offset < input.Length;)
            {
                if (characterLimit > 0 && count >= characterLimit) break;
                var codePoint = char.ConvertToUtf32(input, offset);
                offset += codePoint > 0xffff ? 2 : 1;
                var position = start + accepted.Length;
                var context = validation != TextInputCharacterValidation.None || custom != null || digitsOnly
                    ? remaining.Insert(start, accepted.ToString())
                    : remaining;
                codePoint = Validate(context, position, codePoint, validation, digitsOnly);
                if (codePoint != 0 && custom != null) codePoint = custom(context, position, codePoint);
                if (codePoint <= 0 || codePoint > 0x10ffff || codePoint >= 0xd800 && codePoint <= 0xdfff)
                {
                    continue;
                }
                if (codePoint == '\r') codePoint = '\n';
                if (codePoint == '\n' && lineMode == TextInputLineMode.SingleLine) continue;
                accepted.Append(char.ConvertFromUtf32(codePoint));
                ++count;
            }
            return accepted.ToString();
        }

        private static int Validate(string text, int position, int codePoint,
            TextInputCharacterValidation validation, bool digitsOnly)
        {
            var digit = codePoint >= '0' && codePoint <= '9';
            if (digitsOnly) return digit ? codePoint : 0;
            switch (validation)
            {
                case TextInputCharacterValidation.Integer:
                case TextInputCharacterValidation.Decimal:
                    if (position == 0 && text.StartsWith("-", StringComparison.Ordinal)) return 0;
                    if (digit) return codePoint;
                    if (codePoint == '-' && position == 0 && text.IndexOf('-') < 0) return codePoint;
                    if (validation == TextInputCharacterValidation.Decimal && codePoint == '.' &&
                        text.IndexOf('.') < 0) return codePoint;
                    return 0;
                case TextInputCharacterValidation.Alphanumeric:
                    return digit || codePoint >= 'a' && codePoint <= 'z' || codePoint >= 'A' && codePoint <= 'Z'
                        ? codePoint : 0;
                case TextInputCharacterValidation.Name:
                    var scalar = char.ConvertFromUtf32(codePoint);
                    if (char.IsLetter(scalar, 0))
                    {
                        var wordStart = position == 0 || text[position - 1] == ' ' || text[position - 1] == '\'';
                        var cased = wordStart ? scalar.ToUpperInvariant() : scalar.ToLowerInvariant();
                        return char.ConvertToUtf32(cased, 0);
                    }
                    var category = CharUnicodeInfo.GetUnicodeCategory(scalar, 0);
                    if ((category == UnicodeCategory.NonSpacingMark || category == UnicodeCategory.SpacingCombiningMark) &&
                        position > 0 && text[position - 1] != ' ' && text[position - 1] != '\'') return codePoint;
                    if ((codePoint == ' ' || codePoint == '\'') && position > 0 &&
                        text[position - 1] != ' ' && text[position - 1] != '\'' &&
                        (position == text.Length || text[position] != ' ' && text[position] != '\'')) return codePoint;
                    return 0;
                case TextInputCharacterValidation.EmailAddress:
                    if (digit || codePoint >= 'a' && codePoint <= 'z' || codePoint >= 'A' && codePoint <= 'Z')
                        return codePoint;
                    if (codePoint == '@') return text.IndexOf('@') < 0 ? codePoint : 0;
                    if (codePoint == '.')
                        return (position == 0 || text[position - 1] != '.') &&
                               (position == text.Length || text[position] != '.') ? codePoint : 0;
                    return codePoint <= 0x7f && "!#$%&'*+-/=?^_`{|}~".IndexOf((char)codePoint) >= 0 ? codePoint : 0;
                default:
                    return codePoint;
            }
        }
    }
}
