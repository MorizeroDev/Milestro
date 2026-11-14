#ifndef MILESTRO_SKIA_GLYPH_LAYOUT_SNAPSHOT_H
#define MILESTRO_SKIA_GLYPH_LAYOUT_SNAPSHOT_H

#include "Milestro/common/milestro_export_macros.h"
#include <include/core/SkCanvas.h>
#include <include/core/SkFont.h>
#include <cstdint>
#include <memory>
#include <vector>

namespace milestro::skia {
namespace textlayout { class Paragraph; }

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

class MILESTRO_API GlyphLayoutSnapshot {
public:
    struct Run {
        SkFont font;
        SkColor color;
        std::vector<SkGlyphID> glyphs;
        std::vector<SkPoint> positions;
        std::vector<SkRect> bounds;
    };

    void updateSimpleText(const SkFont& font, const char* text, size_t size, SkColor color);
    bool copySimpleText(const GlyphLayoutSnapshot& source, const SkFont& font, SkColor color);
    void paint(SkCanvas* canvas, SkScalar originX, SkScalar baselineY) const;
    SkScalar measure(SkRect* bounds) const;
    bool supportsAtlas() const;
    size_t glyphCount() const;
    bool copyGlyphs(void* destination, size_t capacityBytes) const;
    bool rasterBounds(size_t index, float scale, SkIRect& bounds) const;
    bool rasterize(size_t index, float scale, uint8_t* pixels, size_t capacity) const;
    static std::unique_ptr<GlyphLayoutSnapshot> FromParagraph(textlayout::Paragraph& paragraph);

    void appendRun(Run run);

private:
    const Run* findGlyph(size_t& index) const;
    std::vector<Run> runs;
    std::vector<SkScalar> simpleAdvances;
    SkRect inkBounds = SkRect::MakeEmpty();
    SkScalar advance = 0;
};

}
#endif
