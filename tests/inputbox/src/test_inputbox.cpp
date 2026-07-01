#include "Milestro/skia/FontRegistry.h"
#include "Milestro/skia/textlayout/InputBox.h"
#include "Milestro/skia/textlayout/ParagraphStyle.h"
#include "Milestro/skia/textlayout/TextStyle.h"

#include "include/core/SkString.h"
#include "modules/skunicode/include/SkUnicode.h"

#include <filesystem>
#include <gtest/gtest.h>
#include <memory>
#include <string>
#include <vector>

namespace fs = std::filesystem;
namespace milestro_text = milestro::skia::textlayout;

namespace {

int RegisterFontsInDirectory(const fs::path& dirPath) {
    auto* fontRegistry = milestro::skia::GetFontRegistry();
    int successCount = 0;
    if (!fs::exists(dirPath) || !fs::is_directory(dirPath)) {
        return successCount;
    }

    for (const auto& entry: fs::directory_iterator(dirPath)) {
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

std::unique_ptr<milestro_text::InputBox> MakeInputBox() {
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
    paragraphStyle->setMaxLines(1);

    auto inputBox = std::make_unique<milestro_text::InputBox>(paragraphStyle.get(), textStyle.get());
    inputBox->setViewport(320, 64);
    return inputBox;
}

std::u16string ToUtf16(const std::string& text) {
    return SkUnicode::convertUtf8ToUtf16(text.c_str(), static_cast<int>(text.size()));
}

} // namespace

class InputBoxTest : public ::testing::Test {
protected:
    void SetUp() override {
        const auto fontDir = fs::current_path() / "data" / "font";
        ASSERT_GT(RegisterFontsInDirectory(fontDir), 0);
    }
};

TEST_F(InputBoxTest, BoundaryMapConvertsUtf8AndUtf16Offsets) {
    const std::string text = "A \xE6\x97\xA5 e\xCC\x81 \xF0\x9F\x91\x8D\xF0\x9F\x8F\xBD";
    milestro_text::TextBoundaryMap map(text);

    EXPECT_EQ(map.utf8ToUtf16(0), 0U);
    EXPECT_EQ(map.utf8ToUtf16(text.size()), ToUtf16(text).size());
    EXPECT_EQ(map.utf16ToUtf8(0), 0U);
    EXPECT_EQ(map.utf16ToUtf8(ToUtf16(text).size()), text.size());

    const auto emojiByteOffset = text.find("\xF0\x9F\x91\x8D");
    ASSERT_NE(emojiByteOffset, std::string::npos);
    const auto emojiUtf16Offset = map.utf8ToUtf16(emojiByteOffset);
    EXPECT_EQ(map.utf16ToUtf8(emojiUtf16Offset), emojiByteOffset);
}

TEST_F(InputBoxTest, BackspaceDeletesWholeRepresentativeClusters) {
    struct Case {
        std::string text;
        std::string expectedAfterBackspace;
    };

    const std::vector<Case> cases = {
            {"abc", "ab"},
            {"\xE6\x97\xA5\xE6\x9C\xAC", "\xE6\x97\xA5"},
            {"e\xCC\x81", ""},
            {"\xC3\xA9", ""},
            {"x\xF0\x9F\x91\x8D\xF0\x9F\x8F\xBD", "x"},
            {"x\xF0\x9F\x91\xA8\xE2\x80\x8D\xF0\x9F\x91\xA9\xE2\x80\x8D\xF0\x9F\x91\xA7", "x"},
    };

    for (const auto& entry: cases) {
        auto inputBox = MakeInputBox();
        inputBox->setText(entry.text.c_str(), entry.text.size());
        inputBox->setCursorUtf8(entry.text.size(), skia::textlayout::Affinity::kDownstream);

        ASSERT_TRUE(inputBox->deleteBackward()) << entry.text;
        EXPECT_EQ(inputBox->getText(), entry.expectedAfterBackspace);
        EXPECT_EQ(inputBox->getCursorUtf8(), entry.expectedAfterBackspace.size());
    }
}

TEST_F(InputBoxTest, CaretMovementSnapsToBoundaryMapForComplexScripts) {
    const std::string text =
            "A e\xCC\x81 \xE6\x97\xA5\xE6\x9C\xAC "
            "\xF0\x9F\x91\xA8\xE2\x80\x8D\xF0\x9F\x91\xA9\xE2\x80\x8D\xF0\x9F\x91\xA7 "
            "\xF0\x9F\x91\x8D\xF0\x9F\x8F\xBD "
            "\xE0\xB8\xAA\xE0\xB8\xA7\xE0\xB8\xB1\xE0\xB8\xAA\xE0\xB8\x94\xE0\xB8\xB5";

    auto inputBox = MakeInputBox();
    inputBox->setText(text.c_str(), text.size());
    inputBox->setCursorUtf8(0, skia::textlayout::Affinity::kDownstream);

    size_t previous = 0;
    int steps = 0;
    while (inputBox->moveNext()) {
        const auto cursor = inputBox->getCursorUtf8();
        EXPECT_GT(cursor, previous);
        EXPECT_EQ(cursor, inputBox->snapUtf8(cursor, milestro_text::TextBoundarySnapMode::Nearest));
        EXPECT_EQ(cursor, inputBox->snapUtf8(previous, milestro_text::TextBoundarySnapMode::Next));
        previous = cursor;
        ++steps;
    }

    EXPECT_EQ(previous, text.size());
    EXPECT_GT(steps, 1);
}

TEST_F(InputBoxTest, DeleteForwardUsesThaiBoundaryInsteadOfRawBytes) {
    const std::string thai = "\xE0\xB8\xAA\xE0\xB8\xA7\xE0\xB8\xB1\xE0\xB8\xAA\xE0\xB8\x94\xE0\xB8\xB5";
    auto inputBox = MakeInputBox();
    inputBox->setText(thai.c_str(), thai.size());
    inputBox->setCursorUtf8(0, skia::textlayout::Affinity::kDownstream);

    const auto firstBoundary = inputBox->snapUtf8(0, milestro_text::TextBoundarySnapMode::Next);
    ASSERT_GT(firstBoundary, 0U);
    ASSERT_LE(firstBoundary, thai.size());

    ASSERT_TRUE(inputBox->deleteForward());
    EXPECT_EQ(inputBox->getText().size(), thai.size() - firstBoundary);
    EXPECT_EQ(inputBox->getCursorUtf8(), 0U);
}

TEST_F(InputBoxTest, CaretMetricsHitTestAndHorizontalScrollAreNative) {
    const std::string text = "ASCII long horizontal input 1234567890";
    auto inputBox = MakeInputBox();
    inputBox->setViewport(80, 64);
    inputBox->setText(text.c_str(), text.size());
    inputBox->setCursorUtf8(text.size(), skia::textlayout::Affinity::kDownstream);

    const auto metrics = inputBox->getMetrics();
    const auto caret = inputBox->getCaretRect();
    EXPECT_GT(metrics.contentWidth, metrics.viewportWidth);
    EXPECT_GT(metrics.scrollX, 0);
    EXPECT_GT(caret.right, caret.left);
    EXPECT_GT(caret.bottom, caret.top);

    EXPECT_TRUE(inputBox->hitTest(0, 18));
    EXPECT_LT(inputBox->getCursorUtf8(), text.size());
    EXPECT_EQ(inputBox->getCursorUtf8(),
              inputBox->snapUtf8(inputBox->getCursorUtf8(), milestro_text::TextBoundarySnapMode::Nearest));
}
