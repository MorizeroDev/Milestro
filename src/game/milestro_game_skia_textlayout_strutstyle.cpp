#include <Milestro/game/milestro_game_interface.h>
#include <Milestro/log/log.h>
#include "milestro_game_retcode.h"
#include "Milestro/skia/textlayout/ParagraphStyle.h"

extern "C" {
int64_t MilestroSkiaTextlayoutStrutStyleCreate(milestro::skia::textlayout::StrutStyle *&ret) try {
    ret = new milestro::skia::textlayout::StrutStyle();
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutStrutStyleDestroy(milestro::skia::textlayout::StrutStyle *&ret) try {
    delete ret;
    ret = nullptr;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutStrutStyleSetFontFamilies(milestro::skia::textlayout::StrutStyle *s,
                                                         uint8_t **families,
                                                         uint32_t size) {
    std::vector<SkString> fontFamilies(size);
    for (int i = 0; i < size; i++) {
        fontFamilies[i] = SkString(reinterpret_cast<const char *>(families[i]));
    }
    s->setFontFamilies(std::move(fontFamilies));
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutStrutStyleSetFontStyle(milestro::skia::textlayout::StrutStyle *s,
                                                      int32_t weight, int32_t width, int32_t slant) {
    SkFontStyle style(weight, width, (SkFontStyle::Slant) slant);
    s->setFontStyle(style);
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutStrutStyleGetFontStyle(milestro::skia::textlayout::StrutStyle *s,
                                                      int32_t &weight, int32_t &width, int32_t &slant) {
    auto style = s->getFontStyle();
    weight = style.weight();
    width = style.width();
    slant = style.slant();
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutStrutStyleSetFontSize(milestro::skia::textlayout::StrutStyle *s,
                                                     float size) {
    s->setFontSize(size);
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutStrutStyleGetFontSize(milestro::skia::textlayout::StrutStyle *s,
                                                     float &size) {
    size = s->getFontSize();
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutStrutStyleSetHeight(milestro::skia::textlayout::StrutStyle *s,
                                                   float height) {
    s->setHeight(height);
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutStrutStyleGetHeight(milestro::skia::textlayout::StrutStyle *s,
                                                   float &height) {
    height = s->getHeight();
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutStrutStyleSetLeading(milestro::skia::textlayout::StrutStyle *s,
                                                    float leading) {
    s->setLeading(leading);
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutStrutStyleGetLeading(milestro::skia::textlayout::StrutStyle *s,
                                                    float &leading) {
    leading = s->getLeading();
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutStrutStyleSetStrutEnabled(milestro::skia::textlayout::StrutStyle *s,
                                                          int32_t v) {
    s->setStrutEnabled(v != 0);
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutStrutStyleGetStrutEnabled(milestro::skia::textlayout::StrutStyle *s,
                                                          int32_t &v) {
    v = s->getStrutEnabled() ? 1 : 0;
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutStrutStyleSetForceStrutHeight(milestro::skia::textlayout::StrutStyle *s,
                                                             int32_t v) {
    s->setForceStrutHeight(v != 0);
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutStrutStyleGetForceStrutHeight(milestro::skia::textlayout::StrutStyle *s,
                                                             int32_t &v) {
    v = s->getForceStrutHeight() ? 1 : 0;
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutStrutStyleSetHeightOverride(milestro::skia::textlayout::StrutStyle *s,
                                                           int32_t v) {
    s->setHeightOverride(v != 0);
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutStrutStyleGetHeightOverride(milestro::skia::textlayout::StrutStyle *s,
                                                           int32_t &v) {
    v = s->getHeightOverride() ? 1 : 0;
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutStrutStyleSetHalfLeading(milestro::skia::textlayout::StrutStyle *s,
                                                        int32_t halfLeading) {
    s->setHalfLeading(halfLeading != 0);
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTextlayoutStrutStyleGetHalfLeading(milestro::skia::textlayout::StrutStyle *s,
                                                        int32_t &halfLeading) {
    halfLeading = s->getHalfLeading() ? 1 : 0;
    return MILESTRO_API_RET_OK;
}
}
