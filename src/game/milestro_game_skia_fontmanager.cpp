#include <Milestro/game/milestro_game_interface.h>
#include <Milestro/log/log.h>
#include "skia/FontManager.h"
#include "milestro_game_retcode.h"

extern "C" {
int64_t MilestroSkiaFontManagerRegisterFont(uint8_t *path) {
    auto fontMgr = milestro::skia::GetFontManager();
    fontMgr->RegisterFont(reinterpret_cast<char *>(path));
    return MILESTRO_API_RET_OK;
}
}
