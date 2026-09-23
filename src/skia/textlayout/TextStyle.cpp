#include "Milestro/skia/textlayout/TextStyle.h"

namespace milestro::skia::textlayout {

TextStyle::TextStyle() = default;

TextStyle::TextStyle(::skia::textlayout::TextStyle textStyle)
    : textStyle(std::move(textStyle)) {
    setFontFamilyDeclarationFromSkiaFamilies(this->textStyle.getFontFamilies(), false);
}

TextStyle::TextStyle(const TextStyle &other) = default;

TextStyle::TextStyle(TextStyle &&other) = default;

TextStyle &TextStyle::operator=(const TextStyle &other) = default;

TextStyle &TextStyle::operator=(TextStyle &&other) = default;

TextStyle::~TextStyle() = default;

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
