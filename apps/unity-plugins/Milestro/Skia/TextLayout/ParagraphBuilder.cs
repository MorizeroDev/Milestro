using System;
using System.Text;
using Milestro.Binding;

namespace Milestro.Skia.TextLayout
{
    public class ParagraphBuilder
    {
        public IntPtr Ptr { get; private set; }

        public ParagraphBuilder(ParagraphStyle paragraphStyle)
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutParagraphBuilderCreate(out var ptr, paragraphStyle.Ptr));
            Ptr = ptr;
        }

        ~ParagraphBuilder()
        {
            var ptr = Ptr;
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutParagraphBuilderDestroy(out ptr));
            Ptr = ptr;
        }

        public void PushStyle(TextStyle textStyle)
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutParagraphBuilderPushStyle(Ptr, textStyle.Ptr));
        }

        public void Pop()
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutParagraphBuilderPop(Ptr));
        }

        public void AddText(string text)
        {
            var payload = Encoding.UTF8.GetBytes(text);
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutParagraphBuilderAddText(Ptr, payload, (ulong)payload.Length)
            );
        }

        public Paragraph Build()
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutParagraphBuilderBuild(Ptr, out var paragraphPtr));
            return new Paragraph(paragraphPtr);
        }
    }
}