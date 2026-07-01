#include "Milestro/skia/FontRegistry.h"
#include "Milestro/skia/unicode/Unicode.h"
#include "Milestro/skia/textlayout/Paragraph.h"
#include "Milestro/skia/textlayout/ParagraphBuilder.h"
#include "Milestro/skia/textlayout/ParagraphStyle.h"
#include "Milestro/skia/textlayout/TextStyle.h"

#include "include/core/SkString.h"
#include "modules/skparagraph/include/Paragraph.h"
#include "modules/skunicode/include/SkUnicode.h"

#include <algorithm>
#include <filesystem>
#include <gtest/gtest.h>
#include <memory>
#include <string>
#include <vector>

namespace fs = std::filesystem;
namespace milestro_text = milestro::skia::textlayout;
namespace skia_text = skia::textlayout;

namespace {

int RegisterFontsInDirectory(const fs::path &dirPath) {
    auto *fontRegistry = milestro::skia::GetFontRegistry();
    int successCount = 0;
    if (!fs::exists(dirPath) || !fs::is_directory(dirPath)) {
        return successCount;
    }

    for (const auto &entry: fs::directory_iterator(dirPath)) {
        if (entry.path().extension() != ".bytes") {
            continue;
        }

        const auto result = fontRegistry->RegisterFontFromFile(entry.path().string().c_str());
        if (result == milestro::skia::MilestroRegisteredFontMgr::RegisterResult::Succeed ||
            result == milestro::skia::MilestroRegisteredFontMgr::RegisterResult::Duplicated) {
            ++successCount;
        }
    }
    return successCount;
}

std::unique_ptr<milestro_text::Paragraph> BuildParagraph(const std::string &text, SkScalar layoutWidth) {
    auto textStyle = std::make_unique<milestro_text::TextStyle>();
    std::vector<SkString> fontFamilies;
    fontFamilies.emplace_back("Source Han Sans VF");
    fontFamilies.emplace_back("Noto Color Emoji");
    textStyle->setFontFamilies(fontFamilies);
    textStyle->setFontSize(36);
    textStyle->setLocale(SkString("en"));
    textStyle->setColor(SK_ColorWHITE);

    auto paragraphStyle = std::make_unique<milestro_text::ParagraphStyle>();
    paragraphStyle->setTextStyle(textStyle.get());

    auto paragraphBuilder = std::make_unique<milestro_text::ParagraphBuilder>(paragraphStyle.get());
    paragraphBuilder->pushStyle(textStyle.get());
    paragraphBuilder->addText(text.c_str(), text.size());

    std::unique_ptr<milestro_text::Paragraph> paragraph(paragraphBuilder->build());
    paragraph->layout(layoutWidth);
    return paragraph;
}

std::u16string ToUtf16(const std::string &text) {
    return SkUnicode::convertUtf8ToUtf16(text.c_str(), static_cast<int>(text.size()));
}

} // namespace

class InputBoxApiSpikeTest : public ::testing::Test {
protected:
    void SetUp() override {
        const auto fontDir = fs::current_path() / "data" / "font";
        ASSERT_GT(RegisterFontsInDirectory(fontDir), 0);
    }
};

TEST_F(InputBoxApiSpikeTest, ParagraphExposesEditingPrimitivesForRepresentativeText) {
    const std::vector<std::string> samples = {
            "ASCII input 123",
            "日本語と中文",
            "e\xCC\x81 and \xC3\xA9",
            "emoji \xF0\x9F\x91\xA8\xE2\x80\x8D\xF0\x9F\x91\xA9\xE2\x80\x8D\xF0\x9F\x91\xA7 skin \xF0\x9F\x91\x8D\xF0\x9F\x8F\xBD",
            "\xE0\xB8\xAA\xE0\xB8\xA7\xE0\xB8\xB1\xE0\xB8\xAA\xE0\xB8\x94\xE0\xB8\xB5",
            "\xD7\xA9\xD7\x9C\xD7\x95\xD7\x9D English 123 \xD8\xB3\xD9\x84\xD8\xA7\xD9\x85",
    };

    for (const auto &sample: samples) {
        auto paragraph = BuildParagraph(sample, 480);
        auto *skParagraph = paragraph->unwrap();
        ASSERT_NE(skParagraph, nullptr);

        const auto utf16Length = ToUtf16(sample).size();
        ASSERT_GT(utf16Length, 0U);

        const auto hit = skParagraph->getGlyphPositionAtCoordinate(1, 18);
        EXPECT_LE(static_cast<size_t>(hit.position), utf16Length);

        auto boxes = skParagraph->getRectsForRange(
                0,
                static_cast<unsigned>(utf16Length),
                skia_text::RectHeightStyle::kTight,
                skia_text::RectWidthStyle::kTight);
        EXPECT_FALSE(boxes.empty());

        skia_text::Paragraph::GlyphInfo glyphInfo;
        EXPECT_TRUE(skParagraph->getGlyphInfoAtUTF16Offset(0, &glyphInfo));
        EXPECT_LE(glyphInfo.fGraphemeClusterTextRange.start, glyphInfo.fGraphemeClusterTextRange.end);

        skia_text::Paragraph::GlyphClusterInfo clusterInfo;
        EXPECT_TRUE(skParagraph->getClosestGlyphClusterAt(1, 18, &clusterInfo));
        EXPECT_LE(clusterInfo.fClusterTextRange.start, clusterInfo.fClusterTextRange.end);

        std::vector<skia_text::LineMetrics> lineMetrics;
        skParagraph->getLineMetrics(lineMetrics);
        EXPECT_FALSE(lineMetrics.empty());
        EXPECT_TRUE(skParagraph->getLineMetricsAt(0, nullptr));
        EXPECT_EQ(0, skParagraph->getLineNumberAtUTF16Offset(0));

        const auto word = skParagraph->getWordBoundary(0);
        EXPECT_LE(word.start, word.end);

        EXPECT_GT(skParagraph->getHeight(), 0);
        EXPECT_GT(skParagraph->getMaxIntrinsicWidth(), 0);
        EXPECT_GE(skParagraph->getLongestLine(), 0);
    }
}

TEST_F(InputBoxApiSpikeTest, SkUnicodeProvidesGraphemeAndGlyphClusterFlags) {
    std::string sample =
            "A e\xCC\x81 \xF0\x9F\x91\xA8\xE2\x80\x8D\xF0\x9F\x91\xA9\xE2\x80\x8D\xF0\x9F\x91\xA7 "
            "\xE0\xB8\xAA\xE0\xB8\xA7\xE0\xB8\xB1\xE0\xB8\xAA\xE0\xB8\x94\xE0\xB8\xB5";

    auto unicode = milestro::skia::GetUnicodeProvider()->unwrap();
    ASSERT_NE(unicode, nullptr);

    skia_private::TArray<SkUnicode::CodeUnitFlags, true> flags;
    ASSERT_TRUE(unicode->computeCodeUnitFlags(sample.data(), static_cast<int>(sample.size()), false, &flags));
    ASSERT_GT(flags.size(), 0);

    int graphemeStarts = 0;
    int glyphClusterStarts = 0;
    for (int i = 0; i < flags.size(); ++i) {
        if (SkUnicode::hasGraphemeStartFlag(flags[i])) {
            ++graphemeStarts;
        }
        if ((flags[i] & SkUnicode::CodeUnitFlags::kGlyphClusterStart) == SkUnicode::CodeUnitFlags::kGlyphClusterStart) {
            ++glyphClusterStarts;
        }
    }

    EXPECT_GT(graphemeStarts, 0);
    EXPECT_GT(glyphClusterStarts, 0);
    EXPECT_LT(graphemeStarts, static_cast<int>(sample.size()));
}
