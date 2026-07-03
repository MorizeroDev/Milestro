using System;
using System.Text;
using Milestro.Binding;
using Milestro.Model;
using Milestro.Util;
using Paraparty.UnityNative.Base;
using UnityEngine;

namespace Milestro.Skia.TextLayout
{
    public sealed class InputBox : DisposableNativeObject
    {
        public InputBox(ParagraphStyle paragraphStyle, TextStyle textStyle)
        {
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutInputBoxCreate(out ptr, paragraphStyle.NativePtr, textStyle.NativePtr));
        }

        public string Text
        {
            get
            {
                ThrowIfDisposed();
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxGetText(NativePtr, out var value));
                using var ret = new BytesWrapper(value);
                return ret.GetString();
            }
            set => SetText(value);
        }

        public InputBoxCaret Cursor
        {
            get
            {
                ThrowIfDisposed();
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutInputBoxGetCursor(NativePtr, out var utf8, out var utf16, out var affinity));
                return new InputBoxCaret(utf8, utf16, affinity);
            }
        }

        public bool SoftWrap
        {
            get
            {
                ThrowIfDisposed();
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxGetSoftWrap(NativePtr, out var ret));
                return ret != 0;
            }
            set => SetSoftWrap(value);
        }

        public bool MaskInput
        {
            get
            {
                ThrowIfDisposed();
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxGetMaskInput(NativePtr, out var ret));
                return ret != 0;
            }
            set => SetMaskInput(value);
        }

        public void SetText(string text)
        {
            ThrowIfDisposed();
            var bytes = GetUtf8Bytes(text);
            unsafe
            {
                fixed (byte* ptr = bytes)
                {
                    ExitCodeUtil.ThrowIfFailed(
                        BindingC.SkiaTextlayoutInputBoxSetText(NativePtr, ptr, (ulong)bytes.Length));
                }
            }
        }

        internal InputBoxDrawSnapshot CreateDrawSnapshot()
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxCreateDrawSnapshot(NativePtr, out var ptr));
            return new InputBoxDrawSnapshot(ptr);
        }

        public void SetViewport(Vector2 size)
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxSetViewport(NativePtr, size.x, size.y));
        }

        public void SetSoftWrap(bool softWrap)
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxSetSoftWrap(NativePtr, softWrap ? 1 : 0));
        }

        public void SetMaskInput(bool maskInput)
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxSetMaskInput(NativePtr, maskInput ? 1 : 0));
        }

        public void SetCaretColor(Color32 color)
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutInputBoxSetCaretColor(NativePtr, color.r, color.g, color.b, color.a));
        }

        public void SetSelectionColor(Color32 color)
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutInputBoxSetSelectionColor(NativePtr, color.r, color.g, color.b, color.a));
        }

        public void SetCaretWidth(float width)
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxSetCaretWidth(NativePtr, width));
        }

        public void SetCaretVisible(bool visible)
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxSetCaretVisible(NativePtr, visible ? 1 : 0));
        }

        public void InsertText(string text)
        {
            ThrowIfDisposed();
            var bytes = GetUtf8Bytes(text);
            unsafe
            {
                fixed (byte* ptr = bytes)
                {
                    ExitCodeUtil.ThrowIfFailed(
                        BindingC.SkiaTextlayoutInputBoxInsertText(NativePtr, ptr, (ulong)bytes.Length));
                }
            }
        }

        public bool SetComposition(string text)
        {
            ThrowIfDisposed();
            var bytes = GetUtf8Bytes(text);
            unsafe
            {
                fixed (byte* ptr = bytes)
                {
                    ExitCodeUtil.ThrowIfFailed(
                        BindingC.SkiaTextlayoutInputBoxSetComposition(NativePtr, ptr, (ulong)bytes.Length, out var changed));
                    return changed != 0;
                }
            }
        }

        public bool CommitComposition(string text)
        {
            ThrowIfDisposed();
            var bytes = GetUtf8Bytes(text);
            unsafe
            {
                fixed (byte* ptr = bytes)
                {
                    ExitCodeUtil.ThrowIfFailed(
                        BindingC.SkiaTextlayoutInputBoxCommitComposition(NativePtr, ptr, (ulong)bytes.Length, out var changed));
                    return changed != 0;
                }
            }
        }

        public bool ClearComposition()
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxClearComposition(NativePtr, out var changed));
            return changed != 0;
        }

        public bool DeleteBackward()
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxDeleteBackward(NativePtr, out var changed));
            return changed != 0;
        }

        public bool DeleteForward()
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxDeleteForward(NativePtr, out var changed));
            return changed != 0;
        }

        public bool Undo()
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxUndo(NativePtr, out var changed));
            return changed != 0;
        }

        public bool Redo()
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxRedo(NativePtr, out var changed));
            return changed != 0;
        }

        public void BreakUndoGroup()
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxBreakUndoGroup(NativePtr));
        }

        public bool MovePrevious(bool extendSelection = false)
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutInputBoxMovePreviousExtendingSelection(NativePtr,
                    extendSelection ? 1 : 0,
                    out var changed));
            return changed != 0;
        }

        public bool MoveNext(bool extendSelection = false)
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutInputBoxMoveNextExtendingSelection(NativePtr,
                    extendSelection ? 1 : 0,
                    out var changed));
            return changed != 0;
        }

        public bool MoveUp(bool extendSelection = false)
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutInputBoxMoveUpExtendingSelection(NativePtr,
                    extendSelection ? 1 : 0,
                    out var changed));
            return changed != 0;
        }

        public bool MoveDown(bool extendSelection = false)
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutInputBoxMoveDownExtendingSelection(NativePtr,
                    extendSelection ? 1 : 0,
                    out var changed));
            return changed != 0;
        }

        public bool MoveLineStart(bool extendSelection = false)
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutInputBoxMoveLineStartExtendingSelection(NativePtr,
                    extendSelection ? 1 : 0,
                    out var changed));
            return changed != 0;
        }

        public bool MoveLineEnd(bool extendSelection = false)
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutInputBoxMoveLineEndExtendingSelection(NativePtr,
                    extendSelection ? 1 : 0,
                    out var changed));
            return changed != 0;
        }

        public bool MoveDocumentStart(bool extendSelection = false)
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutInputBoxMoveDocumentStartExtendingSelection(NativePtr,
                    extendSelection ? 1 : 0,
                    out var changed));
            return changed != 0;
        }

        public bool MoveDocumentEnd(bool extendSelection = false)
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutInputBoxMoveDocumentEndExtendingSelection(NativePtr,
                    extendSelection ? 1 : 0,
                    out var changed));
            return changed != 0;
        }

        public bool HitTest(Vector2 localPosition, bool extendSelection = false)
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutInputBoxHitTestExtendingSelection(NativePtr,
                    localPosition.x,
                    localPosition.y,
                    extendSelection ? 1 : 0,
                    out var changed));
            return changed != 0;
        }

        public void EnsureCaretVisible()
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxEnsureCaretVisible(NativePtr));
        }

        public bool ScrollByX(float delta)
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxScrollByX(NativePtr, delta, out var changed));
            return changed != 0;
        }

        public bool ScrollByY(float delta)
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxScrollByY(NativePtr, delta, out var changed));
            return changed != 0;
        }

        public void SetCursorUtf8(ulong utf8Offset, int affinity = 1)
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxSetCursorUtf8(NativePtr, utf8Offset, affinity));
        }

        public InputBoxSelection Selection
        {
            get
            {
                ThrowIfDisposed();
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxGetSelection(NativePtr,
                    out var anchorUtf8,
                    out var focusUtf8,
                    out var startUtf8,
                    out var endUtf8,
                    out var anchorAffinity,
                    out var focusAffinity,
                    out var hasSelection));
                return new InputBoxSelection(anchorUtf8,
                    focusUtf8,
                    startUtf8,
                    endUtf8,
                    anchorAffinity,
                    focusAffinity,
                    hasSelection != 0);
            }
        }

        public string SelectedText
        {
            get
            {
                ThrowIfDisposed();
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxGetSelectedText(NativePtr,
                    out var value));
                using var ret = new BytesWrapper(value);
                return ret.GetString();
            }
        }

        public bool SetSelectionUtf8(ulong anchorUtf8,
            ulong focusUtf8,
            int anchorAffinity = 1,
            int focusAffinity = 1)
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxSetSelectionUtf8(NativePtr,
                anchorUtf8,
                focusUtf8,
                anchorAffinity,
                focusAffinity,
                out var changed));
            return changed != 0;
        }

        public bool ClearSelection()
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxClearSelection(NativePtr, out var changed));
            return changed != 0;
        }

        public bool SelectAll()
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxSelectAll(NativePtr, out var changed));
            return changed != 0;
        }

        public ulong Utf8ToUtf16(ulong utf8Offset)
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutInputBoxUtf8ToUtf16(NativePtr, utf8Offset, out var utf16Offset));
            return utf16Offset;
        }

        public ulong Utf16ToUtf8(ulong utf16Offset)
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutInputBoxUtf16ToUtf8(NativePtr, utf16Offset, out var utf8Offset));
            return utf8Offset;
        }

        public ulong SnapUtf8(ulong utf8Offset, TextBoundarySnapMode mode)
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutInputBoxSnapUtf8(NativePtr, utf8Offset, (int)mode, out var snappedUtf8Offset));
            return snappedUtf8Offset;
        }

        public Rect GetCaretRect()
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutInputBoxGetCaretRect(NativePtr, out var left, out var top, out var right, out var bottom));
            return Rect.MinMaxRect(left, top, right, bottom);
        }

        public Rect GetCompositionRect()
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutInputBoxGetCompositionRect(NativePtr,
                    out var left,
                    out var top,
                    out var right,
                    out var bottom));
            return Rect.MinMaxRect(left, top, right, bottom);
        }

        public InputBoxMetrics GetMetrics()
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxGetMetrics(NativePtr,
                out var height,
                out var longestLine,
                out var minIntrinsicWidth,
                out var maxIntrinsicWidth,
                out var contentWidth,
                out var scrollX,
                out var scrollY,
                out var viewportWidth,
                out var viewportHeight));
            return new InputBoxMetrics(height,
                longestLine,
                minIntrinsicWidth,
                maxIntrinsicWidth,
                contentWidth,
                scrollX,
                scrollY,
                viewportWidth,
                viewportHeight);
        }

        public ulong GetLineCount()
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxGetLineCount(NativePtr, out var lineCount));
            return lineCount;
        }

        public InputBoxLineMetrics GetLineMetrics(ulong lineNumber)
        {
            ThrowIfDisposed();
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxGetLineMetrics(NativePtr,
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

        protected override void DisposeUnmanaged()
        {
            if (ptr != IntPtr.Zero)
            {
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutInputBoxDestroy(ref ptr));
            }

            base.DisposeUnmanaged();
        }

        private static byte[] GetUtf8Bytes(string text)
        {
            return Encoding.UTF8.GetBytes(Utf16Util.RemoveUnpairedSurrogates(text ?? string.Empty));
        }
    }
}
