#include "../../../include/Milestro/skia/FontRegistry.h"
#include "../../../include/Milestro/skia/textlayout/FontCollection.h"

#include "include/core/SkFont.h"
#include "include/core/SkFontArguments.h"
#include "include/core/SkFontStyle.h"
#include "include/core/SkPath.h"
#include "include/core/SkSpan.h"
#include "include/core/SkString.h"
#include <algorithm>
#include <cstdint>
#include <gtest/gtest.h>
#include <optional>
#include <vector>

namespace milestro_skia = milestro::skia;
namespace milestro_textlayout = milestro::skia::textlayout;

namespace {

std::optional<SkScalar> GetVariationCoordinate(const sk_sp<SkTypeface>& typeface, SkFourByteTag axis) {
    if (typeface == nullptr) {
        return std::nullopt;
    }

    const int coordinateCount = typeface->getVariationDesignPosition({});
    if (coordinateCount <= 0) {
        return std::nullopt;
    }

    std::vector<SkFontArguments::VariationPosition::Coordinate> coordinates(coordinateCount);
    const int coordinatesRead = typeface->getVariationDesignPosition(SkSpan(coordinates.data(), coordinates.size()));
    if (coordinatesRead <= 0) {
        return std::nullopt;
    }

    const auto end = coordinates.begin() + std::min(coordinatesRead, coordinateCount);
    const auto found = std::find_if(coordinates.begin(), end, [axis](const auto& coordinate) {
        return coordinate.axis == axis;
    });
    return found == end ? std::nullopt : std::optional<SkScalar>(found->value);
}

sk_sp<SkTypeface> FindTypefaceForStyle(milestro_textlayout::FontCollection* collection,
                                       const char* familyName,
                                       const SkFontStyle& style) {
    const std::vector<SkString> families{SkString(familyName)};
    const auto typefaces = collection->unwrap()->findTypefaces(families, style);
    return typefaces.empty() ? nullptr : typefaces.front();
}

sk_sp<SkTypeface>
FindTypefaceForWeight(milestro_textlayout::FontCollection* collection, const char* familyName, int weight) {
    return FindTypefaceForStyle(collection,
                                familyName,
                                SkFontStyle(weight, SkFontStyle::kNormal_Width, SkFontStyle::kUpright_Slant));
}

void ExpectVariationCoordinate(const sk_sp<SkTypeface>& typeface, SkFourByteTag axis, SkScalar expected) {
    const auto coordinate = GetVariationCoordinate(typeface, axis);
    ASSERT_TRUE(coordinate.has_value());
    EXPECT_FLOAT_EQ(*coordinate, expected);
}

uint64_t Fnv1a(const void* data, size_t size) {
    const auto* bytes = static_cast<const uint8_t*>(data);
    uint64_t hash = 14695981039346656037ull;
    for (size_t index = 0; index < size; ++index) {
        hash ^= bytes[index];
        hash *= 1099511628211ull;
    }
    return hash;
}

std::optional<uint64_t> GlyphPathHash(const sk_sp<SkTypeface>& typeface, SkUnichar character) {
    SkFont font(typeface, 64.0f);
    const auto path = font.getPath(font.unicharToGlyph(character));
    if (!path.has_value()) {
        return std::nullopt;
    }
    const auto data = path->serialize();
    return data == nullptr ? std::nullopt : std::optional<uint64_t>(Fnv1a(data->data(), data->size()));
}

int GetStreamIndex(const sk_sp<SkTypeface>& typeface) {
    int index = -1;
    return typeface != nullptr && typeface->openStream(&index) != nullptr ? index : -1;
}

} // namespace

TEST(SkiaVariableFontWeightTest, RegisteredVariableFontUsesContinuousWeightCoordinate) {
    auto registry = milestro_skia::GetFontRegistry();
    auto collection = milestro_textlayout::GetFontCollection();
    ASSERT_NE(registry, nullptr);
    ASSERT_NE(collection, nullptr);
    ASSERT_NE(registry->RegisterFontFromFile(MILESTRO_TEST_FONT_PATH),
              milestro_skia::MilestroRegisteredFontMgr::RegisterResult::Failed);
    collection->clearCaches();

    const auto styleSet = registry->GetRegisteredFontMgr()->matchFamily("Source Han Sans VF");
    ASSERT_NE(styleSet, nullptr);
    ASSERT_GT(styleSet->count(), 0);

    sk_sp<SkTypeface> namedRegular;
    sk_sp<SkTypeface> namedMedium;
    for (int index = 0; index < styleSet->count(); ++index) {
        SkFontStyle style;
        styleSet->getStyle(index, &style, nullptr);
        if (style.weight() == 400) {
            namedRegular = styleSet->createTypeface(index);
        } else if (style.weight() == 500) {
            namedMedium = styleSet->createTypeface(index);
        }
    }
    ASSERT_NE(namedRegular, nullptr);
    ASSERT_NE(namedMedium, nullptr);

    struct Case {
        int requested;
        int expected;
    };
    const Case cases[] = {
            {1, 250},   {249, 250}, {250, 250}, {251, 251}, {299, 299},  {300, 300}, {399, 399},
            {400, 400}, {401, 401}, {499, 499}, {500, 500}, {501, 501},  {699, 699}, {700, 700},
            {701, 701}, {899, 899}, {900, 900}, {901, 900}, {1000, 900},
    };

    for (const auto& testCase: cases) {
        SCOPED_TRACE(testCase.requested);
        const auto typeface = FindTypefaceForWeight(collection, "Source Han Sans VF", testCase.requested);
        ASSERT_NE(typeface, nullptr);
        const auto coordinate = GetVariationCoordinate(typeface, SkFontArguments::VariationPosition::Coordinate::wght);
        ASSERT_TRUE(coordinate.has_value());
        EXPECT_FLOAT_EQ(*coordinate, static_cast<SkScalar>(testCase.expected));
        EXPECT_EQ(typeface->fontStyle().weight(), testCase.expected);
    }

    const auto regular = FindTypefaceForWeight(collection, "Source Han Sans VF", 400);
    const auto intermediate = FindTypefaceForWeight(collection, "Source Han Sans VF", 401);
    ASSERT_NE(regular, nullptr);
    ASSERT_NE(intermediate, nullptr);
    EXPECT_NE(regular->uniqueID(), namedMedium->uniqueID());
    EXPECT_NE(intermediate->uniqueID(), namedRegular->uniqueID());
    EXPECT_NE(intermediate->uniqueID(), namedMedium->uniqueID());
    const auto regularHash = GlyphPathHash(regular, 0x6c38);
    const auto intermediateHash = GlyphPathHash(intermediate, 0x6c38);
    ASSERT_TRUE(regularHash.has_value());
    ASSERT_TRUE(intermediateHash.has_value());
    EXPECT_NE(*regularHash, *intermediateHash);

    const auto repeated = FindTypefaceForWeight(collection, "Source Han Sans VF", 401);
    ASSERT_NE(repeated, nullptr);
    EXPECT_EQ(repeated->uniqueID(), intermediate->uniqueID());

    collection->clearCaches();
    const auto afterCollectionCacheClear = FindTypefaceForWeight(collection, "Source Han Sans VF", 401);
    ASSERT_NE(afterCollectionCacheClear, nullptr);
    EXPECT_EQ(afterCollectionCacheClear->uniqueID(), intermediate->uniqueID());
}

TEST(SkiaVariableFontWeightTest, RegisteredStaticFontKeepsDiscreteStyleMatching) {
    auto registry = milestro_skia::GetFontRegistry();
    auto collection = milestro_textlayout::GetFontCollection();
    ASSERT_NE(registry, nullptr);
    ASSERT_NE(collection, nullptr);
    ASSERT_NE(registry->RegisterFontFromFile(MILESTRO_TEST_EMOJI_FONT_PATH),
              milestro_skia::MilestroRegisteredFontMgr::RegisterResult::Failed);
    collection->clearCaches();

    const auto normal = FindTypefaceForWeight(collection, "Noto Color Emoji", 400);
    const auto arbitrary = FindTypefaceForWeight(collection, "Noto Color Emoji", 401);
    ASSERT_NE(normal, nullptr);
    ASSERT_NE(arbitrary, nullptr);
    EXPECT_EQ(GetVariationCoordinate(arbitrary, SkFontArguments::VariationPosition::Coordinate::wght), std::nullopt);
    EXPECT_EQ(arbitrary->uniqueID(), normal->uniqueID());
}

TEST(SkiaVariableFontWeightTest, IndependentVariableFontAndFallbackStateStayIsolated) {
    auto registry = milestro_skia::GetFontRegistry();
    auto collection = milestro_textlayout::GetFontCollection();
    ASSERT_NE(registry, nullptr);
    ASSERT_NE(collection, nullptr);
    ASSERT_NE(registry->RegisterFontFromFile(MILESTRO_TEST_FONT_PATH),
              milestro_skia::MilestroRegisteredFontMgr::RegisterResult::Failed);
    ASSERT_NE(registry->RegisterFontFromFile(MILESTRO_TEST_FIRA_FONT_PATH),
              milestro_skia::MilestroRegisteredFontMgr::RegisterResult::Failed);

    collection->setFontFallbackEnabled(true);
    collection->clearCaches();
    const auto sourceHan = FindTypefaceForWeight(collection, "Source Han Sans VF", 401);
    const auto fira = FindTypefaceForWeight(collection, "Fira Code", 401);
    ASSERT_NE(sourceHan, nullptr);
    ASSERT_NE(fira, nullptr);
    ExpectVariationCoordinate(sourceHan, SkFontArguments::VariationPosition::Coordinate::wght, 401.0f);
    ExpectVariationCoordinate(fira, SkFontArguments::VariationPosition::Coordinate::wght, 401.0f);
    EXPECT_NE(sourceHan->uniqueID(), fira->uniqueID());

    collection->setFontFallbackEnabled(false);
    collection->clearCaches();
    const auto withoutFallback = FindTypefaceForWeight(collection, "Source Han Sans VF", 401);
    ASSERT_NE(withoutFallback, nullptr);
    EXPECT_EQ(withoutFallback->uniqueID(), sourceHan->uniqueID());
    collection->setFontFallbackEnabled(true);
    collection->clearCaches();

    const int cases[] = {300, 399, 400, 401, 500, 501, 699, 700, 701};
    for (const int requested: cases) {
        SCOPED_TRACE(requested);
        const auto typeface = FindTypefaceForWeight(collection, "Fira Code", requested);
        ASSERT_NE(typeface, nullptr);
        const auto coordinate = GetVariationCoordinate(typeface, SkFontArguments::VariationPosition::Coordinate::wght);
        ASSERT_TRUE(coordinate.has_value());
        EXPECT_FLOAT_EQ(*coordinate, static_cast<SkScalar>(std::clamp(requested, 300, 700)));
    }
}

TEST(SkiaVariableFontWeightTest, MultiAxisFontPreservesSelectedWidthCoordinate) {
    auto registry = milestro_skia::GetFontRegistry();
    auto collection = milestro_textlayout::GetFontCollection();
    ASSERT_NE(registry, nullptr);
    ASSERT_NE(collection, nullptr);
    ASSERT_NE(registry->RegisterFontFromFile(MILESTRO_TEST_MULTI_AXIS_FONT_PATH),
              milestro_skia::MilestroRegisteredFontMgr::RegisterResult::Failed);
    collection->clearCaches();

    const auto condensed =
            FindTypefaceForStyle(collection,
                                 "Variable",
                                 SkFontStyle(401, SkFontStyle::kUltraCondensed_Width, SkFontStyle::kUpright_Slant));
    const auto expanded =
            FindTypefaceForStyle(collection,
                                 "Variable",
                                 SkFontStyle(401, SkFontStyle::kUltraExpanded_Width, SkFontStyle::kUpright_Slant));
    ASSERT_NE(condensed, nullptr);
    ASSERT_NE(expanded, nullptr);
    ExpectVariationCoordinate(condensed, SkFontArguments::VariationPosition::Coordinate::wght, 401.0f);
    ExpectVariationCoordinate(condensed, SkSetFourByteTag('w', 'd', 't', 'h'), 50.0f);
    ExpectVariationCoordinate(expanded, SkFontArguments::VariationPosition::Coordinate::wght, 401.0f);
    ExpectVariationCoordinate(expanded, SkSetFourByteTag('w', 'd', 't', 'h'), 200.0f);
    EXPECT_EQ(GetStreamIndex(condensed), 0);
    EXPECT_EQ(GetStreamIndex(expanded), 0);
    EXPECT_NE(condensed->uniqueID(), expanded->uniqueID());
}

TEST(SkiaVariableFontWeightTest, VariableCollectionFacesUseIndependentBaseAndCacheIdentity) {
    auto registry = milestro_skia::GetFontRegistry();
    auto collection = milestro_textlayout::GetFontCollection();
    ASSERT_NE(registry, nullptr);
    ASSERT_NE(collection, nullptr);
    ASSERT_NE(registry->RegisterFontFromFile(MILESTRO_TEST_VARIABLE_TTC_PATH),
              milestro_skia::MilestroRegisteredFontMgr::RegisterResult::Failed);
    collection->clearCaches();

    const auto japanese = FindTypefaceForWeight(collection, "Noto Sans CJK JP", 401);
    const auto korean = FindTypefaceForWeight(collection, "Noto Sans CJK KR", 401);
    ASSERT_NE(japanese, nullptr);
    ASSERT_NE(korean, nullptr);
    ExpectVariationCoordinate(japanese, SkFontArguments::VariationPosition::Coordinate::wght, 401.0f);
    ExpectVariationCoordinate(korean, SkFontArguments::VariationPosition::Coordinate::wght, 401.0f);
    EXPECT_EQ(GetStreamIndex(japanese), 0);
    EXPECT_EQ(GetStreamIndex(korean), 1);
    EXPECT_NE(japanese->uniqueID(), korean->uniqueID());

    collection->clearCaches();
    EXPECT_EQ(FindTypefaceForWeight(collection, "Noto Sans CJK JP", 401)->uniqueID(), japanese->uniqueID());
    EXPECT_EQ(FindTypefaceForWeight(collection, "Noto Sans CJK KR", 401)->uniqueID(), korean->uniqueID());
}

TEST(SkiaVariableFontWeightTest, StaticCollectionKeepsCssWeightSelection) {
    auto registry = milestro_skia::GetFontRegistry();
    auto collection = milestro_textlayout::GetFontCollection();
    ASSERT_NE(registry, nullptr);
    ASSERT_NE(collection, nullptr);
    ASSERT_NE(registry->RegisterFontFromFile(MILESTRO_TEST_STATIC_TTC_PATH),
              milestro_skia::MilestroRegisteredFontMgr::RegisterResult::Failed);
    collection->clearCaches();

    const auto regular = FindTypefaceForWeight(collection, "Test", 400);
    const auto aboveRegular = FindTypefaceForWeight(collection, "Test", 401);
    const auto bold = FindTypefaceForWeight(collection, "Test", 700);
    ASSERT_NE(regular, nullptr);
    ASSERT_NE(aboveRegular, nullptr);
    ASSERT_NE(bold, nullptr);
    EXPECT_EQ(GetStreamIndex(regular), 0);
    EXPECT_EQ(GetStreamIndex(aboveRegular), 0);
    EXPECT_EQ(GetStreamIndex(bold), 1);
    EXPECT_EQ(aboveRegular->uniqueID(), regular->uniqueID());
    EXPECT_NE(aboveRegular->uniqueID(), bold->uniqueID());
}
