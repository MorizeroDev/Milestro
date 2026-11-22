#ifndef MILESTRO_SKIA_GLYPH_FONT_DESCRIPTOR_H
#define MILESTRO_SKIA_GLYPH_FONT_DESCRIPTOR_H

#include <cstdint>

namespace milestro::skia {

struct GlyphFontDescriptor {
    uint32_t typefaceId, fontFlags, color;
    float size, scaleX, skewX;
};

static_assert(sizeof(GlyphFontDescriptor) == 24);

}
#endif
