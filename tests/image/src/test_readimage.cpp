#include "Milestro/skia/textlayout/ParagraphBuilder.h"
#include "Milestro/game/milestro_game_interface.h"
#include "Milestro/io/milestro_io.h"
#include "Milestro/util/milestro_encoding.h"
#include <filesystem>
#include <gtest/gtest.h>
#include <iostream>
#include <string>
#include <vector>

namespace fs = std::filesystem;

class ReadImageTest : public ::testing::Test {
protected:
    void SetUp() override {
        imageDir = fs::current_path() / "data" / "image";
    }

    void TearDown() override {
    }

    void TestDrawSimpleImage(std::string name) {
        milestro::skia::Image *img;
        auto data = milestro::io::readFile(
            milestro::util::encoding::WStringToString((imageDir / name).wstring())
        );
        EXPECT_GE(MilestroSkiaImageCreate(img, data.data(), data.size()), 0);
        data.clear();

        EXPECT_GE(MilestroSkiaImageSetColorType(img, 4 /* kRGBA_8888_SkColorType */), 0);

        int width;
        EXPECT_GE(MilestroSkiaImageGetWidth(img, width), 0);
        int height;
        EXPECT_GE(MilestroSkiaImageGetHeight(img, height), 0);

        milestro::skia::Canvas canvas(width, height, nullptr);
        EXPECT_GE(MilestroSkiaCanvasDrawImageSimple(&canvas, img, 0, 0), 0);
#ifdef MILESTRO_USE_CLI
        canvas.SaveToPng((name + ".simple.png").c_str());
#endif
        EXPECT_GE(MilestroSkiaImageDestroy(img), 0);
    }

    void TestDrawImage(std::string name) {
        milestro::skia::Image *img;
        auto data = milestro::io::readFile(
            milestro::util::encoding::WStringToString((imageDir / name).wstring())
        );
        EXPECT_GE(MilestroSkiaImageCreate(img, data.data(), data.size()), 0);
        data.clear();

        EXPECT_GE(MilestroSkiaImageSetColorType(img, 4 /* kRGBA_8888_SkColorType */), 0);

        int width;
        EXPECT_GE(MilestroSkiaImageGetWidth(img, width), 0);
        int height;
        EXPECT_GE(MilestroSkiaImageGetHeight(img, height), 0);

        milestro::skia::Canvas canvas(width / 2, height / 2, nullptr);
        EXPECT_GE(MilestroSkiaCanvasDrawImage(&canvas, img,
                                              0, 0, width, height,
                                              0, 0, width / 2, height / 2
        ), 0);
#ifdef MILESTRO_USE_CLI
        canvas.SaveToPng((name + ".full.png").c_str());
#endif

        EXPECT_GE(MilestroSkiaImageDestroy(img), 0);
    }


    void TestDrawSimpleImageYFlipped(std::string name) {
        milestro::skia::Image *img;
        auto data = milestro::io::readFile(
                milestro::util::encoding::WStringToString((imageDir / name).wstring())
        );
        EXPECT_GE(MilestroSkiaImageCreate(img, data.data(), data.size()), 0);
        data.clear();

        EXPECT_GE(MilestroSkiaImageSetColorType(img, 4 /* kRGBA_8888_SkColorType */), 0);

        int width;
        EXPECT_GE(MilestroSkiaImageGetWidth(img, width), 0);
        int height;
        EXPECT_GE(MilestroSkiaImageGetHeight(img, height), 0);

        std::vector<uint8_t> pixels(width * height * 4);
        milestro::skia::Canvas canvas(width, height, pixels.data(), false, true);
        EXPECT_GE(MilestroSkiaCanvasDrawImageSimple(&canvas, img, 0, 0), 0);
#ifdef MILESTRO_USE_CLI
        canvas.SaveToPng((name + ".simple.yflipped.png").c_str());
#endif
        EXPECT_GE(MilestroSkiaImageDestroy(img), 0);
    }

    fs::path imageDir;
};

TEST_F(ReadImageTest, DrawSimpleImage) {
    TestDrawSimpleImage("a_reincarnation_of_a_scattering_spring.jpg");
    TestDrawSimpleImage("bg_day_character.png");
    TestDrawSimpleImage("test-large.avif");
    TestDrawSimpleImage("test-small.avif");
}

TEST_F(ReadImageTest, DrawImage) {
    TestDrawImage("a_reincarnation_of_a_scattering_spring.jpg");
    TestDrawImage("bg_day_character.png");
    TestDrawImage("test-large.avif");
    TestDrawImage("test-small.avif");
}

TEST_F(ReadImageTest, DrawSimpleImageYFlipped) {
    TestDrawSimpleImageYFlipped("a_reincarnation_of_a_scattering_spring.jpg");
    TestDrawSimpleImageYFlipped("bg_day_character.png");
    TestDrawSimpleImageYFlipped("test-large.avif");
    TestDrawSimpleImageYFlipped("test-small.avif");
}

int main(int argc, char **argv) {
    std::cout << "Milestro Test - Read Image" << std::endl;

    testing::InitGoogleTest(&argc, argv);
    return RUN_ALL_TESTS();
}
