#include "milestro_game_retcode.h"
#include "Milestro/skia/Font.h"
#include <Milestro/game/milestro_game_interface.h>
#include <cstdint>

extern "C" {

int64_t MilestroSkiaFontDestroy(milestro::skia::Font *&ret) try {
    delete ret;
    ret = nullptr;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaFontGetPath(milestro::skia::Font *font,
                                milestro::skia::Path *&path,
                                uint16_t glyphId) try {
    auto info = font->getPath(glyphId);
    if (info == nullptr) {
        return MILESTRO_API_RET_GLYPH_NOTFOUND;
    }
    path = info;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaFontGetMetrics(milestro::skia::Font *font,
                                   float &ascent,
                                   float &descent,
                                   float &leading) try {
    if (font == nullptr) {
        return MILESTRO_API_RET_FAILED;
    }

    const auto metrics = font->getMetrics();
    ascent = metrics.fAscent;
    descent = metrics.fDescent;
    leading = metrics.fLeading;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaFontMeasureText(milestro::skia::Font *font,
                                    const uint8_t *text,
                                    uint64_t textSize,
                                    float &boundsLeft,
                                    float &boundsTop,
                                    float &boundsRight,
                                    float &boundsBottom,
                                    float &advanceX) try {
    if (font == nullptr) {
        return MILESTRO_API_RET_FAILED;
    }

    SkRect bounds = SkRect::MakeEmpty();
    advanceX = font->measureText(reinterpret_cast<const char *>(text), static_cast<size_t>(textSize), &bounds);
    boundsLeft = bounds.left();
    boundsTop = bounds.top();
    boundsRight = bounds.right();
    boundsBottom = bounds.bottom();
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}
}
