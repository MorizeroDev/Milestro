#ifndef MILESTRO_SKIA_TEXTLAYOUT_PARAGRAPHSTYLE_H
#define MILESTRO_SKIA_TEXTLAYOUT_PARAGRAPHSTYLE_H

#include "modules/skparagraph/include/ParagraphStyle.h"
#include "TextStyle.h"
#include "Milestro/common/milestro_export_macros.h"
#include "Milestro/skia/FontFamilyDeclaration.h"

#include <cstddef>
#include <limits>
#include <string>
#include <utility>
#include <vector>

namespace milestro::skia::textlayout {

class MILESTRO_API StrutStyle {
public:
    StrutStyle() {}

    explicit StrutStyle(::skia::textlayout::StrutStyle style) {
        this->style = std::move(style);
        setFontFamilyDeclarationFromSkiaFamilies(this->style.getFontFamilies(), false);
    }

    const std::vector<SkString> &getFontFamilies() const { return style.getFontFamilies(); }
    void setFontFamilies(std::vector<SkString> families) {
        setFontFamilyDeclarationFromSkiaFamilies(families, true);
        style.setFontFamilies(std::move(families));
    }
    void setFontFamilyTokens(std::vector<FontFamilyToken> families) {
        fontFamilyTokens = std::move(families);
        hasFontFamilyDeclaration = true;
        style.setFontFamilies(toSkiaFamilies(fontFamilyTokens));
    }
    const std::vector<FontFamilyToken> &getFontFamilyTokens() const { return fontFamilyTokens; }
    bool hasFontFamilyTokens() const { return hasFontFamilyDeclaration; }

    SkFontStyle getFontStyle() const { return style.getFontStyle(); }
    void setFontStyle(SkFontStyle fontStyle) { style.setFontStyle(fontStyle); }

    SkScalar getFontSize() const { return style.getFontSize(); }
    void setFontSize(SkScalar size) { style.setFontSize(size); }

    void setHeight(SkScalar height) { style.setHeight(height); }
    SkScalar getHeight() const { return style.getHeight(); }

    void setLeading(SkScalar Leading) { style.setLeading(Leading); }
    SkScalar getLeading() const { return style.getLeading(); }

    bool getStrutEnabled() const { return style.getStrutEnabled(); }
    void setStrutEnabled(bool v) { style.setStrutEnabled(v); }

    bool getForceStrutHeight() const { return style.getForceStrutHeight(); }
    void setForceStrutHeight(bool v) { style.setForceStrutHeight(v); }

    bool getHeightOverride() const { return style.getHeightOverride(); }
    void setHeightOverride(bool v) { style.setHeightOverride(v); }

    void setHalfLeading(bool halfLeading) { style.setHalfLeading(halfLeading); }
    bool getHalfLeading() const { return style.getHalfLeading(); }

    const ::skia::textlayout::StrutStyle spawn() const {
        return style;
    }

private:
    static std::vector<SkString> toSkiaFamilies(const std::vector<FontFamilyToken> &families) {
        std::vector<SkString> ret;
        ret.reserve(families.size());
        for (const auto &family: families) {
            ret.emplace_back(family.value.c_str());
        }

        return ret;
    }

    void setFontFamilyDeclarationFromSkiaFamilies(const std::vector<SkString> &families, bool declarationSet) {
        fontFamilyTokens.clear();
        fontFamilyTokens.reserve(families.size());
        for (const auto &family: families) {
            fontFamilyTokens.emplace_back(FontFamilyToken::Bare(family.c_str()));
        }

        hasFontFamilyDeclaration = declarationSet || !fontFamilyTokens.empty();
    }

    ::skia::textlayout::StrutStyle style;
    std::vector<FontFamilyToken> fontFamilyTokens;
    bool hasFontFamilyDeclaration = false;
};

class MILESTRO_API ParagraphStyle {
public:
    StrutStyle *getStrutStyle() const {
        auto ret = new StrutStyle(style.getStrutStyle());
        if (hasStrutFontFamilyDeclaration) {
            ret->setFontFamilyTokens(strutFontFamilyTokens);
        }
        return ret;
    }
    void setStrutStyle(StrutStyle *strutStyle) {
        style.setStrutStyle(strutStyle->spawn());
        strutFontFamilyTokens = strutStyle->getFontFamilyTokens();
        hasStrutFontFamilyDeclaration = strutStyle->hasFontFamilyTokens();
    }

    TextStyle *getTextStyle() const {
        auto ret = new TextStyle(style.getTextStyle());
        if (hasTextFontFamilyDeclaration) {
            ret->setFontFamilyTokens(textFontFamilyTokens);
        }
        return ret;
    }
    void setTextStyle(TextStyle *textStyle) {
        style.setTextStyle(textStyle->spawn());
        textFontFamilyTokens = textStyle->getFontFamilyTokens();
        hasTextFontFamilyDeclaration = textStyle->hasFontFamilyTokens();
    }

    const std::vector<FontFamilyToken> &getTextFontFamilyTokens() const { return textFontFamilyTokens; }
    bool hasTextFontFamilyTokens() const { return hasTextFontFamilyDeclaration; }
    const std::vector<FontFamilyToken> &getStrutFontFamilyTokens() const { return strutFontFamilyTokens; }
    bool hasStrutFontFamilyTokens() const { return hasStrutFontFamilyDeclaration; }

    ::skia::textlayout::TextDirection getTextDirection() const { return style.getTextDirection(); }
    void setTextDirection(::skia::textlayout::TextDirection direction) { style.setTextDirection(direction); }

    ::skia::textlayout::TextAlign getTextAlign() const { return style.getTextAlign(); }
    void setTextAlign(::skia::textlayout::TextAlign align) { style.setTextAlign(align); }

    size_t getMaxLines() const { return style.getMaxLines(); }
    void setMaxLines(size_t maxLines) { style.setMaxLines(maxLines); }
    void clearMaxLines() { style.setMaxLines(std::numeric_limits<size_t>::max()); }

    SkString getEllipsis() const { return style.getEllipsis(); }
//    std::u16string getEllipsisUtf16() const { return style.getEllipsisUtf16(); }

//    void setEllipsis(const std::u16string &ellipsis) { style.setEllipsis(ellipsis); }
    void setEllipsis(const SkString &ellipsis) { style.setEllipsis(ellipsis); }

    SkScalar getHeight() const { return style.getHeight(); }
    void setHeight(SkScalar height) { style.setHeight(height); }

    ::skia::textlayout::TextHeightBehavior getTextHeightBehavior() const { return style.getTextHeightBehavior(); }
    void setTextHeightBehavior(::skia::textlayout::TextHeightBehavior v) { style.setTextHeightBehavior(v); }

    bool unlimited_lines() const {
        return style.unlimited_lines();
    }
    bool ellipsized() const { return style.ellipsized(); }
    ::skia::textlayout::TextAlign effective_align() const { return style.effective_align(); }

    bool hintingIsOn() const { return style.hintingIsOn(); }
    void turnHintingOff() { style.turnHintingOff(); }

    bool getReplaceTabCharacters() const { return style.getReplaceTabCharacters(); }
    void setReplaceTabCharacters(bool value) { style.setReplaceTabCharacters(value); }

    bool getApplyRoundingHack() const { return style.getApplyRoundingHack(); }
    void setApplyRoundingHack(bool value) { style.setApplyRoundingHack(value); }

private:
    ::skia::textlayout::ParagraphStyle style;
    std::vector<FontFamilyToken> textFontFamilyTokens;
    std::vector<FontFamilyToken> strutFontFamilyTokens;
    bool hasTextFontFamilyDeclaration = false;
    bool hasStrutFontFamilyDeclaration = false;

public:
    const ::skia::textlayout::ParagraphStyle unwrap() const {
        return style;
    }
};
}

#endif //MILESTRO_TEXTLAYOUT_PARAGRAPHS_H
