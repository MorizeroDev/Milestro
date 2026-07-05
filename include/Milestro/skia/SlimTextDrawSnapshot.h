#ifndef MILESTRO_SKIA_SLIM_TEXT_DRAW_SNAPSHOT_H
#define MILESTRO_SKIA_SLIM_TEXT_DRAW_SNAPSHOT_H

#include "Milestro/common/milestro_export_macros.h"
#include "Milestro/util/milestro_class.h"
#include <include/core/SkCanvas.h>
#include <include/core/SkRect.h>
#include <cstddef>

namespace milestro::skia {

class MILESTRO_API SlimTextDrawSnapshot {
public:
    virtual ~SlimTextDrawSnapshot() = default;

    MILESTRO_DECLARE_NON_COPYABLE(SlimTextDrawSnapshot)

    virtual void paintText(SkCanvas* canvas, SkScalar x, SkScalar baselineY) const = 0;

    virtual bool updateText(const char* source, size_t size) {
        (void) source;
        (void) size;
        return false;
    }

    virtual bool copyTextFrom(const SlimTextDrawSnapshot& source) {
        return updateText(source.textData(), source.textSize());
    }

    virtual SkScalar measureText(SkRect* bounds) const {
        if (bounds != nullptr) {
            bounds->setEmpty();
        }

        return 0.0f;
    }

    virtual const char* textData() const {
        return nullptr;
    }

    virtual size_t textSize() const {
        return 0;
    }

protected:
    SlimTextDrawSnapshot() = default;
};

} // namespace milestro::skia

#endif // MILESTRO_SKIA_SLIM_TEXT_DRAW_SNAPSHOT_H
