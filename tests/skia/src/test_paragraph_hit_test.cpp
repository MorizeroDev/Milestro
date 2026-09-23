#include "Milestro/game/milestro_game_interface.h"
#include "Milestro/game/milestro_game_retcode.h"
#include "Milestro/skia/FontRegistry.h"
#include "Milestro/skia/textlayout/FontCollection.h"
#include "Milestro/skia/textlayout/Paragraph.h"
#include "Milestro/skia/textlayout/ParagraphBuilder.h"
#include "Milestro/skia/textlayout/ParagraphStyle.h"
#include "Milestro/skia/textlayout/TextStyle.h"

#include "include/core/SkString.h"
#include "modules/skparagraph/include/Paragraph.h"
#include "modules/skunicode/include/SkUnicode.h"

#include <gtest/gtest.h>
#include <limits>
#include <memory>
#include <string>
#include <vector>

namespace milestro_text = milestro::skia::textlayout;
namespace skia_text = skia::textlayout;

namespace {

std::unique_ptr<milestro_text::Paragraph>
BuildParagraph(const std::string& text,
               SkScalar layoutWidth,
               skia_text::TextAlign align = skia_text::TextAlign::kLeft,
               skia_text::TextDirection direction = skia_text::TextDirection::kLtr,
               size_t maxLines = std::numeric_limits<size_t>::max()) {
    milestro_text::TextStyle textStyle;
    textStyle.setFontFamilies({SkString("Source Han Sans VF")});
    textStyle.setFontSize(36);
    textStyle.setLocale(SkString("en"));
    textStyle.setColor(SK_ColorWHITE);

    milestro_text::ParagraphStyle paragraphStyle;
    paragraphStyle.setTextStyle(&textStyle);
    paragraphStyle.setTextAlign(align);
    paragraphStyle.setTextDirection(direction);
    if (maxLines != std::numeric_limits<size_t>::max()) {
        paragraphStyle.setMaxLines(maxLines);
        paragraphStyle.setEllipsis(SkString("\xE2\x80\xA6"));
    }

    milestro_text::ParagraphBuilder builder(&paragraphStyle);
    builder.pushStyle(&textStyle);
    builder.addText(text.c_str(), text.size());
    std::unique_ptr<milestro_text::Paragraph> paragraph(builder.build());
    paragraph->layout(layoutWidth);
    return paragraph;
}

size_t Utf16Length(const std::string& text) {
    return SkUnicode::convertUtf8ToUtf16(text.c_str(), static_cast<int>(text.size())).size();
}

SkPoint Center(const skia_text::TextBox& box) {
    return SkPoint::Make(box.rect.centerX(), box.rect.centerY());
}

std::vector<skia_text::TextBox> BoxesForRange(milestro_text::Paragraph* paragraph, size_t startUtf16, size_t endUtf16) {
    return paragraph->unwrap()->getRectsForRange(static_cast<unsigned>(startUtf16),
                                                 static_cast<unsigned>(endUtf16),
                                                 skia_text::RectHeightStyle::kTight,
                                                 skia_text::RectWidthStyle::kTight);
}

} // namespace

class SkiaParagraphHitTest : public ::testing::Test {
protected:
    void SetUp() override {
        const auto result = milestro::skia::GetFontRegistry()->RegisterFontFromFile(MILESTRO_TEST_FONT_PATH);
        ASSERT_NE(result, milestro::skia::MilestroRegisteredFontMgr::RegisterResult::Failed);
        milestro_text::GetFontCollection()->clearCaches();
    }
};

TEST_F(SkiaParagraphHitTest, HitsEveryTightBoxForWrappedUnicodeRangeOnly) {
    const std::string prefix = "prefix ";
    const std::string linked = "\xF0\x9F\x98\x80 linked words \xE6\x9B\x9C";
    auto paragraph = BuildParagraph(prefix + linked + " suffix", 150);
    const auto start = Utf16Length(prefix);
    const auto end = start + Utf16Length(linked);
    const auto boxes = BoxesForRange(paragraph.get(), start, end);

    ASSERT_GE(boxes.size(), 2u);
    for (const auto& box: boxes) {
        const auto center = Center(box);
        EXPECT_TRUE(paragraph->hitTestRange(start, end, center.x(), center.y()));
    }

    const auto prefixBoxes = BoxesForRange(paragraph.get(), 0, start);
    ASSERT_FALSE(prefixBoxes.empty());
    const auto prefixCenter = Center(prefixBoxes.front());
    EXPECT_FALSE(paragraph->hitTestRange(start, end, prefixCenter.x(), prefixCenter.y()));
}

TEST_F(SkiaParagraphHitTest, UsesLaidOutRtlAndAlignmentGeometry) {
    const std::string linked = "\xD7\xA9\xD7\x9C\xD7\x95\xD7\x9D";
    auto paragraph = BuildParagraph(linked, 300, skia_text::TextAlign::kRight, skia_text::TextDirection::kRtl);
    const auto boxes = BoxesForRange(paragraph.get(), 0, Utf16Length(linked));

    ASSERT_FALSE(boxes.empty());
    const auto center = Center(boxes.front());
    EXPECT_TRUE(paragraph->hitTestRange(0, Utf16Length(linked), center.x(), center.y()));
    EXPECT_FALSE(paragraph->hitTestRange(0, Utf16Length(linked), 1, center.y()));
}

TEST_F(SkiaParagraphHitTest, EllipsizedHiddenRangeHasNoHitBox) {
    const std::string visiblePrefix = "visible text before ";
    const std::string hiddenLink = "HIDDEN-LINK-RANGE";
    auto paragraph = BuildParagraph(visiblePrefix + hiddenLink,
                                    145,
                                    skia_text::TextAlign::kLeft,
                                    skia_text::TextDirection::kLtr,
                                    1);
    const auto hiddenStart = Utf16Length(visiblePrefix);
    const auto hiddenEnd = hiddenStart + Utf16Length(hiddenLink);
    const auto hiddenBoxes = BoxesForRange(paragraph.get(), hiddenStart, hiddenEnd);
    const auto visibleBoxes = BoxesForRange(paragraph.get(), 0, 1);

    ASSERT_TRUE(hiddenBoxes.empty());
    ASSERT_FALSE(visibleBoxes.empty());
    const auto visibleCenter = Center(visibleBoxes.front());
    EXPECT_FALSE(paragraph->hitTestRange(hiddenStart, hiddenEnd, visibleCenter.x(), visibleCenter.y()));
}

TEST_F(SkiaParagraphHitTest, RejectsInvalidRangesCoordinatesAndNullCAbiParagraph) {
    auto paragraph = BuildParagraph("link", 200);
    EXPECT_FALSE(paragraph->hitTestRange(1, 1, 10, 10));
    EXPECT_FALSE(paragraph->hitTestRange(0, 4, std::numeric_limits<float>::quiet_NaN(), 10));
    EXPECT_FALSE(paragraph->hitTestRange(0, 4, 10, std::numeric_limits<float>::infinity()));

    int32_t hit = -1;
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphHitTestRange(nullptr, 0, 4, 10, 10, hit), MILESTRO_API_RET_FAILED);

    EXPECT_EQ(MilestroSkiaTextlayoutParagraphHitTestRange(paragraph.get(),
                                                          std::numeric_limits<uint64_t>::max() - 1,
                                                          std::numeric_limits<uint64_t>::max(),
                                                          10,
                                                          10,
                                                          hit),
              MILESTRO_API_RET_OK);
    EXPECT_EQ(hit, 0);

    const auto boxes = BoxesForRange(paragraph.get(), 0, 4);
    ASSERT_FALSE(boxes.empty());
    const auto center = Center(boxes.front());
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphHitTestRange(paragraph.get(), 0, 4, center.x(), center.y(), hit),
              MILESTRO_API_RET_OK);
    EXPECT_EQ(hit, 1);
}

TEST_F(SkiaParagraphHitTest, RubyCAbiBuildsNativeParagraphAndReportsInvalidInput) {
    milestro_text::TextStyle textStyle;
    textStyle.setFontFamilies({SkString("Source Han Sans VF")});
    textStyle.setFontSize(36);
    milestro_text::ParagraphStyle paragraphStyle;
    paragraphStyle.setTextStyle(&textStyle);
    milestro_text::ParagraphBuilder builder(&paragraphStyle);
    const std::string text = "\xE6\xBC\xA2\xE5\xAD\x97\xE5\xBE\x8C";
    const std::string reading = "kanji";
    const auto* payload = reinterpret_cast<const uint8_t*>(reading.data());
    builder.addText(text.data(), text.size());
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphBuilderAddRuby(nullptr, 0, 6, payload, reading.size()),
              MILESTRO_API_RET_FAILED);
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphBuilderAddRuby(&builder, 0, 6, nullptr, reading.size()),
              MILESTRO_API_RET_FAILED);
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphBuilderAddRuby(&builder, 6, 0, payload, reading.size()),
              MILESTRO_API_RET_FAILED);
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphBuilderAddRuby(&builder, 0, 6, payload, 0),
              MILESTRO_API_RET_FAILED);
    ASSERT_EQ(MilestroSkiaTextlayoutParagraphBuilderAddRuby(&builder, 0, 6, payload, reading.size()),
              MILESTRO_API_RET_OK);
    milestro_text::Paragraph* built = nullptr;
    ASSERT_EQ(MilestroSkiaTextlayoutParagraphBuilderBuild(&builder, built), MILESTRO_API_RET_OK);
    std::unique_ptr<milestro_text::Paragraph> paragraph(built);
    paragraph->layout(80);
    EXPECT_EQ(paragraph->unwrap()->getLineNumberAt(0), paragraph->unwrap()->getLineNumberAt(3));
    auto plain = BuildParagraph(text, 80);
    EXPECT_GT(paragraph->unwrap()->getHeight(), plain->unwrap()->getHeight());
    auto boxes = BoxesForRange(paragraph.get(), 0, 2);
    ASSERT_FALSE(boxes.empty());
    const auto center = Center(boxes.front());
    EXPECT_TRUE(paragraph->hitTestRange(0, 2, center.x(), center.y()));
    int annotations = 0;
    paragraph->unwrap()->visit([&](int, const skia_text::Paragraph::VisitorInfo* info) {
        if (info && (info->flags & skia_text::Paragraph::kRubyAnnotation_VisitorFlag)) annotations += info->count;
    });
    EXPECT_GT(annotations, 0);

    milestro_text::ParagraphBuilder invalid(&paragraphStyle);
    invalid.addText(text.data(), text.size());
    ASSERT_EQ(MilestroSkiaTextlayoutParagraphBuilderAddRuby(&invalid, 0, text.size() + 1, payload, reading.size()),
              MILESTRO_API_RET_OK);
    built = nullptr;
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphBuilderBuild(&invalid, built), MILESTRO_API_RET_FAILED);
    EXPECT_EQ(built, nullptr);
}

TEST_F(SkiaParagraphHitTest, RubyCAbiAcceptsStyleAndRejectsInvalidEnums) {
    milestro_text::TextStyle textStyle;
    textStyle.setFontFamilies({SkString("Source Han Sans VF")});
    textStyle.setFontSize(36);
    milestro_text::ParagraphStyle paragraphStyle;
    paragraphStyle.setTextStyle(&textStyle);
    milestro_text::ParagraphBuilder builder(&paragraphStyle);
    const std::string text = "\xE6\xBC\xA2";
    const std::string reading = "kan";
    builder.addText(text.data(), text.size());

    EXPECT_EQ(MilestroSkiaTextlayoutParagraphBuilderAddRubyWithStyle(&builder,
                                                                     0,
                                                                     text.size(),
                                                                     reinterpret_cast<const uint8_t*>(reading.data()),
                                                                     reading.size(),
                                                                     &textStyle,
                                                                     0.5f,
                                                                     8,
                                                                     1,
                                                                     1,
                                                                     0,
                                                                     0),
              MILESTRO_API_RET_OK);
    milestro_text::Paragraph* built = nullptr;
    ASSERT_EQ(MilestroSkiaTextlayoutParagraphBuilderBuild(&builder, built), MILESTRO_API_RET_OK);
    std::unique_ptr<milestro_text::Paragraph> paragraph(built);
    paragraph->layout(200);
    SkRect base = SkRect::MakeEmpty();
    SkRect annotation = SkRect::MakeEmpty();
    paragraph->unwrap()->visit([&](int, const skia_text::Paragraph::VisitorInfo* info) {
        if (!info)
            return;
        std::vector<SkRect> bounds(info->count);
        info->font.getBounds({info->glyphs, static_cast<size_t>(info->count)}, bounds, nullptr);
        for (int index = 0; index < info->count; ++index) {
            bounds[index].offset(info->origin + info->positions[index]);
            if (info->flags & skia_text::Paragraph::kRubyAnnotation_VisitorFlag)
                annotation.join(bounds[index]);
            else
                base.join(bounds[index]);
        }
    });
    EXPECT_GT(annotation.top(), base.bottom());

    milestro_text::ParagraphBuilder invalid(&paragraphStyle);
    invalid.addText(text.data(), text.size());
    EXPECT_EQ(MilestroSkiaTextlayoutParagraphBuilderAddRubyWithStyle(&invalid,
                                                                     0,
                                                                     text.size(),
                                                                     reinterpret_cast<const uint8_t*>(reading.data()),
                                                                     reading.size(),
                                                                     nullptr,
                                                                     0.5f,
                                                                     2,
                                                                     99,
                                                                     1,
                                                                     0,
                                                                     0),
              MILESTRO_API_RET_FAILED);
}
