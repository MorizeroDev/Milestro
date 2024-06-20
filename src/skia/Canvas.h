#ifndef MILESTRO_SKIA_CANVAS_H
#define MILESTRO_SKIA_CANVAS_H

#include <include/core/SkRefCnt.h>
#include <include/core/SkColor.h>
#include <include/core/SkBitmap.h>
#include "include/core/SkCanvas.h"
#include "Milestro/util/milestro_class.h"

namespace milestro::skia {

class Canvas {
public:
    Canvas(int width, int height) {
        imageInfo = SkImageInfo::MakeN32Premul(width, height);
        bitmap.allocPixels();
        canvas = std::make_unique<SkCanvas>(bitmap);
    }

    bool GetTexture(void *targetSpace) {
        return bitmap.readPixels(
            imageInfo,
            targetSpace,
            bitmap.rowBytes(),
            0, 0);
    }

    MILESTRO_DECLARE_NON_COPYABLE(Canvas)

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
