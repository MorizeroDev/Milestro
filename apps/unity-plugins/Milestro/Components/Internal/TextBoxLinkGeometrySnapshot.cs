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
        internal TextBoxLinkGeometry(int occurrence, IReadOnlyList<Rect> rects)
        {
            Occurrence = occurrence;
            Rects = rects;
        }

        internal int Occurrence { get; }
        internal IReadOnlyList<Rect> Rects { get; }
    }

    internal readonly struct TextBoxLinkGeometrySnapshot
    {
        internal TextBoxLinkGeometrySnapshot(long contentVersion,
            Paragraph paragraph,
            IReadOnlyList<LinkAnnotation> links,
            IReadOnlyList<TextBoxLinkGeometry> geometry,
            Vector2 paintPosition,
            Rect clipRect,
            Vector2Int visibleOutputSize)
        {
            ContentVersion = contentVersion;
            Paragraph = paragraph;
            Links = links;
            Geometry = geometry;
            PaintPosition = paintPosition;
            ClipRect = clipRect;
            VisibleOutputSize = visibleOutputSize;
        }

        internal long ContentVersion { get; }
        internal Paragraph Paragraph { get; }
        internal IReadOnlyList<LinkAnnotation> Links { get; }
        internal IReadOnlyList<TextBoxLinkGeometry> Geometry { get; }
        internal Vector2 PaintPosition { get; }
        internal Rect ClipRect { get; }
        internal Vector2Int VisibleOutputSize { get; }

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
