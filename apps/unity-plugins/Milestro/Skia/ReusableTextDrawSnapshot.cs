using System;
using Milestro.Binding;
using Paraparty.UnityNative.Base;
using UnityEngine;
using SkFont = Milestro.Skia.Font;

namespace Milestro.Skia
{
    internal sealed class ReusableTextDrawSnapshot : DisposableNativeObject
    {
        private readonly SkFont font;
        private int textLength;

        internal ReusableTextDrawSnapshot(SkFont font, int capacity, Color32 color)
        {
            if (font == null)
            {
                throw new ArgumentNullException(nameof(font));
            }

            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            this.font = font;
            Capacity = capacity;
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaReusableTextDrawSnapshotCreate(out ptr,
                font.NativePtr,
                (ulong)capacity,
                color.r,
                color.g,
                color.b,
                color.a));
        }

        internal int Capacity { get; }
        internal int TextLength => textLength;

        internal unsafe void UpdateText(byte[] buffer, int offset, int length)
        {
            ThrowIfRangeInvalid(buffer, offset, length);

            fixed (byte* bufferPtr = buffer)
            {
                var textPtr = length == 0 ? null : bufferPtr + offset;
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaReusableTextDrawSnapshotUpdateText(NativePtr,
                    textPtr,
                    (ulong)length));
            }

            textLength = length;
        }

        internal void CopyTextFrom(ReusableTextDrawSnapshot source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (source.TextLength > Capacity)
            {
                throw new ArgumentException("Source reusable slim text snapshot text exceeds target capacity.",
                    nameof(source));
            }

            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaReusableTextDrawSnapshotCopyTextFrom(NativePtr,
                source.NativePtr));
            textLength = source.TextLength;
        }

        internal Rect MeasureBounds()
        {
            MeasureRaw(out var boundsLeft,
                out var boundsTop,
                out var boundsRight,
                out var boundsBottom,
                out _);
            return Rect.MinMaxRect(boundsLeft, boundsTop, boundsRight, boundsBottom);
        }

        internal FontTextMeasurement MeasureText()
        {
            MeasureRaw(out var boundsLeft,
                out var boundsTop,
                out var boundsRight,
                out var boundsBottom,
                out var advanceX);
            var metrics = font.GetMetrics();
            return new FontTextMeasurement(Rect.MinMaxRect(boundsLeft, boundsTop, boundsRight, boundsBottom),
                advanceX,
                metrics);
        }

        protected override void DisposeUnmanaged()
        {
            if (ptr != IntPtr.Zero)
            {
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaReusableTextDrawSnapshotDestroy(ref ptr));
            }

            base.DisposeUnmanaged();
        }

        private void ThrowIfRangeInvalid(byte[] buffer, int offset, int length)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (offset < 0 || offset > buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            if (length < 0 || length > Capacity || length > buffer.Length - offset)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }
        }

        private void MeasureRaw(out float boundsLeft,
            out float boundsTop,
            out float boundsRight,
            out float boundsBottom,
            out float advanceX)
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaReusableTextDrawSnapshotMeasureText(NativePtr,
                out boundsLeft,
                out boundsTop,
                out boundsRight,
                out boundsBottom,
                out advanceX));
        }
    }
}
