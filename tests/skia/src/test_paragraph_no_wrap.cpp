#include "Milestro/skia/FontRegistry.h"
#include "Milestro/game/milestro_game_interface.h"
#include "Milestro/skia/textlayout/FontCollection.h"
#include "Milestro/skia/textlayout/InputBox.h"
#include "Milestro/skia/textlayout/NoWrapLayout.h"
#include "Milestro/skia/textlayout/Paragraph.h"
#include "Milestro/skia/textlayout/ParagraphBuilder.h"
#include "Milestro/skia/textlayout/ParagraphStyle.h"
#include "Milestro/skia/textlayout/TextStyle.h"

#include "include/core/SkString.h"
#include "modules/skparagraph/include/Metrics.h"
#include "modules/skparagraph/include/Paragraph.h"

#include <gtest/gtest.h>
#include <algorithm>
#include <cmath>
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

SkScalar LegacyNoWrapContentWidth(skia_text::Paragraph* paragraph, const std::string& text) {
    auto width = static_cast<double>(std::max(paragraph->getLongestLine(), paragraph->getMaxIntrinsicWidth()));
    for (int line = 0; line < static_cast<int>(paragraph->lineNumber()); ++line) {
        skia_text::LineMetrics metrics;
        if (paragraph->getLineMetricsAt(line, &metrics)) {
            width = std::max(width, static_cast<double>(metrics.fWidth));
        }
    }
    const auto utf16Length = milestro_text::TextBoundaryMap(text).utf16Length();
    if (utf16Length > 0) {
        const auto boxes = paragraph->getRectsForRange(0, static_cast<unsigned>(utf16Length),
                                                       skia_text::RectHeightStyle::kTight,
                                                       skia_text::RectWidthStyle::kTight);
        for (const auto& box : boxes) {
            width = std::max(width, static_cast<double>(box.rect.width()));
        }
        skia_text::Paragraph::GlyphInfo info;
        if (paragraph->getGlyphInfoAtUTF16Offset(utf16Length - 1, &info)) {
            width = std::max(width, static_cast<double>(info.fGraphemeLayoutBounds.width()));
        }
    }
    return std::isfinite(width) && width > 0 ? static_cast<SkScalar>(width) : 0;
}

void ExpectNoWrapMeasurementMatchesLegacy(milestro_text::Paragraph* paragraph, const std::string& text) {
    auto* core = paragraph->unwrap();
    const auto expected = LegacyNoWrapContentWidth(core, text);
    const auto utf16Length = milestro_text::TextBoundaryMap(text).utf16Length();
    EXPECT_FLOAT_EQ(milestro_text::ResolveNoWrapContentWidth(core, text), expected);
    EXPECT_FLOAT_EQ(milestro_text::ResolveNoWrapContentWidth(core, text.data(), text.size()), expected);
    EXPECT_FLOAT_EQ(milestro_text::ResolveNoWrapContentWidth(core, utf16Length), expected);
    EXPECT_FLOAT_EQ(paragraph->resolveNoWrapContentWidth(utf16Length), expected);
    float width = -1;
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphResolveNoWrapContentWidth(paragraph, utf16Length, width), 0);
    EXPECT_FLOAT_EQ(width, expected);
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

TEST_F(SkiaParagraphNoWrapTest, ContentWidthPreservesUtf16RangesAndFinalClusters) {
    const std::vector<std::string> texts = {
            "", " ", "AB12CD34", "ab  ", "ab\ncd", std::string("a\0b", 3),
            "\xe4\xb8\xad\xe6\x96\x87", "a\xf0\x9f\x98\x80", "ae\xcc\x81",
            "a\xf0\x9f\x91\xa9\xe2\x80\x8d\xf0\x9f\x92\xbb",
            "abc \xd7\x90\xd7\x91\xd7\x92", "a\xef\xbf\xbc"};
    for (size_t index = 0; index < texts.size(); ++index) {
        SCOPED_TRACE(index);
        auto paragraph = BuildParagraph(texts[index], 1048576);
        ExpectNoWrapMeasurementMatchesLegacy(paragraph.get(), texts[index]);
    }
}

TEST_F(SkiaParagraphNoWrapTest, ContentWidthPreservesMalformedUtf8MeasurementContract) {
    auto paragraph = BuildParagraph("abcdef", 1048576);
    const std::vector<std::string> texts = {
            std::string("\x80" "ab", 3), std::string("a\x80" "b", 3), std::string("ab\x80", 3),
            std::string("a\xf0\x9f", 3), std::string("a\xed\xa0\x80" "b", 5),
            std::string("a\xc0\xaf" "b", 4), std::string("a\xf4\x90\x80\x80" "b", 6)};
    for (size_t index = 0; index < texts.size(); ++index) {
        SCOPED_TRACE(index);
        ExpectNoWrapMeasurementMatchesLegacy(paragraph.get(), texts[index]);
    }
}

TEST_F(SkiaParagraphNoWrapTest, ContentWidthPreservesTrailingPlaceholder) {
    milestro_text::TextStyle textStyle;
    textStyle.setFontFamilies({SkString("Source Han Sans VF")});
    textStyle.setFontSize(36);
    milestro_text::ParagraphStyle paragraphStyle;
    paragraphStyle.setTextStyle(&textStyle);
    milestro_text::ParagraphBuilder builder(&paragraphStyle);
    builder.pushStyle(&textStyle);
    builder.addText("a", 1);
    builder.addPlaceholder(120, 24);
    std::unique_ptr<milestro_text::Paragraph> paragraph(builder.build());
    paragraph->layout(1048576);
    ExpectNoWrapMeasurementMatchesLegacy(paragraph.get(), "a\xef\xbf\xbc");
}

TEST_F(SkiaParagraphNoWrapTest, ContentWidthTreatsNullOrZeroLengthTextAsEmpty) {
    auto paragraph = BuildParagraph("abcdef", 1048576);
    const auto expected = LegacyNoWrapContentWidth(paragraph->unwrap(), "");
    EXPECT_FLOAT_EQ(milestro_text::ResolveNoWrapContentWidth(paragraph->unwrap(), nullptr, 12), expected);
    EXPECT_FLOAT_EQ(milestro_text::ResolveNoWrapContentWidth(paragraph->unwrap(), "abcdef", 0), expected);
    EXPECT_FLOAT_EQ(milestro_text::ResolveNoWrapContentWidth(nullptr, "abcdef", 6), 0);
    EXPECT_FLOAT_EQ(milestro_text::ResolveNoWrapContentWidth(nullptr, size_t{6}), 0);
}
