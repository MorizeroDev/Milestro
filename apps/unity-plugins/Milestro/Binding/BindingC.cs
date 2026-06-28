using System;
using System.Runtime.InteropServices;

namespace Milestro.Binding {
    public class BindingC {
#if UNITY_IOS && !UNITY_EDITOR
        private const string dllName = "__Internal";
        private const string EntryPointPrefix = "FrameworkBinding";
#else
        private const string dllName = "libMilestro";
        private const string EntryPointPrefix = "";
#endif


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroGetVersion")]
        internal static extern unsafe long GetVersion(out int major, out int minor, out int patch);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroUnityRenderGetRenderEventAndDataFunc")]
        internal static extern unsafe IntPtr UnityRenderGetRenderEventAndDataFunc();


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroUnityRenderGetMetalRenderEventId")]
        internal static extern unsafe long UnityRenderGetMetalRenderEventId(out int eventId);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroUnityRenderGetRenderTextureEventId")]
        internal static extern unsafe long UnityRenderGetRenderTextureEventId(int graphicsBackend, out int eventId);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroUnityRenderCreateD3D12ExternalTexture")]
        internal static extern unsafe long
        UnityRenderCreateD3D12ExternalTexture(int width, int height, int srgb, int preferredFormat, out IntPtr texture);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroUnityRenderDestroyD3D12ExternalTexture")]
        internal static extern unsafe long UnityRenderDestroyD3D12ExternalTexture(ref IntPtr texture);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaFontManagerRegisterFontFromFile")]
        internal static extern unsafe long SkiaFontManagerRegisterFontFromFile(byte[] path);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaFontManagerGetFontFamilies")]
        internal static extern unsafe long
        SkiaFontManagerGetFontFamilies(byte[] buffer, ulong bufferSize, out ulong needed);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTypefaceDestroy")]
        internal static extern unsafe long SkiaTypefaceDestroy(out IntPtr ret);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTypefaceGetFamilyNames")]
        internal static extern unsafe long
        SkiaTypefaceGetFamilyNames(IntPtr typeFace, byte[] buffer, ulong bufferSize, out ulong needed);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaFontGetPath")]
        internal static extern unsafe long SkiaFontGetPath(IntPtr font, out IntPtr path, ushort glyphId);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaPathDestroy")]
        internal static extern unsafe long SkiaPathDestroy(out IntPtr ret);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaPathToAATriangles")]
        internal static extern unsafe long SkiaPathToAATriangles(IntPtr p, out IntPtr vertexData, float tolerance);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaSvgCreate")]
        internal static extern unsafe long SkiaSvgCreate(out IntPtr ret, void* data, ulong size);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaSvgDestroy")]
        internal static extern unsafe long SkiaSvgDestroy(out IntPtr ret);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaSvgRender")]
        internal static extern unsafe long SkiaSvgRender(IntPtr svg, IntPtr canvas);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaVertexDataDestroy")]
        internal static extern unsafe long SkiaVertexDataDestroy(out IntPtr ret);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaVertexDataGetVertexCount")]
        internal static extern unsafe long SkiaVertexDataGetVertexCount(IntPtr d, out ulong numVertices);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaVertexDataGetVertexSize")]
        internal static extern unsafe long SkiaVertexDataGetVertexSize(IntPtr d, out ulong vertexSize);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaVertexDataFillData")]
        internal static extern unsafe long SkiaVertexDataFillData(IntPtr d, void* dst);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaFontCollectionClearCaches")]
        internal static extern unsafe long SkiaFontCollectionClearCaches();


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaFontCollectionIsFontFallbackEnabled")]
        internal static extern unsafe long SkiaFontCollectionIsFontFallbackEnabled(out int enabled);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaFontCollectionSetFontFallbackEnabled")]
        internal static extern unsafe long SkiaFontCollectionSetFontFallbackEnabled(int enabled);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaCanvasCreate")]
        internal static extern unsafe long SkiaCanvasCreate(out IntPtr ret, int width, int height);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaCanvasCreateWithMemory")]
        internal static extern unsafe long SkiaCanvasCreateWithMemory(out IntPtr ret,
                                                                      int width,
                                                                      int height,
                                                                      void* pixels,
                                                                      long verticalFlip,
                                                                      long clearPixels);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaCanvasDestroy")]
        internal static extern unsafe long SkiaCanvasDestroy(out IntPtr ret);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaCanvasGetTexture")]
        internal static extern unsafe long SkiaCanvasGetTexture(IntPtr ret, void* targetSpace);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaCanvasDrawImageSimple")]
        internal static extern unsafe long SkiaCanvasDrawImageSimple(IntPtr ret, IntPtr image, float x, float y);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaCanvasDrawImage")]
        internal static extern unsafe long SkiaCanvasDrawImage(IntPtr ret,
                                                               IntPtr image,
                                                               float srcLeft,
                                                               float srcTop,
                                                               float srcRight,
                                                               float srcBottom,
                                                               float dstLeft,
                                                               float dstTop,
                                                               float dstRight,
                                                               float dstBottom);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaImageCreate")]
        internal static extern unsafe long SkiaImageCreate(out IntPtr ret, void* data, ulong size);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaImageSetColorType")]
        internal static extern unsafe long SkiaImageSetColorType(IntPtr img, int targetColorType);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaImageGetWidth")]
        internal static extern unsafe long SkiaImageGetWidth(IntPtr img, out int width);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaImageGetHeight")]
        internal static extern unsafe long SkiaImageGetHeight(IntPtr img, out int height);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaImageDestroy")]
        internal static extern unsafe long SkiaImageDestroy(out IntPtr ret);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutParagraphDestroy")]
        internal static extern unsafe long SkiaTextlayoutParagraphDestroy(out IntPtr ret);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutParagraphLayout")]
        internal static extern unsafe long SkiaTextlayoutParagraphLayout(IntPtr p, float width);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutParagraphPaint")]
        internal static extern unsafe long SkiaTextlayoutParagraphPaint(IntPtr p, IntPtr canvas, float x, float y);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutParagraphSplitGlyph")]
        internal static extern unsafe long
        SkiaTextlayoutParagraphSplitGlyph(IntPtr p,
                                          IntPtr context,
                                          float x,
                                          float y,
                                          MilestroCTypes.SkiaTextlayoutParagraphSplitGlyphCallback callback);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutParagraphToSDF")]
        internal static extern unsafe long SkiaTextlayoutParagraphToSDF(IntPtr p,
                                                                        int sdfWidth,
                                                                        int sdfHeight,
                                                                        float sdfScale,
                                                                        float x,
                                                                        float y,
                                                                        void* distanceField);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutParagraphToPath")]
        internal static extern unsafe long SkiaTextlayoutParagraphToPath(IntPtr p, out IntPtr path, float x, float y);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutParagraphBuilderCreate")]
        internal static extern unsafe long SkiaTextlayoutParagraphBuilderCreate(out IntPtr ret, IntPtr style);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutParagraphBuilderDestroy")]
        internal static extern unsafe long SkiaTextlayoutParagraphBuilderDestroy(out IntPtr ret);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutParagraphBuilderPushStyle")]
        internal static extern unsafe long SkiaTextlayoutParagraphBuilderPushStyle(IntPtr b, IntPtr style);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutParagraphBuilderPop")]
        internal static extern unsafe long SkiaTextlayoutParagraphBuilderPop(IntPtr b);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutParagraphBuilderBuild")]
        internal static extern unsafe long SkiaTextlayoutParagraphBuilderBuild(IntPtr b, out IntPtr paragraph);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutParagraphBuilderAddText")]
        internal static extern unsafe long SkiaTextlayoutParagraphBuilderAddText(IntPtr b, byte[] text, ulong len);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutParagraphStyleCreate")]
        internal static extern unsafe long SkiaTextlayoutParagraphStyleCreate(out IntPtr ret);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutParagraphStyleDestroy")]
        internal static extern unsafe long SkiaTextlayoutParagraphStyleDestroy(out IntPtr ret);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutParagraphStyleGetStrutStyle")]
        internal static extern unsafe long SkiaTextlayoutParagraphStyleGetStrutStyle(IntPtr s, out IntPtr strutStyle);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutParagraphStyleSetStrutStyle")]
        internal static extern unsafe long SkiaTextlayoutParagraphStyleSetStrutStyle(IntPtr s, IntPtr strutStyle);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutParagraphStyleGetTextStyle")]
        internal static extern unsafe long SkiaTextlayoutParagraphStyleGetTextStyle(IntPtr s, out IntPtr textStyle);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutParagraphStyleSetTextStyle")]
        internal static extern unsafe long SkiaTextlayoutParagraphStyleSetTextStyle(IntPtr s, IntPtr textStyle);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutParagraphStyleGetTextDirection")]
        internal static extern unsafe long SkiaTextlayoutParagraphStyleGetTextDirection(IntPtr s, out int direction);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutParagraphStyleSetTextDirection")]
        internal static extern unsafe long SkiaTextlayoutParagraphStyleSetTextDirection(IntPtr s, int direction);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutParagraphStyleGetTextAlign")]
        internal static extern unsafe long SkiaTextlayoutParagraphStyleGetTextAlign(IntPtr s, out int align);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutParagraphStyleSetTextAlign")]
        internal static extern unsafe long SkiaTextlayoutParagraphStyleSetTextAlign(IntPtr s, int align);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutParagraphStyleGetMaxLines")]
        internal static extern unsafe long SkiaTextlayoutParagraphStyleGetMaxLines(IntPtr s, out ulong maxLines);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutParagraphStyleSetMaxLines")]
        internal static extern unsafe long SkiaTextlayoutParagraphStyleSetMaxLines(IntPtr s, ulong maxLines);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutParagraphStyleSetEllipsis")]
        internal static extern unsafe long SkiaTextlayoutParagraphStyleSetEllipsis(IntPtr s, byte[] ellipsis);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutParagraphStyleGetHeight")]
        internal static extern unsafe long SkiaTextlayoutParagraphStyleGetHeight(IntPtr s, out float height);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutParagraphStyleSetHeight")]
        internal static extern unsafe long SkiaTextlayoutParagraphStyleSetHeight(IntPtr s, float height);


        [DllImport(dllName,
                   EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutParagraphStyleGetTextHeightBehavior")]
        internal static extern unsafe long SkiaTextlayoutParagraphStyleGetTextHeightBehavior(IntPtr s,
                                                                                             out int behavior);


        [DllImport(dllName,
                   EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutParagraphStyleSetTextHeightBehavior")]
        internal static extern unsafe long SkiaTextlayoutParagraphStyleSetTextHeightBehavior(IntPtr s, int behavior);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutParagraphStyleIsUnlimitedLines")]
        internal static extern unsafe long SkiaTextlayoutParagraphStyleIsUnlimitedLines(IntPtr s,
                                                                                        out int unlimitedLines);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutParagraphStyleIsEllipsized")]
        internal static extern unsafe long SkiaTextlayoutParagraphStyleIsEllipsized(IntPtr s, out int isEllipsized);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutParagraphStyleGetEffectiveAlign")]
        internal static extern unsafe long SkiaTextlayoutParagraphStyleGetEffectiveAlign(IntPtr s,
                                                                                         out int effectiveAlign);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutParagraphStyleIsHintingOn")]
        internal static extern unsafe long SkiaTextlayoutParagraphStyleIsHintingOn(IntPtr s, out int hintingIsOn);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutParagraphStyleTurnHintingOff")]
        internal static extern unsafe long SkiaTextlayoutParagraphStyleTurnHintingOff(IntPtr s);


        [DllImport(dllName,
                   EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutParagraphStyleGetReplaceTabCharacters")]
        internal static extern unsafe long SkiaTextlayoutParagraphStyleGetReplaceTabCharacters(IntPtr s, out int v);


        [DllImport(dllName,
                   EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutParagraphStyleSetReplaceTabCharacters")]
        internal static extern unsafe long SkiaTextlayoutParagraphStyleSetReplaceTabCharacters(IntPtr s, int v);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutParagraphStyleGetApplyRoundingHack")]
        internal static extern unsafe long SkiaTextlayoutParagraphStyleGetApplyRoundingHack(IntPtr s, out int v);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutParagraphStyleSetApplyRoundingHack")]
        internal static extern unsafe long SkiaTextlayoutParagraphStyleSetApplyRoundingHack(IntPtr s, int v);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutStrutStyleCreate")]
        internal static extern unsafe long SkiaTextlayoutStrutStyleCreate(out IntPtr ret);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutStrutStyleDestroy")]
        internal static extern unsafe long SkiaTextlayoutStrutStyleDestroy(out IntPtr ret);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutStrutStyleSetFontFamilies")]
        internal static extern unsafe long
        SkiaTextlayoutStrutStyleSetFontFamilies(IntPtr s, void** families, uint size);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutStrutStyleSetFontStyle")]
        internal static extern unsafe long
        SkiaTextlayoutStrutStyleSetFontStyle(IntPtr s, int weight, int width, int slant);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutStrutStyleGetFontStyle")]
        internal static extern unsafe long
        SkiaTextlayoutStrutStyleGetFontStyle(IntPtr s, out int weight, out int width, out int slant);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutStrutStyleSetFontSize")]
        internal static extern unsafe long SkiaTextlayoutStrutStyleSetFontSize(IntPtr s, float size);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutStrutStyleGetFontSize")]
        internal static extern unsafe long SkiaTextlayoutStrutStyleGetFontSize(IntPtr s, out float size);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutStrutStyleSetHeight")]
        internal static extern unsafe long SkiaTextlayoutStrutStyleSetHeight(IntPtr s, float height);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutStrutStyleGetHeight")]
        internal static extern unsafe long SkiaTextlayoutStrutStyleGetHeight(IntPtr s, out float height);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutStrutStyleSetLeading")]
        internal static extern unsafe long SkiaTextlayoutStrutStyleSetLeading(IntPtr s, float leading);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutStrutStyleGetLeading")]
        internal static extern unsafe long SkiaTextlayoutStrutStyleGetLeading(IntPtr s, out float leading);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutStrutStyleSetStrutEnabled")]
        internal static extern unsafe long SkiaTextlayoutStrutStyleSetStrutEnabled(IntPtr s, int v);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutStrutStyleGetStrutEnabled")]
        internal static extern unsafe long SkiaTextlayoutStrutStyleGetStrutEnabled(IntPtr s, out int v);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutStrutStyleSetForceStrutHeight")]
        internal static extern unsafe long SkiaTextlayoutStrutStyleSetForceStrutHeight(IntPtr s, int v);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutStrutStyleGetForceStrutHeight")]
        internal static extern unsafe long SkiaTextlayoutStrutStyleGetForceStrutHeight(IntPtr s, out int v);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutStrutStyleSetHeightOverride")]
        internal static extern unsafe long SkiaTextlayoutStrutStyleSetHeightOverride(IntPtr s, int v);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutStrutStyleGetHeightOverride")]
        internal static extern unsafe long SkiaTextlayoutStrutStyleGetHeightOverride(IntPtr s, out int v);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutStrutStyleSetHalfLeading")]
        internal static extern unsafe long SkiaTextlayoutStrutStyleSetHalfLeading(IntPtr s, int halfLeading);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutStrutStyleGetHalfLeading")]
        internal static extern unsafe long SkiaTextlayoutStrutStyleGetHalfLeading(IntPtr s, out int halfLeading);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleCreate")]
        internal static extern unsafe long SkiaTextlayoutTextStyleCreate(out IntPtr ret);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleDestroy")]
        internal static extern unsafe long SkiaTextlayoutTextStyleDestroy(out IntPtr ret);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleSetColor")]
        internal static extern unsafe long SkiaTextlayoutTextStyleSetColor(IntPtr s, int r, int g, int b, int a);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleGetColor")]
        internal static extern unsafe long
        SkiaTextlayoutTextStyleGetColor(IntPtr s, out int r, out int g, out int b, out int a);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleGetDecoration")]
        internal static extern unsafe long SkiaTextlayoutTextStyleGetDecoration(IntPtr s, out int decoration);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleSetDecoration")]
        internal static extern unsafe long SkiaTextlayoutTextStyleSetDecoration(IntPtr s, int decoration);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleGetDecorationMode")]
        internal static extern unsafe long SkiaTextlayoutTextStyleGetDecorationMode(IntPtr s, out int decoration);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleSetDecorationMode")]
        internal static extern unsafe long SkiaTextlayoutTextStyleSetDecorationMode(IntPtr s, int decorationMode);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleGetDecorationColor")]
        internal static extern unsafe long
        SkiaTextlayoutTextStyleGetDecorationColor(IntPtr s, out int r, out int g, out int b, out int a);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleSetDecorationColor")]
        internal static extern unsafe long
        SkiaTextlayoutTextStyleSetDecorationColor(IntPtr s, int r, int g, int b, int a);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleGetDecorationStyle")]
        internal static extern unsafe long SkiaTextlayoutTextStyleGetDecorationStyle(IntPtr s, out int decorationStyle);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleSetDecorationStyle")]
        internal static extern unsafe long SkiaTextlayoutTextStyleSetDecorationStyle(IntPtr s, int decorationStyle);


        [DllImport(dllName,
                   EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleGetDecorationThicknessMultiplier")]
        internal static extern unsafe long
        SkiaTextlayoutTextStyleGetDecorationThicknessMultiplier(IntPtr s, out float multiplier);


        [DllImport(dllName,
                   EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleSetDecorationThicknessMultiplier")]
        internal static extern unsafe long SkiaTextlayoutTextStyleSetDecorationThicknessMultiplier(IntPtr s,
                                                                                                   float multiplier);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleSetFontStyle")]
        internal static extern unsafe long
        SkiaTextlayoutTextStyleSetFontStyle(IntPtr s, int weight, int width, int slant);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleGetFontStyle")]
        internal static extern unsafe long
        SkiaTextlayoutTextStyleGetFontStyle(IntPtr s, out int weight, out int width, out int slant);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleAddShadow")]
        internal static extern unsafe long SkiaTextlayoutTextStyleAddShadow(IntPtr s,
                                                                            int colorR,
                                                                            int colorG,
                                                                            int colorB,
                                                                            int colorA,
                                                                            float offsetX,
                                                                            float offsetY,
                                                                            double blurSigma);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleResetShadow")]
        internal static extern unsafe long SkiaTextlayoutTextStyleResetShadow(IntPtr s);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleGetFontFeatureNumber")]
        internal static extern unsafe long SkiaTextlayoutTextStyleGetFontFeatureNumber(IntPtr s, out ulong num);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleAddFontFeature")]
        internal static extern unsafe long
        SkiaTextlayoutTextStyleAddFontFeature(IntPtr s, byte[] fontFeature, int value);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleResetFontFeatures")]
        internal static extern unsafe long SkiaTextlayoutTextStyleResetFontFeatures(IntPtr s);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleSetFontSize")]
        internal static extern unsafe long SkiaTextlayoutTextStyleSetFontSize(IntPtr s, float size);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleGetFontSize")]
        internal static extern unsafe long SkiaTextlayoutTextStyleGetFontSize(IntPtr s, out float size);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleSetFontFamilies")]
        internal static extern unsafe long SkiaTextlayoutTextStyleSetFontFamilies(IntPtr s, void** families, uint size);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleSetBaselineShift")]
        internal static extern unsafe long SkiaTextlayoutTextStyleSetBaselineShift(IntPtr s, float baselineShift);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleGetBaselineShift")]
        internal static extern unsafe long SkiaTextlayoutTextStyleGetBaselineShift(IntPtr s, out float baselineShift);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleSetHeight")]
        internal static extern unsafe long SkiaTextlayoutTextStyleSetHeight(IntPtr s, float height);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleGetHeight")]
        internal static extern unsafe long SkiaTextlayoutTextStyleGetHeight(IntPtr s, out float height);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleSetHeightOverride")]
        internal static extern unsafe long SkiaTextlayoutTextStyleSetHeightOverride(IntPtr s, int v);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleGetHeightOverride")]
        internal static extern unsafe long SkiaTextlayoutTextStyleGetHeightOverride(IntPtr s, out int v);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleSetHalfLeading")]
        internal static extern unsafe long SkiaTextlayoutTextStyleSetHalfLeading(IntPtr s, int halfLeading);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleGetHalfLeading")]
        internal static extern unsafe long SkiaTextlayoutTextStyleGetHalfLeading(IntPtr s, out int halfLeading);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleSetLetterSpacing")]
        internal static extern unsafe long SkiaTextlayoutTextStyleSetLetterSpacing(IntPtr s, float letterSpacing);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleGetLetterSpacing")]
        internal static extern unsafe long SkiaTextlayoutTextStyleGetLetterSpacing(IntPtr s, out float letterSpacing);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleSetWordSpacing")]
        internal static extern unsafe long SkiaTextlayoutTextStyleSetWordSpacing(IntPtr s, float wordSpacing);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleGetWordSpacing")]
        internal static extern unsafe long SkiaTextlayoutTextStyleGetWordSpacing(IntPtr s, out float wordSpacing);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleSetTypeface")]
        internal static extern unsafe long SkiaTextlayoutTextStyleSetTypeface(IntPtr s, IntPtr typeFace);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleSetLocale")]
        internal static extern unsafe long SkiaTextlayoutTextStyleSetLocale(IntPtr s, byte[] locale);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleSetTextBaseline")]
        internal static extern unsafe long SkiaTextlayoutTextStyleSetTextBaseline(IntPtr s, int textBaseline);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleGetTextBaseline")]
        internal static extern unsafe long SkiaTextlayoutTextStyleGetTextBaseline(IntPtr s, out int textBaseline);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleSetPlaceholder")]
        internal static extern unsafe long SkiaTextlayoutTextStyleSetPlaceholder(IntPtr s);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroSkiaTextlayoutTextStyleIsPlaceholder")]
        internal static extern unsafe long SkiaTextlayoutTextStyleIsPlaceholder(IntPtr s, out int isPlaceholder);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroIcuIcuUCollatorCreate")]
        internal static extern unsafe long IcuIcuUCollatorCreate(out IntPtr ret, byte[] collation);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroIcuIcuUCollatorDestroy")]
        internal static extern unsafe long IcuIcuUCollatorDestroy(out IntPtr ret);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroIcuIcuUCollatorCompare")]
        internal static extern unsafe long IcuIcuUCollatorCompare(IntPtr cmp, out int result, byte[] a, byte[] b);


        [DllImport(dllName, EntryPoint = EntryPointPrefix + "MilestroIcuIcuUCollatorSetAttribute")]
        internal static extern unsafe long IcuIcuUCollatorSetAttribute(IntPtr collator, int attr, int value);
    }
}
