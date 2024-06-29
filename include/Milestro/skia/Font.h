#ifndef MILESTRO_SKIA_FONT_H
#define MILESTRO_SKIA_FONT_H

#include <include/core/SkTypeface.h>
#include <include/core/SkFont.h>
#include <include/core/SkPath.h>
#include "Milestro/util/milestro_class.h"
#include "Milestro/log/log.h"
#include "Milestro/util/milestro_serializerable.h"
#include "Path.h"

namespace milestro::skia {

class MILESTRO_API Font {
public:
    explicit Font(SkFont font) {
        this->font = std::move(font);
    }

    MILESTRO_DECLARE_NON_COPYABLE(Font)

    SkFont unwrap() {
        return font;
    }

    Path* getPath(SkGlyphID glyphID) {
        SkPath path;
        if (font.getPath(glyphID, &path)) {
            return new Path(path);
        } else {
            return nullptr;
        }
    }

private:
    SkFont font;
};

}

#endif //MILESTRO_SKIA_FONT_H
