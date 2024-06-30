#ifndef MILESTRO_SKIA_CANVAS_H
#define MILESTRO_SKIA_CANVAS_H

#include "include/core/SkRefCnt.h"
#include "include/core/SkColor.h"
#include "include/core/SkBitmap.h"
#include "include/core/SkCanvas.h"
#include "Milestro/util/milestro_class.h"
#include "Milestro/common/milestro_export_macros.h"
#include "Image.h"

#ifdef MILESTRO_USE_CLI
#include "include/encode/SkPngEncoder.h"
#include "include/core/SkStream.h"
#endif

namespace milestro::skia {

class MILESTRO_API Canvas {
public:
    Canvas(int width, int height, void *pixels, bool singleChannel = false) {
        imageInfo = SkImageInfo::MakeN32Premul(width, height);
//        size_t size = 0;
        if (singleChannel) {
            imageInfo = imageInfo.makeColorType(kAlpha_8_SkColorType);
//            size = width * height * 1;
        } else {
            imageInfo = imageInfo.makeColorType(kRGBA_8888_SkColorType);
//            size = width * height * 4;
        }

        if (pixels != nullptr) {
            bitmap.installPixels(imageInfo, pixels, imageInfo.minRowBytes());
        } else {
            bitmap.allocPixels(imageInfo);
        }
        canvas = std::make_unique<SkCanvas>(bitmap);
    }

    MILESTRO_DECLARE_NON_COPYABLE(Canvas)

    void DrawImageSimple(Image *image, float x, float y) {
        sk_sp<SkImage> img = image->unwrap();
        canvas->drawImage(img, x, y);
    }

    void DrawImage(Image *image,
                   float srcLeft, float srcTop, float srcRight, float srcBottom,
                   float dstLeft, float dstTop, float dstRight, float dstBottom
    ) {
        sk_sp<SkImage> img = image->unwrap();
        SkRect src{
            .fLeft =srcLeft,
            .fTop = srcTop,
            .fRight = srcRight,
            .fBottom = srcBottom,
        };
        SkRect dst{
            .fLeft =dstLeft,
            .fTop = dstTop,
            .fRight = dstRight,
            .fBottom = dstBottom,
        };
        SkSamplingOptions sampling(SkCubicResampler::CatmullRom());
        canvas->drawImageRect(img, src, dst, sampling, nullptr, SkCanvas::SrcRectConstraint::kFast_SrcRectConstraint);
    }

    bool GetTexture(void *targetSpace) {
        return bitmap.readPixels(
            imageInfo,
            targetSpace,
            bitmap.rowBytes(),
            0, 0);
    }

#ifdef MILESTRO_USE_CLI
    bool SaveToPng(const char *path) {
        SkPixmap pixmap;
        if (canvas->peekPixels(&pixmap)) {
            SkFILEWStream file(path);
            return file.isValid() && SkPngEncoder::Encode(&file, pixmap, {});
        }
        return false;
    }
#endif

    SkCanvas *unwrap() {
        return canvas.get();
    }
private:
    SkImageInfo imageInfo;
    SkBitmap bitmap;
    std::unique_ptr<SkCanvas> canvas;
};

}

#endif //MILESTRO_SKIA_CANVAS_H
