#include <Milestro/game/milestro_game_interface.h>
#include <Milestro/log/log.h>
#include "skia/FontManager.h"
#include "milestro_game_retcode.h"
#include "Milestro/util/milestro_strutil.h"

extern "C" {
int64_t MilestroSkiaFontManagerRegisterFont(uint8_t *path, milestro::skia::TypeFace *&typeFace) {
    auto fontMgr = milestro::skia::GetFontManager();
    typeFace = fontMgr->RegisterFont(reinterpret_cast<char *>(path));
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaTypeFaceDestroy(milestro::skia::TypeFace *&ret) try {
    delete ret;
    ret = nullptr;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTypeFaceGetFamilyNames(milestro::skia::TypeFace *typeFace,
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
