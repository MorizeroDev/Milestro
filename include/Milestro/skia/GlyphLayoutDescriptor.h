#ifndef MILESTRO_SKIA_GLYPH_LAYOUT_DESCRIPTOR_H
#define MILESTRO_SKIA_GLYPH_LAYOUT_DESCRIPTOR_H

#include <cstdint>

namespace milestro::skia {

struct GlyphLayoutDescriptor {
    uint32_t typefaceId;
    uint32_t glyphId;
    uint32_t fontFlags;
    uint32_t color;
    float fontSize;
    float fontScaleX;
    float fontSkewX;
    float x;
    float y;
    float left;
    float top;
    float right;
    float bottom;
};

static_assert(sizeof(GlyphLayoutDescriptor) == 52);

}
#endif
