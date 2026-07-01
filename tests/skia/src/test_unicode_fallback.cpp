#include "../../../include/Milestro/skia/unicode/Unicode.h"
#include "Milestro/game/milestro_game_interface.h"
#include "Milestro/game/milestro_game_retcode.h"
#include "include/core/SkString.h"
#include "modules/skunicode/include/SkUnicode.h"

#include <cstring>
#include <fstream>
#include <gtest/gtest.h>
#include <vector>

TEST(SkiaUnicodeFallbackTest, ProviderExistsWhenIcuDataWasNotLoaded) {
    auto provider = milestro::skia::GetUnicodeProvider();
    ASSERT_NE(provider, nullptr);

    auto unicode = provider->unwrap();
    ASSERT_NE(unicode, nullptr);

    EXPECT_TRUE(unicode->isWhitespace(' '));
    EXPECT_TRUE(unicode->isHardBreak('\n'));
    EXPECT_STREQ(unicode->toUpper(SkString("istanbul"), "tr").c_str(), "istanbul");

    auto iterator = unicode->makeBreakIterator("en-US", SkUnicode::BreakType::kWords);
    ASSERT_NE(iterator, nullptr);
    const char text[] = "hello world";
    ASSERT_TRUE(iterator->setText(text, static_cast<int>(std::strlen(text))));
    EXPECT_EQ(iterator->first(), 0);
    EXPECT_GT(iterator->next(), 0);

    std::vector<SkUnicode::BidiRegion> bidiRegions;
    EXPECT_TRUE(unicode->getBidiRegions(text,
                                        static_cast<int>(std::strlen(text)),
                                        SkUnicode::TextDirection::kLTR,
                                        &bidiRegions));
    ASSERT_EQ(bidiRegions.size(), 1);
    EXPECT_EQ(bidiRegions[0].start, 0u);
    EXPECT_EQ(bidiRegions[0].end, std::strlen(text));
    EXPECT_EQ(bidiRegions[0].level, 0);

    char flaggedText[] = "a\tb\n";
    skia_private::TArray<SkUnicode::CodeUnitFlags, true> flags;
    EXPECT_TRUE(unicode->computeCodeUnitFlags(flaggedText,
                                             static_cast<int>(std::strlen(flaggedText)),
                                             true,
                                             &flags));
    ASSERT_EQ(flags.size(), std::strlen(flaggedText) + 1);
    EXPECT_EQ(flaggedText[1], ' ');
    EXPECT_TRUE(SkUnicode::hasGraphemeStartFlag(flags[0]));
    EXPECT_TRUE(SkUnicode::hasTabulationFlag(flags[1]));
    EXPECT_TRUE(SkUnicode::hasPartOfWhiteSpaceBreakFlag(flags[1]));
    EXPECT_TRUE(SkUnicode::hasHardLineBreakFlag(flags[4]));

    std::ifstream icudtl(MILESTRO_TEST_ICUDTL_PATH, std::ios::binary);
    ASSERT_TRUE(icudtl.is_open()) << MILESTRO_TEST_ICUDTL_PATH;
    std::vector<uint8_t> data((std::istreambuf_iterator<char>(icudtl)),
                              std::istreambuf_iterator<char>());
    ASSERT_FALSE(data.empty());
    ASSERT_EQ(MilestroCopyAndLoadICU(data.data(), data.size(), nullptr), MILESTRO_API_RET_OK);

    auto upgradedProvider = milestro::skia::GetUnicodeProvider();
    ASSERT_NE(upgradedProvider, nullptr);
    auto upgradedUnicode = upgradedProvider->unwrap();
    ASSERT_NE(upgradedUnicode, nullptr);
    EXPECT_STREQ(upgradedUnicode->toUpper(SkString("istanbul"), "tr").c_str(), "\xC4\xB0STANBUL");

    auto upgradedIterator = upgradedUnicode->makeBreakIterator("en-US", SkUnicode::BreakType::kWords);
    ASSERT_NE(upgradedIterator, nullptr);
    ASSERT_TRUE(upgradedIterator->setText(text, static_cast<int>(std::strlen(text))));
    EXPECT_EQ(upgradedIterator->first(), 0);
    EXPECT_GT(upgradedIterator->next(), 0);
}
