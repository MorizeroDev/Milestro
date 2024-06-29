#include <Milestro/game/milestro_game_interface.h>
#include "Milestro/skia/FontManager.h"
#include "milestro_game_retcode.h"
#include "Milestro/util/milestro_strutil.h"
#include "Milestro/skia/textlayout/FontCollection.h"

extern "C" {
int64_t MilestroSkiaTypefaceDestroy(milestro::skia::Typeface *&ret) try {
    delete ret;
    ret = nullptr;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTypefaceGetFamilyNames(milestro::skia::Typeface *typeFace,
                                           uint8_t *buffer,
                                           uint64_t bufferSize,
                                           uint64_t &needed) try {
    auto info = typeFace->GetFamilyNames();
    auto result = milestro::util::serialization::vectorToJson(info).dump();
    needed = result.size();
    return static_cast<int64_t>(milestro::util::copyStringToBuffer(result, buffer, bufferSize));
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}
}
