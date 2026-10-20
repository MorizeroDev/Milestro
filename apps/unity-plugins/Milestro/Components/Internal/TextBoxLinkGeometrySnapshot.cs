using System.Collections.Generic;
using Milestro.RichTextParser;
using Milestro.Skia.TextLayout;
using UnityEngine;

namespace Milestro.Components.Internal
{
    internal readonly struct TextBoxContentSnapshot
    {
        internal TextBoxContentSnapshot(long contentVersion,
            string plainText,
            IReadOnlyList<LinkAnnotation> links)
        {
            ContentVersion = contentVersion;
            PlainText = plainText;
            Links = links;
        }

        internal long ContentVersion { get; }
        internal string PlainText { get; }
        internal IReadOnlyList<LinkAnnotation> Links { get; }

        internal bool TryGetLink(int occurrence, out LinkAnnotation link)
        {
            foreach (var candidate in Links)
            {
                if (candidate.Occurrence == occurrence)
                {
                    link = candidate;
                    return true;
                }
            }

            link = default;
            return false;
        }

        internal string GetLinkLabel(LinkAnnotation link)
        {
            if (link.StartUtf16 < 0 ||
                link.EndUtf16 < link.StartUtf16 ||
                link.EndUtf16 > PlainText.Length)
            {
                return "";
            }

            return PlainText.Substring(link.StartUtf16, link.EndUtf16 - link.StartUtf16);
        }
    }

    internal readonly struct TextBoxLinkGeometry
    {
        internal TextBoxLinkGeometry(int occurrence,
            IReadOnlyList<Rect> rects,
            IReadOnlyList<TextBoxLinkBlockRange> blockRanges)
        {
            Occurrence = occurrence;
            Rects = rects;
            BlockRanges = blockRanges;
        }

        internal int Occurrence { get; }
        internal IReadOnlyList<Rect> Rects { get; }
        internal IReadOnlyList<TextBoxLinkBlockRange> BlockRanges { get; }
    }

    /// <summary>
    /// The portion of one link annotation that falls inside a single paragraph block,
    /// expressed in that block's local UTF-16 range.
    /// </summary>
    internal readonly struct TextBoxLinkBlockRange
    {
        internal TextBoxLinkBlockRange(int blockIndex,
            int startUtf16,
            int endUtf16,
            Vector2 frameOffset)
        {
            BlockIndex = blockIndex;
            StartUtf16 = startUtf16;
            EndUtf16 = endUtf16;
            FrameOffset = frameOffset;
        }

        internal int BlockIndex { get; }
        internal int StartUtf16 { get; }
        internal int EndUtf16 { get; }

        /// <summary>
        /// Offset in unscaled paragraph coordinates from the stacked-content origin to this
        /// block's paragraph origin.
        /// </summary>
        internal Vector2 FrameOffset { get; }
    }

    internal readonly struct TextBoxLinkGeometrySnapshot
    {
        internal TextBoxLinkGeometrySnapshot(long contentVersion,
            IReadOnlyList<Paragraph> paragraphs,
            IReadOnlyList<LinkAnnotation> links,
            IReadOnlyList<TextBoxLinkGeometry> geometry,
            Vector2 paintPosition,
            Rect clipRect,
            Vector2Int visibleOutputSize,
            float fitScale = 1f)
        {
            ContentVersion = contentVersion;
            Paragraphs = paragraphs;
            Links = links;
            Geometry = geometry;
            PaintPosition = paintPosition;
            ClipRect = clipRect;
            VisibleOutputSize = visibleOutputSize;
            FitScale = fitScale > 0f && float.IsFinite(fitScale) ? fitScale : 1f;
        }

        internal long ContentVersion { get; }
        internal IReadOnlyList<Paragraph> Paragraphs { get; }
        internal IReadOnlyList<LinkAnnotation> Links { get; }
        internal IReadOnlyList<TextBoxLinkGeometry> Geometry { get; }
        internal Vector2 PaintPosition { get; }
        internal Rect ClipRect { get; }
        internal Vector2Int VisibleOutputSize { get; }
        internal float FitScale { get; }

        internal bool TryGetGeometry(int occurrence, out TextBoxLinkGeometry geometry)
        {
            foreach (var candidate in Geometry)
            {
                if (candidate.Occurrence == occurrence)
                {
                    geometry = candidate;
                    return true;
                }
            }

            geometry = default;
            return false;
        }
    }
}
