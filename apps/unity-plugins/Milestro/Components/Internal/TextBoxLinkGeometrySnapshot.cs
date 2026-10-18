using System.Collections.Generic;
using Milestro.RichTextParser;
using Milestro.Skia.TextLayout;
using UnityEngine;

namespace Milestro.Components.Internal
{
    internal readonly struct TextBoxLinkGeometrySnapshot
    {
        internal TextBoxLinkGeometrySnapshot(Paragraph paragraph,
            IReadOnlyList<LinkAnnotation> links,
            Vector2 paintPosition,
            Rect clipRect,
            Vector2Int visibleOutputSize)
        {
            Paragraph = paragraph;
            Links = links;
            PaintPosition = paintPosition;
            ClipRect = clipRect;
            VisibleOutputSize = visibleOutputSize;
        }

        internal Paragraph Paragraph { get; }
        internal IReadOnlyList<LinkAnnotation> Links { get; }
        internal Vector2 PaintPosition { get; }
        internal Rect ClipRect { get; }
        internal Vector2Int VisibleOutputSize { get; }
    }
}
