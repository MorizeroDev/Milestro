#include "Milestro/game/milestro_game_interface.h"
#include "Milestro/skia/textlayout/ParagraphBuilder.h"
#include "Milestro/util/milestro_encoding.h"
#include <filesystem>
#include <gtest/gtest.h>
#include <iostream>
#include <vector>

namespace fs = std::filesystem;

class IcuTest : public ::testing::Test {
protected:
    void SetUp() override {
    }

    void TearDown() override {
    }
};

TEST_F(IcuTest, CollationPinyinTest) {
    milestro::icu::IcuUCollator *col;
    EXPECT_GE(MilestroIcuIcuUCollatorCreate(col, (uint8_t *) "zh-CN@collation=pinyin"), 0);
    int32_t result;
    EXPECT_GE(MilestroIcuIcuUCollatorCompare(col, result, (uint8_t *) "你", (uint8_t *) "我"), 0);
    EXPECT_LE(result, 0);
    EXPECT_GE(MilestroIcuIcuUCollatorCompare(col, result, (uint8_t *) "我", (uint8_t *) "他"), 0);
    EXPECT_GE(result, 0);
}

TEST_F(IcuTest, CollationZhuyinTest) {
    milestro::icu::IcuUCollator *col;
    EXPECT_GE(MilestroIcuIcuUCollatorCreate(col, (uint8_t *) "zh-TW@collation=zhuyin"), 0);
    int32_t result;
    EXPECT_GE(MilestroIcuIcuUCollatorCompare(col, result, (uint8_t *) "譁眾取寵", (uint8_t *) "至高無上"), 0);
    EXPECT_LE(result, 0);
    EXPECT_GE(MilestroIcuIcuUCollatorCompare(col, result, (uint8_t *) "色彩缤纷", (uint8_t *) "博览群书"), 0);
    EXPECT_GE(result, 0);
}

int main(int argc, char **argv) {
    std::cout << "Milestro Test - Icu Collation" << std::endl;

    testing::InitGoogleTest(&argc, argv);
    return RUN_ALL_TESTS();
}
