using System;
using Milestro.Model;
using Milestro.Unicode;

namespace Milestro.RichTextParser
{
    /// <summary>
    /// Incrementally builds a <see cref="ParagraphPayload"/>. Tracks UTF-16 offsets, block
    /// boundaries, and link ordering so <see cref="IRichTextParser"/> implementations only
    /// describe content. Create one per parse.
    /// </summary>
    public sealed class ParagraphPayloadBuilder
    {
        private readonly ParagraphPayload payload = new ParagraphPayload();
        private readonly string locale;
        private ParagraphBlock? currentBlock;
        private int textLengthUtf16;
        private int noWrapStartUtf16 = -1;
        private int noWrapEndUtf16 = -1;
        private int linkStartUtf16 = -1;
        private string? linkHref;
        private string? linkId;

        public ParagraphPayloadBuilder(string locale = "en")
        {
            this.locale = locale;
        }

        /// <summary>
        /// Starts a new block-level paragraph. Content appended afterwards belongs to the
        /// new block. Blocks stack with no implicit separator.
        /// </summary>
        public ParagraphPayloadBuilder BeginBlock()
        {
            CloseNoWrap();
            currentBlock = payload.AddBlock(true, textLengthUtf16);
            return this;
        }

        public ParagraphPayloadBuilder BeginBlock(ParagraphStyleState style, TextStyleState textStyle)
        {
            if (style == null) throw new ArgumentNullException(nameof(style));
            if (textStyle == null) throw new ArgumentNullException(nameof(textStyle));
            BeginBlock();
            var target = currentBlock!.Style;
            target.TextAlign = style.TextAlign;
            target.TextDirection = style.TextDirection;
            target.TextIndent = style.TextIndent;
            target.TextIndentEm = style.TextIndentEm;
            target.TextIndentFontSize = textStyle.FontSize;
            target.TextIndentFontSizeScale = textStyle.FontSizeScale ?? 1f;
            target.Width = style.Width;
            target.MarginTop = style.MarginTop;
            target.MarginRight = style.MarginRight;
            target.MarginBottom = style.MarginBottom;
            target.MarginLeft = style.MarginLeft;
            return this;
        }

        public ParagraphPayloadBuilder EndBlock()
        {
            CloseNoWrap();
            currentBlock = null;
            return this;
        }

        /// <summary>
        /// Appends a text run. A null <paramref name="style"/> inherits every style from the
        /// component. A "\n" inside <paramref name="text"/> breaks the line within the
        /// current block.
        /// </summary>
        public ParagraphPayloadBuilder AppendText(string text, TextStyleState? style = null)
        {
            if (string.IsNullOrEmpty(text))
            {
                return this;
            }

            var startUtf16 = textLengthUtf16;
            var block = EnsureBlock();
            AppendTransformedText(block, text, style);
            TrackNoWrap(style, startUtf16);
            return this;
        }

        /// <summary>
        /// Reserves an inline box of the given size at the current position. The application
        /// paints the box content itself; see <see cref="PlaceholderAnnotation"/>.
        /// </summary>
        public ParagraphPayloadBuilder AppendPlaceholder(float width, float height, string key,
            TextStyleState? style = null)
        {
            if (!float.IsFinite(width) || !float.IsFinite(height) || width <= 0f || height <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(width),
                    "Placeholder size must be positive and finite.");
            }

            var block = EnsureBlock();
            var startUtf16 = textLengthUtf16;
            payload.AddPlaceholder(new PlaceholderAnnotation(textLengthUtf16, width, height,
                key ?? "", style ?? new TextStyleState()));
            ++block.LengthUtf16;
            ++textLengthUtf16;
            TrackNoWrap(style, startUtf16);
            return this;
        }

        /// <summary>
        /// Starts a clickable range; everything appended until <see cref="EndLink"/> belongs
        /// to the link. Links cannot be nested.
        /// </summary>
        public ParagraphPayloadBuilder BeginLink(string href, string id = "")
        {
            if (string.IsNullOrEmpty(href))
            {
                throw new ArgumentException("Link href must be a non-empty string.", nameof(href));
            }

            if (linkHref != null)
            {
                throw new InvalidOperationException("Links cannot be nested.");
            }

            linkHref = href;
            linkId = id ?? "";
            linkStartUtf16 = textLengthUtf16;
            return this;
        }

        public ParagraphPayloadBuilder EndLink()
        {
            if (linkHref == null)
            {
                throw new InvalidOperationException("EndLink without a matching BeginLink.");
            }

            payload.AddLink(new LinkAnnotation(linkHref, linkId ?? "", linkStartUtf16,
                textLengthUtf16, payload.Links.Count));
            linkHref = null;
            linkId = null;
            linkStartUtf16 = -1;
            return this;
        }

        /// <summary>
        /// Returns the built payload. A link left open is closed at the current position.
        /// </summary>
        public ParagraphPayload Build()
        {
            CloseNoWrap();
            if (linkHref != null)
            {
                EndLink();
            }

            return payload;
        }

        private ParagraphBlock EnsureBlock()
        {
            if (currentBlock == null)
            {
                currentBlock = payload.AddBlock(false, textLengthUtf16);
            }

            return currentBlock;
        }

        private void AppendTransformedText(ParagraphBlock block, string text, TextStyleState? style)
        {
            if (style?.TextTransform == TextTransform.SmallCaps)
            {
                var runStart = 0;
                while (runStart < text.Length)
                {
                    var isLowercase = char.IsLower(text[runStart]);
                    var runEnd = runStart + 1;
                    while (runEnd < text.Length && char.IsLower(text[runEnd]) == isLowercase)
                    {
                        runEnd++;
                    }

                    var runText = text.Substring(runStart, runEnd - runStart);
                    var runStyle = style;
                    if (isLowercase)
                    {
                        runText = runText.IcuToUpper(style.Locale ?? locale);
                        runStyle = style.Clone();
                        runStyle.IsSmallCapsLowercase = true;
                    }

                    AppendTextSegment(block, runStyle, runText);
                    runStart = runEnd;
                }
                return;
            }

            if (style?.TextTransform == TextTransform.Lowercase)
            {
                text = text.IcuToLower(style.Locale ?? locale);
            }
            else if (style?.TextTransform == TextTransform.Uppercase)
            {
                text = text.IcuToUpper(style.Locale ?? locale);
            }

            AppendTextSegment(block, style, text);
        }

        private void AppendTextSegment(ParagraphBlock block, TextStyleState? style, string text)
        {
            block.Segments.Add(style == null
                ? TextSegment.MakeText(text)
                : TextSegment.MakeText(style, text));
            block.LengthUtf16 += text.Length;
            textLengthUtf16 += text.Length;
        }

        private void TrackNoWrap(TextStyleState? style, int startUtf16)
        {
            if (style?.NoWrap != true)
            {
                CloseNoWrap();
                return;
            }

            if (noWrapStartUtf16 < 0)
            {
                noWrapStartUtf16 = startUtf16;
            }
            noWrapEndUtf16 = textLengthUtf16;
        }

        private void CloseNoWrap()
        {
            if (noWrapStartUtf16 >= 0 && noWrapEndUtf16 > noWrapStartUtf16)
            {
                payload.AddNoWrap(new NoWrapAnnotation(noWrapStartUtf16, noWrapEndUtf16));
            }
            noWrapStartUtf16 = -1;
            noWrapEndUtf16 = -1;
        }
    }
}
