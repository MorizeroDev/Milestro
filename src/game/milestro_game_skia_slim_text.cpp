#include "Milestro/skia/Font.h"
#include "Milestro/skia/ReusableTextDrawSnapshot.h"
#include "Milestro/skia/TextDrawSnapshot.h"
#include "milestro_game_retcode.h"
#include <Milestro/game/milestro_game_interface.h>
#include <include/core/SkColor.h>
#include <cstdint>
#include <string>

extern "C" {

int64_t MilestroSkiaTextDrawSnapshotCreate(milestro::skia::SlimTextDrawSnapshot *&ret,
                                           milestro::skia::Font *font,
                                           const uint8_t *text,
                                           uint64_t textSize,
                                           int32_t r,
                                           int32_t g,
                                           int32_t b,
                                           int32_t a) try {
    if (font == nullptr) {
        return MILESTRO_API_RET_FAILED;
    }

    std::string textString;
    if (text != nullptr && textSize > 0) {
        textString.assign(reinterpret_cast<const char *>(text), static_cast<size_t>(textSize));
    }

    ret = new milestro::skia::TextDrawSnapshot(*font,
                                               std::move(textString),
                                               SkColorSetARGB(a, r, g, b));
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextDrawSnapshotDestroy(milestro::skia::SlimTextDrawSnapshot *&ret) try {
    delete ret;
    ret = nullptr;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaReusableTextDrawSnapshotCreate(milestro::skia::SlimTextDrawSnapshot *&ret,
                                                   milestro::skia::Font *font,
                                                   uint64_t capacity,
                                                   int32_t r,
                                                   int32_t g,
                                                   int32_t b,
                                                   int32_t a) try {
    if (font == nullptr) {
        return MILESTRO_API_RET_FAILED;
    }

    ret = new milestro::skia::ReusableTextDrawSnapshot(*font,
                                                       static_cast<size_t>(capacity),
                                                       SkColorSetARGB(a, r, g, b));
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaReusableTextDrawSnapshotUpdateText(milestro::skia::SlimTextDrawSnapshot *ret,
                                                       const uint8_t *text,
                                                       uint64_t textSize) try {
    if (ret == nullptr) {
        return MILESTRO_API_RET_FAILED;
    }

    return ret->updateText(reinterpret_cast<const char *>(text), static_cast<size_t>(textSize))
               ? MILESTRO_API_RET_OK
               : MILESTRO_API_RET_FAILED;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaReusableTextDrawSnapshotCopyTextFrom(milestro::skia::SlimTextDrawSnapshot *ret,
                                                         milestro::skia::SlimTextDrawSnapshot *source) try {
    if (ret == nullptr || source == nullptr) {
        return MILESTRO_API_RET_FAILED;
    }

    return ret->copyTextFrom(*source) ? MILESTRO_API_RET_OK : MILESTRO_API_RET_FAILED;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaReusableTextDrawSnapshotMeasureText(milestro::skia::SlimTextDrawSnapshot *ret,
                                                        float &boundsLeft,
                                                        float &boundsTop,
                                                        float &boundsRight,
                                                        float &boundsBottom,
                                                        float &advanceX) try {
    if (ret == nullptr) {
        return MILESTRO_API_RET_FAILED;
    }

    SkRect bounds = SkRect::MakeEmpty();
    advanceX = ret->measureText(&bounds);
    boundsLeft = bounds.left();
    boundsTop = bounds.top();
    boundsRight = bounds.right();
    boundsBottom = bounds.bottom();
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaReusableTextDrawSnapshotDestroy(milestro::skia::SlimTextDrawSnapshot *&ret) try {
    delete ret;
    ret = nullptr;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

}
