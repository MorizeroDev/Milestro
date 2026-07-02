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

std::unique_ptr<milestro_text::InputBox> MakeInputBox(
        ::skia::textlayout::TextAlign textAlign = ::skia::textlayout::TextAlign::kLeft) {
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
    paragraphStyle->setTextAlign(textAlign);

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

TEST_F(InputBoxTest, EmptyTextCaretUsesTextMetricsInsteadOfViewportHeight) {
    auto inputBox = MakeInputBox();
    inputBox->setViewport(320, 96);
    inputBox->setText("", 0);

    const auto caret = inputBox->getCaretRect();
    EXPECT_GT(caret.right, caret.left);
    EXPECT_GT(caret.bottom, caret.top);
    EXPECT_LT(caret.bottom - caret.top, 96.0f);
}

TEST_F(InputBoxTest, MixedScriptInputKeepsSingleLineBaselineStable) {
    auto inputBox = MakeInputBox();
    inputBox->setViewport(320, 96);

    inputBox->setText("123", 3);
    milestro_text::InputBoxLineMetrics asciiMetrics;
    ASSERT_TRUE(inputBox->getLineMetrics(0, asciiMetrics));
    const auto asciiCaret = inputBox->getCaretRect();

    const std::string mixed = "123\xE4\xB8\xAD\xE6\x96\x87";
    inputBox->setText(mixed.c_str(), mixed.size());
    milestro_text::InputBoxLineMetrics mixedMetrics;
    ASSERT_TRUE(inputBox->getLineMetrics(0, mixedMetrics));
    const auto mixedCaret = inputBox->getCaretRect();

    EXPECT_NEAR(mixedMetrics.baseline, asciiMetrics.baseline, 0.5f);
    EXPECT_NEAR(mixedCaret.top, asciiCaret.top, 0.5f);
    EXPECT_NEAR(mixedCaret.bottom, asciiCaret.bottom, 0.5f);
}

TEST_F(InputBoxTest, CompositionIsTransientUntilCommit) {
    const std::string committed = "ab";
    const std::string composition = "\xE6\x97\xA5";
    auto inputBox = MakeInputBox();
    inputBox->setText(committed.c_str(), committed.size());
    inputBox->setCursorUtf8(1, skia::textlayout::Affinity::kDownstream);

    ASSERT_TRUE(inputBox->setComposition(composition.c_str(), composition.size()));
    EXPECT_EQ(inputBox->getText(), committed);
    EXPECT_EQ(inputBox->getCursorUtf8(), 1U);

    const auto compositionRect = inputBox->getCompositionRect();
    const auto caretRect = inputBox->getCaretRect();
    EXPECT_GT(compositionRect.right, compositionRect.left);
    EXPECT_GT(compositionRect.bottom, compositionRect.top);
    EXPECT_GE(caretRect.left, compositionRect.left);

    ASSERT_TRUE(inputBox->commitComposition(composition.c_str(), composition.size()));
    EXPECT_EQ(inputBox->getText(), "a" + composition + "b");
    EXPECT_EQ(inputBox->getCursorUtf8(), 1U + composition.size());
}

TEST_F(InputBoxTest, CompositionClearDoesNotMutateCommittedText) {
    const std::string committed = "ab";
    const std::string composition = "\xE3\x81\x8B";
    auto inputBox = MakeInputBox();
    inputBox->setText(committed.c_str(), committed.size());
    inputBox->setCursorUtf8(1, skia::textlayout::Affinity::kDownstream);

    ASSERT_TRUE(inputBox->setComposition(composition.c_str(), composition.size()));
    ASSERT_TRUE(inputBox->clearComposition());
    EXPECT_EQ(inputBox->getText(), committed);
    EXPECT_EQ(inputBox->getCursorUtf8(), 1U);
    EXPECT_FALSE(inputBox->clearComposition());
}

TEST_F(InputBoxTest, EmptyCommitUsesCurrentCompositionText) {
    const std::string composition = "\xE4\xBD\xA0";
    auto inputBox = MakeInputBox();
    inputBox->setText("", 0);

    ASSERT_TRUE(inputBox->setComposition(composition.c_str(), composition.size()));
    ASSERT_TRUE(inputBox->commitComposition(nullptr, 0));
    EXPECT_EQ(inputBox->getText(), composition);
    EXPECT_EQ(inputBox->getCursorUtf8(), composition.size());
}

TEST_F(InputBoxTest, SelectionReplacementUsesClusterBoundaries) {
    const std::string family =
            "\xF0\x9F\x91\xA8\xE2\x80\x8D\xF0\x9F\x91\xA9\xE2\x80\x8D\xF0\x9F\x91\xA7";
    const std::string text = "a" + family + "b";
    auto inputBox = MakeInputBox();
    inputBox->setText(text.c_str(), text.size());

    ASSERT_TRUE(inputBox->setSelectionUtf8(1,
                                           1 + family.size(),
                                           skia::textlayout::Affinity::kDownstream,
                                           skia::textlayout::Affinity::kDownstream));
    const auto selection = inputBox->getSelection();
    ASSERT_TRUE(selection.hasSelection);
    EXPECT_EQ(selection.startUtf8, 1U);
    EXPECT_EQ(selection.endUtf8, 1U + family.size());

    inputBox->insertText("X", 1);
    EXPECT_EQ(inputBox->getText(), "aXb");
    EXPECT_EQ(inputBox->getCursorUtf8(), 2U);
    EXPECT_FALSE(inputBox->hasSelection());
}

TEST_F(InputBoxTest, DeleteBackwardAndForwardRemoveSelectionFirst) {
    auto inputBox = MakeInputBox();
    inputBox->setText("abcd", 4);

    ASSERT_TRUE(inputBox->setSelectionUtf8(1,
                                           3,
                                           skia::textlayout::Affinity::kDownstream,
                                           skia::textlayout::Affinity::kDownstream));
    ASSERT_TRUE(inputBox->deleteBackward());
    EXPECT_EQ(inputBox->getText(), "ad");
    EXPECT_EQ(inputBox->getCursorUtf8(), 1U);
    EXPECT_FALSE(inputBox->hasSelection());

    inputBox->setText("abcd", 4);
    ASSERT_TRUE(inputBox->setSelectionUtf8(1,
                                           3,
                                           skia::textlayout::Affinity::kDownstream,
                                           skia::textlayout::Affinity::kDownstream));
    ASSERT_TRUE(inputBox->deleteForward());
    EXPECT_EQ(inputBox->getText(), "ad");
    EXPECT_EQ(inputBox->getCursorUtf8(), 1U);
    EXPECT_FALSE(inputBox->hasSelection());
}

TEST_F(InputBoxTest, MovementCanExtendAndCollapseSelection) {
    auto inputBox = MakeInputBox();
    inputBox->setText("abcd", 4);
    inputBox->setCursorUtf8(1, skia::textlayout::Affinity::kDownstream);

    ASSERT_TRUE(inputBox->moveNext(true));
    auto selection = inputBox->getSelection();
    ASSERT_TRUE(selection.hasSelection);
    EXPECT_EQ(selection.anchorUtf8, 1U);
    EXPECT_EQ(selection.focusUtf8, 2U);
    EXPECT_EQ(selection.startUtf8, 1U);
    EXPECT_EQ(selection.endUtf8, 2U);

    ASSERT_TRUE(inputBox->moveNext(false));
    selection = inputBox->getSelection();
    EXPECT_FALSE(selection.hasSelection);
    EXPECT_EQ(inputBox->getCursorUtf8(), 2U);

    ASSERT_TRUE(inputBox->movePrevious(true));
    selection = inputBox->getSelection();
    ASSERT_TRUE(selection.hasSelection);
    EXPECT_EQ(selection.startUtf8, 1U);
    EXPECT_EQ(selection.endUtf8, 2U);
}

TEST_F(InputBoxTest, SelectAllReplacesFullText) {
    const std::string text = "ab\xE6\x97\xA5";
    auto inputBox = MakeInputBox();
    inputBox->setText(text.c_str(), text.size());

    ASSERT_TRUE(inputBox->selectAll());
    auto selection = inputBox->getSelection();
    ASSERT_TRUE(selection.hasSelection);
    EXPECT_EQ(selection.startUtf8, 0U);
    EXPECT_EQ(selection.endUtf8, text.size());

    inputBox->insertText("x", 1);
    EXPECT_EQ(inputBox->getText(), "x");
    EXPECT_EQ(inputBox->getCursorUtf8(), 1U);
    EXPECT_FALSE(inputBox->hasSelection());
}

TEST_F(InputBoxTest, RightAlignedSelectionRectsUseParagraphGeometry) {
    auto inputBox = MakeInputBox(::skia::textlayout::TextAlign::kRight);
    inputBox->setViewport(320, 64);

    const auto emptyCaret = inputBox->getCaretRect();
    EXPECT_GT(emptyCaret.left, 300.0f);

    inputBox->setText("abc", 3);

    ASSERT_TRUE(inputBox->selectAll());
    const auto rects = inputBox->getSelectionRects();
    ASSERT_FALSE(rects.empty());
    EXPECT_GT(rects.front().left, 1.0f);

    ASSERT_TRUE(inputBox->hitTest(319, 18));
    EXPECT_GT(inputBox->getCursorUtf8(), 0U);
}

TEST_F(InputBoxTest, SelectionRectsUseUniformLineHeightAcrossFontRuns) {
    auto inputBox = MakeInputBox();
    const std::string text = "A\xF0\x9F\xA4\x94\xE6\x97\xA5" "B";
    inputBox->setText(text.c_str(), text.size());

    ASSERT_TRUE(inputBox->selectAll());
    const auto caret = inputBox->getCaretRect();
    const auto rects = inputBox->getSelectionRects();
    ASSERT_GE(rects.size(), 2U);

    for (const auto& rect: rects) {
        EXPECT_NEAR(rect.top, caret.top, 0.001f);
        EXPECT_NEAR(rect.bottom, caret.bottom, 0.001f);
        EXPECT_GT(rect.right, rect.left);
    }
}

TEST_F(InputBoxTest, SingleLineGeometryUsesStableBaselineInViewport) {
    auto inputBox = MakeInputBox();
    inputBox->setViewport(320, 96);
    inputBox->setText("abc", 3);

    const auto metrics = inputBox->getMetrics();
    const auto caret = inputBox->getCaretRect();
    milestro_text::InputBoxLineMetrics lineMetrics;
    ASSERT_TRUE(inputBox->getLineMetrics(0, lineMetrics));
    const auto baseline = caret.top + lineMetrics.ascent;
    ASSERT_GT(metrics.viewportHeight, metrics.height);
    EXPECT_GT(caret.top, 0.0f);
    EXPECT_LT(caret.bottom, metrics.viewportHeight);

    const std::string mixedText = "A\xF0\x9F\xA4\x94\xE6\x97\xA5" "B";
    inputBox->setText(mixedText.c_str(), mixedText.size());
    const auto mixedCaret = inputBox->getCaretRect();
    milestro_text::InputBoxLineMetrics mixedLineMetrics;
    ASSERT_TRUE(inputBox->getLineMetrics(0, mixedLineMetrics));
    EXPECT_NEAR(mixedCaret.top + mixedLineMetrics.ascent, baseline, 0.001f);

    ASSERT_TRUE(inputBox->selectAll());
    const auto rects = inputBox->getSelectionRects();
    ASSERT_FALSE(rects.empty());
    for (const auto& rect: rects) {
        EXPECT_NEAR(rect.top, mixedCaret.top, 0.001f);
        EXPECT_NEAR(rect.bottom, mixedCaret.bottom, 0.001f);
    }

    EXPECT_TRUE(inputBox->hitTest(1.0f, (mixedCaret.top + mixedCaret.bottom) * 0.5f));
}
