#include "Milestro/skia/textlayout/ParagraphStyle.h"

namespace milestro::skia::textlayout {

ParagraphStyle::ParagraphStyle() {
    // Chromium's default `text-spacing-trim: normal` uses the OpenType
    // `chws` feature to collapse adjacent full-width CJK punctuation. Keep
    // this on the paragraph's inherited style so every run participates in
    // the same shaping pass (including runs that do not set a local style).
    auto textStyle = style.getTextStyle();
    textStyle.addFontFeature(SkString("chws"), 1);
    style.setTextStyle(std::move(textStyle));
}

ParagraphStyle::ParagraphStyle(const ParagraphStyle &other) = default;

ParagraphStyle::ParagraphStyle(ParagraphStyle &&other) = default;

ParagraphStyle &ParagraphStyle::operator=(const ParagraphStyle &other) = default;

ParagraphStyle &ParagraphStyle::operator=(ParagraphStyle &&other) = default;

ParagraphStyle::~ParagraphStyle() = default;

TextStyle *ParagraphStyle::getTextStyle() const {
    auto ret = new TextStyle(style.getTextStyle());
    if (hasTextFontFamilyDeclaration) {
        ret->setFontFamilyTokens(textFontFamilyTokens);
    }
    return ret;
}

void ParagraphStyle::setTextStyle(TextStyle *textStyle) {
    style.setTextStyle(textStyle->spawn());
    textFontFamilyTokens = textStyle->getFontFamilyTokens();
    hasTextFontFamilyDeclaration = textStyle->hasFontFamilyTokens();
}

std::string ParagraphStyle::getEllipsisUtf8() const {
    const auto ellipsis = style.getEllipsis();
    return std::string(ellipsis.c_str(), ellipsis.size());
}

void ParagraphStyle::setEllipsis(const SkString &ellipsis) {
    style.setEllipsis(SkString(ellipsis.c_str(), ellipsis.size()));
}

const ::skia::textlayout::ParagraphStyle ParagraphStyle::unwrap() const {
    return style;
}

}
