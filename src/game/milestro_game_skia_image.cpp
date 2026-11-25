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

int64_t MilestroSkiaImageReadRgba(milestro::skia::Image* img, uint8_t* pixels, uint64_t size) try {
    if (img == nullptr || pixels == nullptr) return MILESTRO_API_RET_FAILED;
    auto image = img->unwrap();
    if (!image) return MILESTRO_API_RET_FAILED;
    const auto info = SkImageInfo::Make(image->width(), image->height(), kRGBA_8888_SkColorType,
                                      kUnpremul_SkAlphaType, SkColorSpace::MakeSRGB());
    const auto rowBytes = info.minRowBytes();
    if (info.computeByteSize(rowBytes) > size) return MILESTRO_API_RET_FAILED;
    return image->readPixels(nullptr, info, pixels, rowBytes, 0, 0)
        ? MILESTRO_API_RET_OK : MILESTRO_API_RET_FAILED;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

}
