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
