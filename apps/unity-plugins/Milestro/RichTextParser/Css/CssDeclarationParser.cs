#nullable enable
using System;

namespace Milestro.RichTextParser.Css
{
    internal struct CssDeclarationParser
    {
        private readonly string source;
        private CssLexer lexer;

        internal CssDeclarationParser(string source)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            lexer = new CssLexer(source);
        }

        internal bool TryRead(out CssDeclaration declaration)
        {
            while (true)
            {
                var token = lexer.Next();
                while (token.Type == CssTokenType.Whitespace || token.Type == CssTokenType.Semicolon)
                {
                    token = lexer.Next();
                }

                if (token.Type == CssTokenType.End)
                {
                    declaration = default;
                    return false;
                }

                var start = token.Start;
                var end = source.Length;
                var separatorStart = -1;
                var separatorEnd = -1;
                var parenthesisDepth = 0;
                var invalid = false;
                while (token.Type != CssTokenType.End)
                {
                    if (token.Type == CssTokenType.Semicolon && parenthesisDepth == 0)
                    {
                        end = token.Start;
                        break;
                    }

                    if (token.Type == CssTokenType.Invalid)
                    {
                        invalid = true;
                    }
                    else if (token.Type == CssTokenType.LeftParenthesis)
                    {
                        parenthesisDepth++;
                    }
                    else if (token.Type == CssTokenType.RightParenthesis)
                    {
                        if (parenthesisDepth == 0) invalid = true;
                        else parenthesisDepth--;
                    }
                    else if (token.Type == CssTokenType.Colon && parenthesisDepth == 0 && separatorStart < 0)
                    {
                        separatorStart = token.Start;
                        separatorEnd = token.End;
                    }

                    token = lexer.Next();
                }

                var raw = new CssSlice(source, start, end - start).Trim();
                if (raw.Length == 0) continue;
                if (invalid || parenthesisDepth != 0 || separatorStart < 0)
                {
                    declaration = new CssDeclaration(raw);
                    return true;
                }

                var property = new CssSlice(source, raw.Start, separatorStart - raw.Start).Trim();
                var value = new CssSlice(source, separatorEnd, end - separatorEnd).Trim();
                if (property.Length == 0 || value.Length == 0)
                {
                    declaration = new CssDeclaration(raw);
                    return true;
                }

                declaration = new CssDeclaration(raw, property, value);
                return true;
            }
        }
    }
}
