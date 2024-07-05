#include <Milestro/game/milestro_game_interface.h>
#include "Milestro/skia/FontManager.h"
#include "milestro_game_retcode.h"
#include "Milestro/util/milestro_strutil.h"
#include "Milestro/skia/Svg.h"

extern "C" {

int64_t MilestroSkiaSvgCreate(milestro::skia::Svg *&ret, void *data, uint64_t size) try {
    auto svgData = SkMemoryStream::MakeCopy(data, size);
    ret = new milestro::skia::Svg(std::move(svgData));
    return MILESTRO_API_RET_OK;
} catch (std::runtime_error &e) {
    MILESTROLOG_ERROR("MilestroSkiaSvgCreate: {}", e.what());
    return MILESTRO_API_RET_FAILED;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaSvgDestroy(milestro::skia::Svg *&ret) try {
    delete ret;
    ret = nullptr;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaSvgRender(milestro::skia::Svg *svg, milestro::skia::Canvas *canvas) try {
    svg->render(canvas);
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

}
