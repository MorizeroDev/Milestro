#ifndef MILESTR_SKIA_TEXTLAYOUT_TEXTSTYLE
#define MILESTR_SKIA_TEXTLAYOUT_TEXTSTYLE

#include "modules/skparagraph/include/TextStyle.h"

namespace milestro::skia::textlayout {
class TextStyle {
public:
    SkColor getColor() const { return textStyle.getColor(); }

    void setColor(SkColor color) { textStyle.setColor(color); }

    ::skia::textlayout::TextDecoration getDecorationType() const { return textStyle.getDecorationType(); }

    ::skia::textlayout::TextDecorationMode getDecorationMode() const { return textStyle.getDecorationMode(); }

    SkColor getDecorationColor() const { return textStyle.getDecorationColor(); }

    ::skia::textlayout::TextDecorationStyle getDecorationStyle() const { return textStyle.getDecorationStyle(); }

    SkScalar getDecorationThicknessMultiplier() const {
        return textStyle.getDecorationThicknessMultiplier();
    }

    void setDecoration(::skia::textlayout::TextDecoration decoration) { textStyle.setDecoration(decoration); }

    void setDecorationMode(::skia::textlayout::TextDecorationMode mode) { textStyle.setDecorationMode(mode); }

    void setDecorationStyle(::skia::textlayout::TextDecorationStyle style) { textStyle.setDecorationStyle(style); }

    void setDecorationColor(SkColor color) { textStyle.setDecorationColor(color); }

    void setDecorationThicknessMultiplier(SkScalar m) { textStyle.setDecorationThicknessMultiplier(m); }

    SkFontStyle getFontStyle() const { return textStyle.getFontStyle(); }

    void setFontStyle(SkFontStyle fontStyle) { textStyle.setFontStyle(fontStyle); }

    void addShadow(::skia::textlayout::TextShadow shadow) { textStyle.addShadow(shadow); }

    void addShadow(SkColor color, SkPoint offset, double blurSigma) {
        textStyle.addShadow(::skia::textlayout::TextShadow(color, offset, blurSigma));
    }

    void resetShadows() { textStyle.resetShadows(); }

    size_t getFontFeatureNumber() const { return textStyle.getFontFeatureNumber(); }

    void addFontFeature(const SkString &fontFeature, int value) { textStyle.addFontFeature(fontFeature, value); }

    void resetFontFeatures() { textStyle.resetFontFeatures(); }

    // getFontArguments
    // setFontArguments

    SkScalar getFontSize() const { return textStyle.getFontSize(); }

    void setFontSize(SkScalar size) { textStyle.setFontSize(size); }

    const std::vector<SkString> &getFontFamilies() const { return textStyle.getFontFamilies(); }

    void setFontFamilies(std::vector<SkString> families) {
        textStyle.setFontFamilies(std::move(families));
    }

    SkScalar getBaselineShift() const { return textStyle.getBaselineShift(); }

    void setBaselineShift(SkScalar baselineShift) { textStyle.setBaselineShift(baselineShift); }

    void setHeight(SkScalar height) { textStyle.setHeight(height); }

    SkScalar getHeight() const { return textStyle.getHeight(); }

    void setHeightOverride(bool heightOverride) { textStyle.setHeightOverride(heightOverride); }

    bool getHeightOverride() const { return textStyle.getHeightOverride(); }

    void setHalfLeading(bool halfLeading) { textStyle.setHalfLeading(halfLeading); }

    bool getHalfLeading() const { return textStyle.getHalfLeading(); }

    void setLetterSpacing(SkScalar letterSpacing) { textStyle.setLetterSpacing(letterSpacing); }

    SkScalar getLetterSpacing() const { return textStyle.getLetterSpacing(); }

    void setWordSpacing(SkScalar wordSpacing) { textStyle.setWordSpacing(wordSpacing); }

    SkScalar getWordSpacing() const { return textStyle.getWordSpacing(); }

    SkTypeface *getTypeface() const { return textStyle.getTypeface(); }

    sk_sp<SkTypeface> refTypeface() const { return textStyle.refTypeface(); }

    void setTypeface(sk_sp<SkTypeface> typeface) { textStyle.setTypeface(std::move(typeface)); }

    SkString getLocale() const { return textStyle.getLocale(); }

    void setLocale(const SkString &locale) { textStyle.setLocale(locale); }

    ::skia::textlayout::TextBaseline getTextBaseline() const { return textStyle.getTextBaseline(); }

    void setTextBaseline(::skia::textlayout::TextBaseline baseline) { textStyle.setTextBaseline(baseline); }

    void getFontMetrics(SkFontMetrics *metrics) const {
        textStyle.getFontMetrics(metrics);
    };

    bool isPlaceholder() const { return textStyle.isPlaceholder(); }

    void setPlaceholder() { textStyle.setPlaceholder(); }

    const ::skia::textlayout::TextStyle &unwrap() {
        return textStyle;
    }
private:
    ::skia::textlayout::TextStyle textStyle;
};

}

#endif //MILESTR_TEXTLAYOUT_TEXTILE
