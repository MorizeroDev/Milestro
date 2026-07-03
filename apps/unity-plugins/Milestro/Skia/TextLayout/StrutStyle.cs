using System;
using System.Collections.Generic;
using Milestro.Binding;
using Paraparty.UnityNative;
using Paraparty.UnityNative.Base;

namespace Milestro.Skia.TextLayout
{
    /// <summary>
    /// 虽然你看到这里是引用类型，但是实际上用的时候是值类型
    /// </summary>
    public class StrutStyle : DisposableNativeObject
    {
        internal StrutStyle(IntPtr ptr)
            : base(ptr)
        {
        }

        public StrutStyle()
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutStrutStyleCreate(out ptr));
        }

        protected override void DisposeUnmanaged()
        {
            if (ptr != IntPtr.Zero)
            {
                var nativePtr = ptr;
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutStrutStyleDestroy(out nativePtr));
                ptr = nativePtr;
            }

            base.DisposeUnmanaged();
        }

        public unsafe void SetFontFamilies(List<string> fontFamily)
        {
            using var families = new UnmanagedStringArray(fontFamily);
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutStrutStyleSetFontFamilies(NativePtr, families.Ptr, families.Length)
            );
        }

        public void GetFontStyle(out int weight, out int width, out int slant)
        {
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutStrutStyleGetFontStyle(NativePtr, out weight, out width, out slant)
            );
        }

        public void SetFontStyle(int weight, int width, int slant)
        {
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutStrutStyleSetFontStyle(NativePtr, weight, width, slant)
            );
        }

        public float FontSize
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutStrutStyleGetFontSize(NativePtr, out var ret)
                );
                return ret;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutStrutStyleSetFontSize(NativePtr, value)
                );
        }

        public float Height
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutStrutStyleGetHeight(NativePtr, out var ret)
                );
                return ret;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutStrutStyleSetHeight(NativePtr, value)
                );
        }

        public bool Leading
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutStrutStyleGetStrutEnabled(NativePtr, out var ret)
                );
                return ret != 0;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutStrutStyleSetStrutEnabled(NativePtr, value ? 1 : 0)
                );
        }

        public bool ForceStrutHeight
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutStrutStyleGetForceStrutHeight(NativePtr, out var ret)
                );
                return ret != 0;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutStrutStyleSetForceStrutHeight(NativePtr, value ? 1 : 0)
                );
        }

        public bool HeightOverride
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutStrutStyleGetHeightOverride(NativePtr, out var ret)
                );
                return ret != 0;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutStrutStyleSetHeightOverride(NativePtr, value ? 1 : 0)
                );
        }

        public bool HalfLeading
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutStrutStyleGetHalfLeading(NativePtr, out var ret)
                );
                return ret != 0;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutStrutStyleSetHalfLeading(NativePtr, value ? 1 : 0)
                );
        }
    }
}
