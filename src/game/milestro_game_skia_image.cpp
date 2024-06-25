#include <Milestro/game/milestro_game_interface.h>
#include "Milestro/skia/Image.h"
#include "milestro_game_retcode.h"

extern "C" {

int64_t MilestroSkiaImageCreate(milestro::skia::Image *&ret, void *data, uint64_t size) try {
    ret = new milestro::skia::Image(data, size);
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaImageSetColorType(milestro::skia::Image *img, int32_t targetColorType) try {
    img->SetColorType((SkColorType) targetColorType);
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaImageGetWidth(milestro::skia::Image *img, int32_t &width) try {
    width = img->GetWidth();
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaImageGetHeight(milestro::skia::Image *img, int32_t &height) try {
    height = img->GetHeight();
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaImageDestroy(milestro::skia::Image *&ret) try {
    delete ret;
    ret = nullptr;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

}
