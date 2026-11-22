#include "Milestro/game/milestro_game_interface.h"
#include "Milestro/skia/FontRegistry.h"
#include "Milestro/skia/GlyphLayoutSnapshot.h"
#include "Milestro/skia/ReusableTextDrawSnapshot.h"
#include "Milestro/skia/TextDrawSnapshot.h"
#include "Milestro/skia/textlayout/FontCollection.h"
#include "Milestro/skia/textlayout/Paragraph.h"
#include "Milestro/skia/textlayout/ParagraphBuilder.h"
#include "Milestro/skia/textlayout/ParagraphStyle.h"
#include "Milestro/skia/textlayout/TextStyle.h"
#include "include/core/SkBitmap.h"
#include "include/core/SkImage.h"
#include "include/core/SkTypeface.h"
#include "include/ports/SkFontMgr_empty.h"
#include <gtest/gtest.h>
#include <algorithm>
#include <cmath>
#include <cstring>
#include <limits>

namespace {

namespace textlayout = milestro::skia::textlayout;
using milestro::skia::GlyphLayoutDescriptor;
using milestro::skia::GlyphLayoutSnapshot;

SkFont TestFont() {
    return SkFont(SkFontMgr_New_Custom_Empty()->makeFromFile(MILESTRO_TEST_FONT_PATH), 31.25f);
}

SkBitmap Bitmap() {
    SkBitmap bitmap;
    bitmap.allocN32Pixels(512, 192);
    bitmap.eraseColor(SK_ColorTRANSPARENT);
    return bitmap;
}

void ExpectPixelsEqual(const SkBitmap& expected, const SkBitmap& actual) {
    ASSERT_EQ(expected.computeByteSize(), actual.computeByteSize());
    EXPECT_EQ(std::memcmp(expected.getPixels(), actual.getPixels(), expected.computeByteSize()), 0);
}

std::vector<GlyphLayoutDescriptor> Descriptors(const GlyphLayoutSnapshot& snapshot) {
    std::vector<GlyphLayoutDescriptor> descriptors(snapshot.glyphCount());
    EXPECT_TRUE(snapshot.copyGlyphs(descriptors.data(), descriptors.size() * sizeof(GlyphLayoutDescriptor)));
    return descriptors;
}

void ExpectAtlasCompositionMatchesPaint(const GlyphLayoutSnapshot& snapshot, float scale,
                                       SkPoint origin) {
    SkBitmap expected;
    SkBitmap actual;
    ASSERT_TRUE(expected.tryAllocPixels(SkImageInfo::MakeA8(512, 192)));
    ASSERT_TRUE(actual.tryAllocPixels(SkImageInfo::MakeA8(512, 192)));
    expected.eraseColor(SK_ColorTRANSPARENT);
    actual.eraseColor(SK_ColorTRANSPARENT);
    SkCanvas expectedCanvas(expected);
    expectedCanvas.scale(scale, scale);
    snapshot.paint(&expectedCanvas, origin.x(), origin.y());
    SkCanvas actualCanvas(actual);
    const auto descriptors = Descriptors(snapshot);
    for (size_t index = 0; index < descriptors.size(); ++index) {
        const auto& descriptor = descriptors[index];
        const bool subpixel = (descriptor.fontFlags & 4) != 0;
        const bool baselineSnap = (descriptor.fontFlags & 32) != 0;
        const float samplesX = subpixel ? 4.0f : 1.0f;
        const float samplesY = subpixel && !baselineSnap ? 4.0f : 1.0f;
        const float roundedX = std::floor((origin.x() + descriptor.x) * scale * samplesX + 0.5f) / samplesX;
        const float roundedY = std::floor((origin.y() + descriptor.y) * scale * samplesY + 0.5f) / samplesY;
        const float anchorX = std::floor(roundedX);
        const float anchorY = std::floor(roundedY);
        const float phaseX = roundedX - anchorX;
        const float phaseY = roundedY - anchorY;
        SkIRect bounds;
        ASSERT_TRUE(snapshot.rasterBounds(index, scale, phaseX, phaseY, bounds));
        if (bounds.isEmpty()) continue;
        std::vector<uint8_t> pixels(size_t(bounds.width()) * size_t(bounds.height()));
        ASSERT_TRUE(snapshot.rasterize(index, scale, phaseX, phaseY, pixels.data(), pixels.size()));
        SkBitmap glyph;
        ASSERT_TRUE(glyph.installPixels(SkImageInfo::MakeA8(bounds.width(), bounds.height()),
                                       pixels.data(), bounds.width()));
        SkPaint paint;
        paint.setColor(SK_ColorWHITE);
        actualCanvas.drawImage(SkImages::RasterFromBitmap(glyph), anchorX + bounds.left(),
                               anchorY + bounds.top(), SkSamplingOptions(), &paint);
    }
    int differentPixels = 0;
    int maximumDifference = 0;
    SkIPoint largestDifference = SkIPoint::Make(0, 0);
    for (int row = 0; row < expected.height(); ++row) {
        for (int column = 0; column < expected.width(); ++column) {
            const int difference = std::abs(int(*expected.getAddr8(column, row)) - int(*actual.getAddr8(column, row)));
            if (difference > 0) ++differentPixels;
            if (difference > maximumDifference) {
                maximumDifference = difference;
                largestDifference = SkIPoint::Make(column, row);
            }
        }
    }
    EXPECT_EQ(differentPixels, 0) << "maximumDifference=" << maximumDifference
        << " at=" << largestDifference.x() << "," << largestDifference.y()
        << " expected=" << int(*expected.getAddr8(largestDifference.x(), largestDifference.y()))
        << " actual=" << int(*actual.getAddr8(largestDifference.x(), largestDifference.y()));
}

std::unique_ptr<textlayout::Paragraph> MakeParagraph(bool decorated = false, bool vertical = false,
                                                   bool whiteForeground = false) {
    textlayout::TextStyle style;
    style.setFontFamilies({SkString("Source Han Sans VF")});
    style.setFontSize(28);
    style.setColor(whiteForeground ? SK_ColorWHITE : SK_ColorRED);
    if (decorated) { style.setDecoration(::skia::textlayout::TextDecoration::kUnderline); }
    textlayout::ParagraphStyle paragraphStyle;
    paragraphStyle.setTextStyle(&style);
    if (vertical) { paragraphStyle.setWritingMode(::skia::textlayout::WritingMode::kVerticalRl); }
    textlayout::ParagraphBuilder builder(&paragraphStyle);
    builder.pushStyle(&style);
    const std::string first = "12 fi \xE5\xA4\xA9\n";
    builder.addText(first.data(), first.size());
    style.setColor(whiteForeground ? SK_ColorWHITE : SK_ColorBLUE);
    style.setFontSize(34);
    builder.pushStyle(&style);
    const std::string second = "345\xE5\x9C\xB0";
    builder.addText(second.data(), second.size());
    auto paragraph = std::unique_ptr<textlayout::Paragraph>(builder.build());
    paragraph->layout(220);
    return paragraph;
}

class GlyphLayoutSnapshotTest : public testing::Test {
protected:
    void SetUp() override {
        ASSERT_NE(milestro::skia::GetFontRegistry()->RegisterFontFromFile(MILESTRO_TEST_FONT_PATH),
                  milestro::skia::MilestroRegisteredFontMgr::RegisterResult::Failed);
        textlayout::GetFontCollection()->clearCaches();
    }
};

TEST_F(GlyphLayoutSnapshotTest, ReusableMeasureAndPaintMatchSimpleTextIncludingWhitespace) {
    auto font = TestFont();
    ASSERT_NE(font.getTypeface(), nullptr);
    milestro::skia::Font wrapped(font);
    milestro::skia::ReusableTextDrawSnapshot snapshot(wrapped, 128, SK_ColorGREEN);
    const std::vector<std::string> samples = {"", "   ", "jAf 01239", "Wj", "\xE5\xA4\xA9 12"};
    for (const auto& text : samples) {
        ASSERT_TRUE(snapshot.updateText(text.data(), text.size()));
        SkRect expectedBounds;
        const float expectedAdvance = font.measureText(text.data(), text.size(), SkTextEncoding::kUTF8, &expectedBounds);
        SkRect actualBounds;
        EXPECT_NEAR(snapshot.measureText(&actualBounds), expectedAdvance, 0.001f);
        EXPECT_NEAR(actualBounds.left(), expectedBounds.left(), 0.001f);
        EXPECT_NEAR(actualBounds.top(), expectedBounds.top(), 0.001f);
        EXPECT_NEAR(actualBounds.right(), expectedBounds.right(), 0.001f);
        EXPECT_NEAR(actualBounds.bottom(), expectedBounds.bottom(), 0.001f);
        auto expected = Bitmap();
        auto actual = Bitmap();
        SkCanvas expectedCanvas(expected);
        SkCanvas actualCanvas(actual);
        SkPaint paint;
        paint.setColor(SK_ColorGREEN);
        expectedCanvas.drawSimpleText(text.data(), text.size(), SkTextEncoding::kUTF8, 21.25f, 70.5f, font, paint);
        snapshot.paintText(&actualCanvas, 21.25f, 70.5f);
        ExpectPixelsEqual(expected, actual);
    }
}

TEST_F(GlyphLayoutSnapshotTest, OwnedExportSurvivesSourceChangeAndDestructionAndCopyRecolors) {
    auto font = TestFont();
    milestro::skia::GlyphLayoutSnapshot* owned = nullptr;
    {
        milestro::skia::Font wrapped(font);
        milestro::skia::ReusableTextDrawSnapshot source(wrapped, 32, SK_ColorRED);
        milestro::skia::ReusableTextDrawSnapshot destination(wrapped, 32, SK_ColorBLUE);
        ASSERT_TRUE(source.updateText("123", 3));
        ASSERT_TRUE(destination.copyTextFrom(source));
        const auto descriptors = Descriptors(*destination.glyphLayout());
        ASSERT_EQ(descriptors.size(), 3u);
        EXPECT_EQ(descriptors.front().color, SK_ColorBLUE);
        ASSERT_EQ(MilestroSkiaGlyphLayoutSnapshotCreateFromText(&source, owned), 0);
        ASSERT_NE(owned, nullptr);
        ASSERT_TRUE(source.updateText("9", 1));
    }
    EXPECT_EQ(owned->glyphCount(), 3u);
    EXPECT_EQ(Descriptors(*owned).front().color, SK_ColorRED);
    SkIRect bounds;
    ASSERT_TRUE(owned->rasterBounds(0, 2, 0, 0, bounds));
    std::vector<uint8_t> pixels(size_t(bounds.width()) * size_t(bounds.height()));
    ASSERT_TRUE(owned->rasterize(0, 2, 0, 0, pixels.data(), pixels.size()));
    EXPECT_TRUE(std::any_of(pixels.begin(), pixels.end(), [](uint8_t pixel) { return pixel != 0; }));
    EXPECT_EQ(MilestroSkiaGlyphLayoutSnapshotDestroy(owned), 0);
    EXPECT_EQ(owned, nullptr);
}

TEST_F(GlyphLayoutSnapshotTest, CompactSimpleExportMatchesOwnedLayoutAndRejectsShortBuffers) {
    milestro::skia::Font wrapped(TestFont());
    milestro::skia::ReusableTextDrawSnapshot source(wrapped, 128, SK_ColorWHITE);
    struct FontDescriptor {
        uint32_t typefaceId, flags, color;
        float size, scaleX, skewX;
    } fontData{};
    struct Placement { uint32_t glyphId; float x, y; };
    std::vector<Placement> placements(128);
    for (const std::string text : {"123456789", "9", "", " 12", "\xE5\xA4\xA9"}) {
        ASSERT_TRUE(source.updateText(text.data(), text.size()));
        const auto expected = Descriptors(*source.glyphLayout());
        uint64_t count = 0;
        EXPECT_EQ(MilestroSkiaReusableTextDrawSnapshotCopyGlyphs(&source,
            reinterpret_cast<uint8_t*>(&fontData), reinterpret_cast<uint8_t*>(placements.data()),
            placements.size() * sizeof(Placement), count), 0);
        ASSERT_EQ(count, expected.size());
        for (size_t index = 0; index < expected.size(); ++index) {
            EXPECT_EQ(placements[index].glyphId, expected[index].glyphId);
            EXPECT_EQ(placements[index].x, expected[index].x);
            EXPECT_EQ(placements[index].y, expected[index].y);
            EXPECT_EQ(fontData.typefaceId, expected[index].typefaceId);
            EXPECT_EQ(fontData.flags, expected[index].fontFlags);
            EXPECT_EQ(fontData.color, expected[index].color);
            EXPECT_EQ(fontData.size, expected[index].fontSize);
            EXPECT_EQ(fontData.scaleX, expected[index].fontScaleX);
            EXPECT_EQ(fontData.skewX, expected[index].fontSkewX);
            SkIRect expectedBounds;
            ASSERT_TRUE(source.glyphLayout()->rasterBounds(index, 1.25f, 0.25f, 0, expectedBounds));
            int32_t left, top, width, height;
            ASSERT_EQ(MilestroSkiaReusableTextDrawSnapshotGetRasterBounds(&source, index,
                1.25f, 0.25f, 0, left, top, width, height), 0);
            EXPECT_EQ(SkIRect::MakeXYWH(left, top, width, height), expectedBounds);
            std::vector<uint8_t> expectedPixels(size_t(width) * size_t(height));
            auto actualPixels = expectedPixels;
            ASSERT_TRUE(source.glyphLayout()->rasterize(index, 1.25f, 0.25f, 0,
                expectedPixels.data(), expectedPixels.size()));
            ASSERT_EQ(MilestroSkiaReusableTextDrawSnapshotRasterize(&source, index,
                1.25f, 0.25f, 0, actualPixels.data(), actualPixels.size()), 0);
            EXPECT_EQ(expectedPixels, actualPixels);
        }
        if (!expected.empty()) {
            placements.front().glyphId = 0xffffffff;
            EXPECT_LT(MilestroSkiaReusableTextDrawSnapshotCopyGlyphs(&source,
                reinterpret_cast<uint8_t*>(&fontData), reinterpret_cast<uint8_t*>(placements.data()),
                expected.size() * sizeof(Placement) - 1, count), 0);
            EXPECT_EQ(placements.front().glyphId, 0xffffffff);
        }
    }
    uint64_t count = 99;
    EXPECT_LT(MilestroSkiaReusableTextDrawSnapshotCopyGlyphs(nullptr,
        reinterpret_cast<uint8_t*>(&fontData), nullptr, 0, count), 0);
    EXPECT_EQ(count, 0u);
}

TEST_F(GlyphLayoutSnapshotTest, ParagraphCapturesMultilineRunsAndReplaysPixelsAfterSourceLifetime) {
    auto paragraph = MakeParagraph();
    auto snapshot = GlyphLayoutSnapshot::FromParagraph(*paragraph);
    ASSERT_NE(snapshot, nullptr);
    const auto descriptors = Descriptors(*snapshot);
    ASSERT_FALSE(descriptors.empty());
    EXPECT_TRUE(std::any_of(descriptors.begin(), descriptors.end(), [](const auto& glyph) { return glyph.color == SK_ColorRED; }));
    EXPECT_TRUE(std::any_of(descriptors.begin(), descriptors.end(), [](const auto& glyph) { return glyph.color == SK_ColorBLUE; }));
    EXPECT_GT(descriptors.back().y, descriptors.front().y);
    auto expected = Bitmap();
    SkCanvas expectedCanvas(expected);
    paragraph->paint(&expectedCanvas, 23, 17);
    paragraph.reset();
    auto actual = Bitmap();
    SkCanvas actualCanvas(actual);
    snapshot->paint(&actualCanvas, 23, 17);
    ExpectPixelsEqual(expected, actual);
}

TEST_F(GlyphLayoutSnapshotTest, UnsupportedParagraphIsRejectedAsWholeAndApiReturnsNull) {
    for (const bool vertical : {false, true}) {
        auto paragraph = MakeParagraph(!vertical, vertical);
        EXPECT_EQ(GlyphLayoutSnapshot::FromParagraph(*paragraph), nullptr);
        GlyphLayoutSnapshot* snapshot = nullptr;
        EXPECT_EQ(MilestroSkiaGlyphLayoutSnapshotCreateFromParagraph(paragraph.get(), snapshot), 0);
        EXPECT_EQ(snapshot, nullptr);
    }
}

TEST_F(GlyphLayoutSnapshotTest, ParagraphKeepsActualFallbackFontsAndShapedGlyphPositions) {
    ASSERT_NE(milestro::skia::GetFontRegistry()->RegisterFontFromFile(MILESTRO_TEST_FALLBACK_FONT_PATH),
              milestro::skia::MilestroRegisteredFontMgr::RegisterResult::Failed);
    textlayout::GetFontCollection()->clearCaches();
    textlayout::TextStyle style;
    style.setFontFamilies({SkString("Liberation Sans"), SkString("Source Han Sans VF")});
    style.setFontSize(28);
    style.setColor(SK_ColorWHITE);
    textlayout::ParagraphStyle paragraphStyle;
    paragraphStyle.setTextStyle(&style);
    textlayout::ParagraphBuilder builder(&paragraphStyle);
    builder.pushStyle(&style);
    const std::string text = "office \xE5\xA4\xA9";
    builder.addText(text.data(), text.size());
    auto paragraph = std::unique_ptr<textlayout::Paragraph>(builder.build());
    paragraph->layout(300);
    auto snapshot = GlyphLayoutSnapshot::FromParagraph(*paragraph);
    ASSERT_NE(snapshot, nullptr);
    const auto descriptors = Descriptors(*snapshot);
    ASSERT_GE(descriptors.size(), 2u);
    EXPECT_TRUE(std::any_of(descriptors.begin(), descriptors.end(), [&](const auto& glyph) {
        return glyph.typefaceId != descriptors.front().typefaceId;
    }));
    size_t glyphIndex = 0;
    paragraph->unwrap()->extendedVisit([&](int, const ::skia::textlayout::Paragraph::ExtendedVisitorInfo* info) {
        if (!info) { return; }
        for (int index = 0; index < info->count; ++index) {
            ASSERT_LT(glyphIndex, descriptors.size());
            const auto& glyph = descriptors[glyphIndex++];
            EXPECT_EQ(glyph.glyphId, info->glyphs[index]);
            EXPECT_EQ(glyph.typefaceId, info->font.getTypeface()->uniqueID());
            EXPECT_NEAR(glyph.x, info->origin.x() + info->positions[index].x(), 0.001f);
            EXPECT_NEAR(glyph.y, info->origin.y() + info->positions[index].y(), 0.001f);
        }
    });
    EXPECT_EQ(glyphIndex, descriptors.size());
}

TEST_F(GlyphLayoutSnapshotTest, RasterAndBulkCopyRejectInvalidIndicesScaleAndCapacity) {
    GlyphLayoutSnapshot snapshot;
    auto font = TestFont();
    snapshot.updateSimpleText(font, "1 ", 2, SK_ColorWHITE);
    SkIRect bounds;
    EXPECT_FALSE(snapshot.rasterBounds(2, 1, 0, 0, bounds));
    EXPECT_FALSE(snapshot.rasterBounds(0, 0, 0, 0, bounds));
    EXPECT_FALSE(snapshot.rasterBounds(0, std::numeric_limits<float>::infinity(), 0, 0, bounds));
    EXPECT_FALSE(snapshot.rasterBounds(0, 1, -0.25f, 0, bounds));
    EXPECT_FALSE(snapshot.rasterBounds(0, 1, 0.1f, 0, bounds));
    EXPECT_FALSE(snapshot.rasterBounds(0, 1, 0, 1, bounds));
    EXPECT_FALSE(snapshot.rasterBounds(0, 1, 0, std::numeric_limits<float>::quiet_NaN(), bounds));
    EXPECT_FALSE(snapshot.copyGlyphs(nullptr, 0));
    ASSERT_TRUE(snapshot.rasterBounds(0, 1.5f, 0, 0, bounds));
    EXPECT_FALSE(snapshot.rasterize(0, 1.5f, 0, 0, nullptr, 0));
    ASSERT_TRUE(snapshot.rasterBounds(1, 1.5f, 0, 0, bounds));
    EXPECT_TRUE(bounds.isEmpty());
    EXPECT_TRUE(snapshot.rasterize(1, 1.5f, 0, 0, nullptr, 0));
}

TEST_F(GlyphLayoutSnapshotTest, AtlasMaskUsesZeroPhaseGrayscaleAndDoesNotApplyForegroundAlpha) {
    auto font = TestFont();
    font.setEdging(SkFont::Edging::kSubpixelAntiAlias);
    GlyphLayoutSnapshot opaque;
    GlyphLayoutSnapshot transparent;
    opaque.updateSimpleText(font, "1", 1, SK_ColorWHITE);
    transparent.updateSimpleText(font, "1", 1, SkColorSetARGB(64, 5, 90, 210));
    EXPECT_EQ(Descriptors(transparent).front().color, SkColorSetARGB(64, 5, 90, 210));
    SkIRect bounds;
    ASSERT_TRUE(opaque.rasterBounds(0, 1.5f, 0, 0, bounds));
    std::vector<uint8_t> opaquePixels(size_t(bounds.width()) * size_t(bounds.height()));
    std::vector<uint8_t> transparentPixels(opaquePixels.size());
    ASSERT_TRUE(opaque.rasterize(0, 1.5f, 0, 0, opaquePixels.data(), opaquePixels.size()));
    ASSERT_TRUE(transparent.rasterize(0, 1.5f, 0, 0, transparentPixels.data(), transparentPixels.size()));
    EXPECT_EQ(opaquePixels, transparentPixels);
    SkBitmap expected;
    ASSERT_TRUE(expected.tryAllocPixels(SkImageInfo::MakeA8(bounds.width(), bounds.height())));
    expected.eraseColor(SK_ColorTRANSPARENT);
    SkCanvas canvas(expected);
    font.setEdging(SkFont::Edging::kAntiAlias);
    SkPaint paint;
    paint.setColor(SK_ColorWHITE);
    canvas.translate(-bounds.left(), -bounds.top());
    canvas.scale(1.5f, 1.5f);
    canvas.drawSimpleText("1", 1, SkTextEncoding::kUTF8, 0, 0, font, paint);
    for (int row = 0; row < bounds.height(); ++row) {
        EXPECT_EQ(std::memcmp(expected.getAddr8(0, row), opaquePixels.data() + size_t(row) * bounds.width(), bounds.width()), 0);
    }
}

TEST_F(GlyphLayoutSnapshotTest, QuantizedAtlasPhasesMatchWholeTextUnderCanvasScale) {
    for (const bool subpixel : {false, true}) {
        for (const bool baselineSnap : {false, true}) {
            auto font = TestFont();
            font.setEdging(SkFont::Edging::kAntiAlias);
            font.setSubpixel(subpixel);
            font.setBaselineSnap(baselineSnap);
            GlyphLayoutSnapshot snapshot;
            const std::string text = "AV office 0123456789";
            snapshot.updateSimpleText(font, text.data(), text.size(), SK_ColorWHITE);
            for (const float scale : {1.0f, 1.25f}) {
                for (const auto origin : {SkPoint::Make(20.35f, 60.65f), SkPoint::Make(-3.35f, 60.15f)}) {
                    SCOPED_TRACE(testing::Message() << "subpixel=" << subpixel << " baselineSnap="
                        << baselineSnap << " scale=" << scale << " origin=" << origin.x() << "," << origin.y());
                    ExpectAtlasCompositionMatchesPaint(snapshot, scale, origin);
                }
            }
        }
    }
}

TEST_F(GlyphLayoutSnapshotTest, QuantizedAtlasPhasesMatchParagraphUnderCanvasScale) {
    auto paragraph = MakeParagraph(false, false, true);
    auto snapshot = GlyphLayoutSnapshot::FromParagraph(*paragraph);
    ASSERT_NE(snapshot, nullptr);
    for (const float scale : {1.0f, 1.25f}) {
        for (const auto origin : {SkPoint::Make(20.35f, 10.65f), SkPoint::Make(-3.35f, 10.15f)}) {
            SCOPED_TRACE(testing::Message() << "scale=" << scale << " origin=" << origin.x() << "," << origin.y());
            ExpectAtlasCompositionMatchesPaint(*snapshot, scale, origin);
        }
    }
}

TEST_F(GlyphLayoutSnapshotTest, SkiaColoredCoverageDiffersFromCanonicalWhiteAtlasCoverage) {
    auto coloredParagraph = MakeParagraph();
    auto whiteParagraph = MakeParagraph(false, false, true);
    auto coloredSnapshot = GlyphLayoutSnapshot::FromParagraph(*coloredParagraph);
    auto whiteSnapshot = GlyphLayoutSnapshot::FromParagraph(*whiteParagraph);
    ASSERT_NE(coloredSnapshot, nullptr);
    ASSERT_NE(whiteSnapshot, nullptr);
    const auto coloredDescriptors = Descriptors(*coloredSnapshot);
    const auto whiteDescriptors = Descriptors(*whiteSnapshot);
    ASSERT_EQ(coloredDescriptors.size(), whiteDescriptors.size());
    for (size_t index = 0; index < coloredDescriptors.size(); ++index) {
        EXPECT_EQ(coloredDescriptors[index].glyphId, whiteDescriptors[index].glyphId);
        EXPECT_EQ(coloredDescriptors[index].x, whiteDescriptors[index].x);
        EXPECT_EQ(coloredDescriptors[index].y, whiteDescriptors[index].y);
        SkIRect bounds;
        ASSERT_TRUE(coloredSnapshot->rasterBounds(index, 1, 0, 0, bounds));
        std::vector<uint8_t> coloredPixels(size_t(bounds.width()) * size_t(bounds.height()));
        std::vector<uint8_t> whitePixels(coloredPixels.size());
        ASSERT_TRUE(coloredSnapshot->rasterize(index, 1, 0, 0, coloredPixels.data(), coloredPixels.size()));
        ASSERT_TRUE(whiteSnapshot->rasterize(index, 1, 0, 0, whitePixels.data(), whitePixels.size()));
        EXPECT_EQ(coloredPixels, whitePixels);
    }
    SkBitmap colored;
    SkBitmap white;
    ASSERT_TRUE(colored.tryAllocPixels(SkImageInfo::MakeA8(512, 192)));
    ASSERT_TRUE(white.tryAllocPixels(SkImageInfo::MakeA8(512, 192)));
    colored.eraseColor(SK_ColorTRANSPARENT);
    white.eraseColor(SK_ColorTRANSPARENT);
    SkCanvas coloredCanvas(colored);
    SkCanvas whiteCanvas(white);
    coloredSnapshot->paint(&coloredCanvas, 20, 10);
    whiteSnapshot->paint(&whiteCanvas, 20, 10);
    EXPECT_NE(std::memcmp(colored.getPixels(), white.getPixels(), colored.computeByteSize()), 0);
}

TEST_F(GlyphLayoutSnapshotTest, ColorFontKeepsTextureRenderingAndRejectsAlphaAtlasExport) {
    auto face = SkFontMgr_New_Custom_Empty()->makeFromFile(MILESTRO_TEST_EMOJI_FONT_PATH);
    ASSERT_NE(face, nullptr);
    milestro::skia::Font font(SkFont(std::move(face), 32));
    milestro::skia::TextDrawSnapshot source(font, "\xF0\x9F\x98\x80", SK_ColorWHITE);
    EXPECT_FALSE(source.glyphLayout()->supportsAtlas());
    GlyphLayoutSnapshot* snapshot = nullptr;
    EXPECT_EQ(MilestroSkiaGlyphLayoutSnapshotCreateFromText(&source, snapshot), 0);
    EXPECT_EQ(snapshot, nullptr);
}

}
