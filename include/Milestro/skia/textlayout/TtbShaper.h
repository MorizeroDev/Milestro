#ifndef MILESTRO_SKIA_TEXTLAYOUT_TTBSHAPER_H
#define MILESTRO_SKIA_TEXTLAYOUT_TTBSHAPER_H

#include "Milestro/common/milestro_export_macros.h"
#include "include/core/SkFont.h"
#include "include/core/SkFourByteTag.h"
#include "include/core/SkPoint.h"
#include "include/core/SkSpan.h"
#include "include/core/SkTypes.h"

#include <cstddef>
#include <cstdint>
#include <string_view>
#include <vector>

namespace milestro::skia::textlayout {

enum class TtbOrientation {
    kUpright,
    kSideways,
};

enum class TtbHorizontalDirection {
    kLeftToRight,
    kRightToLeft,
};

struct TtbFeature {
    SkFourByteTag tag;
    uint32_t value;
    size_t start = 0;
    size_t end = 0;
};

struct TtbShapeRequest {
    std::string_view utf8;
    size_t runStart = 0;
    size_t runLength = 0;
    SkFont font;
    SkFourByteTag script = 0;
    std::string_view language;
    TtbOrientation orientation = TtbOrientation::kUpright;
    TtbHorizontalDirection sidewaysDirection = TtbHorizontalDirection::kLeftToRight;
    SkSpan<const TtbFeature> features;
};

struct TtbGlyph {
    SkGlyphID glyph = 0;
    uint32_t clusterUtf8 = 0;
    SkPoint position;
    SkPoint offset;
    SkVector advance;
};

struct TtbShapedRun {
    TtbOrientation orientation = TtbOrientation::kUpright;
    TtbHorizontalDirection sidewaysDirection = TtbHorizontalDirection::kLeftToRight;
    SkVector advance;
    uint32_t unresolvedGlyphs = 0;
    std::vector<TtbGlyph> glyphs;
};

MILESTRO_API bool ShapeTtbRun(const TtbShapeRequest& request, TtbShapedRun* result);

} // namespace milestro::skia::textlayout

#endif // MILESTRO_SKIA_TEXTLAYOUT_TTBSHAPER_H
