#include "Milestro/game/milestro_game_interface.h"
#include "Milestro/game/milestro_game_model.h"
#include "Milestro/game/milestro_game_retcode.h"
#include "Milestro/skia/Canvas.h"
#include "Milestro/skia/FontRegistry.h"
#include "Milestro/skia/textlayout/InputBox.h"
#include "Milestro/skia/textlayout/ParagraphStyle.h"
#include "Milestro/skia/textlayout/TextStyle.h"

#include "include/core/SkString.h"
#include "modules/skunicode/include/SkUnicode.h"

#include <algorithm>
#include <cmath>
#include <filesystem>
#include <fstream>
#include <gtest/gtest.h>
#include <memory>
#include <string>
#include <vector>

namespace fs = std::filesystem;
namespace milestro_text = milestro::skia::textlayout;

namespace {

struct PaintedInputBox {
    int width = 0;
    int height = 0;
    std::vector<uint8_t> pixels;
};

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

std::unique_ptr<milestro_text::InputBox>
MakeInputBox(::skia::textlayout::TextAlign textAlign = ::skia::textlayout::TextAlign::kLeft,
             bool useMonospace = false,
             bool unlimitedLines = false,
             ::skia::textlayout::TextDirection textDirection = ::skia::textlayout::TextDirection::kLtr) {
    auto textStyle = std::make_unique<milestro_text::TextStyle>();
    std::vector<SkString> fontFamilies;
    fontFamilies.emplace_back(useMonospace ? "Fira Code" : "Source Han Sans VF");
    fontFamilies.emplace_back("Noto Color Emoji");
    textStyle->setFontFamilies(fontFamilies);
    textStyle->setFontSize(36);
    textStyle->setLocale(SkString("en"));
    textStyle->setColor(SK_ColorWHITE);

    auto paragraphStyle = std::make_unique<milestro_text::ParagraphStyle>();
    paragraphStyle->setTextStyle(textStyle.get());
    if (unlimitedLines) {
        paragraphStyle->clearMaxLines();
    } else {
        paragraphStyle->setMaxLines(1);
    }
    paragraphStyle->setTextAlign(textAlign);
    paragraphStyle->setTextDirection(textDirection);

    auto inputBox = std::make_unique<milestro_text::InputBox>(paragraphStyle.get(), textStyle.get());
    inputBox->setViewport(320, 64);
    return inputBox;
}

std::string MixedNoWrapPiText() {
    return "\xF0\x9F\xA4\x94 \xF0\xB0\xBB\x9D \xF0\x9F\xAB\x84 \xF0\x9F\xA4\xB0 "
           "\xF0\x9F\xA7\x91\xE2\x80\x8D\xF0\x9F\xA7\x91\xE2\x80\x8D\xF0\x9F\xA7\x92 "
           "\xE6\xB5\x8B\xE8\xAF\x95 123 "
           "\xE5\x95\x8A\xE5\x90\xA7\xE6\xAC\xA1\xE7\x9A\x84\xE9\xA2\x9D\xE4\xBD\x9B\xE6\xAD\x8C "
           "3.14159265358979323846264338327950288";
}

std::u16string ToUtf16(const std::string& text) {
    return SkUnicode::convertUtf8ToUtf16(text.c_str(), static_cast<int>(text.size()));
}

PaintedInputBox PaintSnapshot(milestro_text::InputBox& inputBox,
                              int width = 320,
                              int height = 96,
                              float paintWidth = -1.0f,
                              float paintHeight = -1.0f,
                              float paintX = 0.0f,
                              float paintY = 0.0f,
                              float presentationOffsetX = 0.0f,
                              float presentationOffsetY = 0.0f) {
    PaintedInputBox result{
            width,
            height,
            std::vector<uint8_t>(static_cast<size_t>(width * height * 4), 0),
    };
    milestro::skia::Canvas canvas(width, height, result.pixels.data());
    auto snapshot = inputBox.createDrawSnapshot();
    snapshot->paint(canvas.unwrap(),
                    paintX,
                    paintY,
                    paintWidth > 0.0f ? paintWidth : static_cast<float>(width),
                    paintHeight > 0.0f ? paintHeight : static_cast<float>(height),
                    presentationOffsetX,
                    presentationOffsetY);
    return result;
}

size_t CountInkPixels(const PaintedInputBox& image, int left = 0, int right = -1) {
    if (right < 0) {
        right = image.width;
    }
    left = std::max(0, std::min(left, image.width));
    right = std::max(left, std::min(right, image.width));

    size_t count = 0;
    for (int y = 0; y < image.height; ++y) {
        for (int x = left; x < right; ++x) {
            const auto base = static_cast<size_t>((y * image.width + x) * 4);
            if (image.pixels[base] != 0 || image.pixels[base + 1] != 0 || image.pixels[base + 2] != 0 ||
                image.pixels[base + 3] != 0) {
                ++count;
            }
        }
    }
    return count;
}

void ExpectSingleLineInkInsideViewport(::skia::textlayout::TextAlign textAlign,
                                       ::skia::textlayout::TextDirection textDirection,
                                       float minLeft,
                                       float maxRight) {
    auto inputBox = MakeInputBox(textAlign, true, false, textDirection);
    inputBox->setViewport(320, 96);
    inputBox->setSoftWrap(false);
    inputBox->setText("abc", 3);

    milestro_text::InputBoxLineMetrics lineMetrics;
    ASSERT_TRUE(inputBox->getLineMetrics(0, lineMetrics));
    EXPECT_GE(lineMetrics.left, minLeft);
    EXPECT_LE(lineMetrics.left + lineMetrics.width, maxRight);

    const auto image = PaintSnapshot(*inputBox, 320, 96, 320.0f, 96.0f);
    EXPECT_GT(CountInkPixels(image, 0, 320), 0U);
}

bool ImagesEqual(const PaintedInputBox& left, const PaintedInputBox& right) {
    return left.width == right.width && left.height == right.height && left.pixels == right.pixels;
}

void ExpectImageTranslated(const PaintedInputBox& source, const PaintedInputBox& translated, int deltaX, int deltaY) {
    ASSERT_EQ(source.width, translated.width);
    ASSERT_EQ(source.height, translated.height);
    for (int y = 0; y < source.height; ++y) {
        for (int x = 0; x < source.width; ++x) {
            const int sourceX = x - deltaX;
            const int sourceY = y - deltaY;
            for (int channel = 0; channel < 4; ++channel) {
                const auto translatedIndex = static_cast<size_t>((y * source.width + x) * 4 + channel);
                const uint8_t expected =
                        sourceX >= 0 && sourceX < source.width && sourceY >= 0 && sourceY < source.height
                                ? source.pixels[static_cast<size_t>((sourceY * source.width + sourceX) * 4 + channel)]
                                : 0;
                ASSERT_EQ(translated.pixels[translatedIndex], expected)
                        << "pixel=" << x << "," << y << " channel=" << channel;
            }
        }
    }
}

size_t GraphemeClusterCount(const std::string& text) {
    milestro_text::TextBoundaryMap map(text);
    return map.boundaryCount() == 0 ? 0 : map.boundaryCount() - 1;
}

void ExpectWrappedMultiLineNoHorizontalScroll(const std::string& text) {
    auto inputBox = MakeInputBox();
    inputBox->setViewport(180, 64);
    inputBox->setText(text.c_str(), text.size());
    inputBox->setCursorUtf8(text.size(), skia::textlayout::Affinity::kDownstream);

    const auto metrics = inputBox->getMetrics();
    const auto caret = inputBox->getCaretRect();
    EXPECT_GT(inputBox->getLineCount(), 1U);
    EXPECT_FLOAT_EQ(metrics.contentWidth, metrics.viewportWidth);
    EXPECT_FLOAT_EQ(metrics.scrollX, 0.0f);
    EXPECT_GT(metrics.scrollY, 0.0f);
    EXPECT_GE(caret.top - metrics.scrollY, -0.001f);
    EXPECT_LE(caret.bottom - metrics.scrollY, metrics.viewportHeight + 0.001f);

    milestro_text::InputBoxLineMetrics secondLineMetrics;
    EXPECT_TRUE(inputBox->getLineMetrics(1, secondLineMetrics));
}

void ExpectNoSelectionRectOverlap(const std::vector<milestro_text::InputBoxCaretRect>& rects) {
    constexpr float epsilon = 0.001f;
    for (size_t i = 0; i < rects.size(); ++i) {
        for (size_t j = i + 1; j < rects.size(); ++j) {
            const auto overlapWidth = std::min(rects[i].right, rects[j].right) - std::max(rects[i].left, rects[j].left);
            const auto overlapHeight =
                    std::min(rects[i].bottom, rects[j].bottom) - std::max(rects[i].top, rects[j].top);
            EXPECT_FALSE(overlapWidth > epsilon && overlapHeight > epsilon)
                    << "rect " << i << " overlaps rect " << j << " width=" << overlapWidth
                    << " height=" << overlapHeight;
        }
    }
}

} // namespace

class InputBoxTest : public ::testing::Test {
protected:
    static void SetUpTestSuite() {
        std::ifstream icudtl(MILESTRO_TEST_ICUDTL_PATH, std::ios::binary);
        ASSERT_TRUE(icudtl.is_open()) << MILESTRO_TEST_ICUDTL_PATH;
        std::vector<uint8_t> data((std::istreambuf_iterator<char>(icudtl)), std::istreambuf_iterator<char>());
        ASSERT_FALSE(data.empty());
        ASSERT_EQ(MilestroCopyAndLoadICU(data.data(), data.size(), nullptr), MILESTRO_API_RET_OK);
    }

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
    const std::string text = "A e\xCC\x81 \xE6\x97\xA5\xE6\x9C\xAC "
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

TEST_F(InputBoxTest, CaretMetricsHitTestAndVerticalScrollAreNative) {
    const std::string text = "ASCII long horizontal input 1234567890";
    auto inputBox = MakeInputBox();
    inputBox->setViewport(80, 64);
    inputBox->setText(text.c_str(), text.size());
    inputBox->setCursorUtf8(text.size(), skia::textlayout::Affinity::kDownstream);

    const auto metrics = inputBox->getMetrics();
    const auto caret = inputBox->getCaretRect();
    EXPECT_GT(inputBox->getLineCount(), 1U);
    EXPECT_FLOAT_EQ(metrics.contentWidth, metrics.viewportWidth);
    EXPECT_FLOAT_EQ(metrics.scrollX, 0.0f);
    EXPECT_GT(metrics.scrollY, 0.0f);
    EXPECT_GT(caret.right, caret.left);
    EXPECT_GT(caret.bottom, caret.top);
    EXPECT_GE(caret.top - metrics.scrollY, -0.001f);
    EXPECT_LE(caret.bottom - metrics.scrollY, metrics.viewportHeight + 0.001f);

    EXPECT_TRUE(inputBox->hitTest(0, 18));
    EXPECT_LT(inputBox->getCursorUtf8(), text.size());
    EXPECT_EQ(inputBox->getCursorUtf8(),
              inputBox->snapUtf8(inputBox->getCursorUtf8(), milestro_text::TextBoundarySnapMode::Nearest));
}

TEST_F(InputBoxTest, CompositionEnsureVisibleUsesScrolledVerticalViewport) {
    const std::string text = "line one\nline two\nline three\nline four\nline five";
    const std::string composition = "\xE6\x97\xA5\xE6\x9C\xAC";
    auto inputBox = MakeInputBox();
    inputBox->setViewport(160, 72);
    inputBox->setText(text.c_str(), text.size());
    inputBox->setCursorUtf8(text.size(), skia::textlayout::Affinity::kDownstream);

    ASSERT_TRUE(inputBox->setComposition(composition.c_str(), composition.size()));
    const auto metrics = inputBox->getMetrics();
    const auto compositionRect = inputBox->getCompositionRect();
    EXPECT_FLOAT_EQ(metrics.scrollX, 0.0f);
    EXPECT_GT(metrics.scrollY, 0.0f);
    EXPECT_GE(compositionRect.top - metrics.scrollY, -0.001f);
    EXPECT_LE(compositionRect.bottom - metrics.scrollY, metrics.viewportHeight + 0.001f);
}

TEST_F(InputBoxTest, ManualVerticalScrollClampsAndLeavesEnsureVisibleIntact) {
    const std::string text = "line one\nline two\nline three\nline four\nline five";
    auto inputBox = MakeInputBox();
    inputBox->setViewport(160, 72);
    inputBox->setText(text.c_str(), text.size());
    inputBox->setCursorUtf8(text.size(), skia::textlayout::Affinity::kDownstream);

    const auto initialMetrics = inputBox->getMetrics();
    ASSERT_GT(initialMetrics.height, initialMetrics.viewportHeight);
    ASSERT_GT(initialMetrics.scrollY, 0.0f);
    const auto maxScrollY = std::ceil(initialMetrics.height - initialMetrics.viewportHeight);

    EXPECT_TRUE(inputBox->scrollByY(-100000.0f));
    EXPECT_FLOAT_EQ(inputBox->getMetrics().scrollY, 0.0f);
    EXPECT_FALSE(inputBox->scrollByY(-1.0f));

    EXPECT_TRUE(inputBox->scrollByY(maxScrollY * 2.0f));
    EXPECT_FLOAT_EQ(inputBox->getMetrics().scrollY, maxScrollY);
    EXPECT_FALSE(inputBox->scrollByY(1.0f));

    EXPECT_TRUE(inputBox->scrollByY(-100000.0f));
    EXPECT_FLOAT_EQ(inputBox->getMetrics().scrollY, 0.0f);
    inputBox->ensureCaretVisible();
    EXPECT_GT(inputBox->getMetrics().scrollY, 0.0f);
}

TEST_F(InputBoxTest, ManualVerticalScrollDoesNotConsumeWhenContentFits) {
    auto inputBox = MakeInputBox();
    inputBox->setViewport(320, 64);
    inputBox->setText("short", 5);

    const auto metrics = inputBox->getMetrics();
    ASSERT_FLOAT_EQ(metrics.scrollY, 0.0f);
    ASSERT_LE(metrics.height, metrics.viewportHeight);
    EXPECT_FALSE(inputBox->scrollByY(48.0f));
    EXPECT_FLOAT_EQ(inputBox->getMetrics().scrollY, 0.0f);
}

TEST_F(InputBoxTest, MixedEmojiCjkAndLongNumberWrapWithoutHorizontalScroll) {
    const std::string firstRuntimeCase = "\xF0\x9F\xA4\x94 \xF0\xB0\xBB\x9D \xF0\x9F\xAB\x84 \xF0\x9F\xA4\xB0 "
                                         "\xF0\x9F\xA7\x91\xE2\x80\x8D\xF0\x9F\xA7\x91\xE2\x80\x8D\xF0\x9F\xA7\x92 "
                                         "\xE6\xB5\x8B\xE8\xAF\x95 123 "
                                         "3.14159265358979323846264338327950288419716939937510";
    const std::string secondRuntimeCase =
            "\xF0\x9F\xA4\x94 \xF0\xB0\xBB\x9D \xF0\x9F\xAB\x84 \xF0\x9F\xA4\xB0 "
            "\xF0\x9F\xA7\x91\xE2\x80\x8D\xF0\x9F\xA7\x91\xE2\x80\x8D\xF0\x9F\xA7\x92 "
            "\xE6\xB5\x8B\xE8\xAF\x95 123 "
            "\xE5\x95\x8A\xE5\x90\xA7\xE6\xAC\xA1\xE7\x9A\x84\xE9\xA2\x9D\xE4\xBD\x9B\xE6\xAD\x8C "
            "3.14159265358979323846264338327950288";

    ExpectWrappedMultiLineNoHorizontalScroll(firstRuntimeCase);
    ExpectWrappedMultiLineNoHorizontalScroll(secondRuntimeCase);
}

TEST_F(InputBoxTest, WrapModeSoftWrapsLongTextWithoutHorizontalScroll) {
    const std::string text = "soft wrap keeps this long text inside the viewport 12345678901234567890";
    auto inputBox = MakeInputBox(::skia::textlayout::TextAlign::kLeft, true);
    inputBox->setViewport(96, 72);
    inputBox->setSoftWrap(true);
    inputBox->setText(text.c_str(), text.size());
    inputBox->setCursorUtf8(text.size(), skia::textlayout::Affinity::kDownstream);

    const auto metrics = inputBox->getMetrics();
    EXPECT_GT(inputBox->getLineCount(), 1U);
    EXPECT_FLOAT_EQ(metrics.contentWidth, metrics.viewportWidth);
    EXPECT_FLOAT_EQ(metrics.scrollX, 0.0f);
    EXPECT_FALSE(inputBox->scrollByX(48.0f));
    EXPECT_FLOAT_EQ(inputBox->getMetrics().scrollX, 0.0f);
}

TEST_F(InputBoxTest, NoWrapModeKeepsLongNumberOnOneLineAndScrollsHorizontally) {
    const std::string text = "12345678901234567890123456789012345678901234567890";
    auto inputBox = MakeInputBox(::skia::textlayout::TextAlign::kLeft, true);
    inputBox->setViewport(96, 72);
    inputBox->setSoftWrap(false);
    inputBox->setText(text.c_str(), text.size());
    inputBox->setCursorUtf8(text.size(), skia::textlayout::Affinity::kDownstream);

    const auto metrics = inputBox->getMetrics();
    const auto caret = inputBox->getCaretRect();
    EXPECT_EQ(inputBox->getLineCount(), 1U);
    EXPECT_GT(metrics.contentWidth, metrics.viewportWidth);
    EXPECT_GT(metrics.scrollX, 0.0f);
    EXPECT_LE(caret.right - metrics.scrollX, metrics.viewportWidth + 0.001f);
    EXPECT_GE(caret.left - metrics.scrollX, -0.001f);

    EXPECT_TRUE(inputBox->scrollByX(-metrics.scrollX));
    EXPECT_FLOAT_EQ(inputBox->getMetrics().scrollX, 0.0f);
}

TEST_F(InputBoxTest, NoWrapSingleLineCenterAndEndAlignTextInsideViewport) {
    ExpectSingleLineInkInsideViewport(::skia::textlayout::TextAlign::kCenter,
                                      ::skia::textlayout::TextDirection::kLtr,
                                      40.0f,
                                      280.0f);
    ExpectSingleLineInkInsideViewport(::skia::textlayout::TextAlign::kRight,
                                      ::skia::textlayout::TextDirection::kLtr,
                                      120.0f,
                                      320.0f);
    ExpectSingleLineInkInsideViewport(::skia::textlayout::TextAlign::kEnd,
                                      ::skia::textlayout::TextDirection::kLtr,
                                      120.0f,
                                      320.0f);
    ExpectSingleLineInkInsideViewport(::skia::textlayout::TextAlign::kEnd,
                                      ::skia::textlayout::TextDirection::kRtl,
                                      0.0f,
                                      200.0f);
}

TEST_F(InputBoxTest, SharedNoWrapMeasurementKeepsMixedPiLineOnOneLine) {
    const auto text = MixedNoWrapPiText();
    auto inputBox = MakeInputBox(::skia::textlayout::TextAlign::kLeft, true, true);
    inputBox->setViewport(320, 160);
    inputBox->setSoftWrap(false);
    inputBox->setText(text.c_str(), text.size());
    inputBox->setCursorUtf8(text.size(), skia::textlayout::Affinity::kDownstream);

    const auto metrics = inputBox->getMetrics();
    ASSERT_EQ(inputBox->getLineCount(), 1U);
    EXPECT_GT(metrics.contentWidth, metrics.viewportWidth);
    EXPECT_GT(metrics.scrollX, 0.0f);

    milestro_text::InputBoxLineMetrics lineMetrics;
    ASSERT_TRUE(inputBox->getLineMetrics(0, lineMetrics));
    EXPECT_EQ(lineMetrics.startUtf8, 0U);
    EXPECT_EQ(lineMetrics.endUtf8, text.size());
}

TEST_F(InputBoxTest, FocusedEllipsisUsesNoWrapEditScrollForMixedPiLine) {
    const auto text = MixedNoWrapPiText();
    auto inputBox = MakeInputBox(::skia::textlayout::TextAlign::kLeft, true, false);
    inputBox->setViewport(160, 96);
    inputBox->setSoftWrap(false);
    inputBox->setTextOverflow(milestro_text::TextOverflow::Ellipsis);
    inputBox->setFocused(true);
    inputBox->setText(text.c_str(), text.size());
    inputBox->setCursorUtf8(text.size(), skia::textlayout::Affinity::kDownstream);

    const auto metrics = inputBox->getMetrics();
    const auto caret = inputBox->getCaretRect();
    ASSERT_EQ(inputBox->getLineCount(), 1U);
    EXPECT_GT(metrics.contentWidth, metrics.viewportWidth);
    EXPECT_GT(metrics.scrollX, 0.0f);
    EXPECT_LE(caret.right - metrics.scrollX, metrics.viewportWidth + 0.001f);
    EXPECT_EQ(inputBox->getText(), text);

    milestro_text::InputBoxLineMetrics lineMetrics;
    ASSERT_TRUE(inputBox->getLineMetrics(0, lineMetrics));
    EXPECT_EQ(lineMetrics.startUtf8, 0U);
    EXPECT_EQ(lineMetrics.endUtf8, text.size());
}

TEST_F(InputBoxTest, UnfocusedEllipsisIsDisplayOnlyAndDoesNotMutateEditingState) {
    const auto text = MixedNoWrapPiText();
    auto focusedInputBox = MakeInputBox(::skia::textlayout::TextAlign::kLeft, true, false);
    focusedInputBox->setViewport(160, 96);
    focusedInputBox->setSoftWrap(false);
    focusedInputBox->setTextOverflow(milestro_text::TextOverflow::Ellipsis);
    focusedInputBox->setText(text.c_str(), text.size());
    focusedInputBox->setCursorUtf8(text.size(), skia::textlayout::Affinity::kDownstream);

    auto unfocusedInputBox = MakeInputBox(::skia::textlayout::TextAlign::kLeft, true, false);
    unfocusedInputBox->setViewport(160, 96);
    unfocusedInputBox->setSoftWrap(false);
    unfocusedInputBox->setTextOverflow(milestro_text::TextOverflow::Ellipsis);
    unfocusedInputBox->setText(text.c_str(), text.size());
    unfocusedInputBox->setCursorUtf8(text.size(), skia::textlayout::Affinity::kDownstream);
    ASSERT_TRUE(unfocusedInputBox->setSelectionUtf8(0,
                                                    text.size(),
                                                    skia::textlayout::Affinity::kDownstream,
                                                    skia::textlayout::Affinity::kDownstream));
    unfocusedInputBox->setFocused(false);

    const auto beforeSelection = unfocusedInputBox->getSelection();
    const auto beforeScrollX = unfocusedInputBox->getMetrics().scrollX;
    ASSERT_GT(beforeScrollX, 0.0f);

    const auto focusedPaint = PaintSnapshot(*focusedInputBox, 260, 96, 160.0f, 96.0f);
    const auto unfocusedPaint = PaintSnapshot(*unfocusedInputBox, 260, 96, 160.0f, 96.0f);
    EXPECT_FALSE(ImagesEqual(focusedPaint, unfocusedPaint));
    EXPECT_EQ(CountInkPixels(unfocusedPaint, 160, 260), 0U);

    EXPECT_EQ(unfocusedInputBox->getText(), text);
    EXPECT_EQ(unfocusedInputBox->getCursorUtf8(), text.size());
    const auto afterSelection = unfocusedInputBox->getSelection();
    EXPECT_EQ(afterSelection.anchorUtf8, beforeSelection.anchorUtf8);
    EXPECT_EQ(afterSelection.focusUtf8, beforeSelection.focusUtf8);
    EXPECT_EQ(afterSelection.startUtf8, beforeSelection.startUtf8);
    EXPECT_EQ(afterSelection.endUtf8, beforeSelection.endUtf8);
    EXPECT_EQ(unfocusedInputBox->getMetrics().scrollX, beforeScrollX);
}

TEST_F(InputBoxTest, UnfocusedOverflowCanPaintPastSingleLineViewport) {
    const std::string text = "123456789012345678901234567890";
    auto inputBox = MakeInputBox(::skia::textlayout::TextAlign::kLeft, true, false);
    inputBox->setViewport(96, 72);
    inputBox->setSoftWrap(false);
    inputBox->setTextOverflow(milestro_text::TextOverflow::Overflow);
    inputBox->setText(text.c_str(), text.size());
    inputBox->setCursorUtf8(0, skia::textlayout::Affinity::kDownstream);
    inputBox->setFocused(false);

    const auto image = PaintSnapshot(*inputBox, 240, 72, 96.0f, 72.0f);
    EXPECT_GT(CountInkPixels(image, 96, 240), 0U);
}

TEST_F(InputBoxTest, MultilineIgnoresEllipsisDisplayMode) {
    const std::string text = "first long line wraps in multiline mode\nsecond line";
    auto clipInputBox = MakeInputBox(::skia::textlayout::TextAlign::kLeft, true, true);
    clipInputBox->setViewport(160, 120);
    clipInputBox->setSoftWrap(true);
    clipInputBox->setTextOverflow(milestro_text::TextOverflow::Clip);
    clipInputBox->setText(text.c_str(), text.size());
    clipInputBox->setFocused(false);

    auto ellipsisInputBox = MakeInputBox(::skia::textlayout::TextAlign::kLeft, true, true);
    ellipsisInputBox->setViewport(160, 120);
    ellipsisInputBox->setSoftWrap(true);
    ellipsisInputBox->setTextOverflow(milestro_text::TextOverflow::Ellipsis);
    ellipsisInputBox->setText(text.c_str(), text.size());
    ellipsisInputBox->setFocused(false);

    EXPECT_GT(ellipsisInputBox->getLineCount(), 1U);
    EXPECT_TRUE(ImagesEqual(PaintSnapshot(*clipInputBox, 160, 120), PaintSnapshot(*ellipsisInputBox, 160, 120)));
}

TEST_F(InputBoxTest, CompositionSuppressesUnfocusedEllipsisDisplay) {
    const std::string text = "123456789012345678901234567890";
    const std::string composition = "\xE6\x97\xA5";
    auto withoutComposition = MakeInputBox(::skia::textlayout::TextAlign::kLeft, true, false);
    withoutComposition->setViewport(96, 72);
    withoutComposition->setSoftWrap(false);
    withoutComposition->setTextOverflow(milestro_text::TextOverflow::Ellipsis);
    withoutComposition->setText(text.c_str(), text.size());
    withoutComposition->setFocused(false);

    auto focusedWithComposition = MakeInputBox(::skia::textlayout::TextAlign::kLeft, true, false);
    focusedWithComposition->setViewport(96, 72);
    focusedWithComposition->setSoftWrap(false);
    focusedWithComposition->setTextOverflow(milestro_text::TextOverflow::Ellipsis);
    focusedWithComposition->setText(text.c_str(), text.size());
    focusedWithComposition->setCursorUtf8(text.size(), skia::textlayout::Affinity::kDownstream);
    ASSERT_TRUE(focusedWithComposition->setComposition(composition.c_str(), composition.size()));

    auto withComposition = MakeInputBox(::skia::textlayout::TextAlign::kLeft, true, false);
    withComposition->setViewport(96, 72);
    withComposition->setSoftWrap(false);
    withComposition->setTextOverflow(milestro_text::TextOverflow::Ellipsis);
    withComposition->setText(text.c_str(), text.size());
    withComposition->setCursorUtf8(text.size(), skia::textlayout::Affinity::kDownstream);
    ASSERT_TRUE(withComposition->setComposition(composition.c_str(), composition.size()));
    withComposition->setFocused(false);

    const auto withoutCompositionPaint = PaintSnapshot(*withoutComposition, 180, 72, 96.0f, 72.0f);
    const auto focusedWithCompositionPaint = PaintSnapshot(*focusedWithComposition, 180, 72, 96.0f, 72.0f);
    const auto withCompositionPaint = PaintSnapshot(*withComposition, 180, 72, 96.0f, 72.0f);
    EXPECT_FALSE(ImagesEqual(withoutCompositionPaint, withCompositionPaint));
    EXPECT_TRUE(ImagesEqual(focusedWithCompositionPaint, withCompositionPaint));
    EXPECT_EQ(CountInkPixels(withCompositionPaint, 96, 180), 0U);
    EXPECT_EQ(withComposition->getText(), text);
    EXPECT_TRUE(withComposition->hasComposition());
}

TEST_F(InputBoxTest, PresentationOffsetMovesTextSelectionAndCaretTogetherWithoutChangingMetrics) {
    auto inputBox = MakeInputBox(::skia::textlayout::TextAlign::kLeft, true, false);
    inputBox->setViewport(200, 80);
    inputBox->setText("abc", 3);
    inputBox->setFocused(true);
    inputBox->setCaretVisible(true);
    ASSERT_TRUE(inputBox->selectAll());
    const auto metricsBefore = inputBox->getMetrics();
    const auto selectionBefore = inputBox->getSelection();

    const auto baseline = PaintSnapshot(*inputBox, 320, 120, 200.0f, 80.0f, 20.0f, 10.0f);
    const auto translated = PaintSnapshot(*inputBox, 320, 120, 200.0f, 80.0f, 20.0f, 10.0f, 7.0f, 5.0f);

    ASSERT_GT(CountInkPixels(baseline), 0U);
    ExpectImageTranslated(baseline, translated, 7, 5);
    const auto metricsAfter = inputBox->getMetrics();
    const auto selectionAfter = inputBox->getSelection();
    EXPECT_FLOAT_EQ(metricsAfter.scrollX, metricsBefore.scrollX);
    EXPECT_FLOAT_EQ(metricsAfter.scrollY, metricsBefore.scrollY);
    EXPECT_EQ(selectionAfter.anchorUtf8, selectionBefore.anchorUtf8);
    EXPECT_EQ(selectionAfter.focusUtf8, selectionBefore.focusUtf8);
}

TEST_F(InputBoxTest, PresentationOffsetMovesCompositionUnderlineWithSnapshot) {
    const std::string composition = "xy";
    auto inputBox = MakeInputBox(::skia::textlayout::TextAlign::kLeft, true, false);
    inputBox->setViewport(200, 80);
    inputBox->setText("abc", 3);
    inputBox->setCursorUtf8(3, skia::textlayout::Affinity::kDownstream);
    inputBox->setFocused(true);
    inputBox->setCaretVisible(true);
    ASSERT_TRUE(inputBox->setComposition(composition.c_str(), composition.size()));

    const auto baseline = PaintSnapshot(*inputBox, 320, 120, 200.0f, 80.0f, 20.0f, 10.0f);
    const auto translated = PaintSnapshot(*inputBox, 320, 120, 200.0f, 80.0f, 20.0f, 10.0f, 7.0f, 5.0f);

    ASSERT_GT(CountInkPixels(baseline), 0U);
    ExpectImageTranslated(baseline, translated, 7, 5);
    EXPECT_TRUE(inputBox->hasComposition());
    EXPECT_EQ(inputBox->getText(), "abc");
}

TEST_F(InputBoxTest, NoWrapModeOnlyBreaksAtHardNewlines) {
    const std::string firstLine = "123456789012345678901234567890";
    const std::string secondLine = "abc";
    const std::string text = firstLine + "\n" + secondLine;
    auto inputBox = MakeInputBox(::skia::textlayout::TextAlign::kLeft, true);
    inputBox->setViewport(96, 160);
    inputBox->setSoftWrap(false);
    inputBox->setText(text.c_str(), text.size());

    ASSERT_EQ(inputBox->getLineCount(), 2U);
    milestro_text::InputBoxLineMetrics firstMetrics;
    milestro_text::InputBoxLineMetrics secondMetrics;
    ASSERT_TRUE(inputBox->getLineMetrics(0, firstMetrics));
    ASSERT_TRUE(inputBox->getLineMetrics(1, secondMetrics));
    EXPECT_EQ(firstMetrics.startUtf8, 0U);
    EXPECT_EQ(firstMetrics.endUtf8, firstLine.size());
    EXPECT_EQ(secondMetrics.startUtf8, firstLine.size() + 1U);
    EXPECT_EQ(secondMetrics.endUtf8, text.size());
    EXPECT_GT(inputBox->getMetrics().contentWidth, inputBox->getMetrics().viewportWidth);
}

TEST_F(InputBoxTest, NoWrapModeDoesNotSoftBreakMixedLineAfterTrailingNewlineInsert) {
    const auto text = MixedNoWrapPiText();
    auto inputBox = MakeInputBox(::skia::textlayout::TextAlign::kLeft, true, true);
    inputBox->setViewport(320, 160);
    inputBox->setSoftWrap(false);
    inputBox->setText(text.c_str(), text.size());
    inputBox->setCursorUtf8(text.size(), skia::textlayout::Affinity::kDownstream);
    ASSERT_GT(inputBox->getMetrics().scrollX, 0.0f);

    inputBox->insertText("\n", 1);

    EXPECT_EQ(inputBox->getText(), text + "\n");
    EXPECT_EQ(inputBox->getCursorUtf8(), text.size() + 1U);
    ASSERT_GE(inputBox->getLineCount(), 1U);
    milestro_text::InputBoxLineMetrics firstMetrics;
    ASSERT_TRUE(inputBox->getLineMetrics(0, firstMetrics));
    EXPECT_EQ(firstMetrics.startUtf8, 0U);
    EXPECT_EQ(firstMetrics.endUtf8, text.size());
    EXPECT_LE(inputBox->getLineCount(), 2U);
}

TEST_F(InputBoxTest, LineEndUsesUtf8OffsetForMixedNoWrapLine) {
    const auto text = MixedNoWrapPiText();
    auto inputBox = MakeInputBox(::skia::textlayout::TextAlign::kLeft, true, true);
    inputBox->setViewport(320, 160);
    inputBox->setSoftWrap(false);
    inputBox->setText(text.c_str(), text.size());
    inputBox->setCursorUtf8(0, skia::textlayout::Affinity::kDownstream);

    ASSERT_TRUE(inputBox->moveLineEnd());
    EXPECT_EQ(inputBox->getCursorUtf8(), text.size());

    inputBox->insertText("\n", 1);
    EXPECT_EQ(inputBox->getText(), text + "\n");
}

TEST_F(InputBoxTest, FixedMarginsDoNotVerticallyCenterSingleVisualLine) {
    auto singleLineInputBox = MakeInputBox(::skia::textlayout::TextAlign::kLeft, true, false);
    singleLineInputBox->setViewport(320, 160);
    singleLineInputBox->setText("abc", 3);

    auto multiLineInputBox = MakeInputBox(::skia::textlayout::TextAlign::kLeft, true, true);
    multiLineInputBox->setViewport(320, 160);
    multiLineInputBox->setText("abc", 3);

    const auto singleLineCaret = singleLineInputBox->getCaretRect();
    const auto multiLineCaret = multiLineInputBox->getCaretRect();
    EXPECT_NEAR(singleLineCaret.top, 0.0f, 1.0f);
    EXPECT_NEAR(multiLineCaret.top, 0.0f, 1.0f);
}

TEST_F(InputBoxTest, AutoVerticalMarginsMoveContentWithinViewport) {
    auto inputBox = MakeInputBox(::skia::textlayout::TextAlign::kLeft, true, true);
    inputBox->setViewport(320, 160);
    inputBox->setText("abc", 3);

    inputBox->setAutoMargin(false, false, false, false);
    const auto startCaret = inputBox->getCaretRect();
    EXPECT_NEAR(startCaret.top, 0.0f, 1.0f);

    inputBox->setAutoMargin(false, true, false, true);
    const auto centerCaret = inputBox->getCaretRect();
    EXPECT_GT(centerCaret.top, startCaret.top + 20.0f);
    EXPECT_LT(centerCaret.bottom, 160.0f);

    inputBox->setAutoMargin(false, true, false, false);
    const auto endCaret = inputBox->getCaretRect();
    EXPECT_GT(endCaret.top, centerCaret.top + 20.0f);
    EXPECT_LE(endCaret.bottom, 160.0f);
}

TEST_F(InputBoxTest, AutoHorizontalMarginsMoveContentWithinViewport) {
    auto inputBox = MakeInputBox(::skia::textlayout::TextAlign::kLeft, true, true);
    inputBox->setViewport(320, 96);
    inputBox->setSoftWrap(false);
    inputBox->setText("abc", 3);
    inputBox->setCursorUtf8(0, skia::textlayout::Affinity::kDownstream);

    inputBox->setAutoMargin(false, false, false, false);
    const auto startCaret = inputBox->getCaretRect();
    EXPECT_NEAR(startCaret.left, 0.0f, 1.0f);

    inputBox->setAutoMargin(true, false, true, false);
    const auto centerCaret = inputBox->getCaretRect();
    EXPECT_GT(centerCaret.left, startCaret.left + 40.0f);
    EXPECT_LT(centerCaret.right, 320.0f);

    inputBox->setAutoMargin(true, false, false, false);
    const auto endCaret = inputBox->getCaretRect();
    EXPECT_GT(endCaret.left, centerCaret.left + 40.0f);
    EXPECT_LE(endCaret.right, 320.0f);
}

TEST_F(InputBoxTest, MaskInputDoesNotMutateTextSelectionOrEditingOffsets) {
    const std::string text = "ab\xE6\x97\xA5\xE6\x9C\xAC\xF0\x9F\x91\x8D";
    auto inputBox = MakeInputBox(::skia::textlayout::TextAlign::kLeft, false, true);
    inputBox->setViewport(320, 160);
    inputBox->setText(text.c_str(), text.size());

    inputBox->setMaskInput(true);
    EXPECT_TRUE(inputBox->getMaskInput());
    EXPECT_EQ(inputBox->getText(), text);

    const auto selectionStart = text.find("\xE6\x97\xA5");
    ASSERT_NE(selectionStart, std::string::npos);
    ASSERT_TRUE(inputBox->setSelectionUtf8(selectionStart,
                                           text.size(),
                                           skia::textlayout::Affinity::kDownstream,
                                           skia::textlayout::Affinity::kDownstream));
    auto selection = inputBox->getSelection();
    ASSERT_TRUE(selection.hasSelection);
    EXPECT_EQ(selection.startUtf8, selectionStart);
    EXPECT_EQ(selection.endUtf8, text.size());

    inputBox->insertText("X", 1);
    EXPECT_EQ(inputBox->getText(), text.substr(0, selectionStart) + "X");
    EXPECT_EQ(inputBox->getCursorUtf8(), selectionStart + 1U);
}

TEST_F(InputBoxTest, MaskInputRendersOneMaskClusterPerVisibleTextCluster) {
    const std::string text = "ab"
                             "\xE6\x97\xA5\xE6\x9C\xAC"
                             "e\xCC\x81"
                             "\xF0\x9F\x91\x8D\xF0\x9F\x8F\xBD"
                             "\xF0\x9F\x91\xA8\xE2\x80\x8D\xF0\x9F\x91\xA9\xE2\x80\x8D\xF0\x9F\x91\xA7";
    const auto clusterCount = GraphemeClusterCount(text);
    ASSERT_GT(clusterCount, 1U);

    auto maskedInputBox = MakeInputBox(::skia::textlayout::TextAlign::kLeft, true, true);
    maskedInputBox->setViewport(520, 120);
    maskedInputBox->setSoftWrap(false);
    maskedInputBox->setText(text.c_str(), text.size());
    maskedInputBox->setMaskInput(true);
    maskedInputBox->setMaskChar("#", 1);

    std::string expectedMask(clusterCount, '#');
    auto expectedInputBox = MakeInputBox(::skia::textlayout::TextAlign::kLeft, true, true);
    expectedInputBox->setViewport(520, 120);
    expectedInputBox->setSoftWrap(false);
    expectedInputBox->setText(expectedMask.c_str(), expectedMask.size());

    EXPECT_TRUE(ImagesEqual(PaintSnapshot(*maskedInputBox, 520, 120), PaintSnapshot(*expectedInputBox, 520, 120)));
    EXPECT_EQ(maskedInputBox->getText(), text);
    EXPECT_EQ(maskedInputBox->getCursorUtf8(), 0U);

    maskedInputBox->setCursorUtf8(text.size(), skia::textlayout::Affinity::kDownstream);
    ASSERT_TRUE(maskedInputBox->deleteBackward());
    EXPECT_EQ(maskedInputBox->getText(),
              text.substr(0,
                          text.size() - std::string("\xF0\x9F\x91\xA8\xE2\x80\x8D"
                                                    "\xF0\x9F\x91\xA9\xE2\x80\x8D"
                                                    "\xF0\x9F\x91\xA7")
                                                .size()));
}

TEST_F(InputBoxTest, MaskCharUsesFirstVisibleClusterAndFallbackForEmptyInput) {
    auto inputBox = MakeInputBox(::skia::textlayout::TextAlign::kLeft, true, true);
    inputBox->setViewport(240, 96);
    inputBox->setSoftWrap(false);
    inputBox->setText("abc", 3);
    inputBox->setMaskInput(true);
    inputBox->setMaskChar("XY", 2);

    auto expectedX = MakeInputBox(::skia::textlayout::TextAlign::kLeft, true, true);
    expectedX->setViewport(240, 96);
    expectedX->setSoftWrap(false);
    expectedX->setText("XXX", 3);
    EXPECT_TRUE(ImagesEqual(PaintSnapshot(*inputBox, 240, 96), PaintSnapshot(*expectedX, 240, 96)));

    inputBox->setMaskChar(nullptr, 0);
    auto expectedFallback = MakeInputBox(::skia::textlayout::TextAlign::kLeft, true, true);
    expectedFallback->setViewport(240, 96);
    expectedFallback->setSoftWrap(false);
    expectedFallback->setText("***", 3);
    EXPECT_TRUE(ImagesEqual(PaintSnapshot(*inputBox, 240, 96), PaintSnapshot(*expectedFallback, 240, 96)));
}

TEST_F(InputBoxTest, MaskInputPreservesHardNewlineLineBoundaries) {
    const std::string firstLine = "abc";
    const std::string secondLine = "\xE6\x97\xA5\xE6\x9C\xAC";
    const std::string text = firstLine + "\n" + secondLine;
    auto inputBox = MakeInputBox(::skia::textlayout::TextAlign::kLeft, false, true);
    inputBox->setViewport(320, 160);
    inputBox->setText(text.c_str(), text.size());
    inputBox->setMaskInput(true);

    ASSERT_EQ(inputBox->getLineCount(), 2U);
    milestro_text::InputBoxLineMetrics firstMetrics;
    milestro_text::InputBoxLineMetrics secondMetrics;
    ASSERT_TRUE(inputBox->getLineMetrics(0, firstMetrics));
    ASSERT_TRUE(inputBox->getLineMetrics(1, secondMetrics));
    EXPECT_EQ(firstMetrics.startUtf8, 0U);
    EXPECT_EQ(firstMetrics.endUtf8, firstLine.size());
    EXPECT_EQ(secondMetrics.startUtf8, firstLine.size() + 1U);
    EXPECT_EQ(secondMetrics.endUtf8, text.size());
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
    const std::string family = "\xF0\x9F\x91\xA8\xE2\x80\x8D\xF0\x9F\x91\xA9\xE2\x80\x8D\xF0\x9F\x91\xA7";
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

TEST_F(InputBoxTest, NewlineIsPlainTextAndCreatesParagraphLines) {
    auto inputBox = MakeInputBox();
    const std::string text = "ab\ncd";
    inputBox->setText(text.c_str(), text.size());

    EXPECT_EQ(inputBox->getText(), text);
    EXPECT_EQ(inputBox->getLineCount(), 2U);

    milestro_text::InputBoxLineMetrics firstLine;
    milestro_text::InputBoxLineMetrics secondLine;
    ASSERT_TRUE(inputBox->getLineMetrics(0, firstLine));
    ASSERT_TRUE(inputBox->getLineMetrics(1, secondLine));
    EXPECT_EQ(firstLine.startUtf8, 0U);
    EXPECT_EQ(firstLine.endUtf8, 2U);
    EXPECT_EQ(secondLine.startUtf8, 3U);
    EXPECT_EQ(secondLine.endUtf8, text.size());

    inputBox->setCursorUtf8(3, skia::textlayout::Affinity::kDownstream);
    ASSERT_TRUE(inputBox->deleteBackward());
    EXPECT_EQ(inputBox->getText(), "abcd");
    EXPECT_EQ(inputBox->getLineCount(), 1U);
    EXPECT_EQ(inputBox->getCursorUtf8(), 2U);
}

TEST_F(InputBoxTest, CaretMovesToEmptyTrailingLineAfterInsertedNewline) {
    auto inputBox = MakeInputBox(::skia::textlayout::TextAlign::kLeft, true, true);
    inputBox->setViewport(320, 160);
    inputBox->setText("ab", 2);
    inputBox->setCursorUtf8(2, skia::textlayout::Affinity::kDownstream);
    const auto firstLineCaret = inputBox->getCaretRect();

    inputBox->insertText("\n", 1);

    EXPECT_EQ(inputBox->getText(), "ab\n");
    EXPECT_EQ(inputBox->getCursorUtf8(), 3U);
    const auto trailingLineCaret = inputBox->getCaretRect();
    EXPECT_GT(trailingLineCaret.top, firstLineCaret.top + 1.0f);
    EXPECT_GT(trailingLineCaret.bottom, firstLineCaret.bottom + 1.0f);
    EXPECT_NEAR(trailingLineCaret.left, 0.0f, 0.001f);
}

TEST_F(InputBoxTest, TrailingAsciiSpacesAdvanceCaretAndEditingState) {
    auto inputBox = MakeInputBox(::skia::textlayout::TextAlign::kLeft, true);
    inputBox->setSoftWrap(false);
    inputBox->setText("ab", 2);
    inputBox->setCursorUtf8(2, skia::textlayout::Affinity::kDownstream);
    const auto beforeSpace = inputBox->getCaretRect();

    inputBox->insertText(" ", 1);
    const auto afterOneSpace = inputBox->getCaretRect();
    EXPECT_EQ(inputBox->getText(), "ab ");
    EXPECT_EQ(inputBox->getCursorUtf8(), 3U);
    EXPECT_FALSE(inputBox->hasSelection());
    EXPECT_GT(afterOneSpace.left, beforeSpace.left);

    inputBox->insertText(" ", 1);
    const auto afterTwoSpaces = inputBox->getCaretRect();
    EXPECT_EQ(inputBox->getText(), "ab  ");
    EXPECT_EQ(inputBox->getCursorUtf8(), 4U);
    EXPECT_GT(afterTwoSpaces.left, afterOneSpace.left);

    ASSERT_TRUE(inputBox->movePrevious(true));
    const auto selection = inputBox->getSelection();
    ASSERT_TRUE(selection.hasSelection);
    EXPECT_EQ(selection.startUtf8, 3U);
    EXPECT_EQ(selection.endUtf8, 4U);
    EXPECT_NEAR(inputBox->getCaretRect().left, afterOneSpace.left, 0.001f);
    const auto selectionRects = inputBox->getSelectionRects();
    ASSERT_FALSE(selectionRects.empty());
    EXPECT_GT(selectionRects.back().right, selectionRects.back().left);

    ASSERT_TRUE(inputBox->moveNext(false));
    EXPECT_FALSE(inputBox->hasSelection());
    EXPECT_EQ(inputBox->getCursorUtf8(), 4U);
    EXPECT_NEAR(inputBox->getCaretRect().left, afterTwoSpaces.left, 0.001f);

    ASSERT_TRUE(inputBox->deleteBackward());
    EXPECT_EQ(inputBox->getText(), "ab ");
    EXPECT_NEAR(inputBox->getCaretRect().left, afterOneSpace.left, 0.001f);
    ASSERT_TRUE(inputBox->undo());
    EXPECT_EQ(inputBox->getText(), "ab  ");
    EXPECT_NEAR(inputBox->getCaretRect().left, afterTwoSpaces.left, 0.001f);
    ASSERT_TRUE(inputBox->redo());
    EXPECT_EQ(inputBox->getText(), "ab ");
    EXPECT_NEAR(inputBox->getCaretRect().left, afterOneSpace.left, 0.001f);
}

TEST_F(InputBoxTest, TrailingAsciiSpacesAdvanceAtAlignedAndExplicitLineEnds) {
    const std::vector<::skia::textlayout::TextAlign> alignments = {
            ::skia::textlayout::TextAlign::kLeft,
            ::skia::textlayout::TextAlign::kCenter,
            ::skia::textlayout::TextAlign::kRight,
    };

    for (const auto alignment: alignments) {
        auto inputBox = MakeInputBox(alignment, true, true);
        inputBox->setViewport(320, 160);
        inputBox->setSoftWrap(false);

        const std::string text = "ab  \ncd  ";
        inputBox->setText(text.c_str(), text.size());

        inputBox->setCursorUtf8(2, skia::textlayout::Affinity::kDownstream);
        const auto firstLineBeforeSpaces = inputBox->getCaretRect();
        inputBox->setCursorUtf8(3, skia::textlayout::Affinity::kDownstream);
        const auto firstLineAfterOneSpace = inputBox->getCaretRect();
        inputBox->setCursorUtf8(4, skia::textlayout::Affinity::kDownstream);
        const auto firstLineAfterTwoSpaces = inputBox->getCaretRect();
        EXPECT_GT(firstLineAfterOneSpace.left, firstLineBeforeSpaces.left);
        EXPECT_GT(firstLineAfterTwoSpaces.left, firstLineAfterOneSpace.left);
        EXPECT_NEAR(firstLineAfterTwoSpaces.top, firstLineBeforeSpaces.top, 0.001f);

        inputBox->setCursorUtf8(7, skia::textlayout::Affinity::kDownstream);
        const auto finalLineBeforeSpaces = inputBox->getCaretRect();
        inputBox->setCursorUtf8(8, skia::textlayout::Affinity::kDownstream);
        const auto finalLineAfterOneSpace = inputBox->getCaretRect();
        inputBox->setCursorUtf8(9, skia::textlayout::Affinity::kDownstream);
        const auto finalLineAfterTwoSpaces = inputBox->getCaretRect();
        EXPECT_GT(finalLineAfterOneSpace.left, finalLineBeforeSpaces.left);
        EXPECT_GT(finalLineAfterTwoSpaces.left, finalLineAfterOneSpace.left);
        EXPECT_NEAR(finalLineAfterTwoSpaces.top, finalLineBeforeSpaces.top, 0.001f);
        EXPECT_GT(finalLineAfterTwoSpaces.top, firstLineAfterTwoSpaces.top);
    }
}

TEST_F(InputBoxTest, SoftWrappedTrailingSpaceAdvancesCaretOnItsResolvedLine) {
    auto inputBox = MakeInputBox(::skia::textlayout::TextAlign::kLeft, true, true);
    inputBox->setViewport(58, 160);
    inputBox->setSoftWrap(true);
    const std::string text = "ab  cd";
    inputBox->setText(text.c_str(), text.size());

    ASSERT_GT(inputBox->getLineCount(), 1U);
    milestro_text::InputBoxLineMetrics firstLine;
    ASSERT_TRUE(inputBox->getLineMetrics(0, firstLine));
    ASSERT_GT(firstLine.endUtf8, firstLine.startUtf8);
    ASSERT_EQ(text[firstLine.endUtf8 - 1], ' ');

    inputBox->setCursorUtf8(firstLine.endUtf8 - 1, skia::textlayout::Affinity::kUpstream);
    const auto beforeTrailingSpace = inputBox->getCaretRect();
    inputBox->setCursorUtf8(firstLine.endUtf8, skia::textlayout::Affinity::kUpstream);
    const auto afterTrailingSpace = inputBox->getCaretRect();
    EXPECT_NEAR(afterTrailingSpace.top, beforeTrailingSpace.top, 0.001f);
    EXPECT_GT(afterTrailingSpace.left, beforeTrailingSpace.left);
    EXPECT_FLOAT_EQ(inputBox->getMetrics().scrollX, 0.0f);
}

TEST_F(InputBoxTest, NbspRemainsVisibleWhileAsciiTrailingSpacesUseCaretGeometry) {
    auto inputBox = MakeInputBox(::skia::textlayout::TextAlign::kLeft, true);
    const std::string text = "ab\xC2\xA0";
    inputBox->setText(text.c_str(), text.size());
    inputBox->setCursorUtf8(2, skia::textlayout::Affinity::kDownstream);
    const auto beforeNbsp = inputBox->getCaretRect();
    inputBox->setCursorUtf8(text.size(), skia::textlayout::Affinity::kDownstream);
    const auto afterNbsp = inputBox->getCaretRect();
    EXPECT_GT(afterNbsp.left, beforeNbsp.left);
}

TEST_F(InputBoxTest, LineEndRangeProbePreservesEmojiAndRtlDirection) {
    const std::string family = "\xF0\x9F\x91\xA8\xE2\x80\x8D\xF0\x9F\x91\xA9\xE2\x80\x8D\xF0\x9F\x91\xA7";
    auto emojiInputBox = MakeInputBox(::skia::textlayout::TextAlign::kLeft, false);
    emojiInputBox->setText(("a" + family).c_str(), 1 + family.size());
    emojiInputBox->setCursorUtf8(1, skia::textlayout::Affinity::kDownstream);
    const auto beforeEmoji = emojiInputBox->getCaretRect();
    emojiInputBox->setCursorUtf8(1 + family.size(), skia::textlayout::Affinity::kDownstream);
    EXPECT_GT(emojiInputBox->getCaretRect().left, beforeEmoji.left);

    const std::string rtlText = "\xD7\x90\xD7\x91  ";
    auto rtlInputBox = MakeInputBox(::skia::textlayout::TextAlign::kRight,
                                    true,
                                    false,
                                    ::skia::textlayout::TextDirection::kRtl);
    rtlInputBox->setText(rtlText.c_str(), rtlText.size());
    rtlInputBox->setCursorUtf8(4, skia::textlayout::Affinity::kDownstream);
    const auto beforeSpaces = rtlInputBox->getCaretRect();
    rtlInputBox->setCursorUtf8(5, skia::textlayout::Affinity::kDownstream);
    const auto afterOneSpace = rtlInputBox->getCaretRect();
    rtlInputBox->setCursorUtf8(6, skia::textlayout::Affinity::kDownstream);
    const auto afterTwoSpaces = rtlInputBox->getCaretRect();
    EXPECT_LT(afterOneSpace.left, beforeSpaces.left);
    EXPECT_LT(afterTwoSpaces.left, afterOneSpace.left);
}

TEST_F(InputBoxTest, CompositionTrailingSpacesAdvanceAndStayHorizontallyVisible) {
    auto inputBox = MakeInputBox(::skia::textlayout::TextAlign::kLeft, true);
    inputBox->setViewport(96, 72);
    inputBox->setSoftWrap(false);
    const std::string committed = "1234567890";
    inputBox->setText(committed.c_str(), committed.size());
    inputBox->setCursorUtf8(committed.size(), skia::textlayout::Affinity::kDownstream);
    const auto beforeComposition = inputBox->getCaretRect();

    ASSERT_TRUE(inputBox->setComposition("  ", 2));
    const auto duringComposition = inputBox->getCaretRect();
    EXPECT_GT(duringComposition.left, beforeComposition.left);
    EXPECT_LE(duringComposition.right - inputBox->getMetrics().scrollX,
              inputBox->getMetrics().viewportWidth + 0.001f);

    ASSERT_TRUE(inputBox->commitComposition(nullptr, 0));
    EXPECT_EQ(inputBox->getText(), committed + "  ");
    EXPECT_EQ(inputBox->getCursorUtf8(), committed.size() + 2U);
    const auto afterCommit = inputBox->getCaretRect();
    EXPECT_NEAR(afterCommit.left, duringComposition.left, 0.001f);
    const auto metrics = inputBox->getMetrics();
    EXPECT_GT(metrics.scrollX, 0.0f);
    EXPECT_LE(afterCommit.right - metrics.scrollX, metrics.viewportWidth + 0.001f);

    ASSERT_TRUE(inputBox->hitTest(afterCommit.left - 1.0f, (afterCommit.top + afterCommit.bottom) * 0.5f));
    EXPECT_GE(inputBox->getCursorUtf8(), committed.size() + 1U);
}

TEST_F(InputBoxTest, CaretWidthCanBeZeroForHiddenCaretGeometry) {
    auto inputBox = MakeInputBox(::skia::textlayout::TextAlign::kLeft, true, true);
    inputBox->setViewport(320, 160);
    inputBox->setText("ab", 2);
    inputBox->setCursorUtf8(2, skia::textlayout::Affinity::kDownstream);

    inputBox->setCaretWidth(0.0f);

    const auto caret = inputBox->getCaretRect();
    EXPECT_FLOAT_EQ(caret.left, caret.right);
}

TEST_F(InputBoxTest, UpDownNavigationPreservesCaretXAcrossLines) {
    auto inputBox = MakeInputBox(::skia::textlayout::TextAlign::kLeft, true);
    inputBox->setViewport(320, 160);
    const std::string text = "aaaa\naaaa\naaaa";
    inputBox->setText(text.c_str(), text.size());
    inputBox->setCursorUtf8(2, skia::textlayout::Affinity::kDownstream);
    const auto firstCaret = inputBox->getCaretRect();

    ASSERT_TRUE(inputBox->moveDown());
    EXPECT_EQ(inputBox->getCursorUtf8(), 7U);
    const auto secondCaret = inputBox->getCaretRect();
    EXPECT_NEAR(secondCaret.left, firstCaret.left, 0.5f);
    EXPECT_GT(secondCaret.top, firstCaret.top);

    ASSERT_TRUE(inputBox->moveDown());
    EXPECT_EQ(inputBox->getCursorUtf8(), 12U);
    const auto thirdCaret = inputBox->getCaretRect();
    EXPECT_NEAR(thirdCaret.left, firstCaret.left, 0.5f);
    EXPECT_GT(thirdCaret.top, secondCaret.top);

    ASSERT_TRUE(inputBox->moveUp());
    EXPECT_EQ(inputBox->getCursorUtf8(), 7U);
}

TEST_F(InputBoxTest, ShiftUpDownExtendsSelectionAcrossLines) {
    auto inputBox = MakeInputBox(::skia::textlayout::TextAlign::kLeft, true);
    inputBox->setViewport(320, 160);
    inputBox->setText("aaaa\naaaa\naaaa", 14);
    inputBox->setCursorUtf8(7, skia::textlayout::Affinity::kDownstream);

    ASSERT_TRUE(inputBox->moveDown(true));
    auto selection = inputBox->getSelection();
    ASSERT_TRUE(selection.hasSelection);
    EXPECT_EQ(selection.anchorUtf8, 7U);
    EXPECT_EQ(selection.focusUtf8, 12U);
    EXPECT_EQ(selection.startUtf8, 7U);
    EXPECT_EQ(selection.endUtf8, 12U);

    ASSERT_TRUE(inputBox->moveUp(true));
    selection = inputBox->getSelection();
    EXPECT_FALSE(selection.hasSelection);
    EXPECT_EQ(inputBox->getCursorUtf8(), 7U);
}

TEST_F(InputBoxTest, HomeEndUseLineBoundariesAndDocumentModifierUsesDocumentBoundaries) {
    auto inputBox = MakeInputBox();
    const std::string text = "aaaa\nbbbb\ncccc";
    inputBox->setText(text.c_str(), text.size());
    inputBox->setCursorUtf8(7, skia::textlayout::Affinity::kDownstream);

    ASSERT_TRUE(inputBox->moveLineStart());
    EXPECT_EQ(inputBox->getCursorUtf8(), 5U);

    ASSERT_TRUE(inputBox->moveLineEnd());
    EXPECT_EQ(inputBox->getCursorUtf8(), 9U);

    ASSERT_TRUE(inputBox->moveDocumentStart());
    EXPECT_EQ(inputBox->getCursorUtf8(), 0U);

    ASSERT_TRUE(inputBox->moveDocumentEnd());
    EXPECT_EQ(inputBox->getCursorUtf8(), text.size());
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

TEST_F(InputBoxTest, ContinuousTypingUndoRedoUsesSingleGroup) {
    auto inputBox = MakeInputBox();

    inputBox->insertText("a", 1);
    inputBox->insertText("b", 1);
    inputBox->insertText("c", 1);

    EXPECT_EQ(inputBox->getText(), "abc");
    ASSERT_TRUE(inputBox->undo());
    EXPECT_EQ(inputBox->getText(), "");
    EXPECT_EQ(inputBox->getCursorUtf8(), 0U);

    ASSERT_TRUE(inputBox->redo());
    EXPECT_EQ(inputBox->getText(), "abc");
    EXPECT_EQ(inputBox->getCursorUtf8(), 3U);
}

TEST_F(InputBoxTest, MovementBreaksTypingUndoGroup) {
    auto inputBox = MakeInputBox();

    inputBox->insertText("a", 1);
    inputBox->insertText("b", 1);
    inputBox->insertText("c", 1);
    ASSERT_TRUE(inputBox->movePrevious());
    inputBox->insertText("X", 1);

    EXPECT_EQ(inputBox->getText(), "abXc");
    ASSERT_TRUE(inputBox->undo());
    EXPECT_EQ(inputBox->getText(), "abc");
    EXPECT_EQ(inputBox->getCursorUtf8(), 2U);

    ASSERT_TRUE(inputBox->undo());
    EXPECT_EQ(inputBox->getText(), "");
}

TEST_F(InputBoxTest, SelectionReplacementUndoRestoresSelectionAndRedoRestoresCaret) {
    auto inputBox = MakeInputBox();
    inputBox->setText("abcd", 4);

    ASSERT_TRUE(inputBox->setSelectionUtf8(1,
                                           3,
                                           skia::textlayout::Affinity::kDownstream,
                                           skia::textlayout::Affinity::kDownstream));
    inputBox->insertText("X", 1);
    EXPECT_EQ(inputBox->getText(), "aXd");
    EXPECT_FALSE(inputBox->hasSelection());
    EXPECT_EQ(inputBox->getCursorUtf8(), 2U);

    ASSERT_TRUE(inputBox->undo());
    EXPECT_EQ(inputBox->getText(), "abcd");
    auto selection = inputBox->getSelection();
    ASSERT_TRUE(selection.hasSelection);
    EXPECT_EQ(selection.startUtf8, 1U);
    EXPECT_EQ(selection.endUtf8, 3U);
    EXPECT_EQ(inputBox->getCursorUtf8(), 3U);

    ASSERT_TRUE(inputBox->redo());
    EXPECT_EQ(inputBox->getText(), "aXd");
    EXPECT_FALSE(inputBox->hasSelection());
    EXPECT_EQ(inputBox->getCursorUtf8(), 2U);
}

TEST_F(InputBoxTest, RepeatedBackspaceAndDeleteUndoRestoreDeletedRuns) {
    auto inputBox = MakeInputBox();
    inputBox->setText("abcd", 4);
    inputBox->setCursorUtf8(4, skia::textlayout::Affinity::kDownstream);

    ASSERT_TRUE(inputBox->deleteBackward());
    ASSERT_TRUE(inputBox->deleteBackward());
    EXPECT_EQ(inputBox->getText(), "ab");

    ASSERT_TRUE(inputBox->undo());
    EXPECT_EQ(inputBox->getText(), "abcd");
    EXPECT_EQ(inputBox->getCursorUtf8(), 4U);

    inputBox->breakUndoGroup();
    inputBox->setCursorUtf8(0, skia::textlayout::Affinity::kDownstream);
    ASSERT_TRUE(inputBox->deleteForward());
    ASSERT_TRUE(inputBox->deleteForward());
    EXPECT_EQ(inputBox->getText(), "cd");

    ASSERT_TRUE(inputBox->undo());
    EXPECT_EQ(inputBox->getText(), "abcd");
    EXPECT_EQ(inputBox->getCursorUtf8(), 0U);
}

TEST_F(InputBoxTest, ImeCommitIsSingleUndoGroupAndPreeditIsNotHistory) {
    const std::string composition = "\xE4\xBD\xA0";
    auto inputBox = MakeInputBox();

    ASSERT_TRUE(inputBox->setComposition("n", 1));
    ASSERT_TRUE(inputBox->setComposition("ni", 2));
    EXPECT_FALSE(inputBox->undo());

    ASSERT_TRUE(inputBox->commitComposition(composition.c_str(), composition.size()));
    EXPECT_EQ(inputBox->getText(), composition);

    ASSERT_TRUE(inputBox->undo());
    EXPECT_EQ(inputBox->getText(), "");

    ASSERT_TRUE(inputBox->redo());
    EXPECT_EQ(inputBox->getText(), composition);
}

TEST_F(InputBoxTest, NewEditAfterUndoClearsRedoStack) {
    auto inputBox = MakeInputBox();

    inputBox->insertText("a", 1);
    inputBox->breakUndoGroup();
    inputBox->insertText("b", 1);
    EXPECT_EQ(inputBox->getText(), "ab");

    ASSERT_TRUE(inputBox->undo());
    EXPECT_EQ(inputBox->getText(), "a");
    inputBox->insertText("c", 1);
    EXPECT_EQ(inputBox->getText(), "ac");
    EXPECT_FALSE(inputBox->redo());
}

TEST_F(InputBoxTest, ProgrammaticSetTextClearsEditHistory) {
    auto inputBox = MakeInputBox();

    inputBox->insertText("abc", 3);
    ASSERT_TRUE(inputBox->undo());
    ASSERT_TRUE(inputBox->redo());

    inputBox->setText("reset", 5);
    EXPECT_EQ(inputBox->getText(), "reset");
    EXPECT_FALSE(inputBox->undo());
    EXPECT_FALSE(inputBox->redo());
}

TEST_F(InputBoxTest, UndoRedoPreservesRepresentativeUnicodeClusters) {
    const std::string text = "e\xCC\x81"
                             "\xF0\x9F\xA4\x94"
                             "\xF0\xB0\xBB\x9D"
                             "\xF0\x9F\xA7\x91\xE2\x80\x8D\xF0\x9F\xA7\x91\xE2\x80\x8D\xF0\x9F\xA7\x92";
    auto inputBox = MakeInputBox();

    inputBox->insertText(text.c_str(), text.size());
    EXPECT_EQ(inputBox->getText(), text);

    ASSERT_TRUE(inputBox->undo());
    EXPECT_EQ(inputBox->getText(), "");

    ASSERT_TRUE(inputBox->redo());
    EXPECT_EQ(inputBox->getText(), text);
}

TEST_F(InputBoxTest, SelectedTextAbiReturnsClusterSafeUtf8Slice) {
    auto inputBox = MakeInputBox();
    const std::string family = "\xF0\x9F\x91\xA8\xE2\x80\x8D\xF0\x9F\x91\xA9\xE2\x80\x8D\xF0\x9F\x91\xA7";
    const std::string text = "x" + family + "y";
    inputBox->setText(text.c_str(), text.size());

    ASSERT_TRUE(inputBox->setSelectionUtf8(1,
                                           1 + family.size(),
                                           skia::textlayout::Affinity::kDownstream,
                                           skia::textlayout::Affinity::kDownstream));

    milestro::game::model::BytesWrapper* selectedText = nullptr;
    ASSERT_EQ(MilestroSkiaTextlayoutInputBoxGetSelectedText(inputBox.get(), selectedText), 0);
    ASSERT_NE(selectedText, nullptr);
    std::unique_ptr<milestro::game::model::BytesWrapper> selectedTextOwner(selectedText);
    EXPECT_EQ(std::string(reinterpret_cast<char*>(selectedTextOwner->GetPtr()), selectedTextOwner->GetSize()), family);

    ASSERT_TRUE(inputBox->clearSelection());
    selectedText = nullptr;
    ASSERT_EQ(MilestroSkiaTextlayoutInputBoxGetSelectedText(inputBox.get(), selectedText), 0);
    ASSERT_NE(selectedText, nullptr);
    selectedTextOwner.reset(selectedText);
    EXPECT_EQ(selectedTextOwner->GetSize(), 0U);
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
    const std::string text = "A\xF0\x9F\xA4\x94\xE6\x97\xA5"
                             "B";
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
    ExpectNoSelectionRectOverlap(rects);
}

TEST_F(InputBoxTest, SelectionRectsPreservePerLineHeightAcrossLines) {
    auto inputBox = MakeInputBox();
    inputBox->setViewport(320, 160);
    const std::string text = "A\xF0\x9F\xA4\x94\nB\xE6\x97\xA5";
    inputBox->setText(text.c_str(), text.size());

    ASSERT_TRUE(inputBox->selectAll());
    const auto rects = inputBox->getSelectionRects();
    ASSERT_GE(rects.size(), 2U);

    milestro_text::InputBoxLineMetrics firstLine;
    milestro_text::InputBoxLineMetrics secondLine;
    ASSERT_TRUE(inputBox->getLineMetrics(0, firstLine));
    ASSERT_TRUE(inputBox->getLineMetrics(1, secondLine));
    const auto firstLineTop = firstLine.baseline - firstLine.ascent;
    const auto firstLineBottom = firstLine.baseline + firstLine.descent;
    const auto secondLineTop = secondLine.baseline - secondLine.ascent;
    const auto secondLineBottom = secondLine.baseline + secondLine.descent;
    const auto firstTop = rects.front().top;
    const auto firstBottom = rects.front().bottom;
    const auto lastTop = rects.back().top;
    const auto lastBottom = rects.back().bottom;
    EXPECT_NEAR(firstTop, firstLineTop, 0.001f);
    EXPECT_NEAR(firstBottom, firstLineBottom, 0.001f);
    EXPECT_GE(lastTop, secondLineTop - 0.001f);
    EXPECT_NEAR(lastBottom, secondLineBottom, 0.001f);
    EXPECT_LE(lastBottom - lastTop, secondLine.ascent + secondLine.descent + 0.001f);
    EXPECT_GT(lastBottom - lastTop, 0.0f);
    EXPECT_GT(lastTop, firstTop);
    ExpectNoSelectionRectOverlap(rects);
}

TEST_F(InputBoxTest, SelectionRectsDoNotOverlapForWrappedMixedParagraph) {
    const std::string text = "大型语言模型（英语：large language model，LLM），也称大语言模型，简称大模型，"
                             "是一种基于人工神经网络的语言模型。其名称中的“大型”指模型具有庞大的参数量"
                             "（通常23在数十亿至数万亿级别，如GPT-3含1750亿参数）以及巨大的训练数据规模。"
                             "大语言模型通常采用自监督机器学习方法，从而能够基于海量无标注的文本进行训练。"
                             "大语言模型专为自然语言处理任务而设计，尤其适用于语言生成。[1][2]其中包含"
                             "Gemini和GPT-4o在内的部分多模态大模型能够同时处理文字、图片、音频和视频等"
                             "不同输入形式。规模最大、功能最强大的LLM基本采用生成式预训练 Transformer (GPT) "
                             "模型，它们为ChatGPT、Gemini、Perplexity和Claude等聊天机器人提供了核心功能。";
    auto inputBox = MakeInputBox(::skia::textlayout::TextAlign::kLeft, false, true);
    inputBox->setViewport(1680, 480);
    inputBox->setSoftWrap(true);
    inputBox->setText(text.c_str(), text.size());

    ASSERT_TRUE(inputBox->selectAll());
    const auto rects = inputBox->getSelectionRects();
    ASSERT_GT(rects.size(), 1U);
    ExpectNoSelectionRectOverlap(rects);
}

TEST_F(InputBoxTest, SelectionRectsDoNotOverlapForNoWrapMixedParagraph) {
    const std::string text = "大型语言模型（英语：large language model，LLM），也称大语言模型，简称大模型，"
                             "是一种基于人工神经网络的语言模型。\n其名称中的“大型”指模型具有庞大的参数量"
                             "（通常23在数十亿至数万亿级别，如GPT-3含1750亿参数）以及巨大的训练数据规模。";
    auto inputBox = MakeInputBox(::skia::textlayout::TextAlign::kLeft, false, true);
    inputBox->setViewport(1680, 240);
    inputBox->setSoftWrap(false);
    inputBox->setText(text.c_str(), text.size());

    ASSERT_TRUE(inputBox->selectAll());
    const auto rects = inputBox->getSelectionRects();
    ASSERT_GT(rects.size(), 1U);
    ExpectNoSelectionRectOverlap(rects);
}

TEST_F(InputBoxTest, SingleLineGeometryUsesStableBaselineInViewport) {
    auto inputBox = MakeInputBox();
    inputBox->setViewport(320, 96);
    inputBox->setAutoMargin(false, true, false, true);
    inputBox->setText("abc", 3);

    const auto metrics = inputBox->getMetrics();
    const auto caret = inputBox->getCaretRect();
    milestro_text::InputBoxLineMetrics lineMetrics;
    ASSERT_TRUE(inputBox->getLineMetrics(0, lineMetrics));
    const auto baseline = caret.top + lineMetrics.ascent;
    ASSERT_GT(metrics.viewportHeight, metrics.height);
    EXPECT_GT(caret.top, 0.0f);
    EXPECT_LT(caret.bottom, metrics.viewportHeight);

    const std::string mixedText = "A\xF0\x9F\xA4\x94\xE6\x97\xA5"
                                  "B";
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
