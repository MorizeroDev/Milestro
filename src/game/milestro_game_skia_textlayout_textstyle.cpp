#include <Milestro/game/milestro_game_interface.h>
#include <Milestro/log/log.h>
#include "milestro_game_retcode.h"
#include "Milestro/skia/textlayout/TextStyle.h"
#include "Milestro/skia/FontManager.h"

extern "C" {
int64_t MilestroSkiaTextlayoutTextStyleCreate(milestro::skia::textlayout::TextStyle *&ret) try {
    ret = new milestro::skia::textlayout::TextStyle();
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutTextStyleDestroy(milestro::skia::textlayout::TextStyle *&ret) try {
    delete ret;
    ret = nullptr;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutTextStyleSetColor(milestro::skia::textlayout::TextStyle *s,
                                                int32_t r,
                                                int32_t g,
                                                int32_t b,
                                                int32_t a) {
    s->setColor(SkColorSetARGB(a, r, g, b));
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutTextStyleGetColor(milestro::skia::textlayout::TextStyle *s,
                                                int32_t &r,
                                                int32_t &g,
                                                int32_t &b,
                                                int32_t &a) {
    auto color = s->getColor();
    r = SkColorGetR(color);
    g = SkColorGetG(color);
    b = SkColorGetB(color);
    a = SkColorGetA(color);
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutTextStyleGetDecoration(milestro::skia::textlayout::TextStyle *s, int32_t &decoration) {
    decoration = s->getDecoration();
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutTextStyleSetDecoration(milestro::skia::textlayout::TextStyle *s, int32_t decoration) {
    s->setDecoration((::skia::textlayout::TextDecoration) decoration);
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutTextStyleGetDecorationMode(milestro::skia::textlayout::TextStyle *s,
                                                         int32_t &decoration) {
    decoration = s->getDecorationMode();
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutTextStyleSetDecorationMode(milestro::skia::textlayout::TextStyle *s,
                                                         int32_t decorationMode) {
    s->setDecorationMode((::skia::textlayout::TextDecorationMode) decorationMode);
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutTextStyleGetDecorationColor(milestro::skia::textlayout::TextStyle *s,
                                                          int32_t &r,
                                                          int32_t &g,
                                                          int32_t &b,
                                                          int32_t &a) {
    auto color = s->getDecorationColor();
    r = SkColorGetR(color);
    g = SkColorGetG(color);
    b = SkColorGetB(color);
    a = SkColorGetA(color);
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutTextStyleSetDecorationColor(milestro::skia::textlayout::TextStyle *s,
                                                          int32_t r,
                                                          int32_t g,
                                                          int32_t b,
                                                          int32_t a) {
    s->setDecorationColor(SkColorSetARGB(a, r, g, b));
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutTextStyleGetDecorationStyle(milestro::skia::textlayout::TextStyle *s,
                                                          int32_t &decorationStyle) {
    decorationStyle = s->getDecorationStyle();
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutTextStyleSetDecorationStyle(milestro::skia::textlayout::TextStyle *s,
                                                          int32_t decorationStyle) {
    s->setDecorationStyle((::skia::textlayout::TextDecorationStyle) decorationStyle);
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutTextStyleGetDecorationThicknessMultiplier(milestro::skia::textlayout::TextStyle *s,
                                                                        float &multiplier) {
    multiplier = s->getDecorationThicknessMultiplier();
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutTextStyleSetDecorationThicknessMultiplier(milestro::skia::textlayout::TextStyle *s,
                                                                        float multiplier) {
    s->setDecorationThicknessMultiplier(multiplier);
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutTextStyleSetFontStyle(milestro::skia::textlayout::TextStyle *s,
                                                    int32_t weight, int32_t width, int32_t slant) {
    SkFontStyle style(weight, width, (SkFontStyle::Slant) slant);
    s->setFontStyle(style);
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutTextStyleGetFontStyle(milestro::skia::textlayout::TextStyle *s,
                                                    int32_t &weight, int32_t &width, int32_t &slant) {
    auto style = s->getFontStyle();
    weight = style.weight();
    width = style.width();
    slant = style.slant();
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutTextStyleAddShadow(milestro::skia::textlayout::TextStyle *s,
                                                 int32_t colorR, int32_t colorG, int32_t colorB, int32_t colorA,
                                                 float offsetX, float offsetY,
                                                 double blurSigma) {
    s->addShadow(SkColorSetARGB(colorR, colorG, colorB, colorA), {offsetX, offsetY}, blurSigma);
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutTextStyleResetShadow(milestro::skia::textlayout::TextStyle *s) {
    s->resetShadows();
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutTextStyleGetFontFeatureNumber(milestro::skia::textlayout::TextStyle *s, uint64_t &num) {
    num = s->getFontFeatureNumber();
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutTextStyleAddFontFeature(milestro::skia::textlayout::TextStyle *s,
                                                      uint8_t *fontFeature,
                                                      int32_t value) {
    s->addFontFeature(SkString(reinterpret_cast<const char *>(fontFeature)), value);
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutTextStyleResetFontFeatures(milestro::skia::textlayout::TextStyle *s) {
    s->resetFontFeatures();
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutTextStyleSetFontSize(milestro::skia::textlayout::TextStyle *s,
                                                   float size) {
    s->setFontSize(size);
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutTextStyleGetFontSize(milestro::skia::textlayout::TextStyle *s,
                                                   float &size) {
    size = s->getFontSize();
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutTextStyleSetFontFamilies(milestro::skia::textlayout::TextStyle *s,
                                                       uint8_t **families,
                                                       uint32_t size) {
    std::vector<SkString> fontFamilies(size);
    for (int i = 0; i < size; i++) {
        fontFamilies[i] = SkString(reinterpret_cast<const char *>(families[i]));
    }
    s->setFontFamilies(std::move(fontFamilies));
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutTextStyleSetBaselineShift(milestro::skia::textlayout::TextStyle *s, float baselineShift) {
    s->setBaselineShift(baselineShift);
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutTextStyleGetBaselineShift(milestro::skia::textlayout::TextStyle *s,
                                                        float &baselineShift) {
    baselineShift = s->getBaselineShift();
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutTextStyleSetHeight(milestro::skia::textlayout::TextStyle *s,
                                                 float height) {
    s->setHeight(height);
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutTextStyleGetHeight(milestro::skia::textlayout::TextStyle *s,
                                                 float &height) {
    height = s->getHeight();
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutTextStyleSetHeightOverride(milestro::skia::textlayout::TextStyle *s,
                                                         int32_t v) {
    s->setHeightOverride(v != 0);
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutTextStyleGetHeightOverride(milestro::skia::textlayout::TextStyle *s,
                                                         int32_t &v) {
    v = s->getHeightOverride() ? 1 : 0;
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutTextStyleSetHalfLeading(milestro::skia::textlayout::TextStyle *s,
                                                      int32_t halfLeading) {
    s->setHalfLeading(halfLeading != 0);
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutTextStyleGetHalfLeading(milestro::skia::textlayout::TextStyle *s,
                                                      int32_t &halfLeading) {
    halfLeading = s->getHalfLeading() ? 1 : 0;
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutTextStyleSetLetterSpacing(milestro::skia::textlayout::TextStyle *s,
                                                        float letterSpacing) {
    s->setLetterSpacing(letterSpacing);
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutTextStyleGetLetterSpacing(milestro::skia::textlayout::TextStyle *s,
                                                        float &letterSpacing) {
    letterSpacing = s->getLetterSpacing();
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutTextStyleSetWordSpacing(milestro::skia::textlayout::TextStyle *s,
                                                      float wordSpacing) {
    s->setWordSpacing(wordSpacing);
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutTextStyleGetWordSpacing(milestro::skia::textlayout::TextStyle *s,
                                                      float &wordSpacing) {
    wordSpacing = s->getWordSpacing();
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutTextStyleSetTypeFace(milestro::skia::textlayout::TextStyle *s,
                                                   milestro::skia::TypeFace *typeFace) {
    s->setTypeface(typeFace->unwrap());
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutTextStyleSetLocale(milestro::skia::textlayout::TextStyle *s,
                                                 uint8_t *locale) {
    s->setLocale(SkString(reinterpret_cast<const char *>(locale)));
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutTextStyleSetTextBaseline(milestro::skia::textlayout::TextStyle *s,
                                                       int32_t textBaseline) {
    s->setTextBaseline((::skia::textlayout::TextBaseline) textBaseline);
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutTextStyleGetTextBaseline(milestro::skia::textlayout::TextStyle *s,
                                                       int32_t &textBaseline) {
    textBaseline = static_cast<int32_t>(s->getTextBaseline());
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutTextStyleSetPlaceholder(milestro::skia::textlayout::TextStyle *s) {
    s->setPlaceholder();
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutTextStyleIsPlaceholder(milestro::skia::textlayout::TextStyle *s,
                                                     int32_t &isPlaceholder) {
    isPlaceholder = s->isPlaceholder() ? 1 : 0;
    return MILESTRO_API_RET_OK;
}

}
