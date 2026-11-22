#ifndef MILESTRO_SKIA_GLYPH_PLACEMENT_H
#define MILESTRO_SKIA_GLYPH_PLACEMENT_H

#include <cstdint>

namespace milestro::skia {

struct GlyphPlacement {
    uint32_t glyphId;
    float x, y;
};

static_assert(sizeof(GlyphPlacement) == 12);

}
#endif
