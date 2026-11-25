#ifndef MILESTRO_SKIA_SVG_H
#define MILESTRO_SKIA_SVG_H

#include <include/core/SkStream.h>
#include <modules/svg/include/SkSVGDOM.h>
#include <modules/svg/include/SkSVGSVG.h>
#include <modules/svg/include/SkSVGRenderContext.h>
#include <include/core/SkSurface.h>
#include <cmath>
#include "Milestro/util/milestro_class.h"
#include "Milestro/util/milestro_serializerable.h"
#include "Milestro/common/milestro_export_macros.h"
#include "Canvas.h"
#include "FontRegistry.h"

namespace milestro::skia {

class MILESTRO_API Svg {
public:
    explicit Svg(std::unique_ptr<SkMemoryStream> data) {
        this->data = std::move(data);
        auto fontMgr = GetFontRegistry()->GetRegisteredFontMgr();
        this->svg = SkSVGDOM::Builder()
                .setFontManager(fontMgr)
                .make(*this->data);
        if (!this->svg) {
            throw std::runtime_error("fail to parse svg");
        }
    }

    MILESTRO_DECLARE_NON_COPYABLE(Svg)

    void render(Canvas *canvas) {
        svg->render(canvas->unwrap());
    }

    sk_sp<SkImage> rasterize() {
        auto root = svg->getRoot();
        auto size = root->intrinsicSize(SkSVGLengthContext(SkSize::Make(0, 0)));
        if (size.width() <= 0 || size.height() <= 0) {
            const auto& viewBox = root->getViewBox();
            if (viewBox.has_value() && viewBox->width() > 0 && viewBox->height() > 0) {
                size = SkSize::Make(viewBox->width(), viewBox->height());
            } else {
                size = SkSize::Make(300, 150);
            }
        }
        if (!SkIsFinite(size.width()) || !SkIsFinite(size.height()) ||
            size.width() > 16384 || size.height() > 16384) {
            throw std::invalid_argument("unsupported SVG raster dimensions");
        }
        svg->setContainerSize(size);
        auto surface = SkSurfaces::Raster(SkImageInfo::MakeN32Premul(
            static_cast<int>(std::ceil(size.width())), static_cast<int>(std::ceil(size.height()))));
        if (!surface) throw std::runtime_error("failed to allocate SVG raster surface");
        surface->getCanvas()->clear(SK_ColorTRANSPARENT);
        svg->render(surface->getCanvas());
        return surface->makeImageSnapshot();
    }

private:
    std::unique_ptr<SkMemoryStream> data;
    sk_sp<SkSVGDOM> svg;
};

}

#endif //MILESTRO_SKIA_SVG_H
