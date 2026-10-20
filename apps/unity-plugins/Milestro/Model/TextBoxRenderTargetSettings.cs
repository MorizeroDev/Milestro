namespace Milestro.Model
{
    internal readonly struct TextBoxRenderTargetSettings
    {
        public TextBoxRenderTargetSettings(string content,
            Margin margin,
            ParagraphStyleSettings paragraphStyle,
            TextStyleSettings textStyle,
            TextBoxWrapMode wrapMode,
            TextOverflow textOverflow,
            bool autoSize = false,
            float minSize = 0f,
            float maxSize = 0f,
            TextAutoSizeWrapMode autoSizeWrapMode = TextAutoSizeWrapMode.NoWrap)
        {
            Content = content ?? "";
            Margin = margin ?? Margin.FixedZero();
            Margin.Normalize();
            ParagraphStyle = paragraphStyle ?? new ParagraphStyleSettings();
            TextStyle = textStyle ?? new TextStyleSettings();
            WrapMode = wrapMode;
            TextOverflow = textOverflow;
            AutoSize = autoSize;
            MinSize = minSize;
            MaxSize = maxSize;
            AutoSizeWrapMode = autoSizeWrapMode;
        }

        public string Content { get; }
        public Margin Margin { get; }
        public ParagraphStyleSettings ParagraphStyle { get; }
        public TextStyleSettings TextStyle { get; }
        public TextBoxWrapMode WrapMode { get; }
        public TextOverflow TextOverflow { get; }
        public bool AutoSize { get; }
        public float MinSize { get; }
        public float MaxSize { get; }
        public TextAutoSizeWrapMode AutoSizeWrapMode { get; }
    }
}
