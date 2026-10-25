#include "Milestro/game/milestro_game_interface.h"
#include "Milestro/game/milestro_game_retcode.h"
#include "Milestro/skia/FontRegistry.h"
#include "Milestro/skia/Path.h"
#include "Milestro/skia/textlayout/FontCollection.h"
#include "Milestro/skia/textlayout/Paragraph.h"
#include "Milestro/skia/textlayout/ParagraphBuilder.h"
#include "Milestro/skia/textlayout/ParagraphStyle.h"
#include "Milestro/skia/textlayout/TextStyle.h"

#include "include/core/SkPathBuilder.h"
#include "include/core/SkString.h"
#include "modules/skparagraph/include/Paragraph.h"
#include "modules/skunicode/include/SkUnicode.h"

#include <gtest/gtest.h>

#include <cmath>
#include <fstream>
#include <limits>
#include <memory>
#include <stdexcept>
#include <string>
#include <vector>

namespace milestro_text = milestro::skia::textlayout;
namespace skia_text = skia::textlayout;

namespace {

constexpr SkScalar kTolerance = 0.05f;

void LoadIcuData() {
    std::ifstream input(MILESTRO_TEST_ICUDTL_PATH, std::ios::binary);
    ASSERT_TRUE(input.is_open()) << MILESTRO_TEST_ICUDTL_PATH;
    std::vector<uint8_t> data((std::istreambuf_iterator<char>(input)), std::istreambuf_iterator<char>());
    ASSERT_FALSE(data.empty());
    ASSERT_EQ(MilestroCopyAndLoadICU(data.data(), data.size(), nullptr), MILESTRO_API_RET_OK);
}

size_t Utf16Length(const std::string& text) {
    return SkUnicode::convertUtf8ToUtf16(text.c_str(), static_cast<int>(text.size())).size();
}

SkPath CorePath(skia_text::Paragraph* paragraph, SkScalar x = 0, SkScalar y = 0) {
    SkPathBuilder builder;
    for (size_t line = 0; line < paragraph->lineNumber(); ++line) {
        SkPath path;
        EXPECT_EQ(paragraph->getPath(static_cast<int>(line), &path), 0);
        builder.addPath(path.makeOffset(x, y));
    }
    return builder.detach();
}

struct RectCollector {
    std::vector<SkRect> rects;
};

uint64_t CollectRect(const void* handler, float left, float top, float right, float bottom) {
    auto* collector = const_cast<RectCollector*>(static_cast<const RectCollector*>(handler));
    collector->rects.push_back(SkRect::MakeLTRB(left, top, right, bottom));
    return 0;
}

uint64_t
RecordSplitGlyph(const void* handler, uint16_t, milestro::skia::Font*, float, float, float, float, float, float) {
    ++*const_cast<size_t*>(static_cast<const size_t*>(handler));
    return 0;
}

void ExpectRectsNear(const std::vector<SkRect>& expected, const std::vector<SkRect>& actual) {
    ASSERT_EQ(actual.size(), expected.size());
    for (size_t index = 0; index < expected.size(); ++index) {
        EXPECT_NEAR(actual[index].left(), expected[index].left(), kTolerance);
        EXPECT_NEAR(actual[index].top(), expected[index].top(), kTolerance);
        EXPECT_NEAR(actual[index].right(), expected[index].right(), kTolerance);
        EXPECT_NEAR(actual[index].bottom(), expected[index].bottom(), kTolerance);
    }
}

std::unique_ptr<milestro_text::Paragraph> BuildVerticalParagraph(const std::string& text,
                                                                 milestro_text::TextStyle& textStyle) {
    milestro_text::ParagraphStyle paragraphStyle;
    paragraphStyle.setTextStyle(&textStyle);
    paragraphStyle.setWritingMode(skia_text::WritingMode::kVerticalRl);
    paragraphStyle.setTextAlign(skia_text::TextAlign::kStart);
    milestro_text::ParagraphBuilder builder(&paragraphStyle);
    builder.pushStyle(&textStyle);
    builder.addText(text.c_str(), text.size());
    return std::unique_ptr<milestro_text::Paragraph>(builder.build());
}

std::unique_ptr<milestro_text::Paragraph> BuildHorizontalParagraph(const std::string& text,
                                                                   milestro_text::TextStyle& textStyle) {
    milestro_text::ParagraphStyle paragraphStyle;
    paragraphStyle.setTextStyle(&textStyle);
    milestro_text::ParagraphBuilder builder(&paragraphStyle);
    builder.pushStyle(&textStyle);
    builder.addText(text.c_str(), text.size());
    return std::unique_ptr<milestro_text::Paragraph>(builder.build());
}

} // namespace

TEST(SkiaVerticalParagraphWrapper, P07_P08_P09_P12_CAbiPlaceholderQueriesAndValidation) {
    LoadIcuData();
    auto registry = milestro::skia::GetFontRegistry();
    ASSERT_NE(registry->RegisterFontFromFile(MILESTRO_TEST_FONT_PATH),
              milestro::skia::MilestroRegisteredFontMgr::RegisterResult::Failed);
    milestro_text::TextStyle textStyle;
    textStyle.setFontFamilies({SkString("Source Han Sans VF")});
    textStyle.setFontSize(40);
    milestro_text::ParagraphStyle style;
    style.setTextStyle(&textStyle);
    style.setWritingMode(skia_text::WritingMode::kVerticalRl);
    milestro_text::ParagraphBuilder builder(&style);
    ASSERT_EQ(MilestroSkiaTextlayoutParagraphBuilderAddPlaceholder(&builder, 12, 25, 5, 0, 0), 0);
    builder.addText("天", 3);
    std::unique_ptr<milestro_text::Paragraph> paragraph(builder.build());
    ASSERT_EQ(MilestroSkiaTextlayoutParagraphLayoutWithInlineExtent(paragraph.get(), 100), 0);
    RectCollector placeholders;
    ASSERT_EQ(MilestroSkiaTextlayoutParagraphGetRectsForPlaceholders(paragraph.get(), &placeholders, CollectRect), 0);
    ASSERT_EQ(placeholders.rects.size(), 1u);
    EXPECT_FLOAT_EQ(placeholders.rects[0].width(), 12);
    EXPECT_FLOAT_EQ(placeholders.rects[0].height(), 25);
    RectCollector range;
    ASSERT_EQ(MilestroSkiaTextlayoutParagraphForEachRectInRange(paragraph.get(), 0, 1, &range, CollectRect), 0);
    ExpectRectsNear(range.rects, placeholders.rects);
    const auto& rect = placeholders.rects[0];
    EXPECT_TRUE(paragraph->hitTestRange(0, 2, rect.centerX(), rect.centerY()));
    EXPECT_FALSE(paragraph->hitTestRange(0, 1, rect.left() - 1, rect.centerY()));
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphGetRectsForPlaceholders(nullptr, &range, CollectRect),
              MILESTRO_API_RET_FAILED);
    EXPECT_NE(MilestroSkiaTextlayoutParagraphGetRectsForPlaceholders(paragraph.get(), &range, nullptr), 0);
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphBuilderAddPlaceholder(&builder, 0, 25, 5, 0, 0),
              MILESTRO_API_RET_FAILED);
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphBuilderAddPlaceholder(&builder, 12, 25, 6, 0, 0),
              MILESTRO_API_RET_FAILED);
}

TEST(SkiaVerticalParagraphWrapper, MatchesCoreMetricsRangesHitTestingAndPaths) {
    LoadIcuData();

    auto registry = milestro::skia::GetFontRegistry();
    ASSERT_NE(registry, nullptr);
    ASSERT_NE(registry->RegisterFontFromFile(MILESTRO_TEST_FONT_PATH),
              milestro::skia::MilestroRegisteredFontMgr::RegisterResult::Failed);
    ASSERT_NE(registry->RegisterFontFromFile(MILESTRO_TEST_FALLBACK_FONT_PATH),
              milestro::skia::MilestroRegisteredFontMgr::RegisterResult::Failed);
    milestro_text::GetFontCollection()->setFontFallbackEnabled(true);
    milestro_text::GetFontCollection()->clearCaches();

    const std::string text = "「漢字ABC2026かな」、。ー";
    milestro_text::TextStyle textStyle;
    textStyle.setFontFamilies({SkString("Source Han Sans VF")});
    textStyle.setFontSize(44);
    textStyle.setLocale(SkString("ja"));
    textStyle.setColor(SK_ColorBLACK);

    auto paragraph = BuildVerticalParagraph(text, textStyle);
    paragraph->layoutWithInlineExtent(180);
    auto* core = paragraph->unwrap();
    ASSERT_NE(core, nullptr);
    EXPECT_EQ(paragraph->getWritingMode(), skia_text::WritingMode::kVerticalRl);
    EXPECT_TRUE(paragraph->isVertical());
    EXPECT_EQ(&paragraph->getLayoutMetrics(), &core->getLayoutMetrics());
    const auto& metrics = paragraph->getLayoutMetrics();
    EXPECT_EQ(metrics.writingMode, skia_text::WritingMode::kVerticalRl);
    ASSERT_GT(metrics.columns.size(), 1u);
    EXPECT_NEAR(paragraph->getHeight(), metrics.physicalSize.height(), kTolerance);

    const size_t utf16Length = Utf16Length(text);
    const auto coreBoxes = core->getRectsForRange(0,
                                                  static_cast<unsigned>(utf16Length),
                                                  skia_text::RectHeightStyle::kTight,
                                                  skia_text::RectWidthStyle::kTight);
    ASSERT_FALSE(coreBoxes.empty());
    RectCollector rects;
    EXPECT_EQ(paragraph->forEachRectInRange(0, utf16Length, &rects, CollectRect), 0u);
    std::vector<SkRect> expectedRects;
    for (const auto& box: coreBoxes) {
        if (box.rect.width() > 0 && box.rect.height() > 0) {
            expectedRects.push_back(box.rect);
        }
    }
    ExpectRectsNear(expectedRects, rects.rects);
    for (const auto& box: coreBoxes) {
        const SkPoint center = SkPoint::Make(box.rect.centerX(), box.rect.centerY());
        EXPECT_TRUE(paragraph->hitTestRange(0, utf16Length, center.x(), center.y()));
    }
    EXPECT_FALSE(paragraph->hitTestRange(0,
                                         utf16Length,
                                         metrics.contentBounds.right() + 100,
                                         metrics.contentBounds.bottom() + 100));

    const SkPath corePath = CorePath(core);
    ASSERT_FALSE(corePath.isEmpty());
    std::unique_ptr<milestro::skia::Path> wrapperPath(paragraph->toPath(0, 0));
    ASSERT_NE(wrapperPath, nullptr);
    const SkRect coreBounds = corePath.getBounds();
    const SkRect wrapperBounds = wrapperPath->unwrap().getBounds();
    EXPECT_NEAR(wrapperBounds.left(), coreBounds.left(), kTolerance);
    EXPECT_NEAR(wrapperBounds.top(), coreBounds.top(), kTolerance);
    EXPECT_NEAR(wrapperBounds.right(), coreBounds.right(), kTolerance);
    EXPECT_NEAR(wrapperBounds.bottom(), coreBounds.bottom(), kTolerance);

    std::unique_ptr<milestro::skia::Path> translatedPath(paragraph->toPath(17, 23));
    ASSERT_NE(translatedPath, nullptr);
    const SkRect translatedBounds = translatedPath->unwrap().getBounds();
    EXPECT_NEAR(translatedBounds.left(), coreBounds.left() + 17, kTolerance);
    EXPECT_NEAR(translatedBounds.top(), coreBounds.top() + 23, kTolerance);
    EXPECT_NEAR(translatedBounds.right(), coreBounds.right() + 17, kTolerance);
    EXPECT_NEAR(translatedBounds.bottom(), coreBounds.bottom() + 23, kTolerance);

    size_t splitCallbacks = 0;
    EXPECT_EQ(paragraph->splitGlyph(0, 0, &splitCallbacks, RecordSplitGlyph), static_cast<uint64_t>(-1));
    EXPECT_EQ(splitCallbacks, 0u);
    EXPECT_THROW(paragraph->resolveNoWrapContentWidth(text.c_str(), text.size()), std::logic_error);
}

TEST(SkiaVerticalParagraphWrapper, TextIndentUsesTheSameWrapperMetricsRangesAndPaths) {
    LoadIcuData();

    auto registry = milestro::skia::GetFontRegistry();
    ASSERT_NE(registry, nullptr);
    ASSERT_NE(registry->RegisterFontFromFile(MILESTRO_TEST_FONT_PATH),
              milestro::skia::MilestroRegisteredFontMgr::RegisterResult::Failed);
    ASSERT_NE(registry->RegisterFontFromFile(MILESTRO_TEST_FALLBACK_FONT_PATH),
              milestro::skia::MilestroRegisteredFontMgr::RegisterResult::Failed);
    milestro_text::GetFontCollection()->setFontFallbackEnabled(true);
    milestro_text::GetFontCollection()->clearCaches();

    milestro_text::TextStyle textStyle;
    textStyle.setFontFamilies({SkString("Source Han Sans VF")});
    textStyle.setFontSize(40);
    textStyle.setLocale(SkString("ja"));
    textStyle.setColor(SK_ColorBLACK);

    milestro_text::ParagraphStyle paragraphStyle;
    paragraphStyle.setTextStyle(&textStyle);
    paragraphStyle.setWritingMode(skia_text::WritingMode::kVerticalRl);
    paragraphStyle.setTextAlign(skia_text::TextAlign::kStart);
    paragraphStyle.setTextIndent(24, false);
    milestro_text::ParagraphBuilder builder(&paragraphStyle);
    builder.pushStyle(&textStyle);
    constexpr char kText[] = "天地玄黃";
    builder.addText(kText, sizeof(kText) - 1);
    std::unique_ptr<milestro_text::Paragraph> paragraph(builder.build());
    ASSERT_NE(paragraph, nullptr);

    EXPECT_EQ(MilestroSkiaTextlayoutParagraphLayoutWithInlineExtent(paragraph.get(), 180),
              MILESTRO_API_RET_OK);
    const auto& metrics = paragraph->getLayoutMetrics();
    ASSERT_FALSE(metrics.columns.empty());
    EXPECT_NEAR(metrics.columns[0].physicalBounds.top(), 24, kTolerance);
    EXPECT_EQ(metrics.columns[0].utf16Start, 0u);

    auto* core = paragraph->unwrap();
    ASSERT_NE(core, nullptr);
    const auto coreBoxes = core->getRectsForRange(0,
                                                  1,
                                                  skia_text::RectHeightStyle::kTight,
                                                  skia_text::RectWidthStyle::kTight);
    ASSERT_EQ(coreBoxes.size(), 1u);
    EXPECT_NEAR(coreBoxes.front().rect.top(), 24, kTolerance);

    RectCollector rects;
    EXPECT_EQ(paragraph->forEachRectInRange(0, 1, &rects, CollectRect), 0u);
    ASSERT_EQ(rects.rects.size(), 1u);
    ExpectRectsNear({coreBoxes.front().rect}, rects.rects);

    const SkPath corePath = CorePath(core);
    std::unique_ptr<milestro::skia::Path> wrapperPath(paragraph->toPath(0, 0));
    ASSERT_NE(wrapperPath, nullptr);
    const SkRect wrapperBounds = wrapperPath->unwrap().getBounds();
    EXPECT_NEAR(wrapperBounds.top(), corePath.getBounds().top(), kTolerance);
    EXPECT_NEAR(wrapperBounds.left(), corePath.getBounds().left(), kTolerance);
}

TEST(SkiaVerticalParagraphWrapper, JustifyUsesTheSameWrapperMetricsRangesAndPaths) {
    LoadIcuData();

    auto registry = milestro::skia::GetFontRegistry();
    ASSERT_NE(registry, nullptr);
    ASSERT_NE(registry->RegisterFontFromFile(MILESTRO_TEST_FONT_PATH),
              milestro::skia::MilestroRegisteredFontMgr::RegisterResult::Failed);
    ASSERT_NE(registry->RegisterFontFromFile(MILESTRO_TEST_FALLBACK_FONT_PATH),
              milestro::skia::MilestroRegisteredFontMgr::RegisterResult::Failed);
    milestro_text::GetFontCollection()->setFontFallbackEnabled(true);
    milestro_text::GetFontCollection()->clearCaches();

    milestro_text::TextStyle textStyle;
    textStyle.setFontFamilies({SkString("Source Han Sans VF")});
    textStyle.setFontSize(40);
    textStyle.setLocale(SkString("ja"));
    textStyle.setColor(SK_ColorBLACK);

    auto measure = BuildVerticalParagraph("天", textStyle);
    ASSERT_NE(measure, nullptr);
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphLayoutWithInlineExtent(measure.get(), 1000),
              MILESTRO_API_RET_OK);
    const SkScalar advance = measure->getLayoutMetrics().longestInlineExtent;
    ASSERT_GT(advance, 0);
    const SkScalar height = advance * 4 + 16;
    const SkScalar step = (height - advance * 4) / 3.0f;

    auto build = [&](skia_text::TextAlign align) {
        milestro_text::ParagraphStyle paragraphStyle;
        paragraphStyle.setTextStyle(&textStyle);
        paragraphStyle.setWritingMode(skia_text::WritingMode::kVerticalRl);
        paragraphStyle.setTextAlign(align);
        milestro_text::ParagraphBuilder builder(&paragraphStyle);
        builder.pushStyle(&textStyle);
        constexpr char kText[] = "天地玄黃宇宙洪荒";
        builder.addText(kText, sizeof(kText) - 1);
        return std::unique_ptr<milestro_text::Paragraph>(builder.build());
    };
    auto start = build(skia_text::TextAlign::kStart);
    auto justified = build(skia_text::TextAlign::kJustify);
    ASSERT_NE(start, nullptr);
    ASSERT_NE(justified, nullptr);
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphLayoutWithInlineExtent(start.get(), height),
              MILESTRO_API_RET_OK);
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphLayoutWithInlineExtent(justified.get(), height),
              MILESTRO_API_RET_OK);

    const auto& startMetrics = start->getLayoutMetrics();
    const auto& metrics = justified->getLayoutMetrics();
    ASSERT_EQ(metrics.columns.size(), 2u);
    EXPECT_NEAR(metrics.columns[0].inlineExtent, height, kTolerance);
    EXPECT_NEAR(metrics.columns[1].inlineExtent, advance * 4, kTolerance);
    EXPECT_NEAR(metrics.longestInlineExtent, height, kTolerance);
    EXPECT_NEAR(metrics.minIntrinsicInlineExtent, startMetrics.minIntrinsicInlineExtent, kTolerance);
    EXPECT_NEAR(metrics.maxIntrinsicInlineExtent, startMetrics.maxIntrinsicInlineExtent, kTolerance);

    RectCollector startRects;
    RectCollector rects;
    EXPECT_EQ(start->forEachRectInRange(3, 4, &startRects, CollectRect), 0u);
    EXPECT_EQ(justified->forEachRectInRange(3, 4, &rects, CollectRect), 0u);
    ASSERT_EQ(startRects.rects.size(), 1u);
    ASSERT_EQ(rects.rects.size(), 1u);
    const auto coreBoxes = justified->unwrap()->getRectsForRange(
            3, 4, skia_text::RectHeightStyle::kTight, skia_text::RectWidthStyle::kTight);
    ASSERT_EQ(coreBoxes.size(), 1u);
    ExpectRectsNear({coreBoxes.front().rect}, rects.rects);
    EXPECT_NEAR(rects.rects.front().top(), startRects.rects.front().top() + 3 * step, kTolerance);

    const SkRect startPathBounds = start->toPath(0, 0)->unwrap().getBounds();
    const SkRect pathBounds = justified->toPath(0, 0)->unwrap().getBounds();
    EXPECT_NEAR(pathBounds.bottom(), startPathBounds.bottom() + 3 * step, 0.5f);
    EXPECT_NEAR(pathBounds.top(), startPathBounds.top(), 0.5f);
}

TEST(SkiaVerticalParagraphWrapper, CAbiWritingModeValidatesAndRoundTrips) {
    milestro_text::ParagraphStyle paragraphStyle;
    int32_t writingMode = -1;
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphStyleGetWritingMode(&paragraphStyle, writingMode),
              MILESTRO_API_RET_OK);
    EXPECT_EQ(writingMode, static_cast<int32_t>(skia_text::WritingMode::kHorizontalTb));

    EXPECT_EQ(MilestroSkiaTextlayoutParagraphStyleSetWritingMode(
                      &paragraphStyle,
                      static_cast<int32_t>(skia_text::WritingMode::kVerticalRl)),
              MILESTRO_API_RET_OK);
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphStyleGetWritingMode(&paragraphStyle, writingMode),
              MILESTRO_API_RET_OK);
    EXPECT_EQ(writingMode, static_cast<int32_t>(skia_text::WritingMode::kVerticalRl));

    EXPECT_EQ(MilestroSkiaTextlayoutParagraphStyleSetWritingMode(&paragraphStyle, -1),
              MILESTRO_API_RET_FAILED);
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphStyleSetWritingMode(&paragraphStyle, 2),
              MILESTRO_API_RET_FAILED);
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphStyleGetWritingMode(nullptr, writingMode),
              MILESTRO_API_RET_FAILED);
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphStyleSetWritingMode(nullptr, 0),
              MILESTRO_API_RET_FAILED);
}

TEST(SkiaVerticalParagraphWrapper, CAbiExposesLineHeightColumnMetricsAndFailures) {
    LoadIcuData();

    auto registry = milestro::skia::GetFontRegistry();
    ASSERT_NE(registry, nullptr);
    ASSERT_NE(registry->RegisterFontFromFile(MILESTRO_TEST_FONT_PATH),
              milestro::skia::MilestroRegisteredFontMgr::RegisterResult::Failed);
    ASSERT_NE(registry->RegisterFontFromFile(MILESTRO_TEST_FALLBACK_FONT_PATH),
              milestro::skia::MilestroRegisteredFontMgr::RegisterResult::Failed);
    milestro_text::GetFontCollection()->setFontFallbackEnabled(true);
    milestro_text::GetFontCollection()->clearCaches();

    milestro_text::TextStyle textStyle;
    textStyle.setFontFamilies({SkString("Source Han Sans VF")});
    textStyle.setFontSize(32);
    textStyle.setLocale(SkString("ja"));
    textStyle.setHeightOverride(true);
    textStyle.setHeight(1.5f);
    auto paragraph = BuildVerticalParagraph("漢", textStyle);
    ASSERT_EQ(MilestroSkiaTextlayoutParagraphLayoutWithInlineExtent(
                      paragraph.get(), std::numeric_limits<float>::infinity()),
              MILESTRO_API_RET_OK);

    float physicalWidth = 0;
    float physicalHeight = 0;
    float contentLeft = 0;
    float contentTop = 0;
    float contentRight = 0;
    float contentBottom = 0;
    float inkLeft = 0;
    float inkTop = 0;
    float inkRight = 0;
    float inkBottom = 0;
    float longestInlineExtent = 0;
    float minIntrinsicInlineExtent = 0;
    float maxIntrinsicInlineExtent = 0;
    uint64_t columnCount = 0;
    ASSERT_EQ(MilestroSkiaTextlayoutParagraphGetLayoutMetrics(paragraph.get(),
                                                              physicalWidth,
                                                              physicalHeight,
                                                              contentLeft,
                                                              contentTop,
                                                              contentRight,
                                                              contentBottom,
                                                              inkLeft,
                                                              inkTop,
                                                              inkRight,
                                                              inkBottom,
                                                              longestInlineExtent,
                                                              minIntrinsicInlineExtent,
                                                              maxIntrinsicInlineExtent,
                                                              columnCount),
              MILESTRO_API_RET_OK);
    EXPECT_EQ(columnCount, 1u);
    EXPECT_NEAR(physicalWidth, 48, kTolerance);

    uint64_t lineNumber = 0;
    int32_t hardBreak = 0;
    float left = 0;
    float top = 0;
    float right = 0;
    float bottom = 0;
    float inlineExtent = 0;
    float blockThickness = 0;
    float physicalBaseline = 0;
    uint64_t utf16Start = 0;
    uint64_t utf16End = 0;
    uint64_t utf16EndExcludingWhitespace = 0;
    uint64_t utf16EndIncludingNewline = 0;
    ASSERT_EQ(MilestroSkiaTextlayoutParagraphGetColumnMetrics(paragraph.get(),
                                                              0,
                                                              lineNumber,
                                                              hardBreak,
                                                              left,
                                                              top,
                                                              right,
                                                              bottom,
                                                              inlineExtent,
                                                              blockThickness,
                                                              physicalBaseline,
                                                              utf16Start,
                                                              utf16End,
                                                              utf16EndExcludingWhitespace,
                                                              utf16EndIncludingNewline),
              MILESTRO_API_RET_OK);
    EXPECT_EQ(lineNumber, 0u);
    EXPECT_NEAR(blockThickness, 48, kTolerance);
    EXPECT_NEAR(right - left, 48, kTolerance);
    EXPECT_NEAR(top, 0, kTolerance);
    EXPECT_NEAR(physicalBaseline, blockThickness * 0.5f, 1.0f);

    milestro_text::TextStyle invalidStyle;
    invalidStyle.setFontFamilies({SkString("Source Han Sans VF")});
    invalidStyle.setFontSize(32);
    invalidStyle.setLocale(SkString("ja"));
    invalidStyle.setHeightOverride(true);
    invalidStyle.setHeight(0);
    auto invalid = BuildVerticalParagraph("漢", invalidStyle);
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphLayoutWithInlineExtent(invalid.get(), 200),
              MILESTRO_API_RET_FAILED);
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphGetLayoutMetrics(invalid.get(),
                                                              physicalWidth,
                                                              physicalHeight,
                                                              contentLeft,
                                                              contentTop,
                                                              contentRight,
                                                              contentBottom,
                                                              inkLeft,
                                                              inkTop,
                                                              inkRight,
                                                              inkBottom,
                                                              longestInlineExtent,
                                                              minIntrinsicInlineExtent,
                                                              maxIntrinsicInlineExtent,
                                                              columnCount),
              MILESTRO_API_RET_FAILED);
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphGetHeight(invalid.get(), physicalHeight),
              MILESTRO_API_RET_OK);
    EXPECT_EQ(physicalHeight, 0);
}

TEST(SkiaVerticalParagraphWrapper, CAbiLayoutMetricsColumnsAndInvalidParameters) {
    LoadIcuData();

    auto registry = milestro::skia::GetFontRegistry();
    ASSERT_NE(registry, nullptr);
    ASSERT_NE(registry->RegisterFontFromFile(MILESTRO_TEST_FONT_PATH),
              milestro::skia::MilestroRegisteredFontMgr::RegisterResult::Failed);
    ASSERT_NE(registry->RegisterFontFromFile(MILESTRO_TEST_FALLBACK_FONT_PATH),
              milestro::skia::MilestroRegisteredFontMgr::RegisterResult::Failed);
    milestro_text::GetFontCollection()->setFontFallbackEnabled(true);
    milestro_text::GetFontCollection()->clearCaches();

    const std::string text = "「漢字ABC2026かな」、。ー";
    milestro_text::TextStyle textStyle;
    textStyle.setFontFamilies({SkString("Source Han Sans VF")});
    textStyle.setFontSize(44);
    textStyle.setLocale(SkString("ja"));
    auto paragraph = BuildVerticalParagraph(text, textStyle);

    float height = -1;
    float longestLine = -1;
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphLayoutWithInlineExtent(
                      paragraph.get(), std::numeric_limits<float>::quiet_NaN()),
              MILESTRO_API_RET_FAILED);
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphLayoutWithInlineExtent(paragraph.get(), -1),
              MILESTRO_API_RET_FAILED);
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphLayoutWithInlineExtent(nullptr, 180),
              MILESTRO_API_RET_FAILED);
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphGetHeight(paragraph.get(), height),
              MILESTRO_API_RET_OK);
    EXPECT_EQ(height, 0);
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphGetLongestLine(paragraph.get(), longestLine),
              MILESTRO_API_RET_OK);
    EXPECT_EQ(longestLine, 0);
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphLayout(nullptr, 180), MILESTRO_API_RET_FAILED);
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphLayout(paragraph.get(), 180), MILESTRO_API_RET_OK);
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphLayoutWithInlineExtent(paragraph.get(), 180),
              MILESTRO_API_RET_OK);

    int32_t writingMode = -1;
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphGetWritingMode(paragraph.get(), writingMode),
              MILESTRO_API_RET_OK);
    EXPECT_EQ(writingMode, static_cast<int32_t>(skia_text::WritingMode::kVerticalRl));

    float physicalWidth = 0;
    float physicalHeight = 0;
    float contentLeft = 0;
    float contentTop = 0;
    float contentRight = 0;
    float contentBottom = 0;
    float inkLeft = 0;
    float inkTop = 0;
    float inkRight = 0;
    float inkBottom = 0;
    float longestInlineExtent = 0;
    float minIntrinsicInlineExtent = 0;
    float maxIntrinsicInlineExtent = 0;
    uint64_t columnCount = 0;
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphGetLayoutMetrics(paragraph.get(),
                                                              physicalWidth,
                                                              physicalHeight,
                                                              contentLeft,
                                                              contentTop,
                                                              contentRight,
                                                              contentBottom,
                                                              inkLeft,
                                                              inkTop,
                                                              inkRight,
                                                              inkBottom,
                                                              longestInlineExtent,
                                                              minIntrinsicInlineExtent,
                                                              maxIntrinsicInlineExtent,
                                                              columnCount),
              MILESTRO_API_RET_OK);
    EXPECT_NEAR(physicalHeight, 180, kTolerance);
    EXPECT_GT(physicalWidth, 0);
    EXPECT_GT(columnCount, 1u);
    EXPECT_GT(maxIntrinsicInlineExtent, minIntrinsicInlineExtent);
    EXPECT_GE(inkRight, contentLeft);
    EXPECT_LE(inkLeft, contentRight);

    uint64_t lineNumber = 0;
    int32_t hardBreak = 0;
    float left = 0;
    float top = 0;
    float right = 0;
    float bottom = 0;
    float inlineExtent = 0;
    float blockThickness = 0;
    float physicalBaseline = 0;
    uint64_t utf16Start = 0;
    uint64_t utf16End = 0;
    uint64_t utf16EndExcludingWhitespace = 0;
    uint64_t utf16EndIncludingNewline = 0;
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphGetColumnMetrics(paragraph.get(),
                                                              0,
                                                              lineNumber,
                                                              hardBreak,
                                                              left,
                                                              top,
                                                              right,
                                                              bottom,
                                                              inlineExtent,
                                                              blockThickness,
                                                              physicalBaseline,
                                                              utf16Start,
                                                              utf16End,
                                                              utf16EndExcludingWhitespace,
                                                              utf16EndIncludingNewline),
              MILESTRO_API_RET_OK);
    EXPECT_EQ(lineNumber, 0u);
    EXPECT_GE(right, left);
    EXPECT_GE(bottom, top);
    EXPECT_LE(utf16Start, utf16End);
    EXPECT_LE(utf16EndExcludingWhitespace, utf16End);
    EXPECT_EQ(utf16EndIncludingNewline, utf16End);

    EXPECT_EQ(MilestroSkiaTextlayoutParagraphGetColumnMetrics(paragraph.get(),
                                                              columnCount,
                                                              lineNumber,
                                                              hardBreak,
                                                              left,
                                                              top,
                                                              right,
                                                              bottom,
                                                              inlineExtent,
                                                              blockThickness,
                                                              physicalBaseline,
                                                              utf16Start,
                                                              utf16End,
                                                              utf16EndExcludingWhitespace,
                                                              utf16EndIncludingNewline),
              MILESTRO_API_RET_FAILED);
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphGetLayoutMetrics(nullptr,
                                                              physicalWidth,
                                                              physicalHeight,
                                                              contentLeft,
                                                              contentTop,
                                                              contentRight,
                                                              contentBottom,
                                                              inkLeft,
                                                              inkTop,
                                                              inkRight,
                                                              inkBottom,
                                                              longestInlineExtent,
                                                              minIntrinsicInlineExtent,
                                                              maxIntrinsicInlineExtent,
                                                              columnCount),
              MILESTRO_API_RET_FAILED);

    EXPECT_EQ(MilestroSkiaTextlayoutParagraphLayout(paragraph.get(), -1),
              MILESTRO_API_RET_FAILED);
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphGetLayoutMetrics(paragraph.get(),
                                                              physicalWidth,
                                                              physicalHeight,
                                                              contentLeft,
                                                              contentTop,
                                                              contentRight,
                                                              contentBottom,
                                                              inkLeft,
                                                              inkTop,
                                                              inkRight,
                                                              inkBottom,
                                                              longestInlineExtent,
                                                              minIntrinsicInlineExtent,
                                                              maxIntrinsicInlineExtent,
                                                              columnCount),
              MILESTRO_API_RET_FAILED);
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphGetColumnMetrics(paragraph.get(),
                                                              0,
                                                              lineNumber,
                                                              hardBreak,
                                                              left,
                                                              top,
                                                              right,
                                                              bottom,
                                                              inlineExtent,
                                                              blockThickness,
                                                              physicalBaseline,
                                                              utf16Start,
                                                              utf16End,
                                                              utf16EndExcludingWhitespace,
                                                              utf16EndIncludingNewline),
              MILESTRO_API_RET_FAILED);
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphGetHeight(paragraph.get(), height),
              MILESTRO_API_RET_OK);
    EXPECT_EQ(height, 0);
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphGetLongestLine(paragraph.get(), longestLine),
              MILESTRO_API_RET_OK);
    EXPECT_EQ(longestLine, 0);

    EXPECT_EQ(MilestroSkiaTextlayoutParagraphLayoutWithInlineExtent(
                      paragraph.get(), std::numeric_limits<float>::quiet_NaN()),
              MILESTRO_API_RET_FAILED);
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphGetLayoutMetrics(paragraph.get(),
                                                              physicalWidth,
                                                              physicalHeight,
                                                              contentLeft,
                                                              contentTop,
                                                              contentRight,
                                                              contentBottom,
                                                              inkLeft,
                                                              inkTop,
                                                              inkRight,
                                                              inkBottom,
                                                              longestInlineExtent,
                                                              minIntrinsicInlineExtent,
                                                              maxIntrinsicInlineExtent,
                                                              columnCount),
              MILESTRO_API_RET_FAILED);
}

TEST(SkiaVerticalParagraphWrapper, EllipsisUsesTheSameCAbiMetricsRangesVisitorAndPathGeometry) {
    LoadIcuData();

    auto registry = milestro::skia::GetFontRegistry();
    ASSERT_NE(registry, nullptr);
    ASSERT_NE(registry->RegisterFontFromFile(MILESTRO_TEST_FONT_PATH),
              milestro::skia::MilestroRegisteredFontMgr::RegisterResult::Failed);
    ASSERT_NE(registry->RegisterFontFromFile(MILESTRO_TEST_FALLBACK_FONT_PATH),
              milestro::skia::MilestroRegisteredFontMgr::RegisterResult::Failed);
    milestro_text::GetFontCollection()->setFontFallbackEnabled(true);
    milestro_text::GetFontCollection()->clearCaches();

    milestro_text::TextStyle textStyle;
    textStyle.setFontFamilies({SkString("Source Han Sans VF")});
    textStyle.setFontSize(48);
    textStyle.setLocale(SkString("ja"));
    textStyle.setColor(SK_ColorBLACK);

    milestro_text::ParagraphStyle paragraphStyle;
    paragraphStyle.setTextStyle(&textStyle);
    paragraphStyle.setWritingMode(skia_text::WritingMode::kVerticalRl);
    paragraphStyle.setTextAlign(skia_text::TextAlign::kStart);
    paragraphStyle.setMaxLines(1);
    paragraphStyle.setEllipsis(SkString("\xE2\x80\xA6"));
    milestro_text::ParagraphBuilder builder(&paragraphStyle);
    builder.pushStyle(&textStyle);
    constexpr char kText[] = "天地玄黃";
    builder.addText(kText, sizeof(kText) - 1);
    std::unique_ptr<milestro_text::Paragraph> paragraph(builder.build());
    ASSERT_NE(paragraph, nullptr);

    EXPECT_EQ(MilestroSkiaTextlayoutParagraphLayoutWithInlineExtent(paragraph.get(), 96),
              MILESTRO_API_RET_OK);
    const auto& metrics = paragraph->getLayoutMetrics();
    ASSERT_EQ(metrics.columns.size(), 1u);
    EXPECT_EQ(metrics.columns[0].utf16Start, 0u);
    EXPECT_EQ(metrics.columns[0].utf16End, 1u);
    EXPECT_NEAR(metrics.columns[0].inlineExtent, 96, kTolerance);
    EXPECT_NEAR(metrics.physicalSize.height(), 96, kTolerance);
    EXPECT_TRUE(paragraph->unwrap()->didExceedMaxLines());

    float physicalWidth = 0;
    float physicalHeight = 0;
    float contentLeft = 0;
    float contentTop = 0;
    float contentRight = 0;
    float contentBottom = 0;
    float inkLeft = 0;
    float inkTop = 0;
    float inkRight = 0;
    float inkBottom = 0;
    float longestInlineExtent = 0;
    float minIntrinsicInlineExtent = 0;
    float maxIntrinsicInlineExtent = 0;
    uint64_t columnCount = 0;
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphGetLayoutMetrics(paragraph.get(),
                                                              physicalWidth,
                                                              physicalHeight,
                                                              contentLeft,
                                                              contentTop,
                                                              contentRight,
                                                              contentBottom,
                                                              inkLeft,
                                                              inkTop,
                                                              inkRight,
                                                              inkBottom,
                                                              longestInlineExtent,
                                                              minIntrinsicInlineExtent,
                                                              maxIntrinsicInlineExtent,
                                                              columnCount),
              MILESTRO_API_RET_OK);
    EXPECT_NEAR(physicalWidth, metrics.physicalSize.width(), kTolerance);
    EXPECT_NEAR(physicalHeight, metrics.physicalSize.height(), kTolerance);
    EXPECT_NEAR(longestInlineExtent, metrics.longestInlineExtent, kTolerance);
    EXPECT_EQ(columnCount, 1u);

    uint64_t lineNumber = 0;
    int32_t hardBreak = 0;
    float left = 0;
    float top = 0;
    float right = 0;
    float bottom = 0;
    float inlineExtent = 0;
    float blockThickness = 0;
    float physicalBaseline = 0;
    uint64_t utf16Start = 0;
    uint64_t utf16End = 0;
    uint64_t utf16EndExcludingWhitespace = 0;
    uint64_t utf16EndIncludingNewline = 0;
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphGetColumnMetrics(paragraph.get(),
                                                              0,
                                                              lineNumber,
                                                              hardBreak,
                                                              left,
                                                              top,
                                                              right,
                                                              bottom,
                                                              inlineExtent,
                                                              blockThickness,
                                                              physicalBaseline,
                                                              utf16Start,
                                                              utf16End,
                                                              utf16EndExcludingWhitespace,
                                                              utf16EndIncludingNewline),
              MILESTRO_API_RET_OK);
    EXPECT_EQ(utf16Start, 0u);
    EXPECT_EQ(utf16End, 1u);
    EXPECT_EQ(utf16EndExcludingWhitespace, 1u);
    EXPECT_NEAR(inlineExtent, metrics.columns[0].inlineExtent, kTolerance);
    EXPECT_NEAR(left, metrics.columns[0].physicalBounds.left(), kTolerance);
    EXPECT_NEAR(top, metrics.columns[0].physicalBounds.top(), kTolerance);
    EXPECT_NEAR(right, metrics.columns[0].physicalBounds.right(), kTolerance);
    EXPECT_NEAR(bottom, metrics.columns[0].physicalBounds.bottom(), kTolerance);

    auto* core = paragraph->unwrap();
    const auto coreBoxes = core->getRectsForRange(0,
                                                  4,
                                                  skia_text::RectHeightStyle::kTight,
                                                  skia_text::RectWidthStyle::kTight);
    ASSERT_EQ(coreBoxes.size(), 1u);
    RectCollector rects;
    EXPECT_EQ(paragraph->forEachRectInRange(0, 4, &rects, CollectRect), 0u);
    ASSERT_EQ(rects.rects.size(), 1u);
    ExpectRectsNear({coreBoxes.front().rect}, rects.rects);

    RectCollector hiddenRects;
    EXPECT_EQ(paragraph->forEachRectInRange(1, 4, &hiddenRects, CollectRect), 0u);
    EXPECT_TRUE(hiddenRects.rects.empty());
    EXPECT_TRUE(core->getRectsForRange(1,
                                       4,
                                       skia_text::RectHeightStyle::kTight,
                                       skia_text::RectWidthStyle::kTight)
                        .empty());

    const SkPath corePath = CorePath(core);
    ASSERT_FALSE(corePath.isEmpty());
    std::unique_ptr<milestro::skia::Path> wrapperPath(paragraph->toPath(0, 0));
    ASSERT_NE(wrapperPath, nullptr);
    const SkRect wrapperBounds = wrapperPath->unwrap().getBounds();
    const SkRect coreBounds = corePath.getBounds();
    EXPECT_NEAR(wrapperBounds.left(), coreBounds.left(), kTolerance);
    EXPECT_NEAR(wrapperBounds.top(), coreBounds.top(), kTolerance);
    EXPECT_NEAR(wrapperBounds.right(), coreBounds.right(), kTolerance);
    EXPECT_NEAR(wrapperBounds.bottom(), coreBounds.bottom(), kTolerance);

    bool sawAffix = false;
    size_t visitorGlyphs = 0;
    core->extendedVisit([&](int line,
                            const skia_text::Paragraph::ExtendedVisitorInfo* info) {
        if (info == nullptr) {
            return;
        }
        visitorGlyphs += info->count;
        bool allAnchorsEqual = info->count > 0;
        for (size_t index = 0; index < info->count; ++index) {
            allAnchorsEqual = allAnchorsEqual && info->utf8Starts[index] == 3u;
        }
        if (line == 0 && allAnchorsEqual && info->utf8Starts[info->count] == 3u) {
            sawAffix = true;
        }
    });
    EXPECT_TRUE(sawAffix);
    EXPECT_EQ(visitorGlyphs, 2u);
}

TEST(SkiaVerticalParagraphWrapper, DecorationOnlyChangesInkAcrossWrapperMetricsRangesPathAndHitTesting) {
    LoadIcuData();

    auto registry = milestro::skia::GetFontRegistry();
    ASSERT_NE(registry, nullptr);
    ASSERT_NE(registry->RegisterFontFromFile(MILESTRO_TEST_FONT_PATH),
              milestro::skia::MilestroRegisteredFontMgr::RegisterResult::Failed);
    ASSERT_NE(registry->RegisterFontFromFile(MILESTRO_TEST_FALLBACK_FONT_PATH),
              milestro::skia::MilestroRegisteredFontMgr::RegisterResult::Failed);
    milestro_text::GetFontCollection()->setFontFallbackEnabled(true);
    milestro_text::GetFontCollection()->clearCaches();

    milestro_text::TextStyle plainStyle;
    plainStyle.setFontFamilies({SkString("Source Han Sans VF")});
    plainStyle.setFontSize(40);
    plainStyle.setLocale(SkString("ja"));
    plainStyle.setColor(SK_ColorBLACK);
    auto decoratedStyle = plainStyle;
    decoratedStyle.setDecoration(skia_text::TextDecoration::kUnderline);
    decoratedStyle.setDecorationColor(SK_ColorRED);

    const std::string text = "「漢字ABCかな」";
    auto plain = BuildVerticalParagraph(text, plainStyle);
    auto decorated = BuildVerticalParagraph(text, decoratedStyle);
    ASSERT_NE(plain, nullptr);
    ASSERT_NE(decorated, nullptr);
    plain->layoutWithInlineExtent(180);
    decorated->layoutWithInlineExtent(180);

    const auto& plainMetrics = plain->getLayoutMetrics();
    const auto& decoratedMetrics = decorated->getLayoutMetrics();
    EXPECT_NEAR(decoratedMetrics.physicalSize.width(), plainMetrics.physicalSize.width(), kTolerance);
    EXPECT_NEAR(decoratedMetrics.physicalSize.height(), plainMetrics.physicalSize.height(), kTolerance);
    EXPECT_NEAR(decoratedMetrics.contentBounds.left(), plainMetrics.contentBounds.left(), kTolerance);
    EXPECT_NEAR(decoratedMetrics.contentBounds.top(), plainMetrics.contentBounds.top(), kTolerance);
    EXPECT_NEAR(decoratedMetrics.contentBounds.right(), plainMetrics.contentBounds.right(), kTolerance);
    EXPECT_NEAR(decoratedMetrics.contentBounds.bottom(), plainMetrics.contentBounds.bottom(), kTolerance);
    EXPECT_NEAR(decoratedMetrics.longestInlineExtent, plainMetrics.longestInlineExtent, kTolerance);
    EXPECT_NEAR(decoratedMetrics.minIntrinsicInlineExtent,
                plainMetrics.minIntrinsicInlineExtent,
                kTolerance);
    EXPECT_NEAR(decoratedMetrics.maxIntrinsicInlineExtent,
                plainMetrics.maxIntrinsicInlineExtent,
                kTolerance);
    ASSERT_EQ(decoratedMetrics.columns.size(), plainMetrics.columns.size());
    for (size_t index = 0; index < plainMetrics.columns.size(); ++index) {
        EXPECT_EQ(decoratedMetrics.columns[index].utf16Start, plainMetrics.columns[index].utf16Start);
        EXPECT_EQ(decoratedMetrics.columns[index].utf16End, plainMetrics.columns[index].utf16End);
        EXPECT_NEAR(decoratedMetrics.columns[index].inlineExtent,
                    plainMetrics.columns[index].inlineExtent,
                    kTolerance);
        EXPECT_NEAR(decoratedMetrics.columns[index].blockThickness,
                    plainMetrics.columns[index].blockThickness,
                    kTolerance);
        EXPECT_NEAR(decoratedMetrics.columns[index].physicalBounds.left(),
                    plainMetrics.columns[index].physicalBounds.left(),
                    kTolerance);
    }
    EXPECT_LT(decoratedMetrics.inkBounds.left(), plainMetrics.inkBounds.left());

    float physicalWidth = 0;
    float physicalHeight = 0;
    float contentLeft = 0;
    float contentTop = 0;
    float contentRight = 0;
    float contentBottom = 0;
    float inkLeft = 0;
    float inkTop = 0;
    float inkRight = 0;
    float inkBottom = 0;
    float longestInlineExtent = 0;
    float minIntrinsicInlineExtent = 0;
    float maxIntrinsicInlineExtent = 0;
    uint64_t columnCount = 0;
    ASSERT_EQ(MilestroSkiaTextlayoutParagraphGetLayoutMetrics(decorated.get(),
                                                              physicalWidth,
                                                              physicalHeight,
                                                              contentLeft,
                                                              contentTop,
                                                              contentRight,
                                                              contentBottom,
                                                              inkLeft,
                                                              inkTop,
                                                              inkRight,
                                                              inkBottom,
                                                              longestInlineExtent,
                                                              minIntrinsicInlineExtent,
                                                              maxIntrinsicInlineExtent,
                                                              columnCount),
              MILESTRO_API_RET_OK);
    EXPECT_NEAR(physicalWidth, plainMetrics.physicalSize.width(), kTolerance);
    EXPECT_NEAR(physicalHeight, plainMetrics.physicalSize.height(), kTolerance);
    EXPECT_NEAR(contentLeft, plainMetrics.contentBounds.left(), kTolerance);
    EXPECT_NEAR(contentTop, plainMetrics.contentBounds.top(), kTolerance);
    EXPECT_NEAR(contentRight, plainMetrics.contentBounds.right(), kTolerance);
    EXPECT_NEAR(contentBottom, plainMetrics.contentBounds.bottom(), kTolerance);
    EXPECT_NEAR(inkLeft, decoratedMetrics.inkBounds.left(), kTolerance);
    EXPECT_NEAR(inkTop, decoratedMetrics.inkBounds.top(), kTolerance);
    EXPECT_NEAR(inkRight, decoratedMetrics.inkBounds.right(), kTolerance);
    EXPECT_NEAR(inkBottom, decoratedMetrics.inkBounds.bottom(), kTolerance);
    EXPECT_EQ(columnCount, plainMetrics.columns.size());

    const size_t utf16Length = Utf16Length(text);
    const auto plainBoxes = plain->unwrap()->getRectsForRange(
            0,
            static_cast<unsigned>(utf16Length),
            skia_text::RectHeightStyle::kTight,
            skia_text::RectWidthStyle::kTight);
    RectCollector rects;
    EXPECT_EQ(decorated->forEachRectInRange(0, utf16Length, &rects, CollectRect), 0u);
    std::vector<SkRect> expectedRects;
    for (const auto& box: plainBoxes) {
        if (box.rect.width() > 0 && box.rect.height() > 0) {
            expectedRects.push_back(box.rect);
        }
    }
    ExpectRectsNear(expectedRects, rects.rects);

    const SkRect plainPathBounds = plain->toPath(0, 0)->unwrap().getBounds();
    const SkRect decoratedPathBounds = decorated->toPath(0, 0)->unwrap().getBounds();
    EXPECT_NEAR(decoratedPathBounds.left(), plainPathBounds.left(), kTolerance);
    EXPECT_NEAR(decoratedPathBounds.top(), plainPathBounds.top(), kTolerance);
    EXPECT_NEAR(decoratedPathBounds.right(), plainPathBounds.right(), kTolerance);
    EXPECT_NEAR(decoratedPathBounds.bottom(), plainPathBounds.bottom(), kTolerance);

    ASSERT_FALSE(expectedRects.empty());
    const SkRect& sourceRect = expectedRects.front();
    EXPECT_TRUE(decorated->hitTestRange(0, utf16Length, sourceRect.centerX(), sourceRect.centerY()));
    const SkScalar decorationOnlyX = decoratedMetrics.inkBounds.left() + 0.5f;
    ASSERT_LT(decorationOnlyX, sourceRect.left());
    EXPECT_FALSE(decorated->hitTestRange(0, utf16Length, decorationOnlyX, sourceRect.centerY()));
}

TEST(SkiaVerticalParagraphWrapper, CAbiStrongRtlCheckUsesIcuDirection) {
    LoadIcuData();

    int32_t containsStrongRtl = -1;
    const std::string neutral = "\xD8\x8C\xE6\xBC\xA2";
    EXPECT_EQ(MilestroSkiaTextlayoutContainsStrongRtl(
                      reinterpret_cast<uint8_t*>(const_cast<char*>(neutral.data())),
                      neutral.size(),
                      containsStrongRtl),
              MILESTRO_API_RET_OK);
    EXPECT_EQ(containsStrongRtl, 0);

    const std::string rightToLeftMark = "\xE2\x80\x8F";
    EXPECT_EQ(MilestroSkiaTextlayoutContainsStrongRtl(
                      reinterpret_cast<uint8_t*>(const_cast<char*>(rightToLeftMark.data())),
                      rightToLeftMark.size(),
                      containsStrongRtl),
              MILESTRO_API_RET_OK);
    EXPECT_EQ(containsStrongRtl, 0);

    const std::string arabicLetterMark = "\xD8\x9C";
    EXPECT_EQ(MilestroSkiaTextlayoutContainsStrongRtl(
                      reinterpret_cast<uint8_t*>(const_cast<char*>(arabicLetterMark.data())),
                      arabicLetterMark.size(),
                      containsStrongRtl),
              MILESTRO_API_RET_OK);
    EXPECT_EQ(containsStrongRtl, 0);

    const std::string arabic = "\xD9\x85\xD8\xB1\xD8\xAD\xD8\xA8\xD8\xA7";
    EXPECT_EQ(MilestroSkiaTextlayoutContainsStrongRtl(
                      reinterpret_cast<uint8_t*>(const_cast<char*>(arabic.data())),
                      arabic.size(),
                      containsStrongRtl),
              MILESTRO_API_RET_OK);
    EXPECT_EQ(containsStrongRtl, 1);

    const std::string hebrew = "\xD7\xA9\xD7\x9C\xD7\x95\xD7\x9D";
    EXPECT_EQ(MilestroSkiaTextlayoutContainsStrongRtl(
                      reinterpret_cast<uint8_t*>(const_cast<char*>(hebrew.data())),
                      hebrew.size(),
                      containsStrongRtl),
              MILESTRO_API_RET_OK);
    EXPECT_EQ(containsStrongRtl, 1);

    EXPECT_EQ(MilestroSkiaTextlayoutContainsStrongRtl(nullptr, 0, containsStrongRtl),
              MILESTRO_API_RET_OK);
    EXPECT_EQ(containsStrongRtl, 0);
    EXPECT_EQ(MilestroSkiaTextlayoutContainsStrongRtl(nullptr, 1, containsStrongRtl),
              MILESTRO_API_RET_FAILED);
}

TEST(SkiaVerticalParagraphWrapper, CAbiLayoutMetricsRejectHorizontalParagraph) {
    LoadIcuData();

    auto registry = milestro::skia::GetFontRegistry();
    ASSERT_NE(registry, nullptr);
    ASSERT_NE(registry->RegisterFontFromFile(MILESTRO_TEST_FONT_PATH),
              milestro::skia::MilestroRegisteredFontMgr::RegisterResult::Failed);
    ASSERT_NE(registry->RegisterFontFromFile(MILESTRO_TEST_FALLBACK_FONT_PATH),
              milestro::skia::MilestroRegisteredFontMgr::RegisterResult::Failed);
    milestro_text::GetFontCollection()->setFontFallbackEnabled(true);
    milestro_text::GetFontCollection()->clearCaches();

    milestro_text::TextStyle textStyle;
    textStyle.setFontFamilies({SkString("Source Han Sans VF")});
    textStyle.setFontSize(44);
    textStyle.setLocale(SkString("ja"));
    auto paragraph = BuildHorizontalParagraph("ABC", textStyle);
    paragraph->layout(200);

    int32_t writingMode = -1;
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphGetWritingMode(paragraph.get(), writingMode),
              MILESTRO_API_RET_OK);
    EXPECT_EQ(writingMode, static_cast<int32_t>(skia_text::WritingMode::kHorizontalTb));

    float height = 0;
    float longestLine = 0;
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphGetHeight(paragraph.get(), height),
              MILESTRO_API_RET_OK);
    EXPECT_GT(height, 0);
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphGetLongestLine(paragraph.get(), longestLine),
              MILESTRO_API_RET_OK);
    EXPECT_GT(longestLine, 0);

    float physicalWidth = 0;
    float physicalHeight = 0;
    float contentLeft = 0;
    float contentTop = 0;
    float contentRight = 0;
    float contentBottom = 0;
    float inkLeft = 0;
    float inkTop = 0;
    float inkRight = 0;
    float inkBottom = 0;
    float longestInlineExtent = 0;
    float minIntrinsicInlineExtent = 0;
    float maxIntrinsicInlineExtent = 0;
    uint64_t columnCount = 0;
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphGetLayoutMetrics(paragraph.get(),
                                                              physicalWidth,
                                                              physicalHeight,
                                                              contentLeft,
                                                              contentTop,
                                                              contentRight,
                                                              contentBottom,
                                                              inkLeft,
                                                              inkTop,
                                                              inkRight,
                                                              inkBottom,
                                                              longestInlineExtent,
                                                              minIntrinsicInlineExtent,
                                                              maxIntrinsicInlineExtent,
                                                              columnCount),
              MILESTRO_API_RET_FAILED);

    uint64_t lineNumber = 0;
    int32_t hardBreak = 0;
    float left = 0;
    float top = 0;
    float right = 0;
    float bottom = 0;
    float inlineExtent = 0;
    float blockThickness = 0;
    float physicalBaseline = 0;
    uint64_t utf16Start = 0;
    uint64_t utf16End = 0;
    uint64_t utf16EndExcludingWhitespace = 0;
    uint64_t utf16EndIncludingNewline = 0;
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphGetColumnMetrics(paragraph.get(),
                                                              0,
                                                              lineNumber,
                                                              hardBreak,
                                                              left,
                                                              top,
                                                              right,
                                                              bottom,
                                                              inlineExtent,
                                                              blockThickness,
                                                              physicalBaseline,
                                                              utf16Start,
                                                              utf16End,
                                                              utf16EndExcludingWhitespace,
                                                              utf16EndIncludingNewline),
              MILESTRO_API_RET_FAILED);
}
