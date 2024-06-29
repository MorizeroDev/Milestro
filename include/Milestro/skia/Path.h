#ifndef MILESTRO_SKIA_PATH_H
#define MILESTRO_SKIA_PATH_H

#include <include/core/SkTypeface.h>
#include <include/core/SkFont.h>
#include <include/core/SkPath.h>
#include <include/core/SkPathMeasure.h>
#include <src/gpu/ganesh/GrEagerVertexAllocator.h>
#include <src/gpu/ganesh/geometry/GrAATriangulator.h>
#include "Milestro/util/milestro_class.h"
#include "Milestro/log/log.h"
#include "Milestro/util/milestro_serializerable.h"
#include "VertexData.h"

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

    milestro::skia::VertexData* ToAATriangles(SkScalar tolerance) {
        GrCpuVertexAllocator alloc;
        SkRect clipBounds = path.getBounds();
        auto trianglesResult = GrAATriangulator::PathToAATriangles(path, tolerance, clipBounds, &alloc);
        if (trianglesResult == 0) {
            return nullptr;
        }
        return new VertexData(alloc.detachVertexData());
    }

private:
    SkPath path;
};

}

#endif //MILESTRO_SKIA_FONT_H
