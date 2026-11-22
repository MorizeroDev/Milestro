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
    const auto checkDefaultIgnorables = [](SkUnicode* unicode) {
        for (const SkUnichar codepoint : {0x00AD, 0x034F, 0x061C, 0x200B, 0x200D,
                                         0x2068, 0x2069, 0xFE0F, 0xE0100}) {
            EXPECT_TRUE(unicode->isDefaultIgnorable(codepoint)) << codepoint;
        }
        for (const SkUnichar codepoint : {-1, 0x0000, 0x0020, 0x0041, 0x0301,
                                         0x0600, 0x4E00, 0x1F600, 0x110000}) {
            EXPECT_FALSE(unicode->isDefaultIgnorable(codepoint)) << codepoint;
        }
        char utf8[] = "A\xC2\xAD\xE2\x80\x8B\xF3\xA0\x84\x80" "B";
        skia_private::TArray<SkUnicode::CodeUnitFlags, true> utf8Flags;
        ASSERT_TRUE(unicode->computeCodeUnitFlags(utf8, sizeof(utf8) - 1, false, &utf8Flags));
        ASSERT_EQ(utf8Flags.size(), sizeof(utf8));
        for (size_t index = 0; index < sizeof(utf8) - 1; ++index) {
            EXPECT_EQ((utf8Flags[index] & SkUnicode::kDefaultIgnorable) != 0,
                      index > 0 && index < sizeof(utf8) - 2) << index;
        }
        char16_t utf16[] = u"A\u00AD\u200B\U000E0100B";
        skia_private::TArray<SkUnicode::CodeUnitFlags, true> utf16Flags;
        ASSERT_TRUE(unicode->computeCodeUnitFlags(utf16, 6, false, &utf16Flags));
        ASSERT_EQ(utf16Flags.size(), 7);
        for (size_t index = 0; index < 6; ++index) {
            EXPECT_EQ((utf16Flags[index] & SkUnicode::kDefaultIgnorable) != 0,
                      index > 0 && index < 5) << index;
        }
    };

    auto provider = milestro::skia::GetUnicodeProvider();
    ASSERT_NE(provider, nullptr);

    auto unicode = provider->unwrap();
    ASSERT_NE(unicode, nullptr);

    EXPECT_TRUE(unicode->isWhitespace(' '));
    EXPECT_TRUE(unicode->isHardBreak('\n'));
    checkDefaultIgnorables(unicode.get());
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
    checkDefaultIgnorables(upgradedUnicode.get());
    EXPECT_STREQ(upgradedUnicode->toUpper(SkString("istanbul"), "tr").c_str(), "\xC4\xB0STANBUL");

    auto upgradedIterator = upgradedUnicode->makeBreakIterator("en-US", SkUnicode::BreakType::kWords);
    ASSERT_NE(upgradedIterator, nullptr);
    ASSERT_TRUE(upgradedIterator->setText(text, static_cast<int>(std::strlen(text))));
    EXPECT_EQ(upgradedIterator->first(), 0);
    EXPECT_GT(upgradedIterator->next(), 0);
}

TEST(SkiaUnicodeFallbackTest, Utf16IterationReplacesUnpairedSurrogates) {
    auto provider = milestro::skia::GetUnicodeProvider();
    ASSERT_NE(provider, nullptr);
    auto unicode = provider->unwrap();
    ASSERT_NE(unicode, nullptr);

    for (const char16_t surrogate : {char16_t(0xD800), char16_t(0xDC00)}) {
        std::vector<SkUnichar> codepoints;
        unicode->forEachCodepoint(&surrogate, 1, [&](SkUnichar codepoint, int start, int end) {
            codepoints.push_back(codepoint);
            EXPECT_EQ(start, 0);
            EXPECT_EQ(end, 1);
        });
        EXPECT_EQ(codepoints, std::vector<SkUnichar>{0xFFFD});
    }
}
