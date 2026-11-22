#include "Milestro/skia/textlayout/TextStyle.h"

namespace milestro::skia::textlayout {

TextStyle::TextStyle() {
    // Match Chromium's normal CJK punctuation spacing behavior. Fonts that
    // do not provide `chws` simply ignore the feature.
    textStyle.addFontFeature(SkString("chws"), 1);
}

TextStyle::TextStyle(::skia::textlayout::TextStyle textStyle)
    : textStyle(std::move(textStyle)) {
    setFontFamilyDeclarationFromSkiaFamilies(this->textStyle.getFontFamilies(), false);
}

TextStyle::TextStyle(const TextStyle &other) = default;

TextStyle::TextStyle(TextStyle &&other) = default;

TextStyle &TextStyle::operator=(const TextStyle &other) = default;

TextStyle &TextStyle::operator=(TextStyle &&other) = default;

TextStyle::~TextStyle() = default;

TextStyle TextStyle::richTextBase() const {
    TextStyle result;
    result.textStyle.setDecoration(textStyle.getDecorationType());
    result.textStyle.setDecorationMode(textStyle.getDecorationMode());
    result.textStyle.setDecorationColor(textStyle.getDecorationColor());
    result.textStyle.setDecorationStyle(textStyle.getDecorationStyle());
    result.textStyle.setDecorationThicknessMultiplier(textStyle.getDecorationThicknessMultiplier());
    result.textStyle.setFontStyle(textStyle.getFontStyle());
    result.textStyle.setFontFamilies(textStyle.getFontFamilies());
    result.fontFamilyTokens = fontFamilyTokens;
    result.hasFontFamilyDeclaration = hasFontFamilyDeclaration;
    if (!hasFontFamilyDeclaration) {
        result.setFontFamilyTokens({FontFamilyToken::Bare("sans-serif")});
    }
    result.textStyle.setColor(textStyle.getColor());
    if (textStyle.hasBackground()) {
        result.setBackgroundColor(textStyle.getBackground().getColor());
    }
    result.textStyle.setFontSize(textStyle.getFontSize());
    result.textStyle.setLocale(textStyle.getLocale());
    result.textStyle.setBaselineShift(textStyle.getBaselineShift());
    result.textStyle.setHeight(textStyle.getHeight());
    result.textStyle.setHeightOverride(textStyle.getHeightOverride());
    result.textStyle.setHalfLeading(textStyle.getHalfLeading());
    result.textStyle.setLetterSpacing(textStyle.getLetterSpacing());
    result.textStyle.setWordSpacing(textStyle.getWordSpacing());
    result.textStyle.setTextBaseline(textStyle.getTextBaseline());
    result.copyFontFeaturesFrom(*this);
    for (const auto& shadow : textStyle.getShadows()) {
        result.textStyle.addShadow(shadow);
    }
    return result;
}

std::string TextStyle::getLocaleUtf8() const {
    const auto locale = textStyle.getLocale();
    return std::string(locale.c_str(), locale.size());
}

void TextStyle::setLocale(const SkString &locale) {
    textStyle.setLocale(SkString(locale.c_str(), locale.size()));
}

const ::skia::textlayout::TextStyle TextStyle::spawn() const {
    return textStyle;
}

}
