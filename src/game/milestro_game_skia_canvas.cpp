#include "Milestro/skia/Canvas.h"
#include "milestro_game_retcode.h"
#include <Milestro/game/milestro_game_interface.h>

extern "C" {
int64_t MilestroSkiaCanvasCreate(milestro::skia::Canvas* &ret,
                                 int32_t width,
                                 int32_t height) try {
    ret = new milestro::skia::Canvas(width, height, nullptr, false, false);
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaCanvasCreateWithMemory(milestro::skia::Canvas* &ret,
                                           int32_t width,
                                           int32_t height,
                                           void *pixels,
                                           int64_t verticalFlip,
                                           int64_t clearPixels) try {
    if (clearPixels != 0) {
        memset(pixels, 0, width * height * 4);
    }
    ret = new milestro::skia::Canvas(width, height, pixels, false, verticalFlip != 0);
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaCanvasDestroy(milestro::skia::Canvas*& ret) try {
    delete ret;
    ret = nullptr;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaCanvasGetTexture(milestro::skia::Canvas* ret, void* targetSpace) try {
    auto result = ret->GetTexture(targetSpace);
    return result ? MILESTRO_API_RET_OK : MILESTRO_API_RET_FAILED;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t
MilestroSkiaCanvasDrawImageSimple(milestro::skia::Canvas* ret, milestro::skia::Image* image, float x, float y) try {
    ret->DrawImageSimple(image, x, y);
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaCanvasDrawImage(milestro::skia::Canvas* ret,
                                    milestro::skia::Image* image,
                                    float srcLeft,
                                    float srcTop,
                                    float srcRight,
                                    float srcBottom,
                                    float dstLeft,
                                    float dstTop,
                                    float dstRight,
                                    float dstBottom) try {
    ret->DrawImage(image, srcLeft, srcTop, srcRight, srcBottom, dstLeft, dstTop, dstRight, dstBottom);
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}
}
