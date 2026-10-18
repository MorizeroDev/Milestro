#nullable enable
using System;

namespace Milestro.RichTextParser.Css
{
    internal struct CssLexer
    {
        private const char Backslash = (char)92;
        private readonly string source;
        private readonly int end;
        private int position;

        internal CssLexer(string source)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            position = 0;
            end = source.Length;
        }

        internal CssLexer(CssSlice slice)
        {
            source = slice.Source;
            position = slice.Start;
            end = slice.End;
        }

        internal CssToken Next()
        {
            if (position >= end) return new CssToken(CssTokenType.End, end, 0);
            var start = position;
            var character = source[position++];
            if (char.IsWhiteSpace(character))
            {
                while (position < end && char.IsWhiteSpace(source[position])) position++;
                return new CssToken(CssTokenType.Whitespace, start, position - start);
            }

            switch (character)
            {
                case ':': return new CssToken(CssTokenType.Colon, start, 1);
                case ';': return new CssToken(CssTokenType.Semicolon, start, 1);
                case ',': return new CssToken(CssTokenType.Comma, start, 1);
                case '(': return new CssToken(CssTokenType.LeftParenthesis, start, 1);
                case ')': return new CssToken(CssTokenType.RightParenthesis, start, 1);
            }

            if (character == '\'' || character == '"') return ReadString(start, character);
            if (IsNumberStart(character)) return ReadNumber(start, character);
            if (IsIdentifierStart(character)) return ReadIdentifier(start);
            return new CssToken(CssTokenType.Delimiter, start, 1);
        }

        private CssToken ReadString(int start, char quote)
        {
            while (position < end)
            {
                var character = source[position++];
                if (character == Backslash)
                {
                    if (position < end) position++;
                }
                else if (character == quote)
                {
                    return new CssToken(CssTokenType.String, start, position - start);
                }
            }

            return new CssToken(CssTokenType.Invalid, start, position - start);
        }

        private CssToken ReadNumber(int start, char character)
        {
            var hasFraction = character == '.';
            while (position < end && char.IsDigit(source[position])) position++;
            if (!hasFraction && position < end && source[position] == '.')
            {
                position++;
                while (position < end && char.IsDigit(source[position])) position++;
            }

            if (position < end && (source[position] == 'e' || source[position] == 'E'))
            {
                var exponentStart = position;
                var cursor = position + 1;
                if (cursor < end && (source[cursor] == '+' || source[cursor] == '-')) cursor++;
                var exponentDigits = cursor;
                while (cursor < end && char.IsDigit(source[cursor])) cursor++;
                position = cursor > exponentDigits ? cursor : exponentStart;
            }

            return new CssToken(CssTokenType.Number, start, position - start);
        }

        private CssToken ReadIdentifier(int start)
        {
            while (position < end && IsIdentifierCharacter(source[position])) position++;
            return new CssToken(CssTokenType.Identifier, start, position - start);
        }

        private bool IsNumberStart(char character)
        {
            if (char.IsDigit(character)) return true;
            if (character == '.') return position < end && char.IsDigit(source[position]);
            if ((character != '+' && character != '-') || position >= end) return false;
            if (char.IsDigit(source[position])) return true;
            return source[position] == '.' && position + 1 < end && char.IsDigit(source[position + 1]);
        }

        private static bool IsIdentifierStart(char character)
        {
            return char.IsLetter(character) || character == '_' || character == '-' || character == Backslash ||
                   character >= 128;
        }

        private static bool IsIdentifierCharacter(char character)
        {
            return char.IsLetterOrDigit(character) || character == '_' || character == '-' || character == Backslash ||
                   character >= 128;
        }
    }
}
