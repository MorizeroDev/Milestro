using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Milestro.Binding;
using Milestro.Model;
using Paraparty.UnityNative.Base;
using UnityEngine;

namespace Milestro.Skia.TextLayout
{
    public class Paragraph : DisposableNativeObject
    {
        internal Paragraph(IntPtr ptr)
            : base(ptr)
        {
        }

        internal IntPtr DetachNativePtr()
        {
            var nativePtr = NativePtr;
            ptr = IntPtr.Zero;
            return nativePtr;
        }

        protected override void DisposeUnmanaged()
        {
            if (ptr != IntPtr.Zero)
            {
                var nativePtr = ptr;
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutParagraphDestroy(out nativePtr));
                ptr = nativePtr;
            }

            base.DisposeUnmanaged();
        }

        public void Layout(float width)
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutParagraphLayout(NativePtr, width));
        }

        public void LayoutWithInlineExtent(float inlineExtent)
        {
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutParagraphLayoutWithInlineExtent(NativePtr, inlineExtent));
        }

        public WritingMode WritingMode
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphGetWritingMode(NativePtr, out var ret));
                return (WritingMode)ret;
            }
        }

        public bool IsVertical => WritingMode == WritingMode.VerticalRl;

        public ParagraphLayoutMetrics LayoutMetrics
        {
            get
            {
                ThrowIfDisposed();
                if (!IsVertical)
                {
                    throw new NotSupportedException(
                        "Paragraph.LayoutMetrics is only supported for vertical-rl. " +
                        "Use Height, LongestLine, and the existing horizontal metrics.");
                }

                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphGetLayoutMetrics(NativePtr,
                        out var physicalWidth,
                        out var physicalHeight,
                        out var contentLeft,
                        out var contentTop,
                        out var contentRight,
                        out var contentBottom,
                        out var inkLeft,
                        out var inkTop,
                        out var inkRight,
                        out var inkBottom,
                        out var longestInlineExtent,
                        out var minIntrinsicInlineExtent,
                        out var maxIntrinsicInlineExtent,
                        out var columnCount));

                if (columnCount > int.MaxValue)
                {
                    throw new InvalidOperationException("Native paragraph returned too many columns.");
                }

                var columns = new ParagraphColumnMetrics[(int)columnCount];
                for (var index = 0; index < columns.Length; ++index)
                {
                    ExitCodeUtil.ThrowIfFailed(
                        BindingC.SkiaTextlayoutParagraphGetColumnMetrics(NativePtr,
                            (ulong)index,
                            out var lineNumber,
                            out var hardBreak,
                            out var left,
                            out var top,
                            out var right,
                            out var bottom,
                            out var inlineExtent,
                            out var blockThickness,
                            out var physicalBaseline,
                            out var utf16Start,
                            out var utf16End,
                            out var utf16EndExcludingWhitespace,
                            out var utf16EndIncludingNewline));
                    columns[index] = new ParagraphColumnMetrics(lineNumber,
                        hardBreak != 0,
                        Rect.MinMaxRect(left, top, right, bottom),
                        inlineExtent,
                        blockThickness,
                        physicalBaseline,
                        utf16Start,
                        utf16End,
                        utf16EndExcludingWhitespace,
                        utf16EndIncludingNewline);
                }

                return new ParagraphLayoutMetrics(WritingMode,
                    new Vector2(physicalWidth, physicalHeight),
                    Rect.MinMaxRect(contentLeft, contentTop, contentRight, contentBottom),
                    Rect.MinMaxRect(inkLeft, inkTop, inkRight, inkBottom),
                    longestInlineExtent,
                    minIntrinsicInlineExtent,
                    maxIntrinsicInlineExtent,
                    columns);
            }
        }

        public float Height
        {
            get
            {
                ThrowIfDisposed();
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutParagraphGetHeight(NativePtr, out var ret));
                return ret;
            }
        }

        public float LongestLine
        {
            get
            {
                ThrowIfDisposed();
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutParagraphGetLongestLine(NativePtr, out var ret));
                return ret;
            }
        }

        public float MinIntrinsicWidth
        {
            get
            {
                ThrowIfVerticalLegacyEntryPoint("MinIntrinsicWidth");
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphGetMinIntrinsicWidth(NativePtr, out var ret));
                return ret;
            }
        }

        public float MaxIntrinsicWidth
        {
            get
            {
                ThrowIfVerticalLegacyEntryPoint("MaxIntrinsicWidth");
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphGetMaxIntrinsicWidth(NativePtr, out var ret));
                return ret;
            }
        }

        public unsafe float ResolveNoWrapContentWidth(string text)
        {
            ThrowIfVerticalLegacyEntryPoint("ResolveNoWrapContentWidth");
            var payload = Encoding.UTF8.GetBytes(text ?? "");
            fixed (byte* textPtr = payload)
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphResolveNoWrapContentWidth(NativePtr,
                        textPtr,
                        (ulong)payload.Length,
                        out var ret));
                return ret;
            }
        }

        public static float ResolveNoWrapProbeLayoutWidth(string text, float fontSize, float viewportWidth)
        {
            var textSize = Encoding.UTF8.GetByteCount(text ?? "");
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutResolveNoWrapProbeLayoutWidth((ulong)textSize,
                    fontSize,
                    viewportWidth,
                    out var ret));
            return ret;
        }

        public static float ResolveNoWrapLayoutWidth(float viewportWidth, float contentWidth)
        {
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutResolveNoWrapLayoutWidth(viewportWidth, contentWidth, out var ret));
            return ret;
        }

        private void ThrowIfVerticalLegacyEntryPoint(string operation)
        {
            ThrowIfDisposed();
            if (IsVertical)
            {
                throw new NotSupportedException(
                    $"Paragraph.{operation} is not supported for vertical-rl. Use Layout(), LayoutWithInlineExtent(), and LayoutMetrics.");
            }
        }

        internal bool HitTestRange(int startUtf16, int endUtf16, Vector2 position)
        {
            if (startUtf16 < 0 || endUtf16 <= startUtf16)
            {
                return false;
            }

            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutParagraphHitTestRange(NativePtr,
                    (ulong)startUtf16,
                    (ulong)endUtf16,
                    position.x,
                    position.y,
                    out var hit));
            return hit != 0;
        }

        internal List<Rect> GetRectsForRange(int startUtf16, int endUtf16)
        {
            var rects = new List<Rect>();
            if (startUtf16 < 0 || endUtf16 <= startUtf16)
            {
                return rects;
            }

            ThrowIfDisposed();
            MilestroCTypes.SkiaTextlayoutParagraphRectCallback callback = (_, left, top, right, bottom) =>
            {
                rects.Add(new Rect(left, top, right - left, bottom - top));
                return 0;
            };
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutParagraphForEachRectInRange(NativePtr,
                (ulong)startUtf16,
                (ulong)endUtf16,
                IntPtr.Zero,
                callback));
            return rects;
        }

        public IReadOnlyList<Rect> GetRectsForPlaceholders()
        {
            ThrowIfDisposed();
            var rects = new List<Rect>();
            MilestroCTypes.SkiaTextlayoutParagraphRectCallback callback = (_, left, top, right, bottom) =>
            {
                rects.Add(Rect.MinMaxRect(left, top, right, bottom));
                return 0;
            };
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutParagraphGetRectsForPlaceholders(
                NativePtr, IntPtr.Zero, callback));
            return rects.AsReadOnly();
        }

        public void Paint(Canvas canvas, Vector2 position)
        {
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutParagraphPaint(NativePtr, canvas.NativePtr, position.x, position.y)
            );
        }

        public void SplitGlypl(IntPtr context, Vector2 position,
            MilestroCTypes.SkiaTextlayoutParagraphSplitGlyphCallback callback)
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutParagraphSplitGlyph(NativePtr,
                context,
                position.x, position.y, callback
            ));
        }

        public unsafe void ToSDF(int sdfWidth,
            int sdfHeight,
            float sdfScale,
            float x, float y,
            void* distanceField)
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutParagraphToSDF(NativePtr,
                sdfWidth, sdfHeight, sdfScale,
                x, y,
                distanceField
            ));
        }

        public Path ToPath(float x, float y)
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutParagraphToPath(NativePtr, out var pathPtr, x, y));
            return new Path(pathPtr);
        }
    }
}
