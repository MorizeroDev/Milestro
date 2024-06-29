#include <Milestro/game/milestro_game_interface.h>
#include "Milestro/skia/FontManager.h"
#include "milestro_game_retcode.h"
#include "Milestro/util/milestro_strutil.h"
#include "Milestro/skia/Font.h"

extern "C" {
int64_t MilestroSkiaVertexDataDestroy(milestro::skia::VertexData *&ret) try {
    delete ret;
    ret = nullptr;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaVertexDataGetVertexCount(milestro::skia::VertexData *d,
                                             uint64_t &numVertices) try {
    numVertices = d->GetVertexCount();
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaVertexDataGetVertexSize(milestro::skia::VertexData *d,
                                            uint64_t &vertexSize) try {
    vertexSize = d->GetVertexSize();
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaVertexDataFillData(milestro::skia::VertexData *d,
                                       void *dst) try {
    d->FillData(dst);
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}
}
