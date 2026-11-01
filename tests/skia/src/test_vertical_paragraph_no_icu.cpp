#include "Milestro/skia/FontRegistry.h"
#include "Milestro/skia/textlayout/FontCollection.h"
#include "Milestro/skia/textlayout/Paragraph.h"
#include "Milestro/skia/textlayout/ParagraphBuilder.h"
#include "Milestro/skia/textlayout/ParagraphStyle.h"
#include "Milestro/skia/textlayout/TextStyle.h"
#include "Milestro/skia/unicode/Unicode.h"

#include "include/core/SkString.h"
#include "modules/skparagraph/include/Paragraph.h"

#include <gtest/gtest.h>

#include <cmath>
#include <memory>
#include <string>

namespace milestro_text = milestro::skia::textlayout;
namespace skia_text = skia::textlayout;

namespace {

milestro_text::TextStyle Style(const char* family, SkScalar size) {
    milestro_text::TextStyle style;
    style.setFontFamilies({SkString(family)});
    style.setFontSize(size);
    style.setLocale(SkString("ja"));
    style.setColor(SK_ColorBLACK);
    return style;
}

std::unique_ptr<milestro_text::Paragraph>
BuildParagraph(const std::string& text,
               milestro_text::TextStyle& textStyle,
               skia_text::WritingMode writingMode) {
    milestro_text::ParagraphStyle paragraphStyle;
    paragraphStyle.setTextStyle(&textStyle);
    paragraphStyle.setWritingMode(writingMode);
    milestro_text::ParagraphBuilder builder(&paragraphStyle);
    builder.pushStyle(&textStyle);
    builder.addText(text.c_str(), text.size());
    return std::unique_ptr<milestro_text::Paragraph>(builder.build());
}

std::unique_ptr<milestro_text::Paragraph>
BuildOrientedParagraph(const std::string& text,
                       milestro_text::TextStyle& textStyle,
                       skia_text::VerticalTextOrientation orientation) {
    milestro_text::ParagraphStyle paragraphStyle;
    paragraphStyle.setTextStyle(&textStyle);
    paragraphStyle.setWritingMode(skia_text::WritingMode::kVerticalRl);
    milestro_text::ParagraphBuilder builder(&paragraphStyle);
    builder.pushStyle(&textStyle);
    builder.addText(text.c_str(), text.size());
    builder.pop();
    builder.addOrientationOverride(
            skia_text::TextRange{0, text.size()}, orientation);
    return std::unique_ptr<milestro_text::Paragraph>(builder.build());
}

} // namespace

TEST(SkiaVerticalParagraphNoIcu, HorizontalNopLayoutRemainsAvailableWithoutIcuData) {
    auto unicodeProvider = milestro::skia::GetUnicodeProvider();
    ASSERT_NE(unicodeProvider, nullptr);
    ASSERT_NE(unicodeProvider->unwrap(), nullptr);

    auto registry = milestro::skia::GetFontRegistry();
    ASSERT_NE(registry, nullptr);
    ASSERT_NE(registry->RegisterFontFromFile(MILESTRO_TEST_FONT_PATH),
              milestro::skia::MilestroRegisteredFontMgr::RegisterResult::Failed);
    ASSERT_NE(registry->RegisterFontFromFile(MILESTRO_TEST_FALLBACK_FONT_PATH),
              milestro::skia::MilestroRegisteredFontMgr::RegisterResult::Failed);
    milestro_text::GetFontCollection()->setFontFallbackEnabled(true);
    milestro_text::GetFontCollection()->clearCaches();

    // NoICU is not a supported correctness configuration. This process-isolated
    // target only verifies that the horizontal Nop path remains available and
    // intentionally makes no assertion about vertical output.
    auto horizontalStyle = Style("Liberation Sans", 40);
    auto horizontal = BuildParagraph("ABCかな漢字", horizontalStyle, skia_text::WritingMode::kHorizontalTb);
    horizontal->layout(400);
    EXPECT_GT(horizontal->getHeight(), 0);
    EXPECT_FALSE(horizontal->unwrap()
                         ->getRectsForRange(0, 7, skia_text::RectHeightStyle::kTight, skia_text::RectWidthStyle::kTight)
                         .empty());
}

TEST(SkiaVerticalParagraphNoIcu, OrientationOverridesWorkWithoutIcuVerticalClassification) {
    auto textStyle = Style("Liberation Sans", 40);
    auto upright = BuildOrientedParagraph(
            "ABC", textStyle, skia_text::VerticalTextOrientation::kUpright);
    auto sideways = BuildOrientedParagraph(
            "ABC", textStyle, skia_text::VerticalTextOrientation::kSideways);
    upright->layoutWithInlineExtent(400);
    sideways->layoutWithInlineExtent(400);

    const auto& uprightMetrics = upright->getLayoutMetrics();
    const auto& sidewaysMetrics = sideways->getLayoutMetrics();
    ASSERT_EQ(uprightMetrics.columns.size(), 1u);
    ASSERT_EQ(sidewaysMetrics.columns.size(), 1u);
    EXPECT_GT(uprightMetrics.longestInlineExtent, 0);
    EXPECT_GT(sidewaysMetrics.longestInlineExtent, 0);
    EXPECT_GT(std::abs(uprightMetrics.longestInlineExtent -
                       sidewaysMetrics.longestInlineExtent),
              1);
    EXPECT_FALSE(upright->unwrap()
                         ->getRectsForRange(0, 3,
                                            skia_text::RectHeightStyle::kTight,
                                            skia_text::RectWidthStyle::kTight)
                         .empty());
    EXPECT_FALSE(sideways->unwrap()
                         ->getRectsForRange(0, 3,
                                            skia_text::RectHeightStyle::kTight,
                                            skia_text::RectWidthStyle::kTight)
                         .empty());

    bool sawUpright = false;
    upright->unwrap()->extendedVisit(
            [&](int, const skia_text::Paragraph::ExtendedVisitorInfo* info) {
                if (info == nullptr || info->count == 0) return;
                sawUpright = true;
                EXPECT_TRUE(SkScalarNearlyZero(info->glyphTransform.getSkewX()));
            });
    EXPECT_TRUE(sawUpright);

    bool sawSideways = false;
    sideways->unwrap()->extendedVisit(
            [&](int, const skia_text::Paragraph::ExtendedVisitorInfo* info) {
                if (info == nullptr || info->count == 0) return;
                sawSideways = true;
                EXPECT_NEAR(info->glyphTransform.getSkewX(), -1, 0.01);
                EXPECT_NEAR(info->glyphTransform.getSkewY(), 1, 0.01);
            });
    EXPECT_TRUE(sawSideways);
}

TEST(SkiaVerticalParagraphNoIcu, MixedVerticalLayoutStillConsumesAdapterResults) {
    auto textStyle = Style("Liberation Sans", 40);
    auto mixed = BuildParagraph("ABC漢", textStyle, skia_text::WritingMode::kVerticalRl);
    EXPECT_NO_THROW(mixed->layoutWithInlineExtent(400));
    const auto& metrics = mixed->getLayoutMetrics();
    ASSERT_EQ(metrics.columns.size(), 1u);
    EXPECT_GT(metrics.longestInlineExtent, 0);
    EXPECT_FALSE(mixed->unwrap()
                         ->getRectsForRange(0, 4,
                                            skia_text::RectHeightStyle::kTight,
                                            skia_text::RectWidthStyle::kTight)
                         .empty());
}
