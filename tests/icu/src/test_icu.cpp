#include "../../../include/Milestro/skia/unicode/Unicode.h"
#include "Milestro/game/milestro_game_interface.h"
#include "Milestro/game/milestro_game_retcode.h"
#include "include/core/SkString.h"
#include "modules/skunicode/include/SkUnicode.h"
#include <filesystem>
#include <fstream>
#include <gtest/gtest.h>
#include <iostream>
#include <vector>

namespace fs = std::filesystem;

class IcuTest : public ::testing::Test {
protected:
    static void SetUpTestSuite() {
        std::ifstream icudtl(MILESTRO_TEST_ICUDTL_PATH, std::ios::binary);
        ASSERT_TRUE(icudtl.is_open()) << MILESTRO_TEST_ICUDTL_PATH;
        std::vector<uint8_t> data((std::istreambuf_iterator<char>(icudtl)),
                                  std::istreambuf_iterator<char>());
        ASSERT_FALSE(data.empty());
        ASSERT_EQ(MilestroCopyAndLoadICU(data.data(), data.size(), nullptr), MILESTRO_API_RET_OK);
    }

    void SetUp() override {
    }

    void TearDown() override {
    }
};

TEST_F(IcuTest, CollationPinyinTest) {
    milestro::unicode::StringComparator* col = nullptr;
    ASSERT_EQ(MilestroStringComparatorCreate(col, reinterpret_cast<uint8_t*>(const_cast<char*>("zh-CN@collation=pinyin"))),
              MILESTRO_API_RET_OK);

    int32_t result = 0;
    EXPECT_EQ(MilestroStringComparatorCompare(col, result, reinterpret_cast<uint8_t*>(const_cast<char*>("你")),
                                            reinterpret_cast<uint8_t*>(const_cast<char*>("我"))),
              MILESTRO_API_RET_OK);
    EXPECT_LE(result, 0);
    EXPECT_EQ(MilestroStringComparatorCompare(col, result, reinterpret_cast<uint8_t*>(const_cast<char*>("我")),
                                            reinterpret_cast<uint8_t*>(const_cast<char*>("他"))),
              MILESTRO_API_RET_OK);
    EXPECT_GE(result, 0);
    EXPECT_EQ(MilestroStringComparatorDestroy(col), MILESTRO_API_RET_OK);
}

TEST_F(IcuTest, CollationZhuyinTest) {
    milestro::unicode::StringComparator* col = nullptr;
    ASSERT_EQ(MilestroStringComparatorCreate(col, reinterpret_cast<uint8_t*>(const_cast<char*>("zh-TW@collation=zhuyin"))),
              MILESTRO_API_RET_OK);

    int32_t result = 0;
    EXPECT_EQ(MilestroStringComparatorCompare(col, result, reinterpret_cast<uint8_t*>(const_cast<char*>("譁眾取寵")),
                                            reinterpret_cast<uint8_t*>(const_cast<char*>("至高無上"))),
              MILESTRO_API_RET_OK);
    EXPECT_LE(result, 0);
    EXPECT_EQ(MilestroStringComparatorCompare(col, result, reinterpret_cast<uint8_t*>(const_cast<char*>("色彩缤纷")),
                                            reinterpret_cast<uint8_t*>(const_cast<char*>("博览群书"))),
              MILESTRO_API_RET_OK);
    EXPECT_GE(result, 0);
    EXPECT_EQ(MilestroStringComparatorDestroy(col), MILESTRO_API_RET_OK);
}

TEST_F(IcuTest, SkUnicodeProviderUsesIcuBreakIterator) {
    auto provider = milestro::skia::GetUnicodeProvider();
    ASSERT_NE(provider, nullptr);

    auto unicode = provider->unwrap();
    ASSERT_NE(unicode, nullptr);
    EXPECT_TRUE(unicode->isWhitespace(' '));

    SkString upper = unicode->toUpper(SkString("istanbul"), "tr");
    EXPECT_STREQ(upper.c_str(), "İSTANBUL");

    auto iterator = unicode->makeBreakIterator("en-US", SkUnicode::BreakType::kWords);
    ASSERT_NE(iterator, nullptr);
    const char text[] = "hello world";
    ASSERT_TRUE(iterator->setText(text, static_cast<int>(std::char_traits<char>::length(text))));
    EXPECT_EQ(iterator->first(), 0);
    EXPECT_GT(iterator->next(), 0);
}

int main(int argc, char **argv) {
    std::cout << "Milestro Test - Icu Collation" << std::endl;

    testing::InitGoogleTest(&argc, argv);
    return RUN_ALL_TESTS();
}
