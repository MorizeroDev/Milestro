#nullable enable
using System;

namespace Milestro.RichTextParser.Css
{
    internal readonly struct CssSlice
    {
        internal CssSlice(string source, int start, int length)
        {
            Source = source;
            Start = start;
            Length = length;
        }

        internal string Source { get; }
        internal int Start { get; }
        internal int Length { get; }
        internal int End => Start + Length;
        internal char this[int index] => Source[Start + index];

        internal CssSlice Slice(int start, int length)
        {
            return new CssSlice(Source, Start + start, length);
        }

        internal CssSlice Trim()
        {
            var start = Start;
            var end = End;
            while (start < end && char.IsWhiteSpace(Source[start])) start++;
            while (end > start && char.IsWhiteSpace(Source[end - 1])) end--;
            return new CssSlice(Source, start, end - start);
        }

        internal bool Equals(string value, StringComparison comparison)
        {
            if (value == null || Length != value.Length) return false;
            return Length == 0 || string.Compare(Source, Start, value, 0, Length, comparison) == 0;
        }

        internal bool StartsWith(string value, StringComparison comparison)
        {
            if (value == null || value.Length > Length) return false;
            return value.Length == 0 || string.Compare(Source, Start, value, 0, value.Length, comparison) == 0;
        }

        internal bool EndsWith(string value, StringComparison comparison)
        {
            if (value == null || value.Length > Length) return false;
            return value.Length == 0 ||
                   string.Compare(Source, End - value.Length, value, 0, value.Length, comparison) == 0;
        }

        internal int IndexOf(char value, int start)
        {
            for (var index = Start + start; index < End; index++)
            {
                if (Source[index] == value) return index - Start;
            }
            return -1;
        }

        public override string ToString()
        {
            return Length == 0 ? string.Empty : Source.Substring(Start, Length);
        }
    }
}
