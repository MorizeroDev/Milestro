using System;
using System.Collections.Generic;
using Milestro.Model;
using UnityEngine;

namespace Milestro.Skia.TextLayout
{
    /// <summary>
    /// Managed value snapshot of a native paragraph layout. Values copied from this snapshot
    /// describe only the layout at the time it was created.
    /// </summary>
    public readonly struct ParagraphLayoutMetrics
    {
        public ParagraphLayoutMetrics(WritingMode writingMode,
            Vector2 physicalSize,
            Rect contentBounds,
            Rect inkBounds,
            float longestInlineExtent,
            float minIntrinsicInlineExtent,
            float maxIntrinsicInlineExtent,
            IReadOnlyList<ParagraphColumnMetrics> columns)
        {
            WritingMode = writingMode;
            PhysicalSize = physicalSize;
            ContentBounds = contentBounds;
            InkBounds = inkBounds;
            LongestInlineExtent = longestInlineExtent;
            MinIntrinsicInlineExtent = minIntrinsicInlineExtent;
            MaxIntrinsicInlineExtent = maxIntrinsicInlineExtent;
            Columns = columns ?? Array.Empty<ParagraphColumnMetrics>();
        }

        public WritingMode WritingMode { get; }
        public Vector2 PhysicalSize { get; }
        public Rect ContentBounds { get; }
        public Rect InkBounds { get; }
        public float LongestInlineExtent { get; }
        public float MinIntrinsicInlineExtent { get; }
        public float MaxIntrinsicInlineExtent { get; }
        public IReadOnlyList<ParagraphColumnMetrics> Columns { get; }
    }

    /// <summary>
    /// Physical column metrics for a vertical paragraph. Column zero is the rightmost column;
    /// source ranges are UTF-16 offsets into the paragraph text.
    /// </summary>
    public readonly struct ParagraphColumnMetrics
    {
        public ParagraphColumnMetrics(ulong lineNumber,
            bool hardBreak,
            Rect physicalBounds,
            float inlineExtent,
            float blockThickness,
            float physicalBaseline,
            ulong utf16Start,
            ulong utf16End,
            ulong utf16EndExcludingWhitespace,
            ulong utf16EndIncludingNewline)
        {
            LineNumber = lineNumber;
            HardBreak = hardBreak;
            PhysicalBounds = physicalBounds;
            InlineExtent = inlineExtent;
            BlockThickness = blockThickness;
            PhysicalBaseline = physicalBaseline;
            Utf16Start = utf16Start;
            Utf16End = utf16End;
            Utf16EndExcludingWhitespace = utf16EndExcludingWhitespace;
            Utf16EndIncludingNewline = utf16EndIncludingNewline;
        }

        public ulong LineNumber { get; }
        public bool HardBreak { get; }
        public Rect PhysicalBounds { get; }
        public float InlineExtent { get; }
        public float BlockThickness { get; }
        public float PhysicalBaseline { get; }
        public ulong Utf16Start { get; }
        public ulong Utf16End { get; }
        public ulong Utf16EndExcludingWhitespace { get; }
        public ulong Utf16EndIncludingNewline { get; }
    }
}
