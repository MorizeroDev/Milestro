#include "Milestro/skia/FontManager.h"
#include "Milestro/skia/textlayout/Paragraph.h"
#include "Milestro/skia/textlayout/ParagraphBuilder.h"
#include "Milestro/skia/textlayout/ParagraphStyle.h"
#include "Milestro/skia/textlayout/TextStyle.h"
#include "Milestro/game/milestro_game_interface.h"
#include <chrono>
#include <filesystem>
#include <fstream>
#include <gmock/gmock.h>
#include <gtest/gtest.h>
#include <iostream>
#include <locale>
#include <memory>
#include <string>
#include <thread>
#include <vector>
#include <src/gpu/ganesh/GrDistanceFieldGenFromVector.h>

namespace fs = std::filesystem;
using namespace milestro::skia::textlayout;

int registerFontsInDirectory(milestro::skia::FontManager *fontManager, const std::string &dirPath) {
    int successCount = 0;
    fs::path fontDir(dirPath);

    if (!fs::exists(fontDir) || !fs::is_directory(fontDir)) {
        std::cerr << "Font directory does not exist: " << dirPath << std::endl;
        return successCount;
    }

    for (const auto &entry: fs::directory_iterator(fontDir)) {
        if (entry.path().extension() == ".bytes") {
            std::string fontPath = entry.path().string();
            auto result = fontManager->RegisterFontFromFile(fontPath.c_str());

            switch (result) {
                case milestro::skia::MilestroFontManager::RegisterResult::Succeed:
                    std::cout << "Successfully registered font: " << fontPath << std::endl;
                    successCount++;
                    break;
                case milestro::skia::MilestroFontManager::RegisterResult::Duplicated:
                    std::cout << "Font already registered: " << fontPath << std::endl;
                    break;
                case milestro::skia::MilestroFontManager::RegisterResult::Failed:
                    std::cerr << "Failed to register font: " << fontPath << std::endl;
                    break;
            }
        }
    }

    return successCount;
}

class ReadImageTest : public ::testing::Test {
public:
    uint64_t SplitGlyphCallback(uint16_t glyphId, milestro::skia::Font *font, SkRect bound, SkSize advance) {
        milestro::skia::Path *path;
        MilestroSkiaFontGetPath(font, path, glyphId);

        MilestroSkiaPathDestroy(path);
    }

protected:
    void SetUp() override {
        // 获取 FontManager 实例
        fontManager = milestro::skia::GetFontManager();

        // 设置字体目录路径
        imageDir = fs::current_path() / "data" / "font";
    }

    void TearDown() override {
        // 如果需要清理，可以在这里添加清理代码
    }


    milestro::skia::FontManager *fontManager{};
    fs::path imageDir;
};

TEST_F(ReadImageTest, RegistersFontsCorrectly) {
    // 捕获 std::cout 和 std::cerr
    std::stringstream capturedStdout;
    std::stringstream capturedStderr;
    std::streambuf *oldCout = std::cout.rdbuf(capturedStdout.rdbuf());
    std::streambuf *oldCerr = std::cerr.rdbuf(capturedStderr.rdbuf());

    // 调用被测试的函数
    int registeredCount = registerFontsInDirectory(fontManager, imageDir.string());

    // 恢复 std::cout 和 std::cerr
    std::cout.rdbuf(oldCout);
    std::cerr.rdbuf(oldCerr);

    // 验证结果
    EXPECT_GT(registeredCount, 0);
    EXPECT_TRUE(capturedStdout.str().find("Successfully registered font:") != std::string::npos);
    EXPECT_TRUE(capturedStderr.str().empty());

    // 验证字体是否真的被注册了
    auto familyNames = fontManager->GetFamiliesNames();
    bool foundNewFont = false;
    for (const auto &name: familyNames) {
        if (name.find("Source Han Sans VF") != std::string::npos) {
            foundNewFont = true;
            break;
        }
    }
    EXPECT_TRUE(foundNewFont);

    // 验证注册的字体数量
    int expectedCount = 0;
    for (const auto &entry: fs::directory_iterator(imageDir)) {
        if (entry.path().extension() == ".bytes") {
            expectedCount++;
        }
    }
    EXPECT_EQ(registeredCount, expectedCount);
}

TEST_F(ReadImageTest, HandlesNonExistentDirectory) {
    std::string nonExistentPath = (fs::current_path() / "non_existent_dir").string();

    std::stringstream capturedStderr;
    std::streambuf *oldCerr = std::cerr.rdbuf(capturedStderr.rdbuf());

    int registeredCount = registerFontsInDirectory(fontManager, nonExistentPath);

    std::cerr.rdbuf(oldCerr);

    EXPECT_EQ(registeredCount, 0);
    EXPECT_TRUE(capturedStderr.str().find("Font directory does not exist") != std::string::npos);
}

TEST_F(ReadImageTest, HandlesEmptyDirectory) {
    // 创建一个临时的空目录
    fs::path emptyDir = fs::temp_directory_path() / "empty_font_dir";
    fs::create_directory(emptyDir);

    int registeredCount = registerFontsInDirectory(fontManager, emptyDir.string());

    EXPECT_EQ(registeredCount, 0);

    // 清理临时目录
    fs::remove(emptyDir);
}


TEST_F(ReadImageTest, readfont1) {
    auto familyNames = fontManager->GetFamiliesNames();
    EXPECT_TRUE(std::find(familyNames.begin(), familyNames.end(), "Source Han Sans VF") != familyNames.end());

    auto textStyle = std::make_unique<TextStyle>();
    std::vector<SkString> fontFamilies;
    fontFamilies.emplace_back("Source Han Sans VF");
    fontFamilies.emplace_back("Noto Color Emoji");

    textStyle->setFontFamilies(fontFamilies);
    textStyle->setFontSize(72);
    textStyle->setColor(SK_ColorWHITE);

    auto paragraphStyle = std::make_unique<ParagraphStyle>();
    paragraphStyle->setTextStyle(textStyle.get());
    auto paragraphBuilder = std::make_unique<ParagraphBuilder>(paragraphStyle.get());

    std::vector<std::string> locales = {"ko", "ja", "zh-Hant", "zh-Hans"};
    for (const auto &locale: locales) {
        textStyle->setLocale(SkString(locale.c_str()));
        paragraphBuilder->pushStyle(textStyle.get());
        std::string payload("曜\n");
        paragraphBuilder->addText(payload.c_str(), payload.size());
    }

    auto paragraph = paragraphBuilder->build();
    paragraph->layout(1600);

    paragraph->splitGlyph(100, 100, (void *) this, [](const void *ctx,
                                                      uint16_t glyphId,
                                                      milestro::skia::Font *font,

                                                      float boundLeft, float boundTop,
                                                      float boundRight, float boundBottom,

                                                      float advanceWidth, float advanceHeight) -> uint64_t {
        auto rect = SkRect();
        rect.fLeft = boundLeft;
        rect.fTop = boundTop;
        rect.fRight = boundRight;
        rect.fBottom = boundBottom;

        auto advance = SkSize();
        advance.fHeight = advanceHeight;
        advance.fWidth = advanceWidth;
        return ((ReadImageTest *) ctx)->SplitGlyphCallback(glyphId, font, rect, advance);
    });

#ifdef MILESTRO_USE_CLI
    milestro::skia::Canvas canvas(1920, 1080, nullptr);
    paragraph->paint(&canvas, 100, 100);
    canvas.SaveToPng("locale-test.png");
#endif
}

int main(int argc, char **argv) {
    std::cout << "Milestro Test - Read Font" << std::endl;

    testing::InitGoogleTest(&argc, argv);
    return RUN_ALL_TESTS();
}
