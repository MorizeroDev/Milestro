#ifndef MILESTRO_SKIA_PATH_H
#define MILESTRO_SKIA_PATH_H

#include <include/core/SkTypeface.h>
#include <include/core/SkFont.h>
#include <include/core/SkPath.h>
#include <include/core/SkPathMeasure.h>
#include "Milestro/util/milestro_class.h"
#include "Milestro/log/log.h"
#include "Milestro/util/milestro_serializerable.h"

namespace milestro::skia {

class MILESTRO_API Path {
public:
    explicit Path(SkPath path) {
        this->path = path;
    }

    MILESTRO_DECLARE_NON_COPYABLE(Path)

    SkPath unwrap() {
        return path;
    }

private:
    SkPath path;
};

}

#endif //MILESTRO_SKIA_FONT_H
