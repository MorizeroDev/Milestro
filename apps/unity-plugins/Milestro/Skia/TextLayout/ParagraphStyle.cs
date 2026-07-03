using System;
using Milestro.Binding;
using Paraparty.UnityNative;
using Paraparty.UnityNative.Base;

namespace Milestro.Skia.TextLayout
{
    /// <summary>
    /// 虽然你看到这里是引用类型，但是实际上用的时候是值类型
    /// </summary>
    public class ParagraphStyle : DisposableNativeObject
    {
        public ParagraphStyle()
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutParagraphStyleCreate(out ptr));
        }

        protected override void DisposeUnmanaged()
        {
            if (ptr != IntPtr.Zero)
            {
                var nativePtr = ptr;
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutParagraphStyleDestroy(out nativePtr));
                ptr = nativePtr;
            }

            base.DisposeUnmanaged();
        }


        /// <summary>
        /// 你 Get 了之后记得 Set 回去
        /// </summary>
        /// <returns></returns>
        public StrutStyle GetStrutStyle()
        {
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutParagraphStyleGetStrutStyle(NativePtr, out var ret)
            );
            return new StrutStyle(ret);
        }

        public void SetStrutStyle(StrutStyle strutStyle)
        {
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutParagraphStyleSetStrutStyle(NativePtr, strutStyle.NativePtr)
            );
        }

        /// <summary>
        /// 你 Get 了之后记得 Set 回去
        /// </summary>
        /// <returns></returns>
        public TextStyle GetTextStyle()
        {
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutParagraphStyleGetTextStyle(NativePtr, out var ret)
            );
            return new TextStyle(ret);
        }

        public void SetTextStyle(TextStyle textStyle)
        {
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutParagraphStyleSetTextStyle(NativePtr, textStyle.NativePtr)
            );
        }


        public int TextDirection
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphStyleGetTextDirection(NativePtr, out var ret)
                );
                return ret;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphStyleSetTextDirection(NativePtr, value)
                );
        }

        public int TextAlign
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphStyleGetTextAlign(NativePtr, out var ret)
                );
                return ret;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphStyleSetTextAlign(NativePtr, value)
                );
        }


        public ulong MaxLines
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphStyleGetMaxLines(NativePtr, out var ret)
                );
                return ret;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphStyleSetMaxLines(NativePtr, value)
                );
        }

        public void SetEllipsis(string s)
        {
            var cstr = s.CStr();
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutParagraphStyleSetEllipsis(NativePtr, cstr)
            );
        }

        public float Height
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphStyleGetHeight(NativePtr, out var ret)
                );
                return ret;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphStyleSetHeight(NativePtr, value)
                );
        }

        public int TextHeightBehavior
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphStyleGetTextHeightBehavior(NativePtr, out var ret)
                );
                return ret;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphStyleSetTextHeightBehavior(NativePtr, value)
                );
        }

        public bool IsUnlimitedLines
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphStyleIsUnlimitedLines(NativePtr, out var ret)
                );
                return ret != 0;
            }
        }

        public bool IsEllipsized
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphStyleIsEllipsized(NativePtr, out var ret)
                );
                return ret != 0;
            }
        }

        public bool IsHintingOn
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphStyleIsHintingOn(NativePtr, out var ret)
                );
                return ret != 0;
            }
        }

        public void TurnHintingOff()
        {
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutParagraphStyleTurnHintingOff(NativePtr)
            );
        }

        public bool ReplaceTabCharacters
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphStyleGetReplaceTabCharacters(NativePtr, out var ret)
                );
                return ret != 0;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphStyleSetReplaceTabCharacters(NativePtr, value ? 1 : 0)
                );
        }

        public bool ApplyRoundingHack
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphStyleGetApplyRoundingHack(NativePtr, out var ret)
                );
                return ret != 0;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutParagraphStyleSetApplyRoundingHack(NativePtr, value ? 1 : 0)
                );
        }
    }
}
