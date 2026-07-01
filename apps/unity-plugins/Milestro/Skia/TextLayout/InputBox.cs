using System;
using System.Runtime.InteropServices;
using System.Text;
using Milestro.Binding;
using UnityEngine;

namespace Milestro.Skia.TextLayout
{
    public enum TextBoundarySnapMode
    {
        Previous = 0,
        Next = 1,
        Nearest = 2
    }

    public readonly struct InputBoxCaret
    {
        public readonly ulong Utf8Offset;
        public readonly ulong Utf16Offset;
        public readonly int Affinity;

        public InputBoxCaret(ulong utf8Offset, ulong utf16Offset, int affinity)
        {
            Utf8Offset = utf8Offset;
            Utf16Offset = utf16Offset;
            Affinity = affinity;
        }
    }

    public readonly struct InputBoxMetrics
    {
        public readonly float Height;
        public readonly float LongestLine;
        public readonly float MinIntrinsicWidth;
        public readonly float MaxIntrinsicWidth;
        public readonly float ContentWidth;
        public readonly float ScrollX;
        public readonly float ViewportWidth;
        public readonly float ViewportHeight;

        public InputBoxMetrics(float height,
            float longestLine,
            float minIntrinsicWidth,
            float maxIntrinsicWidth,
            float contentWidth,
            float scrollX,
            float viewportWidth,
            float viewportHeight)
        {
            Height = height;
            LongestLine = longestLine;
            MinIntrinsicWidth = minIntrinsicWidth;
            MaxIntrinsicWidth = maxIntrinsicWidth;
            ContentWidth = contentWidth;
            ScrollX = scrollX;
            ViewportWidth = viewportWidth;
            ViewportHeight = viewportHeight;
        }
    }

    public readonly struct InputBoxLineMetrics
    {
        public readonly ulong StartUtf8;
        public readonly ulong EndUtf8;
        public readonly float Ascent;
        public readonly float Descent;
        public readonly float UnscaledAscent;
        public readonly float Height;
        public readonly float Width;
        public readonly float Left;
        public readonly float Baseline;

        public InputBoxLineMetrics(ulong startUtf8,
            ulong endUtf8,
            float ascent,
            float descent,
            float unscaledAscent,
            float height,
            float width,
            float left,
            float baseline)
        {
            StartUtf8 = startUtf8;
            EndUtf8 = endUtf8;
            Ascent = ascent;
            Descent = descent;
            UnscaledAscent = unscaledAscent;
            Height = height;
            Width = width;
            Left = left;
            Baseline = baseline;
        }
    }

    internal sealed class InputBoxDrawSnapshot : IDisposable
    {
        private bool disposed;

        internal IntPtr Ptr { get; private set; }

        internal InputBoxDrawSnapshot(IntPtr ptr)
        {
            Ptr = ptr;
        }

        ~InputBoxDrawSnapshot()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (Ptr == IntPtr.Zero)
            {
                return;
            }

            var ptr = Ptr;
            var result = BindingC.SkiaTextlayoutInputBoxDrawSnapshotDestroy(ref ptr);
            if (disposing)
            {
                ExitCodeUtil.ThrowIfFailed(result);
            }
            Ptr = ptr;
        }
    }

    public sealed class InputBox : IDisposable
    {
        private bool disposed;

        public IntPtr Ptr { get; private set; }

        public InputBox(ParagraphStyle paragraphStyle, TextStyle textStyle)
        {
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutInputBoxCreate(out var ptr, paragraphStyle.Ptr, textStyle.Ptr));
            Ptr = ptr;
        }

        ~InputBox()
        {
            Dispose(false);
        }

        public string Text
        {
            get
            {
                ThrowIfDisposed();
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxGetText(Ptr, out var ptr, out var size));
                return ReadNativeUtf8(ptr, size);
            }
            set => SetText(value);
        }

        public InputBoxCaret Cursor
        {
            get
            {
                ThrowIfDisposed();
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutInputBoxGetCursor(Ptr, out var utf8, out var utf16, out var affinity));
                return new InputBoxCaret(utf8, utf16, affinity);
            }
        }

        public void SetText(string text)
        {
            ThrowIfDisposed();
            var bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
            unsafe
            {
                fixed (byte* ptr = bytes)
                {
                    ExitCodeUtil.ThrowIfFailed(
                        BindingC.SkiaTextlayoutInputBoxSetText(Ptr, ptr, (ulong)bytes.Length));
                }
            }
        }

        internal InputBoxDrawSnapshot CreateDrawSnapshot()
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxCreateDrawSnapshot(Ptr, out var ptr));
            return new InputBoxDrawSnapshot(ptr);
        }

        public void SetViewport(Vector2 size)
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxSetViewport(Ptr, size.x, size.y));
        }

        public void SetCaretColor(Color32 color)
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutInputBoxSetCaretColor(Ptr, color.r, color.g, color.b, color.a));
        }

        public void SetCaretWidth(float width)
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxSetCaretWidth(Ptr, width));
        }

        public void SetCaretVisible(bool visible)
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxSetCaretVisible(Ptr, visible ? 1 : 0));
        }

        public void InsertText(string text)
        {
            ThrowIfDisposed();
            var bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
            unsafe
            {
                fixed (byte* ptr = bytes)
                {
                    ExitCodeUtil.ThrowIfFailed(
                        BindingC.SkiaTextlayoutInputBoxInsertText(Ptr, ptr, (ulong)bytes.Length));
                }
            }
        }

        public bool DeleteBackward()
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxDeleteBackward(Ptr, out var changed));
            return changed != 0;
        }

        public bool DeleteForward()
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxDeleteForward(Ptr, out var changed));
            return changed != 0;
        }

        public bool MovePrevious()
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxMovePrevious(Ptr, out var changed));
            return changed != 0;
        }

        public bool MoveNext()
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxMoveNext(Ptr, out var changed));
            return changed != 0;
        }

        public bool HitTest(Vector2 localPosition)
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutInputBoxHitTest(Ptr, localPosition.x, localPosition.y, out var changed));
            return changed != 0;
        }

        public void EnsureCaretVisible()
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxEnsureCaretVisible(Ptr));
        }

        public void SetCursorUtf8(ulong utf8Offset, int affinity = 1)
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxSetCursorUtf8(Ptr, utf8Offset, affinity));
        }

        public ulong Utf8ToUtf16(ulong utf8Offset)
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutInputBoxUtf8ToUtf16(Ptr, utf8Offset, out var utf16Offset));
            return utf16Offset;
        }

        public ulong Utf16ToUtf8(ulong utf16Offset)
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutInputBoxUtf16ToUtf8(Ptr, utf16Offset, out var utf8Offset));
            return utf8Offset;
        }

        public ulong SnapUtf8(ulong utf8Offset, TextBoundarySnapMode mode)
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutInputBoxSnapUtf8(Ptr, utf8Offset, (int)mode, out var snappedUtf8Offset));
            return snappedUtf8Offset;
        }

        public Rect GetCaretRect()
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutInputBoxGetCaretRect(Ptr, out var left, out var top, out var right, out var bottom));
            return Rect.MinMaxRect(left, top, right, bottom);
        }

        public InputBoxMetrics GetMetrics()
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxGetMetrics(Ptr,
                out var height,
                out var longestLine,
                out var minIntrinsicWidth,
                out var maxIntrinsicWidth,
                out var contentWidth,
                out var scrollX,
                out var viewportWidth,
                out var viewportHeight));
            return new InputBoxMetrics(height,
                longestLine,
                minIntrinsicWidth,
                maxIntrinsicWidth,
                contentWidth,
                scrollX,
                viewportWidth,
                viewportHeight);
        }

        public ulong GetLineCount()
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxGetLineCount(Ptr, out var lineCount));
            return lineCount;
        }

        public InputBoxLineMetrics GetLineMetrics(ulong lineNumber)
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxGetLineMetrics(Ptr,
                lineNumber,
                out var startUtf8,
                out var endUtf8,
                out var ascent,
                out var descent,
                out var unscaledAscent,
                out var height,
                out var width,
                out var left,
                out var baseline));
            return new InputBoxLineMetrics(startUtf8,
                endUtf8,
                ascent,
                descent,
                unscaledAscent,
                height,
                width,
                left,
                baseline);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (Ptr == IntPtr.Zero)
            {
                return;
            }

            var ptr = Ptr;
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxDestroy(ref ptr));
            Ptr = ptr;
        }

        private void ThrowIfDisposed()
        {
            if (disposed || Ptr == IntPtr.Zero)
            {
                throw new ObjectDisposedException(nameof(InputBox));
            }
        }

        private static string ReadNativeUtf8(IntPtr ptr, ulong size)
        {
            if (ptr == IntPtr.Zero || size == 0)
            {
                return string.Empty;
            }

            if (size > int.MaxValue)
            {
                throw new Exception("Native UTF-8 string is too large.");
            }

            var bytes = new byte[(int)size];
            Marshal.Copy(ptr, bytes, 0, bytes.Length);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
