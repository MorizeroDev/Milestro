#include <Milestro/game/milestro_game_interface.h>
#include <Milestro/log/log.h>
#include "Milestro/skia/Canvas.h"
#include "milestro_game_retcode.h"

extern "C" {
int64_t MilestroSkiaCanvasCreate(milestro::skia::Canvas *&ret, int32_t width, int32_t height) try {
    ret = new milestro::skia::Canvas(width, height);
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaCanvasDestroy(milestro::skia::Canvas *&ret) try {
    delete ret;
    ret = nullptr;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaCanvasGetTexture(milestro::skia::Canvas *ret, void *targetSpace) try {
    auto result = ret->GetTexture(targetSpace);
    return result ? MILESTRO_API_RET_OK : MILESTRO_API_RET_FAILED;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

}
