#ifndef MILESTRO_SKIA_IMAGE_H
#define MILESTRO_SKIA_IMAGE_H

#include <cstddef>
#include <stdexcept>
#include <include/core/SkData.h>
#include <include/core/SkColorSpace.h>
#include <include/core/SkStream.h>
#include <include/core/SkAlphaType.h>
#include <include/core/SkImage.h>
#include "Milestro/util/milestro_class.h"
#include "Milestro/log/log.h"

namespace milestro::skia {

class MILESTRO_API Image {
public:
    Image(void *data, size_t size) {
        imageData = std::make_unique<SkMemoryStream>(data, size, true);
        imageSize = size;

        auto imageStream = SkData::MakeFromStream(imageData.get(), size);
        skImage = SkImages::DeferredFromEncodedData(imageStream, SkAlphaType::kUnpremul_SkAlphaType);

        if (!skImage) {
            MILESTROLOG_ERROR("fail to create SkImage");
            throw std::runtime_error("fail to create SkImage");
        }
    }

    MILESTRO_DECLARE_NON_COPYABLE(Image)

    void SetColorType(SkColorType targetColorType) {
        skImage = skImage->makeColorTypeAndColorSpace(nullptr, targetColorType, SkColorSpace::MakeSRGB());
        if (!skImage) {
            MILESTROLOG_ERROR("fail to set color type");
            throw std::runtime_error("fail to set color type");
        }
    }

    int GetWidth() {
        return skImage->width();
    }

    int GetHeight() {
        return skImage->height();
    }

    sk_sp<SkImage> unwrap() {
        return skImage;
    }

private:
    std::unique_ptr<SkMemoryStream> imageData;
    size_t imageSize;
    sk_sp<SkImage> skImage;
};
}
#endif //MILESTRO_SRC_SKIA_IMAGE_H
