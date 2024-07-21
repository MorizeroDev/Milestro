#ifndef MILESTRO_GAME_INTERFACE_H
#define MILESTRO_GAME_INTERFACE_H

#include <cstdint>
#include <cstdlib>
#include <string>
#include <vector>

#ifdef MILESTRO_BUILDING_ENV

#include "milestro_game_types.h"
#include <Milestro/common/milestro_export_macros.h>

#else

#include "milestro_export_macros.h"
#include "milestro_game_types.h"

#endif

#ifdef __clang__
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wunknown-attributes"
#endif

extern "C" {

// 返回值为大版本号 major
MILESTRO_API int64_t MilestroGetVersion(int32_t &major, int32_t &minor, int32_t &patch);

MILESTRO_API int64_t MilestroSkiaFontManagerRegisterFontFromFile(uint8_t *path);
MILESTRO_API int64_t MilestroSkiaFontManagerGetFontFamilies(uint8_t *buffer,
                                                            uint64_t bufferSize,
                                                            uint64_t &needed);

MILESTRO_API int64_t MilestroSkiaTypefaceDestroy(milestro::skia::Typeface *&ret);
MILESTRO_API int64_t MilestroSkiaTypefaceGetFamilyNames(milestro::skia::Typeface *typeFace,
                                                        uint8_t *buffer,
                                                        uint64_t bufferSize,
                                                        uint64_t &needed);

MILESTRO_API int64_t MilestroSkiaFontGetPath(milestro::skia::Font *font,
                                             milestro::skia::Path *&path,
                                             uint16_t glyphId);

MILESTRO_API int64_t MilestroSkiaPathDestroy(milestro::skia::Path *&ret);
MILESTRO_API int64_t MilestroSkiaPathToAATriangles(milestro::skia::Path *p,
                                                   milestro::skia::VertexData *&vertexData, float tolerance);

MILESTRO_API int64_t MilestroSkiaSvgCreate(milestro::skia::Svg *&ret,
                                           [[milize::CSharpType("void*")]] void *data, uint64_t size);
MILESTRO_API int64_t MilestroSkiaSvgDestroy(milestro::skia::Svg *&ret);
MILESTRO_API int64_t MilestroSkiaSvgRender(milestro::skia::Svg *svg, milestro::skia::Canvas *canvas);

MILESTRO_API int64_t MilestroSkiaVertexDataDestroy(milestro::skia::VertexData *&ret);
MILESTRO_API int64_t MilestroSkiaVertexDataGetVertexCount(milestro::skia::VertexData *d,
                                                          uint64_t &numVertices);
MILESTRO_API int64_t MilestroSkiaVertexDataGetVertexSize(milestro::skia::VertexData *d,
                                                         uint64_t &vertexSize);
MILESTRO_API int64_t MilestroSkiaVertexDataFillData(milestro::skia::VertexData *d,
                                                    [[milize::CSharpType("void*")]] void *dst);

MILESTRO_API int64_t MilestroSkiaFontCollectionClearCaches();
MILESTRO_API int64_t MilestroSkiaFontCollectionIsFontFallbackEnabled(int32_t &enabled);
MILESTRO_API int64_t MilestroSkiaFontCollectionSetFontFallbackEnabled(int32_t enabled);

MILESTRO_API int64_t MilestroSkiaCanvasCreate(milestro::skia::Canvas *&ret,
                                              int32_t width,
                                              int32_t height);
MILESTRO_API int64_t MilestroSkiaCanvasCreateWithMemory(milestro::skia::Canvas *&ret,
                                                        int32_t width,
                                                        int32_t height,
                                                        [[milize::CSharpType("void*")]] void *pixels,
                                                        int64_t verticalFlip,
                                                        int64_t clearPixels);
MILESTRO_API int64_t MilestroSkiaCanvasDestroy(milestro::skia::Canvas *&ret);
MILESTRO_API int64_t MilestroSkiaCanvasGetTexture(milestro::skia::Canvas *ret,
                                                  [[milize::CSharpType("void*")]] void *targetSpace);
MILESTRO_API int64_t MilestroSkiaCanvasDrawImageSimple(milestro::skia::Canvas *ret,
                                                       milestro::skia::Image *image,
                                                       float x,
                                                       float y);
MILESTRO_API int64_t MilestroSkiaCanvasDrawImage(milestro::skia::Canvas *ret,
                                                 milestro::skia::Image *image,
                                                 float srcLeft, float srcTop, float srcRight, float srcBottom,
                                                 float dstLeft, float dstTop, float dstRight, float dstBottom);

MILESTRO_API int64_t MilestroSkiaImageCreate(milestro::skia::Image *&ret,
                                             [[milize::CSharpType("void*")]] void *data,
                                             uint64_t size);
MILESTRO_API int64_t MilestroSkiaImageSetColorType(milestro::skia::Image *img, int32_t targetColorType);
MILESTRO_API int64_t MilestroSkiaImageGetWidth(milestro::skia::Image *img, int32_t &width);
MILESTRO_API int64_t MilestroSkiaImageGetHeight(milestro::skia::Image *img, int32_t &height);
MILESTRO_API int64_t MilestroSkiaImageDestroy(milestro::skia::Image *&ret);

MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphDestroy(milestro::skia::textlayout::Paragraph *&ret);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphLayout(milestro::skia::textlayout::Paragraph *p, float width);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphPaint(milestro::skia::textlayout::Paragraph *p,
                                                          milestro::skia::Canvas *canvas,
                                                          float x, float y);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphSplitGlyph(milestro::skia::textlayout::Paragraph *p,
                                                               [[milize::CSharpType("IntPtr")]] void *context,
                                                               float x, float y,
                                                               [[milize::CSharpType("MilestroCTypes.SkiaTextlayoutParagraphSplitGlyphCallback")]] MilestroSkiaTextlayoutParagraphSplitGlyphCallback callback);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphToSDF(milestro::skia::textlayout::Paragraph *p,
                                                          int32_t sdfWidth, int32_t sdfHeight, float sdfScale,
                                                          float x, float y,
                                                          [[milize::CSharpType("void*")]]  void *distanceField);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphToPath(milestro::skia::textlayout::Paragraph *p,
                                                           milestro::skia::Path *&path,
                                                           float x, float y);

MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphBuilderCreate(milestro::skia::textlayout::ParagraphBuilder *&ret,
                                                                  milestro::skia::textlayout::ParagraphStyle *style);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphBuilderDestroy(milestro::skia::textlayout::ParagraphBuilder *&ret);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphBuilderPushStyle(milestro::skia::textlayout::ParagraphBuilder *b,
                                                                     milestro::skia::textlayout::TextStyle *style);

MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphBuilderPop(milestro::skia::textlayout::ParagraphBuilder *b);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphBuilderBuild(milestro::skia::textlayout::ParagraphBuilder *b,
                                                                 milestro::skia::textlayout::Paragraph *&paragraph);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphBuilderAddText(milestro::skia::textlayout::ParagraphBuilder *b,
                                                                   uint8_t *text, size_t len);

MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphStyleCreate(milestro::skia::textlayout::ParagraphStyle *&ret);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphStyleDestroy(milestro::skia::textlayout::ParagraphStyle *&ret);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphStyleGetStrutStyle(milestro::skia::textlayout::ParagraphStyle *s,
                                                                       milestro::skia::textlayout::StrutStyle *&strutStyle);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphStyleSetStrutStyle(milestro::skia::textlayout::ParagraphStyle *s,
                                                                       milestro::skia::textlayout::StrutStyle *strutStyle);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphStyleGetTextStyle(milestro::skia::textlayout::ParagraphStyle *s,
                                                                      milestro::skia::textlayout::TextStyle *&textStyle);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphStyleSetTextStyle(milestro::skia::textlayout::ParagraphStyle *s,
                                                                      milestro::skia::textlayout::TextStyle *textStyle);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphStyleGetTextDirection(milestro::skia::textlayout::ParagraphStyle *s,
                                                                          int32_t &direction);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphStyleSetTextDirection(milestro::skia::textlayout::ParagraphStyle *s,
                                                                          int32_t direction);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphStyleGetTextAlign(milestro::skia::textlayout::ParagraphStyle *s,
                                                                      int32_t &align);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphStyleSetTextAlign(milestro::skia::textlayout::ParagraphStyle *s,
                                                                      int32_t align);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphStyleGetMaxLines(milestro::skia::textlayout::ParagraphStyle *s,
                                                                     uint64_t &maxLines);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphStyleSetMaxLines(milestro::skia::textlayout::ParagraphStyle *s,
                                                                     uint64_t maxLines);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphStyleSetEllipsis(milestro::skia::textlayout::ParagraphStyle *s,
                                                                     uint8_t *ellipsis);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphStyleGetHeight(milestro::skia::textlayout::ParagraphStyle *s,
                                                                   float &height);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphStyleSetHeight(milestro::skia::textlayout::ParagraphStyle *s,
                                                                   float height);
MILESTRO_API int64_t
MilestroSkiaTextlayoutParagraphStyleGetTextHeightBehavior(milestro::skia::textlayout::ParagraphStyle *s,
                                                          int32_t &behavior);
MILESTRO_API int64_t
MilestroSkiaTextlayoutParagraphStyleSetTextHeightBehavior(milestro::skia::textlayout::ParagraphStyle *s,
                                                          int32_t behavior);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphStyleIsUnlimitedLines(milestro::skia::textlayout::ParagraphStyle *s,
                                                                          int32_t &unlimitedLines);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphStyleIsEllipsized(milestro::skia::textlayout::ParagraphStyle *s,
                                                                      int32_t &isEllipsized);
MILESTRO_API int64_t
MilestroSkiaTextlayoutParagraphStyleGetEffectiveAlign(milestro::skia::textlayout::ParagraphStyle *s,
                                                      int32_t &effectiveAlign);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphStyleIsHintingOn(milestro::skia::textlayout::ParagraphStyle *s,
                                                                     int32_t &hintingIsOn);
MILESTRO_API int64_t MilestroSkiaTextlayoutParagraphStyleTurnHintingOff(milestro::skia::textlayout::ParagraphStyle *s);
MILESTRO_API int64_t
MilestroSkiaTextlayoutParagraphStyleGetReplaceTabCharacters(milestro::skia::textlayout::ParagraphStyle *s,
                                                            int32_t &v);
MILESTRO_API int64_t
MilestroSkiaTextlayoutParagraphStyleSetReplaceTabCharacters(milestro::skia::textlayout::ParagraphStyle *s,
                                                            int32_t v);
MILESTRO_API int64_t
MilestroSkiaTextlayoutParagraphStyleGetApplyRoundingHack(milestro::skia::textlayout::ParagraphStyle *s,
                                                         int32_t &v);
MILESTRO_API int64_t
MilestroSkiaTextlayoutParagraphStyleSetApplyRoundingHack(milestro::skia::textlayout::ParagraphStyle *s,
                                                         int32_t v);

MILESTRO_API int64_t MilestroSkiaTextlayoutStrutStyleCreate(milestro::skia::textlayout::StrutStyle *&ret);

MILESTRO_API int64_t MilestroSkiaTextlayoutStrutStyleDestroy(milestro::skia::textlayout::StrutStyle *&ret);
MILESTRO_API int64_t MilestroSkiaTextlayoutStrutStyleSetFontFamilies(milestro::skia::textlayout::StrutStyle *s,
                                                                     [[milize::CSharpType("void**")]] uint8_t **families,
                                                                     uint32_t size);
MILESTRO_API int64_t MilestroSkiaTextlayoutStrutStyleSetFontStyle(milestro::skia::textlayout::StrutStyle *s,
                                                                  int32_t weight, int32_t width, int32_t slant);
MILESTRO_API int64_t MilestroSkiaTextlayoutStrutStyleGetFontStyle(milestro::skia::textlayout::StrutStyle *s,
                                                                  int32_t &weight, int32_t &width, int32_t &slant);
MILESTRO_API int64_t MilestroSkiaTextlayoutStrutStyleSetFontSize(milestro::skia::textlayout::StrutStyle *s,
                                                                 float size);
MILESTRO_API int64_t MilestroSkiaTextlayoutStrutStyleGetFontSize(milestro::skia::textlayout::StrutStyle *s,
                                                                 float &size);
MILESTRO_API int64_t MilestroSkiaTextlayoutStrutStyleSetHeight(milestro::skia::textlayout::StrutStyle *s,
                                                               float height);
MILESTRO_API int64_t MilestroSkiaTextlayoutStrutStyleGetHeight(milestro::skia::textlayout::StrutStyle *s,
                                                               float &height);
MILESTRO_API int64_t MilestroSkiaTextlayoutStrutStyleSetLeading(milestro::skia::textlayout::StrutStyle *s,
                                                                float leading);
MILESTRO_API int64_t MilestroSkiaTextlayoutStrutStyleGetLeading(milestro::skia::textlayout::StrutStyle *s,
                                                                float &leading);
MILESTRO_API int64_t MilestroSkiaTextlayoutStrutStyleSetStrutEnabled(milestro::skia::textlayout::StrutStyle *s,
                                                                     int32_t v);
MILESTRO_API int64_t MilestroSkiaTextlayoutStrutStyleGetStrutEnabled(milestro::skia::textlayout::StrutStyle *s,
                                                                     int32_t &v);
MILESTRO_API int64_t MilestroSkiaTextlayoutStrutStyleSetForceStrutHeight(milestro::skia::textlayout::StrutStyle *s,
                                                                         int32_t v);
MILESTRO_API int64_t MilestroSkiaTextlayoutStrutStyleGetForceStrutHeight(milestro::skia::textlayout::StrutStyle *s,
                                                                         int32_t &v);
MILESTRO_API int64_t MilestroSkiaTextlayoutStrutStyleSetHeightOverride(milestro::skia::textlayout::StrutStyle *s,
                                                                       int32_t v);
MILESTRO_API int64_t MilestroSkiaTextlayoutStrutStyleGetHeightOverride(milestro::skia::textlayout::StrutStyle *s,
                                                                       int32_t &v);
MILESTRO_API int64_t MilestroSkiaTextlayoutStrutStyleSetHalfLeading(milestro::skia::textlayout::StrutStyle *s,
                                                                    int32_t halfLeading);
MILESTRO_API int64_t MilestroSkiaTextlayoutStrutStyleGetHalfLeading(milestro::skia::textlayout::StrutStyle *s,
                                                                    int32_t &halfLeading);

MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleCreate(milestro::skia::textlayout::TextStyle *&ret);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleDestroy(milestro::skia::textlayout::TextStyle *&ret);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleSetColor(milestro::skia::textlayout::TextStyle *s,
                                                             int32_t r,
                                                             int32_t g,
                                                             int32_t b,
                                                             int32_t a);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleGetColor(milestro::skia::textlayout::TextStyle *s,
                                                             int32_t &r,
                                                             int32_t &g,
                                                             int32_t &b,
                                                             int32_t &a);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleGetDecoration(milestro::skia::textlayout::TextStyle *s,
                                                                  int32_t &decoration);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleSetDecoration(milestro::skia::textlayout::TextStyle *s,
                                                                  int32_t decoration);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleGetDecorationMode(milestro::skia::textlayout::TextStyle *s,
                                                                      int32_t &decoration);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleSetDecorationMode(milestro::skia::textlayout::TextStyle *s,
                                                                      int32_t decorationMode);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleGetDecorationColor(milestro::skia::textlayout::TextStyle *s,
                                                                       int32_t &r,
                                                                       int32_t &g,
                                                                       int32_t &b,
                                                                       int32_t &a);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleSetDecorationColor(milestro::skia::textlayout::TextStyle *s,
                                                                       int32_t r,
                                                                       int32_t g,
                                                                       int32_t b,
                                                                       int32_t a);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleGetDecorationStyle(milestro::skia::textlayout::TextStyle *s,
                                                                       int32_t &decorationStyle);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleSetDecorationStyle(milestro::skia::textlayout::TextStyle *s,
                                                                       int32_t decorationStyle);
MILESTRO_API int64_t
MilestroSkiaTextlayoutTextStyleGetDecorationThicknessMultiplier(milestro::skia::textlayout::TextStyle *s,
                                                                float &multiplier);
MILESTRO_API int64_t
MilestroSkiaTextlayoutTextStyleSetDecorationThicknessMultiplier(milestro::skia::textlayout::TextStyle *s,
                                                                float multiplier);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleSetFontStyle(milestro::skia::textlayout::TextStyle *s,
                                                                 int32_t weight, int32_t width, int32_t slant);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleGetFontStyle(milestro::skia::textlayout::TextStyle *s,
                                                                 int32_t &weight, int32_t &width, int32_t &slant);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleAddShadow(milestro::skia::textlayout::TextStyle *s,
                                                              int32_t colorR,
                                                              int32_t colorG,
                                                              int32_t colorB,
                                                              int32_t colorA,
                                                              float offsetX,
                                                              float offsetY,
                                                              double blurSigma);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleResetShadow(milestro::skia::textlayout::TextStyle *s);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleGetFontFeatureNumber(milestro::skia::textlayout::TextStyle *s,
                                                                         uint64_t &num);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleAddFontFeature(milestro::skia::textlayout::TextStyle *s,
                                                                   uint8_t *fontFeature,
                                                                   int32_t value);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleResetFontFeatures(milestro::skia::textlayout::TextStyle *s);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleSetFontSize(milestro::skia::textlayout::TextStyle *s,
                                                                float size);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleGetFontSize(milestro::skia::textlayout::TextStyle *s,
                                                                float &size);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleSetFontFamilies(milestro::skia::textlayout::TextStyle *s,
                                                                    [[milize::CSharpType("void**")]] uint8_t **families,
                                                                    uint32_t size);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleSetBaselineShift(milestro::skia::textlayout::TextStyle *s,
                                                                     float baselineShift);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleGetBaselineShift(milestro::skia::textlayout::TextStyle *s,
                                                                     float &baselineShift);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleSetHeight(milestro::skia::textlayout::TextStyle *s,
                                                              float height);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleGetHeight(milestro::skia::textlayout::TextStyle *s,
                                                              float &height);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleSetHeightOverride(milestro::skia::textlayout::TextStyle *s,
                                                                      int32_t v);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleGetHeightOverride(milestro::skia::textlayout::TextStyle *s,
                                                                      int32_t &v);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleSetHalfLeading(milestro::skia::textlayout::TextStyle *s,
                                                                   int32_t halfLeading);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleGetHalfLeading(milestro::skia::textlayout::TextStyle *s,
                                                                   int32_t &halfLeading);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleSetLetterSpacing(milestro::skia::textlayout::TextStyle *s,
                                                                     float letterSpacing);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleGetLetterSpacing(milestro::skia::textlayout::TextStyle *s,
                                                                     float &letterSpacing);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleSetWordSpacing(milestro::skia::textlayout::TextStyle *s,
                                                                   float wordSpacing);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleGetWordSpacing(milestro::skia::textlayout::TextStyle *s,
                                                                   float &wordSpacing);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleSetTypeface(milestro::skia::textlayout::TextStyle *s,
                                                                milestro::skia::Typeface *typeFace);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleSetLocale(milestro::skia::textlayout::TextStyle *s,
                                                              uint8_t *locale);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleSetTextBaseline(milestro::skia::textlayout::TextStyle *s,
                                                                    int32_t textBaseline);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleGetTextBaseline(milestro::skia::textlayout::TextStyle *s,
                                                                    int32_t &textBaseline);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleSetPlaceholder(milestro::skia::textlayout::TextStyle *s);
MILESTRO_API int64_t MilestroSkiaTextlayoutTextStyleIsPlaceholder(milestro::skia::textlayout::TextStyle *s,
                                                                  int32_t &isPlaceholder);

MILESTRO_API int64_t MilestroIcuIcuUCollatorCreate(milestro::icu::IcuUCollator *&ret, uint8_t *collation);
MILESTRO_API int64_t MilestroIcuIcuUCollatorDestroy(milestro::icu::IcuUCollator *&ret);
MILESTRO_API int64_t MilestroIcuIcuUCollatorCompare(
        milestro::icu::IcuUCollator *cmp,
        int32_t &result, uint8_t *a, uint8_t *b
);
MILESTRO_API int64_t MilestroIcuIcuUCollatorSetAttribute(
        milestro::icu::IcuUCollator *collator,
        int32_t attr, int32_t value
);
}
#ifdef __clang__
#pragma clang diagnostic pop
#endif

#endif // MILESTRO_GAME_INTERFACE_H
