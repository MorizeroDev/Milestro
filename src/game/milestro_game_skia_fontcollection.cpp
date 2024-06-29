#include <Milestro/game/milestro_game_interface.h>
#include "Milestro/skia/FontManager.h"
#include "milestro_game_retcode.h"
#include "Milestro/util/milestro_strutil.h"
#include "Milestro/skia/textlayout/FontCollection.h"

extern "C" {
int64_t MilestroSkiaFontCollectionClearCaches() {
    auto fc = milestro::skia::textlayout::GetFontCollection();
    fc->clearCaches();
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaFontCollectionIsFontFallbackEnabled(int32_t &enabled) {
    auto fc = milestro::skia::textlayout::GetFontCollection();
    enabled = fc->fontFallbackEnabled() ? 1 : 0;
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaFontCollectionSetFontFallbackEnabled(int32_t enabled) {
    auto fc = milestro::skia::textlayout::GetFontCollection();
    fc->setFontFallbackEnabled(enabled != 0);
    return MILESTRO_API_RET_OK;
}
}
