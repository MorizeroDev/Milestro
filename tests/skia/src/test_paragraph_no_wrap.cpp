#include "Milestro/skia/FontRegistry.h"
#include "Milestro/skia/textlayout/FontCollection.h"
#include "Milestro/skia/textlayout/Paragraph.h"
#include "Milestro/skia/textlayout/ParagraphBuilder.h"
#include "Milestro/skia/textlayout/ParagraphStyle.h"
#include "Milestro/skia/textlayout/TextStyle.h"

#include "include/core/SkString.h"
#include "modules/skparagraph/include/Metrics.h"
#include "modules/skparagraph/include/Paragraph.h"

#include <gtest/gtest.h>
#include <memory>
#include <string>
#include <vector>

namespace milestro_text = milestro::skia::textlayout;
namespace skia_text = skia::textlayout;

namespace {

std::unique_ptr<milestro_text::Paragraph>
BuildParagraph(const std::string& text,
               SkScalar layoutWidth,
               const std::vector<skia_text::TextRange>& noWrapRanges = {},
               bool vertical = false) {
    milestro_text::TextStyle textStyle;
    textStyle.setFontFamilies({SkString("Source Han Sans VF")});
    textStyle.setFontSize(36);
    textStyle.setLocale(SkString("en"));
    textStyle.setColor(SK_ColorWHITE);

    milestro_text::ParagraphStyle paragraphStyle;
    paragraphStyle.setTextStyle(&textStyle);
    if (vertical) {
        paragraphStyle.setWritingMode(skia_text::WritingMode::kVerticalRl);
    }

    milestro_text::ParagraphBuilder builder(&paragraphStyle);
    builder.pushStyle(&textStyle);
    builder.addText(text.c_str(), text.size());
    for (const auto& range : noWrapRanges) {
        builder.addNoWrap(range.start, range.end);
    }
    std::unique_ptr<milestro_text::Paragraph> paragraph(builder.build());
    if (vertical) {
        paragraph->layoutWithInlineExtent(layoutWidth);
    } else {
        paragraph->layout(layoutWidth);
    }
    return paragraph;
}

std::unique_ptr<milestro_text::Paragraph> BuildPlaceholderParagraph(bool noWrap) {
    milestro_text::TextStyle textStyle;
    textStyle.setFontFamilies({SkString("Source Han Sans VF")});
    textStyle.setFontSize(36);
    textStyle.setLocale(SkString("en"));
    textStyle.setColor(SK_ColorWHITE);

    milestro_text::ParagraphStyle paragraphStyle;
    paragraphStyle.setTextStyle(&textStyle);
    milestro_text::ParagraphBuilder builder(&paragraphStyle);
    builder.pushStyle(&textStyle);
    builder.addText("aa ", 3);
    builder.addPlaceholder(24, 24);
    builder.addText(" bb", 3);
    if (noWrap) {
        builder.addNoWrap(0, 9);
    }
    std::unique_ptr<milestro_text::Paragraph> paragraph(builder.build());
    paragraph->layout(40);
    return paragraph;
}

std::vector<skia_text::LineMetrics> GetLines(milestro_text::Paragraph* paragraph) {
    std::vector<skia_text::LineMetrics> lines;
    paragraph->unwrap()->getLineMetrics(lines);
    return lines;
}

}  // namespace

class SkiaParagraphNoWrapTest : public ::testing::Test {
protected:
    void SetUp() override {
        const auto result = milestro::skia::GetFontRegistry()->RegisterFontFromFile(
                MILESTRO_TEST_FONT_PATH);
        ASSERT_NE(result, milestro::skia::MilestroRegisteredFontMgr::RegisterResult::Failed);
        milestro_text::GetFontCollection()->clearCaches();
    }
};

TEST_F(SkiaParagraphNoWrapTest, SuppressesInteriorSoftBreaksButKeepsRangeBoundaries) {
    const std::string text = "aa bb cc dd";
    auto normal = BuildParagraph(text, 65);
    auto noWrap = BuildParagraph(text, 65, {{3, 9}});

    EXPECT_GT(normal->unwrap()->lineNumber(), noWrap->unwrap()->lineNumber());
    const auto lines = GetLines(noWrap.get());
    ASSERT_EQ(lines.size(), 3u);
    EXPECT_EQ(lines[0].fStartIndex, 0u);
    EXPECT_EQ(lines[1].fStartIndex, 3u);
    EXPECT_EQ(lines[2].fStartIndex, 9u);
}

TEST_F(SkiaParagraphNoWrapTest, DoesNotSuppressExplicitHardBreaks) {
    auto paragraph = BuildParagraph("aa\nbb", 200, {{0, 5}});

    const auto lines = GetLines(paragraph.get());
    ASSERT_EQ(lines.size(), 2u);
    EXPECT_TRUE(lines[0].fHardBreak);
    EXPECT_EQ(lines[1].fStartIndex, 3u);
}

TEST_F(SkiaParagraphNoWrapTest, SuppressesSoftBreaksAroundInteriorPlaceholders) {
    auto normal = BuildPlaceholderParagraph(false);
    auto noWrap = BuildPlaceholderParagraph(true);

    EXPECT_GT(normal->unwrap()->lineNumber(), 1u);
    EXPECT_EQ(noWrap->unwrap()->lineNumber(), 1u);
}

TEST_F(SkiaParagraphNoWrapTest, SuppressesForcedClusterBreaksWithoutChangingNormalWords) {
    auto normal = BuildParagraph("aaaaaa", 20);
    auto noWrap = BuildParagraph("aaaaaa", 20, {{0, 6}});
    const auto lines = GetLines(noWrap.get());

    EXPECT_GT(normal->unwrap()->lineNumber(), 1u);
    EXPECT_EQ(noWrap->unwrap()->lineNumber(), 1u)
            << "lineCount=" << lines.size()
            << ", secondStart=" << (lines.size() > 1 ? lines[1].fStartIndex : 0);
}

TEST_F(SkiaParagraphNoWrapTest, AppliesToVerticalColumnBreaking) {
    const std::string text = "aa bb cc dd";
    auto normal = BuildParagraph(text, 65, {}, true);
    auto noWrap = BuildParagraph(text, 65, {{3, 9}}, true);

    const auto normalMetrics = normal->getLayoutMetrics();
    const auto noWrapMetrics = noWrap->getLayoutMetrics();
    EXPECT_GT(normalMetrics.columns.size(), noWrapMetrics.columns.size());
    ASSERT_EQ(noWrapMetrics.columns.size(), 3u);
    EXPECT_EQ(noWrapMetrics.columns[0].utf8Range.start, 0u);
    EXPECT_EQ(noWrapMetrics.columns[1].utf8Range.start, 3u);
    EXPECT_EQ(noWrapMetrics.columns[2].utf8Range.start, 9u);
}

TEST_F(SkiaParagraphNoWrapTest, DefaultLayoutStillUsesSoftBreaks) {
    auto paragraph = BuildParagraph("aa bb cc dd", 65);

    EXPECT_GT(paragraph->unwrap()->lineNumber(), 1u);
}
