using System;
using Milestro.Binding;
using Milestro.Native;

namespace Milestro.Skia.TextLayout
{
    /// <summary>
    /// 虽然你看到这里是引用类型，但是实际上用的时候是值类型
    /// </summary>
    public class ParagraphStyle
    {
        public IntPtr Ptr { get; private set; }

        public ParagraphStyle()
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutParagraphStyleCreate(out var ptr));
            Ptr = ptr;
        }

        ~ParagraphStyle()
        {
            var ptr = Ptr;
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutParagraphStyleDestroy(out ptr));
            Ptr = ptr;
        }


        /// <summary>
        /// 你 Get 了之后记得 Set 回去
        /// </summary>
        /// <returns></returns>
        public StrutStyle GetStrutStyle()
        {
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutParagraphStyleGetStrutStyle(Ptr, out var ret)
            );
            return new StrutStyle(ret);
        }

        public void SetStrutStyle(StrutStyle strutStyle)
        {
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutParagraphStyleSetStrutStyle(Ptr, strutStyle.Ptr)
            );
        }

        /// <summary>
        /// 你 Get 了之后记得 Set 回去
        /// </summary>
        /// <returns></returns>
        public TextStyle GetTextStyle()
        {
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutParagraphStyleGetTextStyle(Ptr, out var ret)
            );
            return new TextStyle(ret);
        }

        public void SetTextStyle(TextStyle textStyle)
        {
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutParagraphStyleSetTextStyle(Ptr, textStyle.Ptr)
            );
        }


        public int TextDirection
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphStyleGetTextDirection(Ptr, out var ret)
                );
                return ret;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphStyleSetTextDirection(Ptr, value)
                );
        }

        public int TextAlign
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphStyleGetTextAlign(Ptr, out var ret)
                );
                return ret;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphStyleSetTextAlign(Ptr, value)
                );
        }


        public ulong MaxLines
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphStyleGetMaxLines(Ptr, out var ret)
                );
                return ret;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphStyleSetMaxLines(Ptr, value)
                );
        }

        public void SetEllipsis(string s)
        {
            var cstr = s.CStr();
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutParagraphStyleSetEllipsis(Ptr, cstr)
            );
        }

        public float Height
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphStyleGetHeight(Ptr, out var ret)
                );
                return ret;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphStyleSetHeight(Ptr, value)
                );
        }

        public int TextHeightBehavior
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphStyleGetTextHeightBehavior(Ptr, out var ret)
                );
                return ret;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphStyleSetTextHeightBehavior(Ptr, value)
                );
        }

        public bool IsUnlimitedLines
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphStyleIsUnlimitedLines(Ptr, out var ret)
                );
                return ret != 0;
            }
        }

        public bool IsEllipsized
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphStyleIsEllipsized(Ptr, out var ret)
                );
                return ret != 0;
            }
        }

        public bool IsHintingOn
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphStyleIsHintingOn(Ptr, out var ret)
                );
                return ret != 0;
            }
        }

        public void TurnHintingOff()
        {
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutParagraphStyleTurnHintingOff(Ptr)
            );
        }

        public bool ReplaceTabCharacters
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphStyleGetReplaceTabCharacters(Ptr, out var ret)
                );
                return ret != 0;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphStyleSetReplaceTabCharacters(Ptr, value ? 1 : 0)
                );
        }

        public bool ApplyRoundingHack
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphStyleGetApplyRoundingHack(Ptr, out var ret)
                );
                return ret != 0;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphStyleSetApplyRoundingHack(Ptr, value ? 1 : 0)
                );
        }
    }
}
