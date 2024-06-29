#ifndef MILESTRO_SKIA_VERTEXDATA_H
#define MILESTRO_SKIA_VERTEXDATA_H

#include <include/core/SkTypeface.h>
#include <include/core/SkFont.h>
#include <include/core/SkPath.h>
#include <include/core/SkPathMeasure.h>
#include <src/gpu/ganesh/GrThreadSafeCache.h>
#include "Milestro/util/milestro_class.h"
#include "Milestro/log/log.h"
#include "Milestro/util/milestro_serializerable.h"

namespace milestro::skia {

class MILESTRO_API VertexData {
public:
    explicit VertexData(sk_sp<GrThreadSafeCache::VertexData> data) {
        this->data = std::move(data);
    }

    MILESTRO_DECLARE_NON_COPYABLE(VertexData)

    sk_sp<GrThreadSafeCache::VertexData> unwrap() {
        return data;
    }

    size_t GetVertexCount() {
        return data->numVertices();
    }

    size_t GetVertexSize() {
        return data->vertexSize();
    }

    void FillData(void* dst) {
        memcpy(dst, data->vertices(), data->size());
    }

private:
    sk_sp<GrThreadSafeCache::VertexData> data;
};

}

#endif //MILESTRO_SKIA_VERTEXDATA_H
