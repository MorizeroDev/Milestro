#include "Milestro/skia/FontManager.h"
#include "Milestro/skia/textlayout/Paragraph.h"
#include "Milestro/skia/textlayout/ParagraphBuilder.h"
#include "Milestro/skia/textlayout/ParagraphStyle.h"
#include "Milestro/skia/textlayout/TextStyle.h"
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

namespace fs = std::filesystem;
using namespace milestro::skia::textlayout;

int registerFontsInDirectory(milestro::skia::FontManager* fontManager, const std::string& dirPath) {
    int successCount = 0;
    fs::path fontDir(dirPath);

    if (!fs::exists(fontDir) || !fs::is_directory(fontDir)) {
        std::cerr << "Font directory does not exist: " << dirPath << std::endl;
        return successCount;
    }

    for (const auto& entry : fs::directory_iterator(fontDir)) {
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

    milestro::skia::FontManager* fontManager{};
    fs::path imageDir;
};

TEST_F(ReadImageTest, RegistersFontsCorrectly) {
    // 捕获 std::cout 和 std::cerr
    std::stringstream capturedStdout;
    std::stringstream capturedStderr;
    std::streambuf* oldCout = std::cout.rdbuf(capturedStdout.rdbuf());
    std::streambuf* oldCerr = std::cerr.rdbuf(capturedStderr.rdbuf());

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
    for (const auto& name : familyNames) {
        if (name.find("Source Han Sans VF") != std::string::npos) {
            foundNewFont = true;
            break;
        }
    }
    EXPECT_TRUE(foundNewFont);

    // 验证注册的字体数量
    int expectedCount = 0;
    for (const auto& entry : fs::directory_iterator(imageDir)) {
        if (entry.path().extension() == ".bytes") {
            expectedCount++;
        }
    }
    EXPECT_EQ(registeredCount, expectedCount);
}

TEST_F(ReadImageTest, HandlesNonExistentDirectory) {
    std::string nonExistentPath = (fs::current_path() / "non_existent_dir").string();

    std::stringstream capturedStderr;
    std::streambuf* oldCerr = std::cerr.rdbuf(capturedStderr.rdbuf());

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
//    fontFamilies.emplace_back("Noto Emoji");
    fontFamilies.emplace_back("Noto Color Emoji");

    textStyle->setFontFamilies(fontFamilies);
    textStyle->setFontSize(72);
    // textStyle->addFontFeature(SkString("liga"), 1);
    textStyle->setColor(SK_ColorBLACK);
    // textStyle->addShadow(SK_ColorWHITE, SkPoint::Make(0, 0), 5);

    auto paragraphStyle = std::make_unique<ParagraphStyle>();
    paragraphStyle->setTextStyle(textStyle.get());

    auto paragraphBuilder = std::make_unique<ParagraphBuilder>(paragraphStyle.get());
//
//    std::vector<std::string> locales = {"ko", "ja", "zh-Hant", "zh-Hans"};
//    for (const auto& locale : locales) {
//        textStyle->setLocale(SkString(locale.c_str()));
//        paragraphBuilder->pushStyle(textStyle.get());
//        paragraphBuilder->addText((locale + ": ").c_str(), locale.length() + 2);
//        paragraphBuilder->addText("曜 😠 👪\n", 9);
//        paragraphBuilder->pop();
//    }

//    std::unique_ptr<Paragraph> paragraph(paragraphBuilder->build());
//    paragraph->layout(1600);
}
//
//TEST_F(FontRegistrationTest, readfont) {
//
//    // Verify registered fonts
//    auto familyNames = fontManager->GetFamiliesNames();
//    EXPECT_TRUE(std::find(familyNames.begin(), familyNames.end(), "Source Han Sans VF") != familyNames.end());
//    EXPECT_TRUE(std::find(familyNames.begin(), familyNames.end(), "Noto Color Emoji") != familyNames.end());
//
//    auto textStyle = std::make_unique<milestro::skia::textlayout::TextStyle>();
//    std::vector<SkString> fontFamilies;
//    fontFamilies.emplace_back("Source Han Sans VF");
//    fontFamilies.emplace_back("Noto Color Emoji");
//
//    textStyle->setFontFamilies(fontFamilies);
//    textStyle->setFontSize(72);
//    textStyle->setColor(SK_ColorBLACK);
//
//    auto paragraphStyle = std::make_unique<milestro::skia::textlayout::ParagraphStyle>();
//    paragraphStyle->setTextStyle(textStyle.get());
//
//    auto paragraphBuilder = std::make_unique<milestro::skia::textlayout::ParagraphBuilder>(paragraphStyle.get());
//    ASSERT_NE(paragraphBuilder.get(), nullptr);
//
//    std::vector<std::string> locales = {"ko", "ja", "zh-Hant", "zh-Hans"};
//    for (const auto& locale : locales) {
//        textStyle->setLocale(SkString(locale.c_str()));
//        paragraphBuilder->pushStyle(textStyle.get());
//        paragraphBuilder->addText((locale + ": ").c_str(), locale.length() + 2);
//        paragraphBuilder->addText("曜 😠 👪\n", 9);
//        paragraphBuilder->pop();
//    }
//
//        std::unique_ptr<Paragraph> paragraph(paragraphBuilder->build());
//        paragraph->layout(1600);
////    std::unique_ptr<milestro::skia::textlayout::Paragraph> paragraph(paragraphBuilder->build());
//    ASSERT_NE(paragraph.get(), nullptr);
//
//    // Layout the paragraph
//    paragraph->layout(1600);
//
//    // Create a canvas to paint on
//    milestro::skia::Canvas canvas(1600, 400);
//
//    // Paint the paragraph
//    paragraph->paint(&canvas, 0, 0);
//
//    // Verify the painted content (this is a basic check, you might want to add more specific verifications)
//    uint32_t* pixels = new uint32_t[1600 * 400];
//    EXPECT_TRUE(canvas.GetTexture(pixels));
//
//    // Check if the canvas is not empty (at least one non-white pixel)
//    bool hasContent = false;
//    for (int i = 0; i < 1600 * 400; ++i) {
//        if (pixels[i] != 0xFFFFFFFF) {  // Assuming white background
//            hasContent = true;
//            break;
//        }
//    }
//    EXPECT_TRUE(hasContent);
//
//    delete[] pixels;
//
//    // Optional: Save the canvas to a PNG file for visual inspection
//    // #ifdef MILESTRO_USE_CLI
//    //     EXPECT_TRUE(canvas.SaveToPng("/path/to/output.png"));
//    // #endif
//}

int main(int argc, char** argv) {
    std::cout << "Milestro Test - Read Font" << std::endl;

    testing::InitGoogleTest(&argc, argv);
    return RUN_ALL_TESTS();
}
