#include "Milestro/skia/FontManager.h"
#include "milestro_game_retcode.h"
#include "Milestro/skia/Font.h"
#include <Milestro/game/milestro_game_interface.h>

extern "C" {
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
}
