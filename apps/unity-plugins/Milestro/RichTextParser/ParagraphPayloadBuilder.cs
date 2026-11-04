using System;

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
        private ParagraphBlock? currentBlock;
        private int textLengthUtf16;
        private int linkStartUtf16 = -1;
        private string? linkHref;
        private string? linkId;

        /// <summary>
        /// Starts a new block-level paragraph. Content appended afterwards belongs to the
        /// new block. Blocks stack with no implicit separator.
        /// </summary>
        public ParagraphPayloadBuilder BeginBlock()
        {
            currentBlock = payload.AddBlock(true, textLengthUtf16);
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

            var block = EnsureBlock();
            block.Segments.Add(style == null
                ? TextSegment.MakeText(text)
                : TextSegment.MakeText(style, text));
            block.LengthUtf16 += text.Length;
            textLengthUtf16 += text.Length;
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
            payload.AddPlaceholder(new PlaceholderAnnotation(textLengthUtf16, width, height,
                key ?? "", style ?? new TextStyleState()));
            ++block.LengthUtf16;
            ++textLengthUtf16;
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
    }
}
