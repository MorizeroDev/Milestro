using System;
using System.Collections.Generic;
using Milestro.Util;

namespace Milestro.Components.Internal
{
    internal readonly struct TextInputAccessibilitySnapshot
    {
        internal TextInputAccessibilitySnapshot(string text, int caretUtf16,
            int selectionStartUtf16, int selectionEndUtf16, bool editing, bool composing)
        {
            Text = text ?? string.Empty;
            CaretUtf16 = caretUtf16;
            SelectionStartUtf16 = selectionStartUtf16;
            SelectionEndUtf16 = selectionEndUtf16;
            Editing = editing;
            Composing = composing;
        }

        internal string Text { get; }
        internal int CaretUtf16 { get; }
        internal int SelectionStartUtf16 { get; }
        internal int SelectionEndUtf16 { get; }
        internal bool Editing { get; }
        internal bool Composing { get; }
        internal bool HasSelection => SelectionEndUtf16 > SelectionStartUtf16;
    }

    internal readonly struct TextInputAccessibilityAnnouncementStrings
    {
        internal TextInputAccessibilityAnnouncementStrings(
            string editingStarted,
            string editingEnded,
            string insertedCountFormat,
            string deletedCountFormat,
            string selectedCountFormat,
            string passwordCharacter,
            string blank,
            int textEchoLimit,
            bool masked)
        {
            EditingStarted = editingStarted;
            EditingEnded = editingEnded;
            InsertedCountFormat = insertedCountFormat;
            DeletedCountFormat = deletedCountFormat;
            SelectedCountFormat = selectedCountFormat;
            PasswordCharacter = passwordCharacter;
            Blank = blank;
            TextEchoLimit = Math.Max(1, textEchoLimit);
            Masked = masked;
        }

        internal string EditingStarted { get; }
        internal string EditingEnded { get; }
        internal string InsertedCountFormat { get; }
        internal string DeletedCountFormat { get; }
        internal string SelectedCountFormat { get; }
        internal string PasswordCharacter { get; }
        internal string Blank { get; }
        internal int TextEchoLimit { get; }
        internal bool Masked { get; }
    }

    internal static class TextInputAccessibilityAnnouncements
    {
        internal static void AppendDiff(
            in TextInputAccessibilitySnapshot previous,
            in TextInputAccessibilitySnapshot current,
            in TextInputAccessibilityAnnouncementStrings strings,
            List<string> output)
        {
            if (!previous.Editing && current.Editing)
            {
                AddIfNotEmpty(output, strings.EditingStarted);
                return;
            }

            if (previous.Editing && !current.Editing)
            {
                AddIfNotEmpty(output, strings.EditingEnded);
                return;
            }

            if (!current.Editing || current.Composing)
            {
                return;
            }

            if (!string.Equals(previous.Text, current.Text, StringComparison.Ordinal))
            {
                AppendTextChange(previous.Text, current.Text, in strings, output);
                return;
            }

            if (current.HasSelection)
            {
                if (!previous.HasSelection ||
                    previous.SelectionStartUtf16 != current.SelectionStartUtf16 ||
                    previous.SelectionEndUtf16 != current.SelectionEndUtf16)
                {
                    AppendSelection(in current, in strings, output);
                }

                return;
            }

            if (previous.HasSelection || current.CaretUtf16 == previous.CaretUtf16)
            {
                return;
            }

            AppendCaretMove(in previous, in current, in strings, output);
        }

        private static void AppendTextChange(string previousText, string currentText,
            in TextInputAccessibilityAnnouncementStrings strings, List<string> output)
        {
            Diff(previousText, currentText, out var removed, out var inserted);
            if (inserted.Length == 0 && removed.Length == 0)
            {
                return;
            }

            if (strings.Masked)
            {
                AddIfNotEmpty(output, strings.PasswordCharacter);
                return;
            }

            output.Add(inserted.Length > 0
                ? EchoOrCount(inserted, strings.InsertedCountFormat, strings.TextEchoLimit)
                : EchoOrCount(removed, strings.DeletedCountFormat, strings.TextEchoLimit));
        }

        private static void AppendSelection(in TextInputAccessibilitySnapshot current,
            in TextInputAccessibilityAnnouncementStrings strings, List<string> output)
        {
            var start = Math.Min(current.SelectionStartUtf16, current.Text.Length);
            var end = Math.Min(current.SelectionEndUtf16, current.Text.Length);
            var utf16Length = end - start;
            if (utf16Length <= 0)
            {
                return;
            }

            var selectedText = current.Text.Substring(start, utf16Length);
            var characterCount = Utf16Util.CountGraphemeClusters(selectedText);
            if (strings.Masked || characterCount > strings.TextEchoLimit)
            {
                output.Add(string.Format(strings.SelectedCountFormat, characterCount));
                return;
            }

            output.Add(selectedText);
        }

        private static void AppendCaretMove(in TextInputAccessibilitySnapshot previous,
            in TextInputAccessibilitySnapshot current,
            in TextInputAccessibilityAnnouncementStrings strings, List<string> output)
        {
            var delta = current.CaretUtf16 - previous.CaretUtf16;
            if (strings.Masked)
            {
                if (delta == 1 || delta == -1)
                {
                    AddIfNotEmpty(output, strings.PasswordCharacter);
                }

                return;
            }

            var crossed = Math.Abs(delta);
            if (crossed <= 2)
            {
                var index = delta > 0 ? current.CaretUtf16 - 1 : current.CaretUtf16;
                var scalar = ReadScalar(current.Text, index);
                if (scalar != null && scalar.Length == crossed)
                {
                    AddIfNotEmpty(output, scalar);
                    return;
                }
            }

            if (LineIndex(current.Text, previous.CaretUtf16) != LineIndex(current.Text, current.CaretUtf16))
            {
                AddIfNotEmpty(output, LineAt(current.Text, current.CaretUtf16) ?? strings.Blank);
                return;
            }

            AddIfNotEmpty(output, WordAt(current.Text, current.CaretUtf16) ?? strings.Blank);
        }

        private static void Diff(string previousText, string currentText,
            out string removed, out string inserted)
        {
            var prefix = 0;
            var maxPrefix = Math.Min(previousText.Length, currentText.Length);
            while (prefix < maxPrefix && previousText[prefix] == currentText[prefix])
            {
                ++prefix;
            }

            var suffix = 0;
            while (suffix < previousText.Length - prefix &&
                   suffix < currentText.Length - prefix &&
                   previousText[previousText.Length - 1 - suffix] == currentText[currentText.Length - 1 - suffix])
            {
                ++suffix;
            }

            removed = previousText.Substring(prefix, previousText.Length - prefix - suffix);
            inserted = currentText.Substring(prefix, currentText.Length - prefix - suffix);
        }

        private static string EchoOrCount(string text, string countFormat, int limit)
        {
            var characterCount = Utf16Util.CountGraphemeClusters(text);
            return characterCount <= limit ? text : string.Format(countFormat, characterCount);
        }

        private static string? ReadScalar(string text, int index)
        {
            if (index < 0 || index >= text.Length)
            {
                return null;
            }

            if (char.IsHighSurrogate(text[index]) &&
                index + 1 < text.Length &&
                char.IsLowSurrogate(text[index + 1]))
            {
                return text.Substring(index, 2);
            }

            if (char.IsLowSurrogate(text[index]) &&
                index - 1 >= 0 &&
                char.IsHighSurrogate(text[index - 1]))
            {
                return text.Substring(index - 1, 2);
            }

            return text.Substring(index, 1);
        }

        private static int LineIndex(string text, int caretUtf16)
        {
            var limit = Math.Min(Math.Max(caretUtf16, 0), text.Length);
            var line = 0;
            for (var index = 0; index < limit; ++index)
            {
                if (text[index] == '\n')
                {
                    ++line;
                }
            }

            return line;
        }

        private static string? LineAt(string text, int caretUtf16)
        {
            var caret = Math.Min(Math.Max(caretUtf16, 0), text.Length);
            var start = caret;
            while (start > 0 && text[start - 1] != '\n')
            {
                --start;
            }

            var end = caret;
            while (end < text.Length && text[end] != '\n')
            {
                ++end;
            }

            var length = end - start;
            if (length == 0)
            {
                return null;
            }

            var line = text.Substring(start, length).TrimEnd('\r');
            return line.Length == 0 ? null : line;
        }

        private static string? WordAt(string text, int caretUtf16)
        {
            var length = text.Length;
            if (length == 0)
            {
                return null;
            }

            var index = Math.Min(Math.Max(caretUtf16, 0), length);
            if (index >= length || (index > 0 && !IsWordChar(text[index])))
            {
                --index;
            }

            if (index < 0 || index >= length || !IsWordChar(text[index]))
            {
                return null;
            }

            var start = index;
            while (start > 0 && IsWordChar(text[start - 1]))
            {
                --start;
            }

            var end = index;
            while (end + 1 < length && IsWordChar(text[end + 1]))
            {
                ++end;
            }

            return text.Substring(start, end - start + 1);
        }

        private static bool IsWordChar(char value)
        {
            return char.IsLetterOrDigit(value) || value == '_';
        }

        private static void AddIfNotEmpty(List<string> output, string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                output.Add(value!);
            }
        }
    }
}
