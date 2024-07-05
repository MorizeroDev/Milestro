#ifndef MILESTRO_SKIA_SVG_H
#define MILESTRO_SKIA_SVG_H

#include <include/core/SkStream.h>
#include <modules/svg/include/SkSVGDOM.h>
#include "Milestro/util/milestro_class.h"
#include "Milestro/util/milestro_serializerable.h"
#include "Milestro/common/milestro_export_macros.h"
#include "Canvas.h"
#include "FontManager.h"

namespace milestro::skia {

class MILESTRO_API Svg {
public:
    explicit Svg(std::unique_ptr<SkMemoryStream> data) {
        this->data = std::move(data);
        auto fontMgr= GetFontManager()->GetFontMgr();
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

private:
    std::unique_ptr<SkMemoryStream> data;
    sk_sp<SkSVGDOM> svg;
};

}

#endif //MILESTRO_SKIA_SVG_H
