#ifndef MILESTRO_SKIA_FONT_H
#define MILESTRO_SKIA_FONT_H

#include "Milestro/common/milestro_export_macros.h"
#include "Milestro/util/milestro_class.h"
#include "Path.h"
#include <include/core/SkFont.h>
#include <include/core/SkFontTypes.h>
#include <include/core/SkFontMetrics.h>
#include <include/core/SkRect.h>
#include <cstddef>
#include <utility>

namespace milestro::skia {

class MILESTRO_API Font {
public:
    explicit Font(SkFont font) : font(std::move(font)) {}

    MILESTRO_DECLARE_NON_COPYABLE(Font)

    SkFont unwrap() const {
        return font;
    }

    Path* getPath(SkGlyphID glyphID) {
        auto path = font.getPath(glyphID);
        return path ? new Path(*path) : nullptr;
    }

    SkFontMetrics getMetrics() const {
        SkFontMetrics metrics;
        font.getMetrics(&metrics);
        return metrics;
    }

    SkScalar measureText(const char* text, size_t size, SkRect* bounds) const {
        if (text == nullptr || size == 0) {
            if (bounds != nullptr) {
                bounds->setEmpty();
            }
            return 0.0f;
        }

        return font.measureText(text, size, SkTextEncoding::kUTF8, bounds);
    }

private:
    SkFont font;
};

}

#endif //MILESTRO_SKIA_FONT_H
