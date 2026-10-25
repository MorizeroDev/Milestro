#include "Milestro/skia/textlayout/TtbShaper.h"
#include "Milestro/skia/unicode/Unicode.h"
#include "include/core/SkBitmap.h"
#include "include/core/SkCanvas.h"
#include "include/core/SkColor.h"
#include "include/core/SkData.h"
#include "include/core/SkFont.h"
#include "include/core/SkFontMgr.h"
#include "include/core/SkFontStyle.h"
#include "include/core/SkMatrix.h"
#include "include/core/SkPaint.h"
#include "include/core/SkPath.h"
#include "include/core/SkPathBuilder.h"
#include "include/core/SkSpan.h"
#include "include/core/SkTextBlob.h"
#include "include/core/SkTypeface.h"
#include "include/encode/SkPngEncoder.h"
#include "include/ports/SkFontMgr_empty.h"
#include "modules/skshaper/include/SkShaper.h"
#include "modules/skshaper/include/SkShaper_harfbuzz.h"
#include "modules/skunicode/include/SkUnicode.h"

#include <hb-ot.h>
#include <hb.h>

#include <algorithm>
#include <array>
#include <cmath>
#include <cstdint>
#include <fstream>
#include <limits>
#include <memory>
#include <optional>
#include <string>
#include <string_view>
#include <utility>
#include <vector>

#include <gtest/gtest.h>

namespace {

using milestro::skia::textlayout::ShapeTtbRun;
using milestro::skia::textlayout::TtbFeature;
using milestro::skia::textlayout::TtbHorizontalDirection;
using milestro::skia::textlayout::TtbOrientation;
using milestro::skia::textlayout::TtbShapedRun;
using milestro::skia::textlayout::TtbShapeRequest;

constexpr float kFontSize = 48.0f;
constexpr int kHarfBuzzScale = static_cast<int>(kFontSize * 64.0f);
constexpr int kCanvasWidth = 320;
constexpr int kCanvasHeight = 480;
constexpr float kTopMargin = 32.0f;
constexpr float kRightColumnCenterX = 248.0f;
constexpr float kLeftColumnLeftX = 160.0f;

constexpr char kUprightText[] = "「天地玄黃」";
constexpr char kSidewaysText[] = "ABC 2026";

struct HbBufferDeleter {
    void operator()(hb_buffer_t* buffer) const {
        hb_buffer_destroy(buffer);
    }
};

struct HbFaceDeleter {
    void operator()(hb_face_t* face) const {
        hb_face_destroy(face);
    }
};

struct HbFontDeleter {
    void operator()(hb_font_t* font) const {
        hb_font_destroy(font);
    }
};

using HbBuffer = std::unique_ptr<hb_buffer_t, HbBufferDeleter>;
using HbFace = std::unique_ptr<hb_face_t, HbFaceDeleter>;
using HbFont = std::unique_ptr<hb_font_t, HbFontDeleter>;

hb_blob_t* ReadTypefaceTable(hb_face_t*, hb_tag_t tag, void* userData) {
    if (!userData) {
        return nullptr;
    }
    const auto* owner = static_cast<const sk_sp<SkTypeface>*>(userData);
    if (!owner || !*owner) {
        return nullptr;
    }
    sk_sp<SkData> data = (*owner)->copyTableData(tag);
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

struct FontContext {
    sk_sp<SkTypeface> typeface;
    HbFace face;
    HbFont hbFont;
    SkFont skFont;
};

std::unique_ptr<FontContext> LoadFont(const char* path) {
    auto manager = SkFontMgr_New_Custom_Empty();
    auto typeface = manager->makeFromFile(path);
    if (!typeface) {
        return nullptr;
    }

    auto context = std::make_unique<FontContext>();
    context->typeface = std::move(typeface);

    auto* tableOwner = new sk_sp<SkTypeface>(context->typeface);
    context->face.reset(hb_face_create_for_tables(ReadTypefaceTable, tableOwner, [](void* owned) {
        delete static_cast<sk_sp<SkTypeface>*>(owned);
    }));
    if (!context->face || hb_face_get_glyph_count(context->face.get()) == 0) {
        return nullptr;
    }
    hb_face_set_upem(context->face.get(), context->typeface->getUnitsPerEm());

    context->hbFont.reset(hb_font_create(context->face.get()));
    if (!context->hbFont) {
        return nullptr;
    }
    hb_ot_font_set_funcs(context->hbFont.get());
    hb_font_set_scale(context->hbFont.get(), kHarfBuzzScale, kHarfBuzzScale);

    context->skFont = SkFont(context->typeface, kFontSize);
    context->skFont.setSubpixel(true);
    context->skFont.setHinting(SkFontHinting::kNone);
    return context;
}

struct ShapedGlyph {
    SkGlyphID glyph = 0;
    uint32_t cluster = 0;
    hb_position_t hbXAdvance = 0;
    hb_position_t hbYAdvance = 0;
    SkPoint baseline;
    SkPoint advance;
    SkPoint verticalOrigin;
};

struct ShapedRun {
    std::vector<ShapedGlyph> glyphs;
    SkPoint advance;
};

SkPoint ToSkiaPoint(hb_position_t xPosition, hb_position_t yPosition) {
    return {static_cast<float>(xPosition) / 64.0f, -static_cast<float>(yPosition) / 64.0f};
}

ShapedRun Shape(const FontContext& font,
                std::string_view text,
                hb_direction_t direction,
                hb_script_t script,
                const char* language,
                std::vector<hb_feature_t> features = {}) {
    HbBuffer buffer(hb_buffer_create());
    if (!buffer) {
        return {};
    }
    hb_buffer_set_direction(buffer.get(), direction);
    hb_buffer_set_script(buffer.get(), script);
    hb_buffer_set_language(buffer.get(), hb_language_from_string(language, -1));
    hb_buffer_add_utf8(buffer.get(), text.data(), static_cast<int>(text.size()), 0, static_cast<int>(text.size()));
    hb_shape(font.hbFont.get(), buffer.get(), features.data(), static_cast<unsigned>(features.size()));

    unsigned int length = 0;
    const hb_glyph_info_t* infos = hb_buffer_get_glyph_infos(buffer.get(), &length);
    const hb_glyph_position_t* positions = hb_buffer_get_glyph_positions(buffer.get(), nullptr);

    ShapedRun run;
    run.glyphs.reserve(length);
    SkPoint pen{0.0f, 0.0f};
    const bool vertical = HB_DIRECTION_IS_VERTICAL(direction);
    for (unsigned int index = 0; index < length; ++index) {
        hb_position_t originX = 0;
        hb_position_t originY = 0;
        const bool hasVerticalOrigin =
                hb_font_get_glyph_v_origin(font.hbFont.get(), infos[index].codepoint, &originX, &originY);

        const SkPoint offset = {
                static_cast<float>(positions[index].x_offset) / 64.0f,
                -static_cast<float>(positions[index].y_offset) / 64.0f,
        };
        const SkPoint verticalOrigin = ToSkiaPoint(originX, originY);

        ShapedGlyph glyph;
        glyph.glyph = static_cast<SkGlyphID>(infos[index].codepoint);
        glyph.cluster = infos[index].cluster;
        glyph.hbXAdvance = positions[index].x_advance;
        glyph.hbYAdvance = positions[index].y_advance;
        glyph.advance = ToSkiaPoint(positions[index].x_advance, positions[index].y_advance);
        glyph.verticalOrigin = vertical && hasVerticalOrigin ? verticalOrigin : SkPoint{};
        glyph.baseline = pen + offset;
        run.glyphs.push_back(glyph);

        pen.set(pen.x() + glyph.advance.x(), pen.y() + glyph.advance.y());
    }
    run.advance = pen;
    return run;
}

hb_feature_t Feature(hb_tag_t tag, uint32_t value) {
    return hb_feature_t{tag, value, HB_FEATURE_GLOBAL_START, HB_FEATURE_GLOBAL_END};
}

TtbFeature TtbFeatureAt(SkFourByteTag tag, uint32_t value, size_t start, size_t end) {
    return TtbFeature{tag, value, start, end};
}

TtbShapeRequest MakeUprightRequest(const FontContext& fontContext, std::string_view text) {
    TtbShapeRequest request;
    request.utf8 = text;
    request.runLength = text.size();
    request.font = fontContext.skFont;
    request.script = SkSetFourByteTag('H', 'a', 'n', 'i');
    request.language = "ja";
    request.orientation = TtbOrientation::kUpright;
    request.sidewaysDirection = TtbHorizontalDirection::kLeftToRight;
    return request;
}

struct GlyphGeometry {
    SkFont font;
    std::vector<SkGlyphID> glyphs;
    std::vector<SkPoint> baselines;
    SkMatrix placement = SkMatrix::I();
};

GlyphGeometry MakeGeometry(const FontContext& font, const ShapedRun& run, const SkMatrix& placement) {
    GlyphGeometry geometry;
    geometry.font = font.skFont;
    geometry.placement = placement;
    geometry.glyphs.reserve(run.glyphs.size());
    geometry.baselines.reserve(run.glyphs.size());
    for (const auto& glyph: run.glyphs) {
        geometry.glyphs.push_back(glyph.glyph);
        geometry.baselines.push_back(glyph.baseline);
    }
    return geometry;
}

sk_sp<SkTextBlob> MakeBlob(const GlyphGeometry& geometry) {
    return SkTextBlob::MakeFromPosGlyphs(geometry.glyphs, geometry.baselines, geometry.font);
}

struct PathResult {
    SkPath path;
    int unconvertedGlyphs = 0;
};

PathResult MakePath(const GlyphGeometry& geometry) {
    struct Context {
        const GlyphGeometry* geometry;
        SkPathBuilder* path;
        int* unconvertedGlyphs;
        size_t glyphIndex;
    };
    PathResult result;
    SkPathBuilder builder;
    Context context{&geometry, &builder, &result.unconvertedGlyphs, 0};
    geometry.font.getPaths(
            geometry.glyphs,
            [](const SkPath* source, const SkMatrix& glyphMatrix, void* rawContext) {
                auto& current = *static_cast<Context*>(rawContext);
                if (source == nullptr) {
                    ++*current.unconvertedGlyphs;
                } else {
                    SkMatrix total = glyphMatrix;
                    const SkPoint baseline = current.geometry->baselines[current.glyphIndex];
                    total.postTranslate(baseline.x(), baseline.y());
                    total.postConcat(current.geometry->placement);
                    current.path->addPath(*source, total);
                }
                ++current.glyphIndex;
            },
            &context);
    result.path = builder.detach();
    return result;
}

SkRect InkBounds(const GlyphGeometry& geometry) {
    std::vector<SkRect> glyphBounds(geometry.glyphs.size());
    geometry.font.getBounds(geometry.glyphs, glyphBounds, nullptr);
    SkRect result = SkRect::MakeEmpty();
    bool hasPoint = false;
    for (size_t index = 0; index < glyphBounds.size(); ++index) {
        SkRect bounds = glyphBounds[index];
        bounds.offset(geometry.baselines[index].x(), geometry.baselines[index].y());
        std::array<SkPoint, 4> corners = {
                SkPoint{bounds.left(), bounds.top()},
                SkPoint{bounds.right(), bounds.top()},
                SkPoint{bounds.left(), bounds.bottom()},
                SkPoint{bounds.right(), bounds.bottom()},
        };
        for (const auto& corner: corners) {
            const SkPoint mapped = geometry.placement.mapPoint(corner);
            if (!hasPoint) {
                result.setLTRB(mapped.x(), mapped.y(), mapped.x(), mapped.y());
                hasPoint = true;
            } else {
                result.setLTRB(std::min(result.left(), mapped.x()),
                               std::min(result.top(), mapped.y()),
                               std::max(result.right(), mapped.x()),
                               std::max(result.bottom(), mapped.y()));
            }
        }
    }
    return result;
}

enum class PaintStrategy {
    kTextBlob,
    kPath,
};

SkBitmap Raster(const std::vector<const GlyphGeometry*>& runs, PaintStrategy strategy) {
    SkBitmap bitmap;
    bitmap.allocN32Pixels(kCanvasWidth, kCanvasHeight);
    bitmap.eraseColor(SK_ColorWHITE);
    SkCanvas canvas(bitmap);

    SkPaint paint;
    paint.setAntiAlias(true);
    paint.setColor(SK_ColorBLACK);
    for (const auto* geometry: runs) {
        if (strategy == PaintStrategy::kTextBlob) {
            canvas.save();
            canvas.concat(geometry->placement);
            canvas.drawTextBlob(MakeBlob(*geometry), 0.0f, 0.0f, paint);
            canvas.restore();
        } else {
            canvas.drawPath(MakePath(*geometry).path, paint);
        }
    }
    return bitmap;
}

std::optional<SkRect> RasterInkBounds(const SkBitmap& bitmap) {
    SkRect bounds = SkRect::MakeEmpty();
    bool found = false;
    for (int pixelY = 0; pixelY < bitmap.height(); ++pixelY) {
        for (int pixelX = 0; pixelX < bitmap.width(); ++pixelX) {
            const auto color = bitmap.getColor(pixelX, pixelY);
            if (SkColorGetR(color) >= 250 && SkColorGetG(color) >= 250 && SkColorGetB(color) >= 250) {
                continue;
            }
            const SkRect pixel = SkRect::MakeXYWH(static_cast<float>(pixelX), static_cast<float>(pixelY), 1.0f, 1.0f);
            if (!found) {
                bounds = pixel;
                found = true;
            } else {
                bounds.join(pixel);
            }
        }
    }
    if (!found) {
        return std::nullopt;
    }
    return bounds;
}

struct BitmapDifference {
    int changedPixels = 0;
    uint64_t totalChannelDelta = 0;
};

BitmapDifference CompareBitmaps(const SkBitmap& left, const SkBitmap& right) {
    BitmapDifference difference;
    const auto* leftPixels = static_cast<const uint8_t*>(left.getPixels());
    const auto* rightPixels = static_cast<const uint8_t*>(right.getPixels());
    const size_t byteCount = left.computeByteSize();
    for (size_t index = 0; index < byteCount; index += 4) {
        int pixelDelta = 0;
        for (size_t channel = 0; channel < 4; ++channel) {
            pixelDelta += std::abs(static_cast<int>(leftPixels[index + channel]) -
                                   static_cast<int>(rightPixels[index + channel]));
        }
        difference.totalChannelDelta += static_cast<uint64_t>(pixelDelta);
        if (pixelDelta > 8) {
            ++difference.changedPixels;
        }
    }
    return difference;
}

void SavePng(const SkBitmap& bitmap, const char* path) {
    auto encoded = SkPngEncoder::Encode(bitmap.pixmap(), SkPngEncoder::Options{});
    if (!encoded) {
        return;
    }
    std::ofstream output(path, std::ios::binary);
    output.write(static_cast<const char*>(encoded->data()), static_cast<std::streamsize>(encoded->size()));
}

class SkiaVerticalTextFeasibility : public ::testing::Test {
protected:
    void SetUp() override {
        font = LoadFont(MILESTRO_TEST_FONT_PATH);
        ASSERT_NE(font, nullptr) << MILESTRO_TEST_FONT_PATH;
    }

    std::unique_ptr<FontContext> font;
};

class RecordingRunHandler final : public SkShaper::RunHandler {
public:
    void beginLine() override {
    }
    void runInfo(const RunInfo&) override {
    }
    void commitRunInfo() override {
    }

    Buffer runBuffer(const RunInfo& info) override {
        glyphs.resize(info.glyphCount);
        positions.resize(info.glyphCount);
        clusters.resize(info.glyphCount);
        return {glyphs.data(), positions.data(), nullptr, clusters.data(), {0.0f, 0.0f}};
    }

    void commitRunBuffer(const RunInfo& info) override {
        advances.push_back(info.fAdvance);
        levels.push_back(info.fBidiLevel);
    }

    void commitLine() override {
    }

    std::vector<SkGlyphID> glyphs;
    std::vector<SkPoint> positions;
    std::vector<uint32_t> clusters;
    std::vector<SkVector> advances;
    std::vector<uint8_t> levels;
};

TEST_F(SkiaVerticalTextFeasibility, TtbShapingUsesVerticalAlternatesAndMetrics) {
    constexpr std::string_view text = "「漢字」、。ー";
    const auto horizontal = Shape(*font, text, HB_DIRECTION_LTR, HB_SCRIPT_HAN, "ja");
    const auto horizontalVert =
            Shape(*font, text, HB_DIRECTION_LTR, HB_SCRIPT_HAN, "ja", {Feature(HB_TAG('v', 'e', 'r', 't'), 1)});
    const auto vertical = Shape(*font, text, HB_DIRECTION_TTB, HB_SCRIPT_HAN, "ja");

    ASSERT_EQ(horizontal.glyphs.size(), text.size() / 3);
    ASSERT_EQ(horizontalVert.glyphs.size(), horizontal.glyphs.size());
    ASSERT_EQ(vertical.glyphs.size(), horizontal.glyphs.size());

    constexpr std::array<size_t, 5> kSubstitutedGlyphs = {0, 3, 4, 5, 6};
    for (const size_t index: kSubstitutedGlyphs) {
        EXPECT_NE(horizontal.glyphs[index].glyph, horizontalVert.glyphs[index].glyph) << "index=" << index;
        EXPECT_EQ(horizontalVert.glyphs[index].glyph, vertical.glyphs[index].glyph) << "index=" << index;
    }
    for (size_t index = 0; index < vertical.glyphs.size(); ++index) {
        EXPECT_EQ(horizontal.glyphs[index].hbXAdvance,
                  hb_font_get_glyph_h_advance(font->hbFont.get(), horizontal.glyphs[index].glyph));
        EXPECT_EQ(horizontal.glyphs[index].hbYAdvance, 0);
        EXPECT_EQ(vertical.glyphs[index].hbYAdvance,
                  hb_font_get_glyph_v_advance(font->hbFont.get(), vertical.glyphs[index].glyph));
        EXPECT_EQ(vertical.glyphs[index].hbXAdvance, 0);
        EXPECT_NEAR(vertical.glyphs[index].advance.x(), 0.0f, 0.01f);
        EXPECT_GT(vertical.glyphs[index].advance.y(), 0.0f);
        EXPECT_NEAR(horizontalVert.glyphs[index].advance.x(), horizontal.glyphs[index].advance.x(), 0.01f)
                << "vert must not change horizontal layout geometry";
    }

    const auto horizontalLatin = Shape(*font, "A", HB_DIRECTION_LTR, HB_SCRIPT_LATIN, "en");
    const auto verticalLatin = Shape(*font, "A", HB_DIRECTION_TTB, HB_SCRIPT_LATIN, "en");
    ASSERT_EQ(horizontalLatin.glyphs.size(), 1u);
    ASSERT_EQ(verticalLatin.glyphs.size(), 1u);

    std::array<SkGlyphID, 1> glyph = {horizontalLatin.glyphs.front().glyph};
    std::array<SkScalar, 1> horizontalAdvance = {};
    font->skFont.getWidths(glyph, horizontalAdvance);
    EXPECT_NEAR(horizontalLatin.advance.x(), horizontalAdvance.front(), 0.01f);
    EXPECT_EQ(verticalLatin.glyphs.front().hbYAdvance,
              hb_font_get_glyph_v_advance(font->hbFont.get(), verticalLatin.glyphs.front().glyph));
}

TEST_F(SkiaVerticalTextFeasibility, TtbShapingPreservesClustersAndMapsVerticalOrigins) {
    constexpr std::string_view text = "「𠀀A」、。ー";
    const std::string originalUtf8(text);
    const auto originalUtf16 = SkUnicode::convertUtf8ToUtf16(text.data(), text.size());
    const auto vertical = Shape(*font, text, HB_DIRECTION_TTB, HB_SCRIPT_HAN, "ja");
    ASSERT_EQ(vertical.glyphs.size(), 7u);

    constexpr std::array<uint32_t, 7> kExpectedUtf8Clusters = {0, 3, 7, 8, 11, 14, 17};
    constexpr std::array<size_t, 7> kExpectedUtf16Clusters = {0, 1, 3, 4, 5, 6, 7};
    for (size_t index = 0; index < vertical.glyphs.size(); ++index) {
        EXPECT_EQ(vertical.glyphs[index].cluster, kExpectedUtf8Clusters[index]) << "glyph index=" << index;
        if (index > 0) {
            EXPECT_LE(vertical.glyphs[index - 1].cluster, vertical.glyphs[index].cluster);
        }
        const auto utf16Prefix =
                SkUnicode::convertUtf8ToUtf16(text.data(), static_cast<int>(vertical.glyphs[index].cluster));
        EXPECT_EQ(utf16Prefix.size(), kExpectedUtf16Clusters[index]) << "glyph index=" << index;
    }
    EXPECT_EQ(originalUtf8, text);
    EXPECT_EQ(originalUtf16, SkUnicode::convertUtf8ToUtf16(text.data(), text.size()));

    const auto& first = vertical.glyphs.front();
    EXPECT_NE(first.verticalOrigin.x(), 0.0f);
    EXPECT_NE(first.verticalOrigin.y(), 0.0f);
    EXPECT_NEAR(first.baseline.x(), -first.verticalOrigin.x(), 0.01f);
    EXPECT_NEAR(first.baseline.y(), -first.verticalOrigin.y(), 0.01f);
}

TEST(SkiaVerticalTextFallbackFeasibility, MissingVerticalMetricsUseFontFallback) {
    auto fallbackFont = LoadFont(MILESTRO_TEST_FALLBACK_FONT_PATH);
    ASSERT_NE(fallbackFont, nullptr) << MILESTRO_TEST_FALLBACK_FONT_PATH;
    EXPECT_EQ(fallbackFont->typeface->getTableSize(SkSetFourByteTag('v', 'h', 'e', 'a')), 0u);
    EXPECT_EQ(fallbackFont->typeface->getTableSize(SkSetFourByteTag('v', 'm', 't', 'x')), 0u);

    const auto horizontal = Shape(*fallbackFont, "A", HB_DIRECTION_LTR, HB_SCRIPT_LATIN, "en");
    const auto vertical = Shape(*fallbackFont, "A", HB_DIRECTION_TTB, HB_SCRIPT_LATIN, "en");
    ASSERT_EQ(horizontal.glyphs.size(), 1u);
    ASSERT_EQ(vertical.glyphs.size(), 1u);
    EXPECT_EQ(horizontal.glyphs.front().glyph, vertical.glyphs.front().glyph);
    EXPECT_EQ(vertical.glyphs.front().hbYAdvance,
              hb_font_get_glyph_v_advance(fallbackFont->hbFont.get(), vertical.glyphs.front().glyph));
    EXPECT_LT(vertical.glyphs.front().verticalOrigin.y(), 0.0f);
}

TEST_F(SkiaVerticalTextFeasibility, TtbShaperApiShapesSourceHanUprightWithGlobalClusters) {
    constexpr std::string_view text = "A「漢A」、。ー";
    constexpr size_t kRunStart = 1;
    const std::string_view runText = text.substr(kRunStart);

    TtbShapeRequest request;
    request.utf8 = text;
    request.runStart = kRunStart;
    request.runLength = runText.size();
    request.font = font->skFont;
    request.script = SkSetFourByteTag('H', 'a', 'n', 'i');
    request.language = "ja";
    request.orientation = TtbOrientation::kUpright;
    request.sidewaysDirection = TtbHorizontalDirection::kLeftToRight;

    TtbShapedRun actual;
    ASSERT_TRUE(ShapeTtbRun(request, &actual));
    const auto expected = Shape(*font, runText, HB_DIRECTION_TTB, HB_SCRIPT_HAN, "ja");

    EXPECT_EQ(actual.orientation, TtbOrientation::kUpright);
    EXPECT_EQ(actual.sidewaysDirection, TtbHorizontalDirection::kLeftToRight);
    ASSERT_EQ(actual.glyphs.size(), expected.glyphs.size());
    ASSERT_EQ(actual.glyphs.size(), 7u);

    constexpr std::array<uint32_t, 7> kExpectedGlobalClusters = {1, 4, 7, 8, 11, 14, 17};
    for (size_t index = 0; index < actual.glyphs.size(); ++index) {
        const auto& glyph = actual.glyphs[index];
        const auto& reference = expected.glyphs[index];
        EXPECT_EQ(glyph.clusterUtf8, kExpectedGlobalClusters[index]);
        EXPECT_EQ(glyph.clusterUtf8, reference.cluster + kRunStart);
        EXPECT_EQ(glyph.glyph, reference.glyph);
        EXPECT_NEAR(glyph.position.x() + glyph.offset.x(), reference.baseline.x(), 0.01f);
        EXPECT_NEAR(glyph.position.y() + glyph.offset.y(), reference.baseline.y(), 0.01f);
        EXPECT_NEAR(glyph.advance.x(), reference.advance.x(), 0.01f);
        EXPECT_NEAR(glyph.advance.y(), reference.advance.y(), 0.01f);
        EXPECT_NEAR(glyph.advance.x(), 0.0f, 0.01f);
        EXPECT_GT(glyph.advance.y(), 0.0f);
    }
    EXPECT_NEAR(actual.advance.x(), expected.advance.x(), 0.01f);
    EXPECT_NEAR(actual.advance.y(), expected.advance.y(), 0.01f);
    EXPECT_GT(actual.advance.y(), 0.0f);
    EXPECT_EQ(actual.unresolvedGlyphs, 0u);
}

TEST_F(SkiaVerticalTextFeasibility, TtbShaperApiScopesVrt2ToItsFeatureRange) {
    constexpr std::string_view text = "「漢」。ー";
    constexpr size_t kVrt2Start = 3;
    constexpr size_t kVrt2End = 6;
    TtbShapeRequest request = MakeUprightRequest(*font, text);
    const std::array<TtbFeature, 1> features = {
            TtbFeatureAt(SkSetFourByteTag('v', 'r', 't', '2'), 1, kVrt2Start, kVrt2End),
    };
    request.features = SkSpan<const TtbFeature>(features);

    TtbShapedRun actual;
    ASSERT_TRUE(ShapeTtbRun(request, &actual));
    const auto expected = Shape(*font, text, HB_DIRECTION_TTB, HB_SCRIPT_HAN, "ja");
    const auto horizontal = Shape(*font, text, HB_DIRECTION_LTR, HB_SCRIPT_HAN, "ja");
    ASSERT_EQ(actual.glyphs.size(), expected.glyphs.size());
    ASSERT_EQ(horizontal.glyphs.size(), expected.glyphs.size());

    bool sawVerticalSubstitution = false;
    for (size_t index = 0; index < actual.glyphs.size(); ++index) {
        const auto& glyph = actual.glyphs[index];
        EXPECT_EQ(glyph.clusterUtf8, expected.glyphs[index].cluster);
        if (glyph.clusterUtf8 >= kVrt2Start && glyph.clusterUtf8 < kVrt2End) {
            continue;
        }
        EXPECT_EQ(glyph.glyph, expected.glyphs[index].glyph) << "index=" << index;
        sawVerticalSubstitution = sawVerticalSubstitution || glyph.glyph != horizontal.glyphs[index].glyph;
    }
    EXPECT_TRUE(sawVerticalSubstitution);
}

TEST_F(SkiaVerticalTextFeasibility, TtbShaperApiPreservesUserVertOutsideVrt2Range) {
    constexpr std::string_view text = "「漢」。ー";
    constexpr size_t kVrt2Start = 3;
    constexpr size_t kVrt2End = 6;
    constexpr size_t kUserVertStart = 6;
    constexpr size_t kUserVertEnd = 9;
    TtbShapeRequest request = MakeUprightRequest(*font, text);
    const std::array<TtbFeature, 2> features = {
            TtbFeatureAt(SkSetFourByteTag('v', 'r', 't', '2'), 1, kVrt2Start, kVrt2End),
            TtbFeatureAt(SkSetFourByteTag('v', 'e', 'r', 't'), 0, kUserVertStart, kUserVertEnd),
    };
    request.features = SkSpan<const TtbFeature>(features);

    TtbShapedRun actual;
    ASSERT_TRUE(ShapeTtbRun(request, &actual));
    const std::vector<hb_feature_t> expectedFeatures = {
            {HB_TAG('v', 'r', 't', '2'), 1, 3, 6},
            {HB_TAG('v', 'e', 'r', 't'), 0, 6, 9},
            {HB_TAG('v', 'e', 'r', 't'), 0, 3, 6},
    };
    const auto expected = Shape(*font, text, HB_DIRECTION_TTB, HB_SCRIPT_HAN, "ja", expectedFeatures);
    const auto automaticVert = Shape(*font, text, HB_DIRECTION_TTB, HB_SCRIPT_HAN, "ja");
    const std::vector<hb_feature_t> disabledFeatures = {
            {HB_TAG('v', 'e', 'r', 't'), 0, 0, static_cast<unsigned int>(text.size())},
    };
    const auto disabledVert = Shape(*font, text, HB_DIRECTION_TTB, HB_SCRIPT_HAN, "ja", disabledFeatures);
    ASSERT_EQ(actual.glyphs.size(), expected.glyphs.size());
    ASSERT_EQ(actual.glyphs.size(), automaticVert.glyphs.size());
    ASSERT_EQ(actual.glyphs.size(), disabledVert.glyphs.size());

    for (size_t index = 0; index < actual.glyphs.size(); ++index) {
        const auto& glyph = actual.glyphs[index];
        const auto& reference = expected.glyphs[index];
        EXPECT_EQ(glyph.clusterUtf8, reference.cluster);
        EXPECT_EQ(glyph.glyph, reference.glyph) << "index=" << index;
        EXPECT_NEAR(glyph.position.x() + glyph.offset.x(), reference.baseline.x(), 0.01f);
        EXPECT_NEAR(glyph.position.y() + glyph.offset.y(), reference.baseline.y(), 0.01f);
        EXPECT_NEAR(glyph.advance.x(), reference.advance.x(), 0.01f);
        EXPECT_NEAR(glyph.advance.y(), reference.advance.y(), 0.01f);
    }

    constexpr size_t kAutomaticVertIndex = 3;
    constexpr size_t kDisabledVertIndex = 2;
    ASSERT_NE(automaticVert.glyphs[kAutomaticVertIndex].glyph, disabledVert.glyphs[kAutomaticVertIndex].glyph);
    EXPECT_EQ(actual.glyphs[kAutomaticVertIndex].glyph, automaticVert.glyphs[kAutomaticVertIndex].glyph);
    ASSERT_NE(automaticVert.glyphs[kDisabledVertIndex].glyph, disabledVert.glyphs[kDisabledVertIndex].glyph);
    EXPECT_EQ(actual.glyphs[kDisabledVertIndex].glyph, disabledVert.glyphs[kDisabledVertIndex].glyph);
    EXPECT_EQ(actual.unresolvedGlyphs, 0u);
}

TEST_F(SkiaVerticalTextFeasibility, TtbShaperApiAppliesLastVrt2OverridePerRange) {
    constexpr std::string_view text = "「漢」";
    TtbShapeRequest request = MakeUprightRequest(*font, text);
    const std::array<TtbFeature, 2> features = {
            TtbFeatureAt(SkSetFourByteTag('v', 'r', 't', '2'), 1, 0, text.size()),
            TtbFeatureAt(SkSetFourByteTag('v', 'r', 't', '2'), 0, 3, text.size()),
    };
    request.features = SkSpan<const TtbFeature>(features);

    TtbShapedRun actual;
    ASSERT_TRUE(ShapeTtbRun(request, &actual));
    const auto expected = Shape(*font, text, HB_DIRECTION_TTB, HB_SCRIPT_HAN, "ja");
    ASSERT_EQ(actual.glyphs.size(), expected.glyphs.size());
    ASSERT_EQ(actual.glyphs.size(), 3u);

    for (size_t index = 1; index < actual.glyphs.size(); ++index) {
        EXPECT_EQ(actual.glyphs[index].clusterUtf8, expected.glyphs[index].cluster);
        EXPECT_EQ(actual.glyphs[index].glyph, expected.glyphs[index].glyph) << "index=" << index;
    }
}

TEST_F(SkiaVerticalTextFeasibility, TtbShaperApiUsesGlobalFeatureRangesWithRunStart) {
    constexpr std::string_view text = "A「漢」";
    constexpr size_t kRunStart = 1;
    TtbShapeRequest request = MakeUprightRequest(*font, text.substr(kRunStart));
    request.utf8 = text;
    request.runStart = kRunStart;
    request.runLength = text.size() - kRunStart;
    const std::array<TtbFeature, 1> features = {
            TtbFeatureAt(SkSetFourByteTag('v', 'r', 't', '2'), 1, kRunStart, kRunStart + 3),
    };
    request.features = SkSpan<const TtbFeature>(features);

    TtbShapedRun actual;
    ASSERT_TRUE(ShapeTtbRun(request, &actual));
    const auto expected = Shape(*font, text.substr(kRunStart), HB_DIRECTION_TTB, HB_SCRIPT_HAN, "ja");
    ASSERT_EQ(actual.glyphs.size(), expected.glyphs.size());
    ASSERT_EQ(actual.glyphs.size(), 3u);
    EXPECT_EQ(actual.glyphs[0].clusterUtf8, kRunStart);
    EXPECT_EQ(actual.glyphs[1].clusterUtf8, kRunStart + 3);
    EXPECT_EQ(actual.glyphs[2].clusterUtf8, kRunStart + 6);

    for (size_t index = 1; index < actual.glyphs.size(); ++index) {
        EXPECT_EQ(actual.glyphs[index].glyph, expected.glyphs[index].glyph) << "index=" << index;
    }
}

TEST(SkiaVerticalTextTtbShaper, ShapesFontWithoutVerticalTablesUprightAndSideways) {
    auto fallbackFont = LoadFont(MILESTRO_TEST_FALLBACK_FONT_PATH);
    ASSERT_NE(fallbackFont, nullptr) << MILESTRO_TEST_FALLBACK_FONT_PATH;

    constexpr std::string_view uprightText = "A";
    TtbShapeRequest upright;
    upright.utf8 = uprightText;
    upright.runLength = uprightText.size();
    upright.font = fallbackFont->skFont;
    upright.script = SkSetFourByteTag('L', 'a', 't', 'n');
    upright.language = "en";
    upright.orientation = TtbOrientation::kUpright;

    TtbShapedRun uprightRun;
    ASSERT_TRUE(ShapeTtbRun(upright, &uprightRun));
    const auto expectedUpright = Shape(*fallbackFont, uprightText, HB_DIRECTION_TTB, HB_SCRIPT_LATIN, "en");
    ASSERT_EQ(uprightRun.glyphs.size(), expectedUpright.glyphs.size());
    ASSERT_EQ(uprightRun.glyphs.size(), 1u);
    for (size_t index = 0; index < uprightRun.glyphs.size(); ++index) {
        const auto& glyph = uprightRun.glyphs[index];
        const auto& reference = expectedUpright.glyphs[index];
        EXPECT_EQ(glyph.glyph, reference.glyph);
        EXPECT_NEAR(glyph.position.x() + glyph.offset.x(), reference.baseline.x(), 0.01f);
        EXPECT_NEAR(glyph.position.y() + glyph.offset.y(), reference.baseline.y(), 0.01f);
        EXPECT_NEAR(glyph.advance.x(), reference.advance.x(), 0.01f);
        EXPECT_NEAR(glyph.advance.y(), reference.advance.y(), 0.01f);
    }
    EXPECT_GT(uprightRun.advance.y(), 0.0f);
    EXPECT_NEAR(uprightRun.advance.x(), 0.0f, 0.01f);
    EXPECT_EQ(uprightRun.unresolvedGlyphs, 0u);

    TtbShapeRequest sideways;
    sideways.utf8 = kSidewaysText;
    sideways.runLength = std::string_view(kSidewaysText).size();
    sideways.font = fallbackFont->skFont;
    sideways.script = SkSetFourByteTag('L', 'a', 't', 'n');
    sideways.language = "en";
    sideways.orientation = TtbOrientation::kSideways;
    sideways.sidewaysDirection = TtbHorizontalDirection::kLeftToRight;

    TtbShapedRun sidewaysRun;
    ASSERT_TRUE(ShapeTtbRun(sideways, &sidewaysRun));
    const auto expectedSideways = Shape(*fallbackFont, kSidewaysText, HB_DIRECTION_LTR, HB_SCRIPT_LATIN, "en");
    ASSERT_EQ(sidewaysRun.glyphs.size(), expectedSideways.glyphs.size());
    EXPECT_EQ(sidewaysRun.orientation, TtbOrientation::kSideways);
    EXPECT_EQ(sidewaysRun.sidewaysDirection, TtbHorizontalDirection::kLeftToRight);
    for (size_t index = 0; index < sidewaysRun.glyphs.size(); ++index) {
        const auto& glyph = sidewaysRun.glyphs[index];
        const auto& reference = expectedSideways.glyphs[index];
        EXPECT_EQ(glyph.glyph, reference.glyph);
        EXPECT_NEAR(glyph.position.x() + glyph.offset.x(), reference.baseline.x(), 0.01f);
        EXPECT_NEAR(glyph.position.y() + glyph.offset.y(), reference.baseline.y(), 0.01f);
        EXPECT_NEAR(glyph.advance.x(), reference.advance.x(), 0.01f);
        EXPECT_NEAR(glyph.advance.y(), 0.0f, 0.01f);
        EXPECT_GE(glyph.advance.x(), 0.0f);
    }
    EXPECT_NEAR(sidewaysRun.advance.x(), expectedSideways.advance.x(), 0.01f);
    EXPECT_NEAR(sidewaysRun.advance.y(), 0.0f, 0.01f);
    EXPECT_GT(sidewaysRun.advance.x(), 0.0f);
    EXPECT_EQ(sidewaysRun.unresolvedGlyphs, 0u);
}

TEST(SkiaVerticalTextTtbShaper, SidewaysRtlUsesVisualPositionsAndLogicalGlyphOrder) {
    auto fallbackFont = LoadFont(MILESTRO_TEST_FALLBACK_FONT_PATH);
    ASSERT_NE(fallbackFont, nullptr) << MILESTRO_TEST_FALLBACK_FONT_PATH;

    constexpr std::string_view text = "Aאבג";
    constexpr size_t kRunStart = 1;
    TtbShapeRequest request;
    request.utf8 = text;
    request.runStart = kRunStart;
    request.runLength = text.size() - kRunStart;
    request.font = fallbackFont->skFont;
    request.script = SkSetFourByteTag('H', 'e', 'b', 'r');
    request.language = "he";
    request.orientation = TtbOrientation::kSideways;
    request.sidewaysDirection = TtbHorizontalDirection::kRightToLeft;

    TtbShapedRun actual;
    ASSERT_TRUE(ShapeTtbRun(request, &actual));
    const auto expected = Shape(*fallbackFont, text.substr(kRunStart), HB_DIRECTION_RTL, HB_SCRIPT_HEBREW, "he");
    ASSERT_EQ(actual.glyphs.size(), expected.glyphs.size());
    ASSERT_EQ(actual.glyphs.size(), 3u);
    EXPECT_EQ(actual.orientation, TtbOrientation::kSideways);
    EXPECT_EQ(actual.sidewaysDirection, TtbHorizontalDirection::kRightToLeft);
    EXPECT_EQ(actual.unresolvedGlyphs, 0u);

    float previousX = 0.0f;
    for (size_t index = 0; index < actual.glyphs.size(); ++index) {
        const auto& glyph = actual.glyphs[index];
        EXPECT_NE(glyph.glyph, 0u);
        if (index > 0) {
            EXPECT_GT(glyph.clusterUtf8, actual.glyphs[index - 1].clusterUtf8);
            EXPECT_LT(glyph.position.x() + glyph.offset.x(), previousX) << "index=" << index;
        }
        previousX = glyph.position.x() + glyph.offset.x();

        const uint32_t localCluster = glyph.clusterUtf8 - static_cast<uint32_t>(kRunStart);
        const auto reference = std::find_if(expected.glyphs.begin(), expected.glyphs.end(), [&](const auto& candidate) {
            return candidate.cluster == localCluster;
        });
        ASSERT_TRUE(reference != expected.glyphs.end()) << "cluster=" << localCluster;
        EXPECT_EQ(glyph.glyph, reference->glyph);
        EXPECT_NEAR(glyph.position.x() + glyph.offset.x(), reference->baseline.x(), 0.01f);
        EXPECT_NEAR(glyph.position.y() + glyph.offset.y(), reference->baseline.y(), 0.01f);
        EXPECT_NEAR(glyph.advance.x(), reference->advance.x(), 0.01f);
        EXPECT_NEAR(glyph.advance.y(), reference->advance.y(), 0.01f);
    }
    EXPECT_NEAR(actual.advance.x(), expected.advance.x(), 0.01f);
    EXPECT_NEAR(actual.advance.y(), 0.0f, 0.01f);
    EXPECT_GT(actual.advance.x(), 0.0f);
}

TEST_F(SkiaVerticalTextFeasibility, TtbShaperApiRejectsRunsThatSplitUtf8Scalars) {
    constexpr std::string_view text = "A漢B";
    TtbShapeRequest request;
    request.utf8 = text;
    request.font = font->skFont;
    request.script = SkSetFourByteTag('H', 'a', 'n', 'i');
    request.language = "ja";

    TtbShapedRun run;
    request.runStart = 2;
    request.runLength = 1;
    EXPECT_FALSE(ShapeTtbRun(request, &run));

    request.runStart = 0;
    request.runLength = 2;
    EXPECT_FALSE(ShapeTtbRun(request, &run));

    request.runStart = 2;
    request.runLength = 0;
    EXPECT_FALSE(ShapeTtbRun(request, &run));

    request.runStart = 1;
    request.runLength = 3;
    ASSERT_TRUE(ShapeTtbRun(request, &run));
    ASSERT_EQ(run.glyphs.size(), 1u);
    EXPECT_EQ(run.glyphs.front().clusterUtf8, 1u);

    request.runLength = 0;
    ASSERT_TRUE(ShapeTtbRun(request, &run));
    EXPECT_TRUE(run.glyphs.empty());
}

TEST_F(SkiaVerticalTextFeasibility, TtbShaperApiCountsMissingGlyphsAndRejectsInvalidInput) {
    constexpr std::string_view missingText = "\xF4\x8F\xBF\xBF";
    TtbShapeRequest request;
    request.utf8 = missingText;
    request.runLength = missingText.size();
    request.font = font->skFont;
    request.script = SkSetFourByteTag('H', 'a', 'n', 'i');
    request.language = "und";

    TtbShapedRun missingRun;
    ASSERT_TRUE(ShapeTtbRun(request, &missingRun));
    ASSERT_FALSE(missingRun.glyphs.empty());
    const auto missingCount = std::count_if(missingRun.glyphs.begin(), missingRun.glyphs.end(), [](const auto& glyph) {
        return glyph.glyph == 0;
    });
    EXPECT_GT(missingCount, 0);
    EXPECT_EQ(missingRun.unresolvedGlyphs, static_cast<uint32_t>(missingCount));
    EXPECT_EQ(missingRun.glyphs.front().glyph, 0u);

    EXPECT_FALSE(ShapeTtbRun(request, nullptr));

    request.runStart = missingText.size() + 1;
    request.runLength = 0;
    TtbShapedRun invalidRun;
    EXPECT_FALSE(ShapeTtbRun(request, &invalidRun));

    request.runStart = 0;
    request.runLength = missingText.size() + 1;
    EXPECT_FALSE(ShapeTtbRun(request, &invalidRun));

    TtbFeature invalidFeature = {SkSetFourByteTag('v', 'e', 'r', 't'),
                                 1,
                                 missingText.size() + 1,
                                 missingText.size() + 2};
    request.runLength = missingText.size();
    request.features = SkSpan<const TtbFeature>(&invalidFeature, 1);
    EXPECT_FALSE(ShapeTtbRun(request, &invalidRun));

    request.features = {};
    request.font.setSize(0);
    EXPECT_FALSE(ShapeTtbRun(request, &invalidRun));

    request.runLength = 0;
    TtbShapedRun emptyRun;
    EXPECT_TRUE(ShapeTtbRun(request, &emptyRun));
    EXPECT_TRUE(emptyRun.glyphs.empty());
    EXPECT_EQ(emptyRun.advance.x(), 0.0f);
    EXPECT_EQ(emptyRun.advance.y(), 0.0f);
}

TEST_F(SkiaVerticalTextFeasibility, TtbShaperApiRejectsOutOfRangeFontScales) {
    TtbShapeRequest request;
    request.utf8 = "A";
    request.runLength = 1;
    request.font = font->skFont;
    request.script = SkSetFourByteTag('H', 'a', 'n', 'i');
    request.language = "ja";

    TtbShapedRun run;
    request.font.setSize(33554432.0f);
    EXPECT_FALSE(ShapeTtbRun(request, &run));

    request.font = font->skFont;
    request.font.setScaleX(1000000.0f);
    EXPECT_FALSE(ShapeTtbRun(request, &run));
}

TEST_F(SkiaVerticalTextFeasibility, CurrentSkShaperApiCannotRequestTtb) {
    constexpr std::string_view text = "「漢字」、。ー";
    auto* unicodeProvider = milestro::skia::GetUnicodeProvider();
    ASSERT_NE(unicodeProvider, nullptr);
    auto unicode = unicodeProvider->unwrap();
    ASSERT_NE(unicode, nullptr);
    auto shaper = SkShapers::HB::ShapeThenWrap(unicode, nullptr);
    ASSERT_NE(shaper, nullptr);

    SkShaper::TrivialFontRunIterator fontRun(font->skFont, text.size());
    SkShaper::TrivialBiDiRunIterator bidiRun(0, text.size());
    SkShaper::TrivialScriptRunIterator scriptRun(SkSetFourByteTag('H', 'a', 'n', 'i'), text.size());
    SkShaper::TrivialLanguageRunIterator languageRun("ja", text.size());
    RecordingRunHandler handler;

    shaper->shape(text.data(), text.size(), fontRun, bidiRun, scriptRun, languageRun, nullptr, 0, 4096.0f, &handler);

    ASSERT_FALSE(handler.advances.empty());
    for (const SkVector advance: handler.advances) {
        EXPECT_GT(advance.x(), 0.0f);
        EXPECT_EQ(advance.y(), 0.0f);
    }
    for (const uint8_t level: handler.levels) {
        EXPECT_EQ(level, 0u);
    }
}

TEST_F(SkiaVerticalTextFeasibility, SharedTransformsAgreeBetweenPaintingAndPaths) {
    const auto uprightRun = Shape(*font, kUprightText, HB_DIRECTION_TTB, HB_SCRIPT_HAN, "ja");
    const auto sidewaysRun = Shape(*font, kSidewaysText, HB_DIRECTION_LTR, HB_SCRIPT_LATIN, "en");
    ASSERT_FALSE(uprightRun.glyphs.empty());
    ASSERT_FALSE(sidewaysRun.glyphs.empty());

    const auto upright = MakeGeometry(*font, uprightRun, SkMatrix::Translate(kRightColumnCenterX, kTopMargin));
    SkMatrix sidewaysPlacement = SkMatrix::RotateDeg(90.0f);
    sidewaysPlacement.postTranslate(kLeftColumnLeftX, kTopMargin);
    const auto sideways = MakeGeometry(*font, sidewaysRun, sidewaysPlacement);

    const auto uprightBlobBounds = MakeBlob(upright)->bounds().makeOffset(kRightColumnCenterX, kTopMargin);
    const auto uprightPath = MakePath(upright);
    EXPECT_EQ(uprightPath.unconvertedGlyphs, 0);
    EXPECT_FALSE(uprightPath.path.isEmpty());
    EXPECT_TRUE(SkRect::MakeLTRB(0.0f, 0.0f, static_cast<float>(kCanvasWidth), static_cast<float>(kCanvasHeight))
                        .contains(InkBounds(upright)));
    EXPECT_FALSE(uprightBlobBounds.isEmpty());

    const auto sidewaysPath = MakePath(sideways);
    EXPECT_EQ(sidewaysPath.unconvertedGlyphs, 0);
    EXPECT_FALSE(sidewaysPath.path.isEmpty());
    EXPECT_GT(sidewaysPath.path.getBounds().height(), sidewaysPath.path.getBounds().width());

    const auto textBitmap = Raster({&upright, &sideways}, PaintStrategy::kTextBlob);
    const auto pathBitmap = Raster({&upright, &sideways}, PaintStrategy::kPath);
    const auto textBounds = RasterInkBounds(textBitmap);
    const auto pathBounds = RasterInkBounds(pathBitmap);
    ASSERT_TRUE(textBounds.has_value());
    ASSERT_TRUE(pathBounds.has_value());
    EXPECT_NEAR(textBounds->left(), pathBounds->left(), 2.0f);
    EXPECT_NEAR(textBounds->top(), pathBounds->top(), 2.0f);
    EXPECT_NEAR(textBounds->right(), pathBounds->right(), 2.0f);
    EXPECT_NEAR(textBounds->bottom(), pathBounds->bottom(), 2.0f);

    const auto difference = CompareBitmaps(textBitmap, pathBitmap);
    EXPECT_LT(difference.changedPixels, kCanvasWidth * kCanvasHeight / 20)
            << "total channel delta=" << difference.totalChannelDelta;
    SavePng(textBitmap, "vertical-text-feasibility.png");
    SavePng(pathBitmap, "vertical-text-paths-feasibility.png");
}

TEST_F(SkiaVerticalTextFeasibility, RightToLeftColumnsMapInlineAndBlockAxesConsistently) {
    const auto uprightRun = Shape(*font, kUprightText, HB_DIRECTION_TTB, HB_SCRIPT_HAN, "ja");
    const auto sidewaysRun = Shape(*font, kSidewaysText, HB_DIRECTION_LTR, HB_SCRIPT_LATIN, "en");
    ASSERT_FALSE(uprightRun.glyphs.empty());
    ASSERT_FALSE(sidewaysRun.glyphs.empty());

    const auto upright = MakeGeometry(*font, uprightRun, SkMatrix::Translate(kRightColumnCenterX, kTopMargin));
    SkMatrix sidewaysPlacement = SkMatrix::RotateDeg(90.0f);
    sidewaysPlacement.postTranslate(kLeftColumnLeftX, kTopMargin);
    const auto sideways = MakeGeometry(*font, sidewaysRun, sidewaysPlacement);

    const SkPoint uprightStart = upright.placement.mapPoint({0.0f, 0.0f});
    const SkPoint uprightAfterOneEm = upright.placement.mapPoint({0.0f, kFontSize});
    EXPECT_NEAR(uprightAfterOneEm.x(), uprightStart.x(), 0.01f);
    EXPECT_NEAR(uprightAfterOneEm.y() - uprightStart.y(), kFontSize, 0.01f);

    const SkPoint sidewaysStart = sideways.placement.mapPoint({0.0f, 0.0f});
    const SkPoint sidewaysAfterOneEm = sideways.placement.mapPoint({kFontSize, 0.0f});
    EXPECT_NEAR(sidewaysAfterOneEm.x(), sidewaysStart.x(), 0.01f);
    EXPECT_NEAR(sidewaysAfterOneEm.y() - sidewaysStart.y(), kFontSize, 0.01f);
    EXPECT_LT(sidewaysStart.x(), uprightStart.x());

    const auto bitmap = Raster({&upright, &sideways}, PaintStrategy::kTextBlob);
    const auto ink = RasterInkBounds(bitmap);
    ASSERT_TRUE(ink.has_value());
    EXPECT_LT(ink->top(), kTopMargin + kFontSize);
    EXPECT_GT(ink->bottom(), kTopMargin + kFontSize * 4.0f);

    SkBitmap leftHalf;
    leftHalf.allocN32Pixels(kCanvasWidth / 2, kCanvasHeight);
    ASSERT_TRUE(bitmap.readPixels(leftHalf.info(), leftHalf.getPixels(), leftHalf.rowBytes(), 0, 0));
    SkBitmap rightHalf;
    rightHalf.allocN32Pixels(kCanvasWidth / 2, kCanvasHeight);
    ASSERT_TRUE(bitmap.readPixels(rightHalf.info(), rightHalf.getPixels(), rightHalf.rowBytes(), kCanvasWidth / 2, 0));
    EXPECT_TRUE(RasterInkBounds(leftHalf).has_value());
    EXPECT_TRUE(RasterInkBounds(rightHalf).has_value());
}

} // namespace
