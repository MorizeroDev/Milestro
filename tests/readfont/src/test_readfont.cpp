#include "Milestro/skia/FontManager.h"
#include "Milestro/skia/textlayout/Paragraph.h"
#include "Milestro/skia/textlayout/ParagraphBuilder.h"
#include "Milestro/skia/textlayout/ParagraphStyle.h"
#include "Milestro/skia/textlayout/TextStyle.h"
#include "Milestro/skia/Path.h"
#include "Milestro/skia/Font.h"
#include "Milestro/game/milestro_game_interface.h"
#include <chrono>
#include <filesystem>
#include <gtest/gtest.h>
#include <iostream>
#include <memory>
#include <string>
#include <vector>
#include <src/gpu/ganesh/GrDistanceFieldGenFromVector.h>
#include <src/gpu/ganesh/geometry/GrAATriangulator.h>
#include <src/gpu/ganesh/GrEagerVertexAllocator.h>
#include <src/core/SkDistanceFieldGen.h>

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

void printVertexData(sk_sp<GrThreadSafeCache::VertexData> vertexData) {
    std::cout << "numVertices: " << vertexData->numVertices() << std::endl;
    std::cout << "vertexSize: " << vertexData->vertexSize() << std::endl;

    auto p = vertexData->vertices();
    for (int i = 0; i < vertexData->numVertices(); i++) {
        std::cout << std::endl << "b.Add(";
        for (int j = 0; j < vertexData->vertexSize() / sizeof(float); j++) {
            if (j != 0) {
                std::cout << ", ";
            }
            std::cout << ((float *) p)[i * 3 + j];
        }
        std::cout << ");";
    }
    std::cout << std::endl;
    std::cout << std::endl;
}


class ReadFontTest : public ::testing::Test {
public:
    uint64_t SplitGlyphCallback(uint16_t glyphId, milestro::skia::Font *font, SkRect bound, SkSize advance) {
        milestro::skia::Path *path;
        MilestroSkiaFontGetPath(font, path, glyphId);

        SkPath glyphPath;
        if (!font->unwrap().getPath(glyphId, &glyphPath)) {
            return -1;
        }

        std::cout << "bound: (" << bound.left() << ", " << bound.top() << ", " << bound.right()
                  << ", " << bound.bottom() << ")" << std::endl;

        SkRect clipBounds = glyphPath.getBounds();
        std::cout << "clipBounds: (" << clipBounds.left() << ", " << clipBounds.top() << ", " << clipBounds.right()
                  << ", " << clipBounds.bottom() << ")" << std::endl;

        GrCpuVertexAllocator alloc;
        auto trianglesResult = GrAATriangulator::PathToAATriangles(glyphPath, 0.1, clipBounds, &alloc);
        std::cout << "trianglesResult: " << trianglesResult << std::endl;

        auto vertexData = alloc.detachVertexData();
//        printVertexData(vertexData);

        return 0;
    }

protected:
    void SetUp() override {
        // 获取 FontManager 实例
        fontManager = milestro::skia::GetFontManager();

        // 设置字体目录路径
        imageDir = fs::current_path() / "data" / "font";

        registeredCount = registerFontsInDirectory(fontManager, imageDir.string());
    }

    void TearDown() override {
        // 如果需要清理，可以在这里添加清理代码
    }

    // 调用被测试的函数
    int registeredCount = 0;
    milestro::skia::FontManager *fontManager{};
    fs::path imageDir;
};

TEST_F(ReadFontTest, RegistersFontsCorrectly) {
    // 捕获 std::cout 和 std::cerr
    std::stringstream capturedStdout;
    std::stringstream capturedStderr;
    std::streambuf *oldCout = std::cout.rdbuf(capturedStdout.rdbuf());
    std::streambuf *oldCerr = std::cerr.rdbuf(capturedStderr.rdbuf());



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

TEST_F(ReadFontTest, HandlesNonExistentDirectory) {
    std::string nonExistentPath = (fs::current_path() / "non_existent_dir").string();

    std::stringstream capturedStderr;
    std::streambuf *oldCerr = std::cerr.rdbuf(capturedStderr.rdbuf());

    int registeredCount = registerFontsInDirectory(fontManager, nonExistentPath);

    std::cerr.rdbuf(oldCerr);

    EXPECT_EQ(registeredCount, 0);
    EXPECT_TRUE(capturedStderr.str().find("Font directory does not exist") != std::string::npos);
}

TEST_F(ReadFontTest, HandlesEmptyDirectory) {
    // 创建一个临时的空目录
    fs::path emptyDir = fs::temp_directory_path() / "empty_font_dir";
    fs::create_directory(emptyDir);

    int registeredCount = registerFontsInDirectory(fontManager, emptyDir.string());

    EXPECT_EQ(registeredCount, 0);

    // 清理临时目录
    fs::remove(emptyDir);
}


TEST_F(ReadFontTest, splitGlyph) {
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
        return ((ReadFontTest *) ctx)->SplitGlyphCallback(glyphId, font, rect, advance);
    });

#ifdef MILESTRO_USE_CLI
    milestro::skia::Canvas canvas(1920, 1080, nullptr);
    paragraph->paint(&canvas, 100, 100);
    canvas.SaveToPng("locale-test.png");
#endif
}

TEST_F(ReadFontTest, paragraphToPath) {
    auto familyNames = fontManager->GetFamiliesNames();
    EXPECT_TRUE(std::find(familyNames.begin(), familyNames.end(), "Source Han Sans VF") != familyNames.end());

    auto textStyle = std::make_unique<TextStyle>();
    std::vector<SkString> fontFamilies;
    fontFamilies.emplace_back("Source Han Sans VF");

    textStyle->setFontFamilies(fontFamilies);
    textStyle->setFontSize(72);
    textStyle->setColor(SK_ColorWHITE);

    auto paragraphStyle = std::make_unique<ParagraphStyle>();
    paragraphStyle->setTextStyle(textStyle.get());
    auto paragraphBuilder = std::make_unique<ParagraphBuilder>(paragraphStyle.get());

    textStyle->setLocale(SkString("zh-Hans"));
    std::string payload("心中有光，闪耀四方。");
    paragraphBuilder->addText(payload.c_str(), payload.size());

//    std::vector<std::string> locales = {"ko", "ja", "zh-Hant", "zh-Hans"};
//    for (const auto &locale: locales) {
//        textStyle->setLocale(SkString(locale.c_str()));
//        paragraphBuilder->pushStyle(textStyle.get());
//        std::string payload("曜\n");
//        paragraphBuilder->addText(payload.c_str(), payload.size());
//    }

    auto paragraph = paragraphBuilder->build();
    paragraph->layout(1600);
    auto path = paragraph->toPath(0, 0);
    auto bound = path->unwrap().getBounds();
    std::cout << "bound: (" << bound.left() << ", " << bound.top() << ", " << bound.right()
              << ", " << bound.bottom() << ")" << std::endl;
    auto triangles = path->ToAATriangles(0.1);
    auto vd = triangles->unwrap();
    printVertexData(vd);
}

TEST_F(ReadFontTest, paragraphToSdf) {
    auto familyNames = fontManager->GetFamiliesNames();
    EXPECT_TRUE(std::find(familyNames.begin(), familyNames.end(), "Source Han Sans VF") != familyNames.end());

    auto textStyle = std::make_unique<TextStyle>();
    std::vector<SkString> fontFamilies;
    fontFamilies.emplace_back("Source Han Sans VF");

    textStyle->setFontFamilies(fontFamilies);
    textStyle->setFontSize(72);
    textStyle->setColor(SK_ColorWHITE);

    auto paragraphStyle = std::make_unique<ParagraphStyle>();
    paragraphStyle->setTextStyle(textStyle.get());

    auto paragraphBuilder = std::make_unique<ParagraphBuilder>(paragraphStyle.get());
    textStyle->setLocale(SkString("zh-Hans"));
    paragraphBuilder->pushStyle(textStyle.get());
    std::string payload = "滴答。\n"
                          "滴答、滴答、滴答……\n"
                          "这座城市被滴答声淹没了。\n"
                          "雨下个不停。\n"
                          "我站在天台楼梯间的房檐下，无数的雨滴落在地上激起一层层波纹，它们似乎在演奏着一场演出时间为永久的交响乐。\n"
                          "早在我出生前，这场雨就已经开始了，而且据我上一辈的人说，早在他们出生之前，这场雨也已经在下了。\n"
                          "雨下个不停，它公平地落下，落在所有地方，带走每一个被它触摸到的人，将人们引入雨的世界。在旁人眼中，这些被雨滴带走的人就像是睡着了一样，做着永远不会结束的梦。\n"
                          "人们将自己藏在室内，用古老结实的钢筋混凝土铸成庇护所，用厚实防水的尼龙布料制成衣物，为了不陷入永恒的梦。\n"
                          "我看着人们在雨的世界里惶恐地活着，身边的人们日复一日地做着同样的事:收集食物、缝补衣物、维护房屋……\n"
                          "我并不知道到底是梦中的世界更加幸福，还是现实的世界更好，我只是静静地听着雨的话语，淅淅沥沥，哗哗啦啦，我静静地听着。\n"
                          "滴答、滴答、滴答……\n"
                          "雨说。";
    paragraphBuilder->addText(payload.c_str(), payload.size());

    std::vector<std::string> locales = {"ko", "ja", "zh-Hant", "zh-Hans"};
    for (const auto &locale: locales) {
        textStyle->setLocale(SkString(locale.c_str()));
        paragraphBuilder->pushStyle(textStyle.get());
        std::string payload = locale + ": 曜\n";
        paragraphBuilder->addText(payload.c_str(), payload.size());
    }

    auto paragraph = paragraphBuilder->build();
    paragraph->layout(1920 - 200);

    auto dfWidth = (1920 *2+ 2 * SK_DistanceFieldPad);
    auto dfHeight = (1080 *2+ 2 * SK_DistanceFieldPad);
    std::vector<uint8_t> distanceField(dfWidth * dfHeight);
    auto err = paragraph->toSDF(1920 * 2, 1080 * 2, 2, 100, 100, distanceField.data());
    ASSERT_GE(err, 0);

#ifdef MILESTRO_USE_CLI
    std::vector<uint32_t> bitmap(dfWidth * dfHeight);
    for (int x = 0; x < dfHeight; x++) {
        for (int y = 0; y < dfWidth; y++) {
            auto idx = x * dfWidth + y;
            auto t = distanceField[idx];
            bitmap[idx] = t << 24 | t << 16 | t << 8 | t;
        }
    }

    milestro::skia::Canvas canvas(dfWidth, dfHeight, bitmap.data());
    canvas.SaveToPng("sdf-test.png");
#endif
}

int main(int argc, char **argv) {
    std::cout << "Milestro Test - Read Font" << std::endl;

    testing::InitGoogleTest(&argc, argv);
    return RUN_ALL_TESTS();
}
