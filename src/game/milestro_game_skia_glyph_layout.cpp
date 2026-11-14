#include "Milestro/game/milestro_game_interface.h"
#include "Milestro/skia/GlyphLayoutSnapshot.h"
#include "Milestro/skia/SlimTextDrawSnapshot.h"
#include "Milestro/skia/textlayout/Paragraph.h"
#include "milestro_game_retcode.h"
#include <limits>

extern "C" {

int64_t MilestroSkiaGlyphLayoutSnapshotCreateFromText(milestro::skia::SlimTextDrawSnapshot* source,
                                                    milestro::skia::GlyphLayoutSnapshot*& ret) try {
    ret = nullptr;
    if (!source) { return MILESTRO_API_RET_FAILED; }
    const auto* layout = source->glyphLayout();
    if (layout && layout->supportsAtlas()) { ret = new milestro::skia::GlyphLayoutSnapshot(*layout); }
    return MILESTRO_API_RET_OK;
} catch (...) { return MILESTRO_API_RET_FAILED; }

int64_t MilestroSkiaGlyphLayoutSnapshotCreateFromParagraph(milestro::skia::textlayout::Paragraph* source,
                                                         milestro::skia::GlyphLayoutSnapshot*& ret) try {
    ret = nullptr;
    if (!source) { return MILESTRO_API_RET_FAILED; }
    ret = milestro::skia::GlyphLayoutSnapshot::FromParagraph(*source).release();
    return MILESTRO_API_RET_OK;
} catch (...) { return MILESTRO_API_RET_FAILED; }

int64_t MilestroSkiaGlyphLayoutSnapshotDestroy(milestro::skia::GlyphLayoutSnapshot*& ret) {
    return MilestroDestroyHandle(ret);
}

int64_t MilestroSkiaGlyphLayoutSnapshotGetGlyphCount(milestro::skia::GlyphLayoutSnapshot* snapshot,
                                                   uint64_t& count) {
    if (!snapshot) { return MILESTRO_API_RET_FAILED; }
    count = snapshot->glyphCount();
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaGlyphLayoutSnapshotCopyGlyphs(milestro::skia::GlyphLayoutSnapshot* snapshot,
                                               uint8_t* data, uint64_t capacityBytes) try {
    return snapshot && capacityBytes <= std::numeric_limits<size_t>::max() &&
           snapshot->copyGlyphs(data, static_cast<size_t>(capacityBytes))
                   ? MILESTRO_API_RET_OK : MILESTRO_API_RET_FAILED;
} catch (...) { return MILESTRO_API_RET_FAILED; }

int64_t MilestroSkiaGlyphLayoutSnapshotGetRasterBounds(milestro::skia::GlyphLayoutSnapshot* snapshot,
                                                     uint64_t index, float scale,
                                                     int32_t& left, int32_t& top,
                                                     int32_t& width, int32_t& height) try {
    SkIRect bounds;
    if (!snapshot || index > std::numeric_limits<size_t>::max() ||
        !snapshot->rasterBounds(static_cast<size_t>(index), scale, bounds)) { return MILESTRO_API_RET_FAILED; }
    left = bounds.left(); top = bounds.top(); width = bounds.width(); height = bounds.height();
    return MILESTRO_API_RET_OK;
} catch (...) { return MILESTRO_API_RET_FAILED; }

int64_t MilestroSkiaGlyphLayoutSnapshotRasterize(milestro::skia::GlyphLayoutSnapshot* snapshot,
                                              uint64_t index, float scale, uint8_t* pixels,
                                              uint64_t capacity) try {
    return snapshot && index <= std::numeric_limits<size_t>::max() &&
           capacity <= std::numeric_limits<size_t>::max() &&
           snapshot->rasterize(static_cast<size_t>(index), scale, pixels, static_cast<size_t>(capacity))
                   ? MILESTRO_API_RET_OK : MILESTRO_API_RET_FAILED;
} catch (...) { return MILESTRO_API_RET_FAILED; }

}
