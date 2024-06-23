#include <Milestro/game/milestro_game_interface.h>
#include "Milestro/skia/FontManager.h"
#include "milestro_game_retcode.h"
#include "Milestro/util/milestro_strutil.h"

extern "C" {
int64_t MilestroSkiaFontManagerRegisterFontFromFile(uint8_t *path) {
    auto fontMgr = milestro::skia::GetFontManager();
    return (int64_t) fontMgr->RegisterFontFromFile(reinterpret_cast<char *>(path));
}

int64_t MilestroSkiaFontManagerRegisterFontFromMemory(uint8_t *fileName, uint8_t *data, uint64_t length) {
    auto fontMgr = milestro::skia::GetFontManager();
    return (int64_t) fontMgr->RegisterFontFromMemory(reinterpret_cast<char *>(fileName), data, length);
}

int64_t MilestroSkiaFontManagerGetFontFamilies(uint8_t *buffer,
                                               uint64_t bufferSize,
                                               uint64_t &needed) {
    auto fontMgr = milestro::skia::GetFontManager();
    auto info = fontMgr->GetFamiliesNames();
    auto result = milestro::util::serialization::vectorToJson(info).dump();
    needed = result.size();
    return static_cast<int64_t>(milestro::util::copyStringToBuffer(result, buffer, bufferSize));
}

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
