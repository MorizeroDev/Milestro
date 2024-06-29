#include <Milestro/game/milestro_game_interface.h>
#include "Milestro/skia/FontManager.h"
#include "milestro_game_retcode.h"
#include "Milestro/util/milestro_strutil.h"
#include "Milestro/skia/Font.h"

extern "C" {
int64_t MilestroSkiaPathDestroy(milestro::skia::Path *&ret) try {
    delete ret;
    ret = nullptr;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaPathToAATriangles(milestro::skia::Path *p,
                                      milestro::skia::VertexData *&vertexData, float tolerance) try {
    auto ret = p->ToAATriangles(tolerance);
    if (ret == nullptr) {
        return MILESTRO_API_RET_FAILED;
    }
    vertexData = ret;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}
}
