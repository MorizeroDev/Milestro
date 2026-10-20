#include "Milestro/skia/FontRegistry.h"
#include "Milestro/skia/textlayout/FontCollection.h"
#include "Milestro/skia/textlayout/Paragraph.h"
#include "Milestro/skia/textlayout/ParagraphBuilder.h"
#include "Milestro/skia/textlayout/ParagraphStyle.h"
#include "Milestro/skia/textlayout/TextStyle.h"

#include "include/core/SkString.h"

#include <gtest/gtest.h>
#include <memory>

namespace milestro_text = milestro::skia::textlayout;

namespace {

constexpr float kFontSize = 36.0f;
constexpr float kLayoutWidth = 500.0f;

milestro_text::TextStyle MakeBaseStyle() {
    milestro_text::TextStyle textStyle;
    textStyle.setFontFamilies({SkString("Source Han Sans VF")});
    textStyle.setFontSize(kFontSize);
    textStyle.setLocale(SkString("en"));
    textStyle.setColor(SK_ColorWHITE);
    return textStyle;
}

float MeasureHeight(milestro_text::TextStyle &style, const char *text, size_t len) {
    milestro_text::ParagraphStyle paragraphStyle;
    paragraphStyle.setTextStyle(&style);
    milestro_text::ParagraphBuilder builder(&paragraphStyle);
    builder.pushStyle(&style);
    builder.addText(text, len);
    std::unique_ptr<milestro_text::Paragraph> paragraph(builder.build());
    paragraph->layout(kLayoutWidth);
    return paragraph->getHeight();
}

float MeasureLongestLine(milestro_text::TextStyle &style, const char *text, size_t len) {
    milestro_text::ParagraphStyle paragraphStyle;
    paragraphStyle.setTextStyle(&style);
    milestro_text::ParagraphBuilder builder(&paragraphStyle);
    builder.pushStyle(&style);
    builder.addText(text, len);
    std::unique_ptr<milestro_text::Paragraph> paragraph(builder.build());
    paragraph->layout(kLayoutWidth);
    return paragraph->getLongestLine();
}

} // namespace

class SkiaParagraphSpacingTest : public ::testing::Test {
protected:
    void SetUp() override {
        const auto result = milestro::skia::GetFontRegistry()->RegisterFontFromFile(MILESTRO_TEST_FONT_PATH);
        ASSERT_NE(result, milestro::skia::MilestroRegisteredFontMgr::RegisterResult::Failed);
        milestro_text::GetFontCollection()->clearCaches();
    }
};

TEST_F(SkiaParagraphSpacingTest, LetterSpacingWidensLongestLine) {
    auto baseStyle = MakeBaseStyle();
    auto spacedStyle = MakeBaseStyle();
    spacedStyle.setLetterSpacing(10.0f);

    float baseWidth = MeasureLongestLine(baseStyle, "Hello", 5);
    float spacedWidth = MeasureLongestLine(spacedStyle, "Hello", 5);

    EXPECT_NEAR(spacedWidth - baseWidth, 50.0f, 6.0f)
        << "base=" << baseWidth << " spaced=" << spacedWidth;
}

TEST_F(SkiaParagraphSpacingTest, WordSpacingOnlyWidensAtWordBoundaries) {
    auto baseStyle = MakeBaseStyle();
    auto spacedStyle = MakeBaseStyle();
    spacedStyle.setWordSpacing(30.0f);

    const std::string twoWords = "Hello World";
    const std::string oneWord = "Hello";
    float baseTwoWords = MeasureLongestLine(baseStyle, twoWords.c_str(), twoWords.size());
    float spacedTwoWords = MeasureLongestLine(spacedStyle, twoWords.c_str(), twoWords.size());
    float baseOneWord = MeasureLongestLine(baseStyle, oneWord.c_str(), oneWord.size());
    float spacedOneWord = MeasureLongestLine(spacedStyle, oneWord.c_str(), oneWord.size());

    EXPECT_NEAR(spacedTwoWords - baseTwoWords, 30.0f, 3.0f);
    EXPECT_NEAR(spacedOneWord - baseOneWord, 0.0f, 1.0f);
}

TEST_F(SkiaParagraphSpacingTest, LineHeightMultiplierRequiresHeightOverride) {
    auto baseStyle = MakeBaseStyle();
    float twoLineHeight = MeasureHeight(baseStyle, "Hello\nWorld", 11);

    auto tallStyle = MakeBaseStyle();
    tallStyle.setHeight(2.0f);
    float tallNoOverrideHeight = MeasureHeight(tallStyle, "Hello\nWorld", 11);

    auto tallOverrideStyle = MakeBaseStyle();
    tallOverrideStyle.setHeight(2.0f);
    tallOverrideStyle.setHeightOverride(true);
    float tallOverrideHeight = MeasureHeight(tallOverrideStyle, "Hello\nWorld", 11);

    EXPECT_NEAR(tallNoOverrideHeight, twoLineHeight, 0.5f)
        << "Height without HeightOverride must be a no-op: twoLine=" << twoLineHeight
        << " tallNoOverride=" << tallNoOverrideHeight;
    EXPECT_GT(tallOverrideHeight, twoLineHeight * 1.25f)
        << "Height with HeightOverride must scale lines: twoLine=" << twoLineHeight
        << " tallOverride=" << tallOverrideHeight;
}
