#include <Milestro/game/milestro_game_interface.h>
#include <Milestro/log/log.h>
#include "milestro_game_retcode.h"
#include "Milestro/skia/textlayout/ParagraphStyle.h"

extern "C" {

int64_t MilestroSkiaTextlayoutParagraphStyleCreate(milestro::skia::textlayout::ParagraphStyle *&ret) try {
    ret = new milestro::skia::textlayout::ParagraphStyle();
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutParagraphStyleDestroy(milestro::skia::textlayout::ParagraphStyle *&ret) try {
    delete ret;
    ret = nullptr;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutParagraphStyleGetStrutStyle(milestro::skia::textlayout::ParagraphStyle *s,
                                                          milestro::skia::textlayout::StrutStyle *&strutStyle
) {
    strutStyle = s->getStrutStyle();
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutParagraphStyleSetStrutStyle(milestro::skia::textlayout::ParagraphStyle *s,
                                                          milestro::skia::textlayout::StrutStyle *strutStyle
) {
    s->setStrutStyle(strutStyle);
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutParagraphStyleGetTextStyle(milestro::skia::textlayout::ParagraphStyle *s,
                                                         milestro::skia::textlayout::TextStyle *&textStyle
) {
    textStyle = s->getTextStyle();
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutParagraphStyleSetTextStyle(milestro::skia::textlayout::ParagraphStyle *s,
                                                         milestro::skia::textlayout::TextStyle *textStyle
) {
    s->setTextStyle(textStyle);
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutParagraphStyleGetTextDirection(milestro::skia::textlayout::ParagraphStyle *s,
                                                             int32_t &direction
) {
    direction = (int32_t) s->getTextDirection();
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutParagraphStyleSetTextDirection(milestro::skia::textlayout::ParagraphStyle *s,
                                                             int32_t direction
) {
    s->setTextDirection((::skia::textlayout::TextDirection) direction);
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutParagraphStyleGetTextAlign(milestro::skia::textlayout::ParagraphStyle *s,
                                                         int32_t &align
) {
    align = (int32_t) s->getTextAlign();
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutParagraphStyleSetTextAlign(milestro::skia::textlayout::ParagraphStyle *s,
                                                         int32_t align
) {
    s->setTextAlign((::skia::textlayout::TextAlign) align);
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutParagraphStyleGetMaxLines(milestro::skia::textlayout::ParagraphStyle *s,
                                                        uint64_t &maxLines
) {
    maxLines = s->getMaxLines();
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutParagraphStyleSetMaxLines(milestro::skia::textlayout::ParagraphStyle *s,
                                                        uint64_t maxLines
) {
    s->setMaxLines(maxLines);
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutParagraphStyleSetEllipsis(milestro::skia::textlayout::ParagraphStyle *s,
                                                        uint8_t *ellipsis
) {
    s->setEllipsis(SkString(reinterpret_cast<const char *>(ellipsis)));
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutParagraphStyleGetHeight(milestro::skia::textlayout::ParagraphStyle *s,
                                                      float &height) {
    height = s->getHeight();
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutParagraphStyleSetHeight(milestro::skia::textlayout::ParagraphStyle *s,
                                                      float height) {
    s->setHeight(height);
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutParagraphStyleGetTextHeightBehavior(milestro::skia::textlayout::ParagraphStyle *s,
                                                                  int32_t &behavior) {
    behavior = (int32_t) s->getTextHeightBehavior();
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutParagraphStyleSetTextHeightBehavior(milestro::skia::textlayout::ParagraphStyle *s,
                                                                  int32_t behavior) {
    s->setTextHeightBehavior((::skia::textlayout::TextHeightBehavior) behavior);
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutParagraphStyleIsUnlimitedLines(milestro::skia::textlayout::ParagraphStyle *s,
                                                             int32_t &unlimitedLines
) {
    unlimitedLines = s->unlimited_lines() ? 1 : 0;
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutParagraphStyleIsEllipsized(milestro::skia::textlayout::ParagraphStyle *s,
                                                         int32_t &isEllipsized
) {
    isEllipsized = s->ellipsized() ? 1 : 0;
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutParagraphStyleGetEffectiveAlign(milestro::skia::textlayout::ParagraphStyle *s,
                                                              int32_t &effectiveAlign
) {
    effectiveAlign = (int32_t) s->effective_align();
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutParagraphStyleIsHintingOn(milestro::skia::textlayout::ParagraphStyle *s,
                                                        int32_t &hintingIsOn
) {
    hintingIsOn = s->hintingIsOn() ? 1 : 0;
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutParagraphStyleTurnHintingOff(milestro::skia::textlayout::ParagraphStyle *s
) {
    s->turnHintingOff();
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutParagraphStyleGetReplaceTabCharacters(milestro::skia::textlayout::ParagraphStyle *s,
                                                                    int32_t &v) {
    v = s->getReplaceTabCharacters() ? 1 : 0;
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutParagraphStyleSetReplaceTabCharacters(milestro::skia::textlayout::ParagraphStyle *s,
                                                                    int32_t v) {
    s->setReplaceTabCharacters(v != 0);
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutParagraphStyleGetApplyRoundingHack(milestro::skia::textlayout::ParagraphStyle *s,
                                                                 int32_t &v) {
    v = s->getApplyRoundingHack() ? 1 : 0;
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutParagraphStyleSetApplyRoundingHack(milestro::skia::textlayout::ParagraphStyle *s,
                                                                 int32_t v) {
    s->setApplyRoundingHack(v != 0);
    return MILESTRO_API_RET_OK;
}

}
