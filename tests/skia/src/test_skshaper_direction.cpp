#include "Milestro/skia/unicode/Unicode.h"
#include "include/core/SkFont.h"
#include "include/core/SkFontMgr.h"
#include "include/core/SkTypeface.h"
#include "include/ports/SkFontMgr_empty.h"
#include "modules/skshaper/include/SkShaper.h"
#include "modules/skshaper/include/SkShaper_harfbuzz.h"

#include <string_view>
#include <vector>

#include <gtest/gtest.h>

namespace {

class RecordingRunHandler final : public SkShaper::RunHandler {
public:
    void beginLine() override {
    }
    void runInfo(const RunInfo&) override {
    }
    void commitRunInfo() override {
    }

    Buffer runBuffer(const RunInfo& info) override {
        glyphs.resize(info.glyphCount);
        positions.resize(info.glyphCount);
        clusters.resize(info.glyphCount);
        return {glyphs.data(), positions.data(), nullptr, clusters.data(), {0.0f, 0.0f}};
    }

    void commitRunBuffer(const RunInfo& info) override {
        advances.push_back(info.fAdvance);
        levels.push_back(info.fBidiLevel);
    }

    void commitLine() override {
    }

    std::vector<SkGlyphID> glyphs;
    std::vector<SkPoint> positions;
    std::vector<uint32_t> clusters;
    std::vector<SkVector> advances;
    std::vector<uint8_t> levels;
};

class SkShaperDirection : public ::testing::Test {
protected:
    void SetUp() override {
        auto manager = SkFontMgr_New_Custom_Empty();
        auto typeface = manager->makeFromFile(MILESTRO_TEST_FONT_PATH);
        ASSERT_NE(typeface, nullptr) << MILESTRO_TEST_FONT_PATH;
        font = SkFont(typeface, 48.0f);
        font.setSubpixel(true);
        font.setHinting(SkFontHinting::kNone);
    }

    SkFont font;
};

TEST_F(SkShaperDirection, CurrentSkShaperApiCannotRequestTtb) {
    constexpr std::string_view text = "「漢字」、。ー";
    auto* unicodeProvider = milestro::skia::GetUnicodeProvider();
    ASSERT_NE(unicodeProvider, nullptr);
    auto unicode = unicodeProvider->unwrap();
    ASSERT_NE(unicode, nullptr);
    auto shaper = SkShapers::HB::ShapeThenWrap(unicode, nullptr);
    ASSERT_NE(shaper, nullptr);

    SkShaper::TrivialFontRunIterator fontRun(font, text.size());
    SkShaper::TrivialBiDiRunIterator bidiRun(0, text.size());
    SkShaper::TrivialScriptRunIterator scriptRun(SkSetFourByteTag('H', 'a', 'n', 'i'), text.size());
    SkShaper::TrivialLanguageRunIterator languageRun("ja", text.size());
    RecordingRunHandler handler;

    shaper->shape(text.data(), text.size(), fontRun, bidiRun, scriptRun, languageRun, nullptr, 0, 4096.0f, &handler);

    ASSERT_FALSE(handler.advances.empty());
    for (const SkVector advance: handler.advances) {
        EXPECT_GT(advance.x(), 0.0f);
        EXPECT_EQ(advance.y(), 0.0f);
    }
    for (const uint8_t level: handler.levels) {
        EXPECT_EQ(level, 0u);
    }
}

} // namespace
