using System;
using System.Text;
using Milestro.Binding;
using Paraparty.UnityNative.Base;

namespace Milestro.Skia.TextLayout
{
    public class ParagraphBuilder : DisposableNativeObject
    {
        private readonly StringBuilder _text = new StringBuilder();
        private ulong _utf8Length;

        public ParagraphBuilder(ParagraphStyle paragraphStyle)
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutParagraphBuilderCreate(out ptr, paragraphStyle.NativePtr));
        }

        protected override void DisposeUnmanaged()
        {
            if (ptr != IntPtr.Zero)
            {
                var nativePtr = ptr;
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutParagraphBuilderDestroy(out nativePtr));
                ptr = nativePtr;
            }

            base.DisposeUnmanaged();
        }

        public void PushStyle(TextStyle textStyle)
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutParagraphBuilderPushStyle(NativePtr, textStyle.NativePtr));
        }

        public void Pop()
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutParagraphBuilderPop(NativePtr));
        }

        public unsafe void AddText(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var payload = Encoding.UTF8.GetBytes(text);
            fixed (byte* textPtr = payload)
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphBuilderAddText(NativePtr, textPtr, (ulong)payload.Length)
                );
            }

            _text.Append(text);
            _utf8Length += (ulong)payload.Length;
        }

        public Paragraph Build()
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutParagraphBuilderBuild(NativePtr, out var paragraphPtr));
            return new Paragraph(paragraphPtr);
        }

        public void AddPlaceholder(float width, float height)
        {
            if (!float.IsFinite(width) || width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (!float.IsFinite(height) || height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutParagraphBuilderAddPlaceholder(
                NativePtr, width, height, 5, 0, 0));
            _text.Append('\uFFFC');
            _utf8Length += 3;
        }

        public unsafe void AddRuby(ulong startUtf8, ulong endUtf8, string annotation)
        {
            AddRubyUtf8(startUtf8, endUtf8, annotation);
        }

        public void AddRuby(ulong startUtf8, ulong endUtf8, string annotation, RubyStyle style)
        {
            AddRubyUtf8(startUtf8, endUtf8, annotation, style);
        }

        public unsafe void AddRubyUtf8(ulong startUtf8, ulong endUtf8, string annotation,
            RubyStyle? style = null)
        {
            if (annotation == null) throw new ArgumentNullException(nameof(annotation));
            if (startUtf8 >= endUtf8 || endUtf8 > _utf8Length)
                throw new ArgumentOutOfRangeException(nameof(endUtf8));
            if (annotation.Length == 0) throw new ArgumentException("Ruby annotation cannot be empty.", nameof(annotation));
            style?.Validate();
            var payload = Encoding.UTF8.GetBytes(annotation);
            fixed (byte* textPtr = payload)
            {
                if (style == null)
                {
                    ExitCodeUtil.ThrowIfFailed(
                        BindingC.SkiaTextlayoutParagraphBuilderAddRuby(NativePtr, startUtf8, endUtf8,
                            textPtr, (ulong)payload.Length)
                    );
                }
                else
                {
                    ExitCodeUtil.ThrowIfFailed(
                        BindingC.SkiaTextlayoutParagraphBuilderAddRubyWithStyle(NativePtr, startUtf8, endUtf8,
                            textPtr, (ulong)payload.Length, style.TextStyle?.NativePtr ?? IntPtr.Zero,
                            style.FontSizeMultiplier, style.Gap, (int)style.Position, (int)style.Alignment,
                            (int)style.Overhang, (int)style.Overflow)
                    );
                }
            }
        }

        public void AddRubyUtf16(int startUtf16, int endUtf16, string annotation, RubyStyle? style = null)
        {
            Utf16RangeToUtf8(_text.ToString(), startUtf16, endUtf16, out var startUtf8, out var endUtf8);
            AddRubyUtf8(startUtf8, endUtf8, annotation, style);
        }

        public void AddRubyText(string baseText, string annotation, RubyStyle? style = null)
        {
            if (baseText == null) throw new ArgumentNullException(nameof(baseText));
            if (baseText.Length == 0) throw new ArgumentException("Ruby base text cannot be empty.", nameof(baseText));
            if (annotation == null) throw new ArgumentNullException(nameof(annotation));
            if (annotation.Length == 0) throw new ArgumentException("Ruby annotation cannot be empty.", nameof(annotation));
            style?.Validate();
            var startUtf8 = _utf8Length;
            AddText(baseText);
            AddRubyUtf8(startUtf8, _utf8Length, annotation, style);
        }

        public void AddTateChuYokoUtf8(ulong startUtf8, ulong endUtf8,
            TateChuYokoSizing sizing = TateChuYokoSizing.Squeeze)
        {
            if (startUtf8 >= endUtf8 || endUtf8 > _utf8Length)
                throw new ArgumentOutOfRangeException(nameof(endUtf8));
            if (sizing != TateChuYokoSizing.Squeeze && sizing != TateChuYokoSizing.Grow)
                throw new ArgumentOutOfRangeException(nameof(sizing));
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutParagraphBuilderAddTateChuYoko(
                    NativePtr, startUtf8, endUtf8, (int)sizing));
        }

        public void AddTateChuYokoUtf16(int startUtf16, int endUtf16,
            TateChuYokoSizing sizing = TateChuYokoSizing.Squeeze)
        {
            Utf16RangeToUtf8(_text.ToString(), startUtf16, endUtf16, out var startUtf8, out var endUtf8);
            AddTateChuYokoUtf8(startUtf8, endUtf8, sizing);
        }

        public void AddOrientationOverrideUtf8(ulong startUtf8, ulong endUtf8,
            VerticalTextOrientation orientation)
        {
            if (startUtf8 >= endUtf8 || endUtf8 > _utf8Length)
                throw new ArgumentOutOfRangeException(nameof(endUtf8));
            if (orientation != VerticalTextOrientation.Mixed &&
                orientation != VerticalTextOrientation.Upright &&
                orientation != VerticalTextOrientation.Sideways)
                throw new ArgumentOutOfRangeException(nameof(orientation));
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutParagraphBuilderAddOrientationOverride(
                    NativePtr, startUtf8, endUtf8, (int)orientation));
        }

        public void AddOrientationOverrideUtf16(int startUtf16, int endUtf16,
            VerticalTextOrientation orientation)
        {
            Utf16RangeToUtf8(_text.ToString(), startUtf16, endUtf16, out var startUtf8, out var endUtf8);
            AddOrientationOverrideUtf8(startUtf8, endUtf8, orientation);
        }

        public void AddNoWrapUtf8(ulong startUtf8, ulong endUtf8)
        {
            if (startUtf8 >= endUtf8 || endUtf8 > _utf8Length)
                throw new ArgumentOutOfRangeException(nameof(endUtf8));
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutParagraphBuilderAddNoWrap(NativePtr, startUtf8, endUtf8));
        }

        public void AddNoWrapUtf16(int startUtf16, int endUtf16)
        {
            Utf16RangeToUtf8(_text.ToString(), startUtf16, endUtf16, out var startUtf8, out var endUtf8);
            AddNoWrapUtf8(startUtf8, endUtf8);
        }

        internal static void Utf16RangeToUtf8(string text, int startUtf16, int endUtf16,
            out ulong startUtf8, out ulong endUtf8)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (startUtf16 < 0 || startUtf16 >= endUtf16 || endUtf16 > text.Length)
                throw new ArgumentOutOfRangeException(nameof(endUtf16));
            if (startUtf16 > 0 && startUtf16 < text.Length &&
                char.IsHighSurrogate(text[startUtf16 - 1]) && char.IsLowSurrogate(text[startUtf16]))
                throw new ArgumentException("Ruby range cannot split a UTF-16 surrogate pair.", nameof(startUtf16));
            if (endUtf16 > 0 && endUtf16 < text.Length &&
                char.IsHighSurrogate(text[endUtf16 - 1]) && char.IsLowSurrogate(text[endUtf16]))
                throw new ArgumentException("Ruby range cannot split a UTF-16 surrogate pair.", nameof(endUtf16));
            startUtf8 = (ulong)Encoding.UTF8.GetByteCount(text, 0, startUtf16);
            endUtf8 = startUtf8 +
                      (ulong)Encoding.UTF8.GetByteCount(text, startUtf16, endUtf16 - startUtf16);
        }
    }
}
