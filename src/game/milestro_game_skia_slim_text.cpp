#include "Milestro/skia/Font.h"
#include "milestro_game_retcode.h"
#include <Milestro/game/milestro_game_interface.h>
#include <include/core/SkColor.h>
#include <cstdint>
#include <string>

extern "C" {

int64_t MilestroSkiaTextDrawSnapshotCreate(milestro::skia::TextDrawSnapshot *&ret,
                                           milestro::skia::Font *font,
                                           uint8_t *text,
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

int64_t MilestroSkiaTextDrawSnapshotDestroy(milestro::skia::TextDrawSnapshot *&ret) try {
    delete ret;
    ret = nullptr;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

}
