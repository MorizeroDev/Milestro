#ifndef MILESTRO_SKIA_CANVAS_H
#define MILESTRO_SKIA_CANVAS_H

#include "include/core/SkRefCnt.h"
#include "include/core/SkColor.h"
#include "include/core/SkBitmap.h"
#include "include/core/SkCanvas.h"
#include "Milestro/util/milestro_class.h"
#include "Milestro/common/milestro_export_macros.h"

#ifdef MILESTRO_USE_CLI
#include "include/encode/SkPngEncoder.h"
#include "include/core/SkStream.h"
#endif

namespace milestro::skia {

class MILESTRO_API Canvas {
public:
    Canvas(int width, int height) {
        imageInfo = SkImageInfo::MakeN32Premul(width, height);
        bitmap.allocPixels(imageInfo);
        canvas = std::make_unique<SkCanvas>(bitmap);
    }

    MILESTRO_DECLARE_NON_COPYABLE(Canvas)

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
