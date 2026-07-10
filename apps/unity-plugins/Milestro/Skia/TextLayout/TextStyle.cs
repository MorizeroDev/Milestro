using System;
using System.Collections.Generic;
using Milestro.Binding;
using Milestro.Model;
using Milestro.Skia;
using Paraparty.UnityNative;
using Paraparty.UnityNative.Base;
using UnityEngine;

namespace Milestro.Skia.TextLayout
{
    /// <summary>
    /// 虽然你看到这里是引用类型，但是实际上用的时候是值类型。也就是说你可以 PushStyle 之后改一下这个对象，然后再 Push 一次
    /// </summary>
    public class TextStyle : DisposableNativeObject
    {
        internal TextStyle(IntPtr ptr)
            : base(ptr)
        {
        }

        public TextStyle()
        {
            ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutTextStyleCreate(out ptr));
        }

        protected override void DisposeUnmanaged()
        {
            if (ptr != IntPtr.Zero)
            {
                var nativePtr = ptr;
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutTextStyleDestroy(out nativePtr));
                ptr = nativePtr;
            }

            base.DisposeUnmanaged();
        }

        public Color32 Color
        {
            get
            {
                int r = 0;
                int g = 0;
                int b = 0;
                int a = 0;
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutTextStyleGetColor(NativePtr, out r, out g, out b, out a));
                return new Color32((byte)r, (byte)g, (byte)b, (byte)a);
            }
            set
            {
                int r = value.r;
                int g = value.g;
                int b = value.b;
                int a = value.a;
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutTextStyleSetColor(NativePtr, r, g, b, a));
            }
        }

        public TextDecoration Decoration
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutTextStyleGetDecoration(NativePtr, out var ret));
                return (TextDecoration)ret;
            }
            set => ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutTextStyleSetDecoration(NativePtr, (int)value));
        }

        public TextDecorationMode DecorationMode
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutTextStyleGetDecorationMode(NativePtr, out var ret));
                return (TextDecorationMode)ret;
            }
            set => ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutTextStyleSetDecorationMode(NativePtr, (int)value));
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
                    BindingC.SkiaTextlayoutTextStyleGetDecorationColor(NativePtr, out r, out g, out b, out a));
                return new Color32((byte)r, (byte)g, (byte)b, (byte)a);
            }
            set
            {
                int r = value.r;
                int g = value.g;
                int b = value.b;
                int a = value.a;
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutTextStyleSetDecorationColor(NativePtr, r, g, b, a));
            }
        }

        public TextDecorationStyle DecorationStyle
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutTextStyleGetDecorationStyle(NativePtr, out var ret));
                return (TextDecorationStyle)ret;
            }
            set => ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutTextStyleSetDecorationStyle(NativePtr, (int)value));
        }

        public float DecorationThicknessMultiplier
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutTextStyleGetDecorationThicknessMultiplier(NativePtr, out var ret));
                return ret;
            }
            set => ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutTextStyleSetDecorationThicknessMultiplier(NativePtr, value)
            );
        }

        public void GetFontStyle(out int weight, out FontWidth width, out FontSlant slant)
        {
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutTextStyleGetFontStyle(NativePtr, out weight, out var widthValue, out var slantValue)
            );
            width = (FontWidth)widthValue;
            slant = (FontSlant)slantValue;
        }

        public void SetFontStyle(int weight, FontWidth width, FontSlant slant)
        {
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutTextStyleSetFontStyle(NativePtr, weight, (int)width, (int)slant)
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
                BindingC.SkiaTextlayoutTextStyleAddShadow(NativePtr,
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
                BindingC.SkiaTextlayoutTextStyleResetShadow(NativePtr)
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
                BindingC.SkiaTextlayoutTextStyleGetFontFeatureNumber(NativePtr, out var ret)
            );
            return ret;
        }

        public void AddFontFeature(string feature, int value)
        {
            var featureCStr = feature.CStr();
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutTextStyleAddFontFeature(NativePtr, featureCStr, value)
            );
        }

        public void ResetFontFeature()
        {
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutTextStyleResetFontFeatures(NativePtr)
            );
        }

        public float FontSize
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutTextStyleGetFontSize(NativePtr, out var ret)
                );
                return ret;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutTextStyleSetFontSize(NativePtr, value)
                );
        }


        private List<string> _cachedFontFamily = new List<string>() { "sans-serif" };
        private List<FontFamilyToken> _cachedFontFamilyTokens = new List<FontFamilyToken>()
        {
            FontFamilyToken.Bare("sans-serif")
        };

        public void SetFontFamilies(List<string> fontFamily)
        {
            var sourceTokens = FontFamilyDeclaration.ToBareTokens(fontFamily);
            SetFontFamilyTokens(sourceTokens);
        }

        public void SetFontFamilyTokens(List<FontFamilyToken> fontFamilies)
        {
            var sourceTokens = fontFamilies ?? new List<FontFamilyToken>();
            FontRegistry.ApplyTextStyleFontFamilyTokens(NativePtr, sourceTokens);
            _cachedFontFamilyTokens = new List<FontFamilyToken>(sourceTokens);
            _cachedFontFamily = FontFamilyParser.ToSourceFamilyList(sourceTokens);
        }

        public List<FontFamilyToken> GetFontFamilyTokens()
        {
            return new List<FontFamilyToken>(_cachedFontFamilyTokens);
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
                    BindingC.SkiaTextlayoutTextStyleGetBaselineShift(NativePtr, out var ret)
                );
                return ret;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutTextStyleSetBaselineShift(NativePtr, value)
                );
        }

        public float Height
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutTextStyleGetHeight(NativePtr, out var ret)
                );
                return ret;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutTextStyleSetHeight(NativePtr, value)
                );
        }


        public bool HeightOverride
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutTextStyleGetHeightOverride(NativePtr, out var ret)
                );
                return ret != 0;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutTextStyleSetHeightOverride(NativePtr, value ? 1 : 0)
                );
        }

        public bool HalfLeading
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutTextStyleGetHalfLeading(NativePtr, out var ret)
                );
                return ret != 0;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutTextStyleSetHalfLeading(NativePtr, value ? 1 : 0)
                );
        }

        public float LetterSpacing
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutTextStyleGetLetterSpacing(NativePtr, out var ret)
                );
                return ret;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutTextStyleSetLetterSpacing(NativePtr, value)
                );
        }

        public float WordSpacing
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutTextStyleGetWordSpacing(NativePtr, out var ret)
                );
                return ret;
            }
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutTextStyleSetWordSpacing(NativePtr, value)
                );
        }

        public TypeFace Typeface
        {
            set =>
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutTextStyleSetTypeface(NativePtr, value.NativePtr)
                );
        }

        private string _cachedLocale = "";

        public string Locale
        {
            set
            {
                var s = value.CStr();
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutTextStyleSetLocale(NativePtr, s)
                );
                _cachedLocale = value;
            }
            get => _cachedLocale;
        }

        public TextBaseline TextBaseline
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutTextStyleGetTextBaseline(NativePtr, out var ret));
                return (TextBaseline)ret;
            }
            set => ExitCodeUtil.ThrowIfFailed(BindingC.SkiaTextlayoutTextStyleSetTextBaseline(NativePtr, (int)value));
        }


        public bool IsPlaceholder
        {
            get
            {
                ExitCodeUtil.ThrowIfFailed(
                    BindingC.SkiaTextlayoutTextStyleIsPlaceholder(NativePtr, out var ret)
                );
                return ret != 0;
            }
        }

        public void SetPlaceHolder()
        {
            ExitCodeUtil.ThrowIfFailed(
                BindingC.SkiaTextlayoutTextStyleSetPlaceholder(NativePtr)
            );
        }
    }
}
