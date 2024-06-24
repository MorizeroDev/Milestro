#ifndef MILESTRO_SKIA_TEXTLAYOUT_PARAGRAPHSTYLE_H
#define MILESTRO_SKIA_TEXTLAYOUT_PARAGRAPHSTYLE_H

#include "modules/skparagraph/include/ParagraphStyle.h"
#include "TextStyle.h"
#include "Milestro/common/milestro_export_macros.h"

namespace milestro::skia::textlayout {

class MILESTRO_API StrutStyle {
public:
    StrutStyle() {}

    explicit StrutStyle(::skia::textlayout::StrutStyle style) {
        this->style = style;
    }

    const std::vector<SkString> &getFontFamilies() const { return style.getFontFamilies(); }
    void setFontFamilies(std::vector<SkString> families) { style.setFontFamilies(std::move(families)); }

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

    const ::skia::textlayout::StrutStyle spawn() {
        return style;
    }

private:
    ::skia::textlayout::StrutStyle style;
};

class MILESTRO_API ParagraphStyle {
public:
    StrutStyle *getStrutStyle() const { return new StrutStyle(style.getStrutStyle()); }
    void setStrutStyle(StrutStyle *strutStyle) { style.setStrutStyle(strutStyle->spawn()); }

    TextStyle *getTextStyle() const { return new TextStyle(style.getTextStyle()); }
    void setTextStyle(TextStyle *textStyle) { style.setTextStyle(textStyle->spawn()); }

    ::skia::textlayout::TextDirection getTextDirection() const { return style.getTextDirection(); }
    void setTextDirection(::skia::textlayout::TextDirection direction) { style.setTextDirection(direction); }

    ::skia::textlayout::TextAlign getTextAlign() const { return style.getTextAlign(); }
    void setTextAlign(::skia::textlayout::TextAlign align) { style.setTextAlign(align); }

    size_t getMaxLines() const { return style.getMaxLines(); }
    void setMaxLines(size_t maxLines) { style.setMaxLines(maxLines); }

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

public:
    const ::skia::textlayout::ParagraphStyle unwrap() {
        return style;
    }
};
}

#endif //MILESTRO_TEXTLAYOUT_PARAGRAPHS_H
