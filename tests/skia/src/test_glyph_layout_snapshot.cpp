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
#include "include/core/SkTypeface.h"
#include "include/ports/SkFontMgr_empty.h"
#include <gtest/gtest.h>
#include <algorithm>
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

std::unique_ptr<textlayout::Paragraph> MakeParagraph(bool decorated = false, bool vertical = false) {
    textlayout::TextStyle style;
    style.setFontFamilies({SkString("Source Han Sans VF")});
    style.setFontSize(28);
    style.setColor(SK_ColorRED);
    if (decorated) { style.setDecoration(::skia::textlayout::TextDecoration::kUnderline); }
    textlayout::ParagraphStyle paragraphStyle;
    paragraphStyle.setTextStyle(&style);
    if (vertical) { paragraphStyle.setWritingMode(::skia::textlayout::WritingMode::kVerticalRl); }
    textlayout::ParagraphBuilder builder(&paragraphStyle);
    builder.pushStyle(&style);
    const std::string first = "12 fi \xE5\xA4\xA9\n";
    builder.addText(first.data(), first.size());
    style.setColor(SK_ColorBLUE);
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
    ASSERT_TRUE(owned->rasterBounds(0, 2, bounds));
    std::vector<uint8_t> pixels(size_t(bounds.width()) * size_t(bounds.height()));
    ASSERT_TRUE(owned->rasterize(0, 2, pixels.data(), pixels.size()));
    EXPECT_TRUE(std::any_of(pixels.begin(), pixels.end(), [](uint8_t pixel) { return pixel != 0; }));
    EXPECT_EQ(MilestroSkiaGlyphLayoutSnapshotDestroy(owned), 0);
    EXPECT_EQ(owned, nullptr);
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
    EXPECT_FALSE(snapshot.rasterBounds(2, 1, bounds));
    EXPECT_FALSE(snapshot.rasterBounds(0, 0, bounds));
    EXPECT_FALSE(snapshot.rasterBounds(0, std::numeric_limits<float>::infinity(), bounds));
    EXPECT_FALSE(snapshot.copyGlyphs(nullptr, 0));
    ASSERT_TRUE(snapshot.rasterBounds(0, 1.5f, bounds));
    EXPECT_FALSE(snapshot.rasterize(0, 1.5f, nullptr, 0));
    ASSERT_TRUE(snapshot.rasterBounds(1, 1.5f, bounds));
    EXPECT_TRUE(bounds.isEmpty());
    EXPECT_TRUE(snapshot.rasterize(1, 1.5f, nullptr, 0));
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
    ASSERT_TRUE(opaque.rasterBounds(0, 1.5f, bounds));
    std::vector<uint8_t> opaquePixels(size_t(bounds.width()) * size_t(bounds.height()));
    std::vector<uint8_t> transparentPixels(opaquePixels.size());
    ASSERT_TRUE(opaque.rasterize(0, 1.5f, opaquePixels.data(), opaquePixels.size()));
    ASSERT_TRUE(transparent.rasterize(0, 1.5f, transparentPixels.data(), transparentPixels.size()));
    EXPECT_EQ(opaquePixels, transparentPixels);
    SkBitmap expected;
    ASSERT_TRUE(expected.tryAllocPixels(SkImageInfo::MakeA8(bounds.width(), bounds.height())));
    expected.eraseColor(SK_ColorTRANSPARENT);
    SkCanvas canvas(expected);
    font.setSize(font.getSize() * 1.5f);
    font.setEdging(SkFont::Edging::kAntiAlias);
    SkPaint paint;
    paint.setColor(SK_ColorWHITE);
    canvas.drawSimpleText("1", 1, SkTextEncoding::kUTF8, -bounds.left(), -bounds.top(), font, paint);
    for (int row = 0; row < bounds.height(); ++row) {
        EXPECT_EQ(std::memcmp(expected.getAddr8(0, row), opaquePixels.data() + size_t(row) * bounds.width(), bounds.width()), 0);
    }
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
