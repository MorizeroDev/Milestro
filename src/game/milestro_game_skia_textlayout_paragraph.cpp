#include <Milestro/game/milestro_game_interface.h>
#include "milestro_game_retcode.h"
#include "Milestro/skia/textlayout/Paragraph.h"
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
                                                  void *context,
                                                  float x, float y,
                                                  MilestroSkiaTextlayoutParagraphSplitGlyphCallback callback
) try {
    return p->splitGlyph(x, y, context, callback);
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutParagraphToSDF(milestro::skia::textlayout::Paragraph *p,
                                             int32_t width, int32_t height,
                                             float x, float y,
                                             uint8_t *distanceField
) try {
    return p->toSDF(width, height, x, y, distanceField);
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTextlayoutParagraphToPath(milestro::skia::textlayout::Paragraph *p,
                                              milestro::skia::Path *&path,
                                              float x, float y
) try {
    auto ret = p->toPath(x, y);
    if (ret == nullptr) {
        return MILESTRO_API_RET_FAILED;
    }
    path = ret;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

}
