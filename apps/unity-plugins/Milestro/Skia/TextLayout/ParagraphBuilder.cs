using System;
using System.Text;
using Milestro.Binding;
using Paraparty.UnityNative.Base;

namespace Milestro.Skia.TextLayout
{
    public class ParagraphBuilder : DisposableNativeObject
    {
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

        public void AddText(string text)
        {
            var payload = Encoding.UTF8.GetBytes(text);
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutParagraphBuilderAddText(NativePtr, payload, (ulong)payload.Length)
            );
        }

        public Paragraph Build()
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutParagraphBuilderBuild(NativePtr, out var paragraphPtr));
            return new Paragraph(paragraphPtr);
        }
    }
}
