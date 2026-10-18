namespace Milestro.Model
{
    public readonly struct TextInputEditResult
    {
        private TextInputEditResult(TextInputEditAction action, string? replacementText)
        {
            Action = action;
            ReplacementText = replacementText;
        }

        public TextInputEditAction Action { get; }
        public string? ReplacementText { get; }

        public static TextInputEditResult Accept()
        {
            return new TextInputEditResult(TextInputEditAction.Accept, null);
        }

        public static TextInputEditResult Reject()
        {
            return new TextInputEditResult(TextInputEditAction.Reject, null);
        }

        public static TextInputEditResult Replace(string replacementText)
        {
            return new TextInputEditResult(TextInputEditAction.Replace, replacementText ?? string.Empty);
        }
    }
}
