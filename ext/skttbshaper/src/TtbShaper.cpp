#include "skttbshaper/TtbShaper.h"

#include "include/core/SkData.h"
#include "include/core/SkTypeface.h"

#include <hb-ot.h>
#include <hb.h>

#include <algorithm>
#include <cmath>
#include <limits>
#include <memory>
#include <utility>
#include <vector>

namespace skia::textlayout {
namespace {

constexpr float kHarfBuzzToSkia = 1.0f / 64.0f;
constexpr double kHarfBuzzScale = 64.0;

bool IsUtf8ScalarBoundary(std::string_view utf8, size_t offset) {
    return offset == utf8.size() || (static_cast<unsigned char>(utf8[offset]) & 0xC0) != 0x80;
}

struct TypefaceTableOwner {
    sk_sp<SkTypeface> typeface;
};

hb_blob_t* ReferenceTypefaceTable(hb_face_t*, hb_tag_t tag, void* userData) {
    const auto* owner = static_cast<const TypefaceTableOwner*>(userData);
    if (owner == nullptr || !owner->typeface) {
        return nullptr;
    }

    sk_sp<SkData> data = owner->typeface->copyTableData(tag);
    if (!data) {
        return nullptr;
    }
    if (data->size() > std::numeric_limits<unsigned int>::max()) {
        return nullptr;
    }

    SkData* ownedData = data.release();
    const char* bytes = static_cast<const char*>(ownedData->data());
    const unsigned int length = static_cast<unsigned int>(ownedData->size());
    return hb_blob_create(bytes, length, HB_MEMORY_MODE_READONLY, ownedData, [](void* owned) {
        static_cast<SkData*>(owned)->unref();
    });
}

hb_script_t ToHarfBuzzScript(SkFourByteTag script) {
    return hb_script_from_iso15924_tag(static_cast<hb_tag_t>(script));
}

hb_language_t ToHarfBuzzLanguage(std::string_view language) {
    if (language.empty() || language.size() > std::numeric_limits<int>::max()) {
        return hb_language_from_string("und", -1);
    }
    hb_language_t result = hb_language_from_string(language.data(), static_cast<int>(language.size()));
    return result == HB_LANGUAGE_INVALID ? hb_language_from_string("und", -1) : result;
}

void AppendVrt2Suppression(const TtbShapeRequest& request,
                           hb_tag_t vrt2Tag,
                           hb_tag_t vertTag,
                           std::vector<hb_feature_t>* features) {
    const size_t runEnd = request.runStart + request.runLength;
    std::vector<size_t> boundaries = {request.runStart, runEnd};
    boundaries.reserve(request.features.size() * 2 + 2);
    for (const TtbFeature& feature: request.features) {
        if (feature.tag != vrt2Tag || feature.start == feature.end || feature.start >= runEnd ||
            feature.end <= request.runStart) {
            continue;
        }
        boundaries.push_back(std::max(feature.start, request.runStart));
        boundaries.push_back(std::min(feature.end, runEnd));
    }

    std::sort(boundaries.begin(), boundaries.end());
    boundaries.erase(std::unique(boundaries.begin(), boundaries.end()), boundaries.end());

    bool suppressed = false;
    size_t suppressStart = 0;
    for (size_t index = 0; index + 1 < boundaries.size(); ++index) {
        const size_t start = boundaries[index];
        const size_t end = boundaries[index + 1];
        const TtbFeature* effective = nullptr;
        for (const TtbFeature& feature: request.features) {
            if (feature.tag == vrt2Tag && feature.start <= start && feature.end >= end) {
                effective = &feature;
            }
        }

        const bool enabled = effective != nullptr && effective->value != 0;
        if (enabled && !suppressed) {
            suppressStart = start;
            suppressed = true;
        } else if (!enabled && suppressed) {
            features->push_back(
                    {vertTag, 0, static_cast<unsigned int>(suppressStart), static_cast<unsigned int>(start)});
            suppressed = false;
        }
    }
    if (suppressed) {
        features->push_back({vertTag, 0, static_cast<unsigned int>(suppressStart), static_cast<unsigned int>(runEnd)});
    }
}

bool BuildFeatures(const TtbShapeRequest& request,
                   hb_tag_t vrt2Tag,
                   hb_tag_t vertTag,
                   std::vector<hb_feature_t>* features) {
    features->clear();
    features->reserve(request.features.size() * 2 + 2);
    for (const TtbFeature& feature: request.features) {
        if (feature.start > feature.end || feature.end > request.utf8.size()) {
            return false;
        }
        features->push_back({static_cast<hb_tag_t>(feature.tag),
                             feature.value,
                             static_cast<unsigned int>(feature.start),
                             static_cast<unsigned int>(feature.end)});
    }

    if (request.orientation == TtbOrientation::kUpright) {
        AppendVrt2Suppression(request, vrt2Tag, vertTag, features);
    }
    return true;
}

bool SetVariations(const SkTypeface& typeface, hb_font_t* font) {
    const int axisCount = typeface.getVariationDesignPosition({});
    if (axisCount <= 0) {
        return axisCount == 0;
    }

    std::vector<SkFontArguments::VariationPosition::Coordinate> coordinates(axisCount);
    if (typeface.getVariationDesignPosition({coordinates.data(), coordinates.size()}) != axisCount) {
        return false;
    }

    std::vector<hb_variation_t> variations;
    variations.reserve(coordinates.size());
    for (const auto& coordinate: coordinates) {
        variations.push_back({static_cast<hb_tag_t>(coordinate.axis), coordinate.value});
    }
    hb_font_set_variations(font, variations.data(), static_cast<unsigned int>(variations.size()));
    return true;
}

} // namespace

bool ShapeTtbRun(const TtbShapeRequest& request, TtbShapedRun* result) {
    if (result == nullptr) {
        return false;
    }
    *result = TtbShapedRun{};
    result->orientation = request.orientation;
    result->sidewaysDirection = request.sidewaysDirection;

    if (request.runStart > request.utf8.size() || request.runLength > request.utf8.size() - request.runStart ||
        request.utf8.size() > static_cast<size_t>(std::numeric_limits<int>::max()) ||
        request.runLength > static_cast<size_t>(std::numeric_limits<int>::max()) / 8) {
        return false;
    }
    const size_t runEnd = request.runStart + request.runLength;
    if (!IsUtf8ScalarBoundary(request.utf8, request.runStart) || !IsUtf8ScalarBoundary(request.utf8, runEnd)) {
        return false;
    }
    if (request.runLength == 0) {
        return true;
    }

    const SkTypeface* typeface = request.font.getTypeface();
    const float fontSize = request.font.getSize();
    const float fontScaleX = request.font.getScaleX();
    if (typeface == nullptr || request.font.getSize() <= 0 || !std::isfinite(fontSize) || !std::isfinite(fontScaleX) ||
        fontScaleX <= 0) {
        return false;
    }

    const double scaledX = static_cast<double>(fontSize) * static_cast<double>(fontScaleX) * kHarfBuzzScale;
    const double scaledY = static_cast<double>(fontSize) * kHarfBuzzScale;
    const double roundedX = std::round(scaledX);
    const double roundedY = std::round(scaledY);
    if (!std::isfinite(scaledX) || !std::isfinite(scaledY) || roundedX < std::numeric_limits<int>::min() ||
        roundedX > std::numeric_limits<int>::max() || roundedY < std::numeric_limits<int>::min() ||
        roundedY > std::numeric_limits<int>::max()) {
        return false;
    }

    const int unitsPerEm = typeface->getUnitsPerEm();
    if (unitsPerEm <= 0) {
        return false;
    }

    auto* tableOwner = new TypefaceTableOwner{request.font.refTypeface()};
    std::unique_ptr<hb_face_t, decltype(&hb_face_destroy)> face(
            hb_face_create_for_tables(ReferenceTypefaceTable,
                                      tableOwner,
                                      [](void* owner) {
                                          delete static_cast<TypefaceTableOwner*>(owner);
                                      }),
            hb_face_destroy);
    if (!face || face.get() == hb_face_get_empty() || hb_face_get_glyph_count(face.get()) <= 0) {
        return false;
    }
    hb_face_set_upem(face.get(), static_cast<unsigned int>(unitsPerEm));

    std::unique_ptr<hb_font_t, decltype(&hb_font_destroy)> hbFont(hb_font_create(face.get()), hb_font_destroy);
    if (!hbFont || hbFont.get() == hb_font_get_empty()) {
        return false;
    }
    hb_ot_font_set_funcs(hbFont.get());
    if (!SetVariations(*typeface, hbFont.get())) {
        return false;
    }

    hb_font_set_scale(hbFont.get(), static_cast<int>(roundedX), static_cast<int>(roundedY));

    const hb_direction_t direction =
            request.orientation == TtbOrientation::kUpright
                    ? HB_DIRECTION_TTB
                    : (request.sidewaysDirection == TtbHorizontalDirection::kLeftToRight ? HB_DIRECTION_LTR
                                                                                         : HB_DIRECTION_RTL);

    std::vector<hb_feature_t> features;
    if (!BuildFeatures(request, HB_TAG('v', 'r', 't', '2'), HB_TAG('v', 'e', 'r', 't'), &features)) {
        return false;
    }

    std::unique_ptr<hb_buffer_t, decltype(&hb_buffer_destroy)> buffer(hb_buffer_create(), hb_buffer_destroy);
    if (!buffer || buffer.get() == hb_buffer_get_empty()) {
        return false;
    }
    hb_buffer_set_direction(buffer.get(), direction);
    hb_buffer_set_script(buffer.get(), ToHarfBuzzScript(request.script));
    hb_buffer_set_language(buffer.get(), ToHarfBuzzLanguage(request.language));
    hb_buffer_set_cluster_level(buffer.get(), HB_BUFFER_CLUSTER_LEVEL_MONOTONE_CHARACTERS);
    hb_buffer_add_utf8(buffer.get(),
                       request.utf8.data(),
                       static_cast<int>(request.utf8.size()),
                       static_cast<int>(request.runStart),
                       static_cast<int>(request.runLength));
    if (!hb_buffer_allocation_successful(buffer.get())) {
        return false;
    }

    if (!hb_shape_full(hbFont.get(),
                       buffer.get(),
                       features.data(),
                       static_cast<unsigned int>(features.size()),
                       nullptr) ||
        !hb_buffer_allocation_successful(buffer.get())) {
        return false;
    }

    unsigned int glyphCount = 0;
    const hb_glyph_info_t* glyphInfo = hb_buffer_get_glyph_infos(buffer.get(), &glyphCount);
    const hb_glyph_position_t* glyphPosition = hb_buffer_get_glyph_positions(buffer.get(), nullptr);
    if (glyphCount != 0 && (glyphInfo == nullptr || glyphPosition == nullptr)) {
        return false;
    }
    result->glyphs.reserve(glyphCount);

    SkPoint pen = SkPoint::Make(0.0f, 0.0f);
    for (unsigned int index = 0; index < glyphCount; ++index) {
        const hb_glyph_info_t& info = glyphInfo[index];
        const hb_glyph_position_t& position = glyphPosition[index];

        TtbGlyph glyph;
        glyph.glyph = static_cast<SkGlyphID>(info.codepoint);
        glyph.clusterUtf8 = info.cluster;
        glyph.position = pen;
        glyph.offset = {position.x_offset * kHarfBuzzToSkia, -position.y_offset * kHarfBuzzToSkia};
        glyph.advance = {position.x_advance * kHarfBuzzToSkia, -position.y_advance * kHarfBuzzToSkia};
        pen += glyph.advance;
        if (info.codepoint == 0) {
            ++result->unresolvedGlyphs;
        }
        result->glyphs.push_back(glyph);
    }
    if (direction == HB_DIRECTION_RTL) {
        std::reverse(result->glyphs.begin(), result->glyphs.end());
    }
    result->advance = pen;
    return true;
}

} // namespace skia::textlayout
