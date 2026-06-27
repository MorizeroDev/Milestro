using System;
using System.Collections.Generic;
using Milestro.Binding;
using Milestro.Model;
using Milestro.Native;
using UnityEngine;

namespace Milestro.Skia.TextLayout
{
    /// <summary>
    /// 虽然你看到这里是引用类型，但是实际上用的时候是值类型。也就是说你可以 PushStyle 之后改一下这个对象，然后再 Push 一次
    /// </summary>
    public class TextStyle
    {
        public IntPtr Ptr { get; private set; }

        internal TextStyle(IntPtr ptr)
        {
            Ptr = ptr;
        }

        public TextStyle()
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutTextStyleCreate(out var ptr));
            Ptr = ptr;
        }

        ~TextStyle()
        {
            var ptr = Ptr;
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutTextStyleDestroy(out ptr));
            Ptr = ptr;
        }

        public Color32 Color
        {
            get
            {
                int r = 0;
                int g = 0;
                int b = 0;
                int a = 0;
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutTextStyleGetColor(Ptr, out r, out g, out b, out a));
                return new Color32((byte)r, (byte)g, (byte)b, (byte)a);
            }
            set
            {
                int r = value.r;
                int g = value.g;
                int b = value.b;
                int a = value.a;
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutTextStyleSetColor(Ptr, r, g, b, a));
            }
        }

        public int Decoration
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutTextStyleGetDecoration(Ptr, out var ret));
                return ret;
            }
            set => ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutTextStyleSetDecoration(Ptr, value));
        }

        public int DecorationMode
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutTextStyleGetDecorationMode(Ptr, out var ret));
                return ret;
            }
            set => ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutTextStyleSetDecorationMode(Ptr, value));
        }

        public Color32 DecorationColor
        {
            get
            {
                int r = 0;
                int g = 0;
                int b = 0;
                int a = 0;
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutTextStyleGetDecorationColor(Ptr, out r, out g, out b, out a));
                return new Color32((byte)r, (byte)g, (byte)b, (byte)a);
            }
            set
            {
                int r = value.r;
                int g = value.g;
                int b = value.b;
                int a = value.a;
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutTextStyleSetDecorationColor(Ptr, r, g, b, a));
            }
        }

        public int DecorationStyle
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutTextStyleGetDecorationStyle(Ptr, out var ret));
                return ret;
            }
            set => ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutTextStyleSetDecorationStyle(Ptr, value));
        }

        public float DecorationThicknessMultiplier
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutTextStyleGetDecorationThicknessMultiplier(Ptr, out var ret));
                return ret;
            }
            set => ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutTextStyleSetDecorationThicknessMultiplier(Ptr, value)
            );
        }

        public void GetFontStyle(out int weight, out int width, out int slant)
        {
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutTextStyleGetFontStyle(Ptr, out weight, out width, out slant)
            );
        }

        public void SetFontStyle(int weight, int width, int slant)
        {
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutTextStyleSetFontStyle(Ptr, weight, width, slant)
            );
        }


        private List<TextShadowDescription> _cachedTextShadow = new List<TextShadowDescription>();

        public void AddShadows(List<TextShadowDescription> shadowList)
        {
            foreach (var item in shadowList)
            {
                AddShadow(item.Color, item.Offset, item.BlurSigma);
            }
        }

        public void AddShadow(Color32 color, Vector2 offset, double blurSigma = 0)
        {
            int colorR = color.r;
            int colorG = color.g;
            int colorB = color.b;
            int colorA = color.a;

            float offsetX = offset.x;
            float offsetY = offset.y;

            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutTextStyleAddShadow(Ptr,
                    colorR, colorG, colorB, colorA,
                    offsetX, offsetY,
                    blurSigma
                )
            );
            _cachedTextShadow.Add(new TextShadowDescription(color, offset, blurSigma));
        }

        public void ResetShadow()
        {
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutTextStyleResetShadow(Ptr)
            );
            _cachedTextShadow.Clear();
        }

        public List<TextShadowDescription> GetShadows()
        {
            var ret = new List<TextShadowDescription>();
            ret.AddRange(_cachedTextShadow);
            return ret;
        }

        public ulong GetFontFeatureNumber()
        {
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutTextStyleGetFontFeatureNumber(Ptr, out var ret)
            );
            return ret;
        }

        public void AddFontFeature(string feature, int value)
        {
            var featureCStr = feature.CStr();
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutTextStyleAddFontFeature(Ptr, featureCStr, value)
            );
        }

        public void ResetFontFeature()
        {
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutTextStyleResetFontFeatures(Ptr)
            );
        }

        public float FontSize
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutTextStyleGetFontSize(Ptr, out var ret)
                );
                return ret;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutTextStyleSetFontSize(Ptr, value)
                );
        }


        private List<string> _cachedFontFamily = new List<string>() { "sans-serif" };

        public unsafe void SetFontFamilies(List<string> fontFamily)
        {
            using var families = new UnmanagedStringArray(fontFamily);
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutTextStyleSetFontFamilies(Ptr, families.Ptr, families.Length)
            );
            _cachedFontFamily = fontFamily;
        }

        public List<string> GetFontFamilies()
        {
            return _cachedFontFamily;
        }

        public float BaselineShift
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutTextStyleGetBaselineShift(Ptr, out var ret)
                );
                return ret;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutTextStyleSetBaselineShift(Ptr, value)
                );
        }

        public float Height
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutTextStyleGetHeight(Ptr, out var ret)
                );
                return ret;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutTextStyleSetHeight(Ptr, value)
                );
        }


        public bool HeightOverride
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutTextStyleGetHeightOverride(Ptr, out var ret)
                );
                return ret != 0;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutTextStyleSetHeightOverride(Ptr, value ? 1 : 0)
                );
        }

        public bool HalfLeading
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutTextStyleGetHalfLeading(Ptr, out var ret)
                );
                return ret != 0;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutTextStyleSetHalfLeading(Ptr, value ? 1 : 0)
                );
        }

        public float LetterSpacing
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutTextStyleGetLetterSpacing(Ptr, out var ret)
                );
                return ret;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutTextStyleSetLetterSpacing(Ptr, value)
                );
        }

        public float WordSpacing
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutTextStyleGetWordSpacing(Ptr, out var ret)
                );
                return ret;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutTextStyleSetWordSpacing(Ptr, value)
                );
        }

        public TypeFace Typeface
        {
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutTextStyleSetTypeface(Ptr, value.Ptr)
                );
        }

        private string _cachedLocale = "";

        public string Locale
        {
            set
            {
                var s = value.CStr();
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutTextStyleSetLocale(Ptr, s)
                );
                _cachedLocale = value;
            }
            get => _cachedLocale;
        }

        public int TextBaseline
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutTextStyleGetTextBaseline(Ptr, out var ret));
                return ret;
            }
            set => ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutTextStyleSetTextBaseline(Ptr, value));
        }


        public bool IsPlaceholder
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutTextStyleIsPlaceholder(Ptr, out var ret)
                );
                return ret != 0;
            }
        }

        public void SetPlaceHolder()
        {
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutTextStyleSetPlaceholder(Ptr)
            );
        }
    }
}
