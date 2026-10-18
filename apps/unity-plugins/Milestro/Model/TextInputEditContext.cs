namespace Milestro.Model
{
    public readonly struct TextInputEditContext
    {
        internal TextInputEditContext(string currentText, string proposedText, string insertedText,
            int selectionStart, int selectionEnd, TextInputEditSource source)
        {
            CurrentText = currentText ?? string.Empty;
            ProposedText = proposedText ?? string.Empty;
            InsertedText = insertedText ?? string.Empty;
            SelectionStart = selectionStart;
            SelectionEnd = selectionEnd;
            Source = source;
        }

        public string CurrentText { get; }
        public string ProposedText { get; }
        public string InsertedText { get; }
        public int SelectionStart { get; }
        public int SelectionEnd { get; }
        public TextInputEditSource Source { get; }
        public bool HasSelection => SelectionStart != SelectionEnd;
    }
}
