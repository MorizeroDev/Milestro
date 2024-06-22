#include <Milestro/game/milestro_game_interface.h>
#include <Milestro/log/log.h>
#include "milestro_game_retcode.h"
#include "Milestro/skia/textlayout/Paragraph.h"
#include "nlohmann/json.hpp"
#include "Milestro/util/milestro_strutil.h"

extern "C" {
int64_t MilestroSkiaTextlayoutParagraphDestroy(milestro::skia::textlayout::Paragraph *&ret) try {
    delete ret;
    ret = nullptr;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutParagraphLayout(milestro::skia::textlayout::Paragraph *p, float width) try {
    p->layout(width);
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutParagraphPaint(milestro::skia::textlayout::Paragraph *p,
                                             milestro::skia::Canvas *canvas,
                                             float x, float y) try {
    p->paint(canvas, x, y);
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutParagraphSplitGlyph(milestro::skia::textlayout::Paragraph *p,
                                                  float x, float y,
                                                  uint8_t *buffer,
                                                  uint64_t bufferSize,
                                                  uint64_t &needed
) try {
    auto info = p->splitGlyph(x, y);
    auto result = info.toJson().dump();
    needed = result.size();
    return static_cast<int64_t>(milestro::util::copyStringToBuffer(result, buffer, bufferSize));
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

}
