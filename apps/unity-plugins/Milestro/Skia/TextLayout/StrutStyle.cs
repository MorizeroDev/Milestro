using System;
using System.Collections.Generic;
using Milestro.Binding;
using Paraparty.UnityNative;

namespace Milestro.Skia.TextLayout
{
    /// <summary>
    /// 虽然你看到这里是引用类型，但是实际上用的时候是值类型
    /// </summary>
    public class StrutStyle
    {
        public IntPtr Ptr { get; private set; }

        internal StrutStyle(IntPtr ptr)
        {
            Ptr = ptr;
        }

        public StrutStyle()
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutStrutStyleCreate(out var ptr));
            Ptr = ptr;
        }

        ~StrutStyle()
        {
            var ptr = Ptr;
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutStrutStyleDestroy(out ptr));
            Ptr = ptr;
        }

        public unsafe void SetFontFamilies(List<string> fontFamily)
        {
            using var families = new UnmanagedStringArray(fontFamily);
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutStrutStyleSetFontFamilies(Ptr, families.Ptr, families.Length)
            );
        }

        public void GetFontStyle(out int weight, out int width, out int slant)
        {
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutStrutStyleGetFontStyle(Ptr, out weight, out width, out slant)
            );
        }

        public void SetFontStyle(int weight, int width, int slant)
        {
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutStrutStyleSetFontStyle(Ptr, weight, width, slant)
            );
        }

        public float FontSize
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutStrutStyleGetFontSize(Ptr, out var ret)
                );
                return ret;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutStrutStyleSetFontSize(Ptr, value)
                );
        }

        public float Height
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutStrutStyleGetHeight(Ptr, out var ret)
                );
                return ret;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutStrutStyleSetHeight(Ptr, value)
                );
        }

        public bool Leading
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutStrutStyleGetStrutEnabled(Ptr, out var ret)
                );
                return ret != 0;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutStrutStyleSetStrutEnabled(Ptr, value ? 1 : 0)
                );
        }

        public bool ForceStrutHeight
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutStrutStyleGetForceStrutHeight(Ptr, out var ret)
                );
                return ret != 0;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutStrutStyleSetForceStrutHeight(Ptr, value ? 1 : 0)
                );
        }

        public bool HeightOverride
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutStrutStyleGetHeightOverride(Ptr, out var ret)
                );
                return ret != 0;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutStrutStyleSetHeightOverride(Ptr, value ? 1 : 0)
                );
        }

        public bool HalfLeading
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutStrutStyleGetHalfLeading(Ptr, out var ret)
                );
                return ret != 0;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutStrutStyleSetHalfLeading(Ptr, value ? 1 : 0)
                );
        }
    }
}
