#include "../../../include/Milestro/skia/FontRegistry.h"
#include "../../../include/Milestro/skia/textlayout/FontCollection.h"
#include "../../../include/Milestro/skia/textlayout/TextStyle.h"
#include "Milestro/game/milestro_game_interface.h"
#include "Milestro/skia/textlayout/ParagraphBuilder.h"

#include "include/core/SkFontStyle.h"
#include "include/core/SkData.h"
#include "include/core/SkStream.h"
#include "include/core/SkString.h"
#include "modules/skparagraph/include/ParagraphCache.h"
#include <cstring>
#include <gtest/gtest.h>
#include <memory>
#include <utility>
#include <vector>
#include <string>

namespace milestro_skia = milestro::skia;
namespace milestro_textlayout = milestro::skia::textlayout;

namespace {

class DefaultOnlyFontMgr final : public SkFontMgr {
public:
    explicit DefaultOnlyFontMgr(sk_sp<SkTypeface> defaultTypeface)
        : defaultTypeface(std::move(defaultTypeface)) {
    }

protected:
    int onCountFamilies() const override { return 0; }
    void onGetFamilyName(int, SkString *) const override {}
    sk_sp<SkFontStyleSet> onCreateStyleSet(int) const override {
        return SkFontStyleSet::CreateEmpty();
    }
    sk_sp<SkFontStyleSet> onMatchFamily(const char[]) const override {
        return SkFontStyleSet::CreateEmpty();
    }
    sk_sp<SkTypeface> onMatchFamilyStyle(const char familyName[],
                                         const SkFontStyle &) const override {
        return familyName == nullptr ? defaultTypeface : nullptr;
    }
    sk_sp<SkTypeface> onMatchFamilyStyleCharacter(const char familyName[],
                                                  const SkFontStyle &,
                                                  const char *[],
                                                  int,
                                                  SkUnichar) const override {
        return familyName == nullptr ? defaultTypeface : nullptr;
    }
    sk_sp<SkTypeface> onMakeFromData(sk_sp<SkData>, int) const override { return nullptr; }
    sk_sp<SkTypeface> onMakeFromStreamIndex(std::unique_ptr<SkStreamAsset>, int) const override {
        return nullptr;
    }
    sk_sp<SkTypeface> onMakeFromStreamArgs(std::unique_ptr<SkStreamAsset>,
                                           const SkFontArguments &) const override {
        return nullptr;
    }
    sk_sp<SkTypeface> onMakeFromFile(const char[], int) const override { return nullptr; }
    sk_sp<SkTypeface> onLegacyMakeTypeface(const char familyName[], SkFontStyle) const override {
        return familyName == nullptr ? defaultTypeface : nullptr;
    }

private:
    sk_sp<SkTypeface> defaultTypeface;
};

sk_sp<SkTypeface> MatchRegisteredTypeface(SkFontMgr *fontMgr, const char *familyName) {
    auto styleSet = fontMgr->matchFamily(familyName);
    return styleSet == nullptr ? nullptr : styleSet->matchStyle(SkFontStyle());
}

} // namespace

class SkiaFontCollectionTest : public ::testing::Test {
protected:
    void TearDown() override {
        milestro_textlayout::GetFontCollection()->ClearFontFamilyKeywordMappings();
    }
};

TEST_F(SkiaFontCollectionTest, BareAndExactTokensHaveDifferentKeywordSemantics) {
    auto collection = milestro_textlayout::GetFontCollection();
    ASSERT_NE(collection, nullptr);

    const auto bare = collection->ResolveFontFamilyNames({
            milestro_skia::FontFamilyToken::Bare("sans-serif"),
    });
    ASSERT_FALSE(bare.empty());
    EXPECT_EQ(bare.front(), "Source Han Sans VF");

    const auto exact = collection->ResolveFontFamilyNames({
            milestro_skia::FontFamilyToken::Exact("sans-serif"),
    });
    ASSERT_EQ(exact.size(), 1u);
    EXPECT_EQ(exact.front(), "sans-serif");
}

TEST_F(SkiaFontCollectionTest, KeywordCyclesSkipOnlyTheRecursiveBranch) {
    auto collection = milestro_textlayout::GetFontCollection();
    ASSERT_NE(collection, nullptr);

    collection->SetFontFamilyKeywordMapping("a", {
            milestro_skia::FontFamilyToken::Bare("b"),
    });
    collection->SetFontFamilyKeywordMapping("b", {
            milestro_skia::FontFamilyToken::Bare("a"),
            milestro_skia::FontFamilyToken::Exact("Cycle Fallback"),
    });

    const auto resolved = collection->ResolveFontFamilyNames({
            milestro_skia::FontFamilyToken::Bare("a"),
    });
    ASSERT_EQ(resolved.size(), 1u);
    EXPECT_EQ(resolved.front(), "Cycle Fallback");
}

TEST_F(SkiaFontCollectionTest, ParagraphAndStrutStylesResolveAtCollectionBoundary) {
    auto collection = milestro_textlayout::GetFontCollection();
    ASSERT_NE(collection, nullptr);
    collection->SetFontFamilyKeywordMapping("heading", {
            milestro_skia::FontFamilyToken::Exact("Sarasa Mono SC"),
            milestro_skia::FontFamilyToken::Bare("sans-serif"),
    });

    milestro_textlayout::TextStyle textStyle;
    textStyle.setFontFamilyTokens({
            milestro_skia::FontFamilyToken::Bare("heading"),
    });
    milestro_textlayout::StrutStyle strutStyle;
    strutStyle.setFontFamilyTokens({
            milestro_skia::FontFamilyToken::Exact("Strut Literal"),
    });
    milestro_textlayout::ParagraphStyle paragraphStyle;
    paragraphStyle.setTextStyle(&textStyle);
    paragraphStyle.setStrutStyle(&strutStyle);

    const auto resolved = collection->ResolveParagraphStyle(paragraphStyle);
    const auto textFamilies = resolved->getTextStyle().getFontFamilies();
    ASSERT_GE(textFamilies.size(), 2u);
    EXPECT_STREQ(textFamilies[0].c_str(), "Sarasa Mono SC");
    EXPECT_STREQ(textFamilies[1].c_str(), "Source Han Sans VF");

    const auto strutFamilies = resolved->getStrutStyle().getFontFamilies();
    ASSERT_EQ(strutFamilies.size(), 1u);
    EXPECT_STREQ(strutFamilies.front().c_str(), "Strut Literal");
}

TEST_F(SkiaFontCollectionTest, ResolvedStyleUsesParagraphFontCollectionLookupPath) {
    auto registry = milestro_skia::GetFontRegistry();
    auto collection = milestro_textlayout::GetFontCollection();
    ASSERT_NE(registry, nullptr);
    ASSERT_NE(collection, nullptr);

    const auto registerResult = registry->RegisterFontFromFile(MILESTRO_TEST_FONT_PATH);
    ASSERT_NE(registerResult, milestro_skia::MilestroRegisteredFontMgr::RegisterResult::Failed);
    collection->clearCaches();

    milestro_textlayout::TextStyle style;
    style.setFontFamilyTokens({
            milestro_skia::FontFamilyToken::Bare("Source Han Sans VF"),
    });
    const auto resolvedStyle = collection->ResolveTextStyle(style);
    ASSERT_EQ(resolvedStyle->getFontFamilies().size(), 1u);
    EXPECT_STREQ(resolvedStyle->getFontFamilies().front().c_str(), "Source Han Sans VF");

    const auto faces = collection->unwrap()->findTypefaces(
            resolvedStyle->getFontFamilies(),
            SkFontStyle());
    ASSERT_FALSE(faces.empty());
    ASSERT_NE(faces.front(), nullptr);

    SkString family(" ");
    faces.front()->getFamilyName(&family);
    EXPECT_STREQ(family.c_str(), "Source Han Sans VF");
}

TEST_F(SkiaFontCollectionTest, SarasaMonoSystemFaceUsesParagraphFontCollectionLookupPath) {
    auto registry = milestro_skia::GetFontRegistry();
    auto collection = milestro_textlayout::GetFontCollection();
    ASSERT_NE(registry, nullptr);
    ASSERT_NE(collection, nullptr);
    if (!registry->IsSystemFontMgrAvailable()) {
        GTEST_SKIP() << "system FontMgr is unavailable";
    }

    const SkFontStyle fontStyle;
    const auto systemStyleSet = registry->GetSystemFontMgr()->matchFamily("Sarasa Mono SC");
    if (systemStyleSet == nullptr || systemStyleSet->count() == 0) {
        GTEST_SKIP() << "Sarasa Mono SC is not installed";
    }
    const auto expected = systemStyleSet->matchStyle(fontStyle);
    if (expected == nullptr) {
        GTEST_SKIP() << "Sarasa Mono SC has no matching default style";
    }

    milestro_textlayout::TextStyle style;
    style.setFontFamilyTokens({
            milestro_skia::FontFamilyToken::Bare("Sarasa Mono SC"),
    });
    const auto resolvedStyle = collection->ResolveTextStyle(style);
    ASSERT_EQ(resolvedStyle->getFontFamilies().size(), 1u);
    EXPECT_STREQ(resolvedStyle->getFontFamilies().front().c_str(), "Sarasa Mono SC");

    const auto faces = collection->unwrap()->findTypefaces(
            resolvedStyle->getFontFamilies(),
            fontStyle);
    ASSERT_FALSE(faces.empty());
    ASSERT_NE(faces.front(), nullptr);
    EXPECT_EQ(faces.front()->uniqueID(), expected->uniqueID());

    SkString family(" ");
    faces.front()->getFamilyName(&family);
    EXPECT_STREQ(family.c_str(), "Sarasa Mono SC");
}

TEST_F(SkiaFontCollectionTest, SystemDefaultCandidatePrecedesLaterRegisteredFamily) {
    auto registry = milestro_skia::GetFontRegistry();
    ASSERT_NE(registry, nullptr);
    ASSERT_NE(registry->RegisterFontFromFile(MILESTRO_TEST_FONT_PATH),
              milestro_skia::MilestroRegisteredFontMgr::RegisterResult::Failed);
    ASSERT_NE(registry->RegisterFontFromFile(MILESTRO_TEST_EMOJI_FONT_PATH),
              milestro_skia::MilestroRegisteredFontMgr::RegisterResult::Failed);

    auto registeredFontMgr = registry->GetRegisteredFontMgr();
    auto systemDefault = MatchRegisteredTypeface(registeredFontMgr.get(), "Source Han Sans VF");
    auto registeredLater = MatchRegisteredTypeface(registeredFontMgr.get(), "Noto Color Emoji");
    ASSERT_NE(systemDefault, nullptr);
    ASSERT_NE(registeredLater, nullptr);
    ASSERT_NE(systemDefault->uniqueID(), registeredLater->uniqueID());

    auto skCollection = sk_make_sp<::skia::textlayout::FontCollection>();
    auto defaultOnlySystemMgr = sk_make_sp<DefaultOnlyFontMgr>(systemDefault);
    milestro_textlayout::FontCollection collection(
            skCollection,
            registeredFontMgr,
            defaultOnlySystemMgr);

    const std::vector<milestro_skia::FontFamilyToken> tokens = {
            milestro_skia::FontFamilyToken::Bare("system-ui"),
            milestro_skia::FontFamilyToken::Exact("Noto Color Emoji"),
    };
    const auto resolvedFamilies = collection.ResolveFontFamilyNames(tokens);
    std::vector<SkString> skiaFamilies;
    skiaFamilies.reserve(resolvedFamilies.size());
    for (const auto &family: resolvedFamilies) {
        skiaFamilies.emplace_back(family.c_str());
    }
    const auto paragraphFaces = skCollection->findTypefaces(skiaFamilies, SkFontStyle());
    ASSERT_GE(paragraphFaces.size(), 2u);
    EXPECT_EQ(paragraphFaces.front()->uniqueID(), systemDefault->uniqueID());
    EXPECT_EQ(paragraphFaces.back()->uniqueID(), registeredLater->uniqueID());

    const auto directFace = collection.ResolveTypeface(
            tokens,
            SkFontStyle::kNormal_Weight,
            true);
    ASSERT_NE(directFace, nullptr);
    EXPECT_EQ(directFace->uniqueID(), paragraphFaces.front()->uniqueID());
}

TEST(SkiaFontFamilyKeywordMapTest, ClearReportsOnlyRemovedUserMappings) {
    milestro_skia::FontFamilyKeywordMap mappings;
    EXPECT_FALSE(mappings.ClearUserMappings());
    mappings.SetUserMapping("   ", {});
    EXPECT_FALSE(mappings.ClearUserMappings());
    mappings.SetUserMapping("sans-serif", {});
    EXPECT_TRUE(mappings.Resolve({milestro_skia::FontFamilyToken::Bare("sans-serif")}).empty());
    EXPECT_TRUE(mappings.ClearUserMappings());
    EXPECT_FALSE(mappings.Resolve({milestro_skia::FontFamilyToken::Bare("sans-serif")}).empty());
    EXPECT_FALSE(mappings.ClearUserMappings());
}

class SkiaFontCollectionCacheTest : public SkiaFontCollectionTest {
protected:
    void SetUp() override {
        collection = milestro_textlayout::GetFontCollection();
        ASSERT_NE(milestro_skia::GetFontRegistry()->RegisterFontFromFile(MILESTRO_TEST_FONT_PATH),
                  milestro_skia::MilestroRegisteredFontMgr::RegisterResult::Failed);
        collection->ClearFontFamilyKeywordMappings();
        collection->clearCaches();
        cache = collection->unwrap()->getParagraphCache();
        cache->setChecker([this](::skia::textlayout::ParagraphImpl*, const char* event, bool) {
            if (std::strcmp(event, "foundParagraph") == 0) {
                ++cacheHits;
            }
        });
    }

    void TearDown() override {
        if (cache != nullptr) {
            cache->setChecker([](::skia::textlayout::ParagraphImpl*, const char*, bool) {});
        }
        SkiaFontCollectionTest::TearDown();
        if (collection != nullptr) {
            collection->clearCaches();
        }
    }

    std::unique_ptr<milestro_textlayout::Paragraph> BuildParagraph() {
        milestro_textlayout::TextStyle textStyle;
        textStyle.setFontFamilies({SkString("Source Han Sans VF")});
        textStyle.setFontSize(24);
        milestro_textlayout::ParagraphStyle paragraphStyle;
        paragraphStyle.setTextStyle(&textStyle);
        milestro_textlayout::ParagraphBuilder builder(&paragraphStyle);
        builder.addText("Cache123", 8);
        std::unique_ptr<milestro_textlayout::Paragraph> paragraph(builder.build());
        paragraph->layout(400);
        return paragraph;
    }

    milestro_textlayout::FontCollection* collection = nullptr;
    ::skia::textlayout::ParagraphCache* cache = nullptr;
    int cacheHits = 0;
};

TEST_F(SkiaFontCollectionCacheTest, EmptyResetPreservesShapedParagraphAndCacheHit) {
    auto first = BuildParagraph();
    ASSERT_EQ(cache->count(), 1);
    ASSERT_GT(first->unwrap()->getLongestLine(), 0);
    EXPECT_EQ(MilestroSkiaFontRegistryResetFontFamilyKeywordMappings(), 0);
    EXPECT_EQ(cache->count(), 1);
    auto second = BuildParagraph();
    EXPECT_EQ(cacheHits, 1);
    EXPECT_EQ(cache->count(), 1);
    EXPECT_FLOAT_EQ(first->unwrap()->getLongestLine(), second->unwrap()->getLongestLine());
    EXPECT_FLOAT_EQ(first->unwrap()->getHeight(), second->unwrap()->getHeight());
}

TEST_F(SkiaFontCollectionCacheTest, EquivalentReplacementPreservesCacheAndDetectsNativeMutation) {
    auto makeMappings = [](bool reverse) {
        milestro_skia::FontFamilyKeywordMap mappings;
        if (reverse) {
            mappings.SetUserMapping("heading", {milestro_skia::FontFamilyToken::Bare("body")});
        }
        mappings.SetUserMapping(" BODY ", {milestro_skia::FontFamilyToken::Exact("Source Han Sans VF")});
        if (!reverse) {
            mappings.SetUserMapping("heading", {milestro_skia::FontFamilyToken::Bare("body")});
        }
        return mappings;
    };
    collection->ReplaceFontFamilyKeywordMappings(makeMappings(false));
    auto first = BuildParagraph();
    ASSERT_EQ(cache->count(), 1);
    collection->ReplaceFontFamilyKeywordMappings(makeMappings(true));
    EXPECT_EQ(cache->count(), 1);
    auto second = BuildParagraph();
    EXPECT_EQ(cacheHits, 1);
    EXPECT_FLOAT_EQ(first->unwrap()->getLongestLine(), second->unwrap()->getLongestLine());
    collection->SetFontFamilyKeywordMapping("body", {milestro_skia::FontFamilyToken::Exact("Other")});
    auto changed = BuildParagraph();
    ASSERT_EQ(cache->count(), 1);
    collection->ReplaceFontFamilyKeywordMappings(makeMappings(false));
    EXPECT_EQ(cache->count(), 0);
    EXPECT_EQ(collection->ResolveFontFamilyNames({milestro_skia::FontFamilyToken::Bare("heading")}),
              std::vector<std::string>{"Source Han Sans VF"});
}

TEST_F(SkiaFontCollectionCacheTest, ReplacementDetectsTokenKindOrderDeletionAndEmptyOverride) {
    auto replace = [this](std::vector<milestro_skia::FontFamilyToken> tokens) {
        milestro_skia::FontFamilyKeywordMap mappings;
        mappings.SetUserMapping("body", std::move(tokens));
        collection->ReplaceFontFamilyKeywordMappings(std::move(mappings));
    };
    replace({milestro_skia::FontFamilyToken::Bare("serif"), milestro_skia::FontFamilyToken::Exact("Other")});
    auto first = BuildParagraph();
    replace({milestro_skia::FontFamilyToken::Exact("serif"), milestro_skia::FontFamilyToken::Exact("Other")});
    EXPECT_EQ(cache->count(), 0);
    auto second = BuildParagraph();
    replace({milestro_skia::FontFamilyToken::Exact("Other"), milestro_skia::FontFamilyToken::Exact("serif")});
    EXPECT_EQ(cache->count(), 0);
    auto third = BuildParagraph();
    replace({});
    EXPECT_EQ(cache->count(), 0);
    EXPECT_TRUE(collection->ResolveFontFamilyNames({milestro_skia::FontFamilyToken::Bare("body")}).empty());
    auto fourth = BuildParagraph();
    collection->ReplaceFontFamilyKeywordMappings({});
    EXPECT_EQ(cache->count(), 0);
    EXPECT_EQ(collection->ResolveFontFamilyNames({milestro_skia::FontFamilyToken::Bare("body")}),
              std::vector<std::string>{"body"});
    auto fifth = BuildParagraph();
    collection->ReplaceFontFamilyKeywordMappings({});
    EXPECT_EQ(cache->count(), 1);
    collection->clearCaches();
    EXPECT_EQ(cache->count(), 0);
}

TEST_F(SkiaFontCollectionCacheTest, InvalidReplacementDoesNotPartiallyApply) {
    collection->SetFontFamilyKeywordMapping("body", {milestro_skia::FontFamilyToken::Exact("Source Han Sans VF")});
    auto first = BuildParagraph();
    EXPECT_NE(MilestroSkiaFontRegistryReplaceFontFamilyKeywordMappings(nullptr, nullptr, nullptr, nullptr, nullptr, 1), 0);
    EXPECT_EQ(cache->count(), 1);
    EXPECT_EQ(collection->ResolveFontFamilyNames({milestro_skia::FontFamilyToken::Bare("body")}),
              std::vector<std::string>{"Source Han Sans VF"});
}

TEST_F(SkiaFontCollectionCacheTest, BulkApiPreservesCacheAndRegistrationStillInvalidates) {
    std::string keyword = "body";
    std::string family = "Source Han Sans VF";
    uint8_t* keywords[] = {reinterpret_cast<uint8_t*>(keyword.data())};
    uint64_t sizes[] = {keyword.size()};
    uint8_t* families[] = {reinterpret_cast<uint8_t*>(family.data())};
    int32_t kinds[] = {static_cast<int32_t>(milestro_skia::FontFamilyTokenKind::Exact)};
    uint32_t counts[] = {1};
    EXPECT_EQ(MilestroSkiaFontRegistryReplaceFontFamilyKeywordMappings(keywords, sizes, families, kinds, counts, 1), 0);
    auto first = BuildParagraph();
    ASSERT_EQ(cache->count(), 1);
    EXPECT_EQ(MilestroSkiaFontRegistryReplaceFontFamilyKeywordMappings(keywords, sizes, families, kinds, counts, 1), 0);
    EXPECT_EQ(cache->count(), 1);
    std::string path = MILESTRO_TEST_FONT_PATH;
    EXPECT_NE(MilestroSkiaFontRegistryRegisterFontFromFile(reinterpret_cast<uint8_t*>(path.data()), path.size()),
              static_cast<int64_t>(milestro_skia::MilestroRegisteredFontMgr::RegisterResult::Failed));
    EXPECT_EQ(cache->count(), 0);
    auto second = BuildParagraph();
    ASSERT_EQ(cache->count(), 1);
    EXPECT_EQ(MilestroSkiaFontCollectionClearCaches(), 0);
    EXPECT_EQ(cache->count(), 0);
    EXPECT_EQ(MilestroSkiaFontRegistryReplaceFontFamilyKeywordMappings(nullptr, nullptr, nullptr, nullptr, nullptr, 0), 0);
    EXPECT_EQ(collection->ResolveFontFamilyNames({milestro_skia::FontFamilyToken::Bare("body")}),
              std::vector<std::string>{"body"});
}

TEST_F(SkiaFontCollectionTest, ReplacementAfterCollectionReinitializationReappliesMappings) {
    auto makeMappings = [] {
        milestro_skia::FontFamilyKeywordMap mappings;
        mappings.SetUserMapping("body", {milestro_skia::FontFamilyToken::Exact("Source Han Sans VF")});
        return mappings;
    };
    milestro_textlayout::GetFontCollection()->ReplaceFontFamilyKeywordMappings(makeMappings());
    auto registry = milestro_skia::GetFontRegistry();
    auto fresh = std::make_unique<milestro_textlayout::FontCollection>(
            sk_make_sp<::skia::textlayout::FontCollection>(),
            registry->GetRegisteredFontMgr(), registry->GetSystemFontMgr());
    auto collection = fresh.get();
    EXPECT_EQ(collection->ResolveFontFamilyNames({milestro_skia::FontFamilyToken::Bare("body")}),
              std::vector<std::string>{"body"});
    collection->ReplaceFontFamilyKeywordMappings(makeMappings());
    EXPECT_EQ(collection->ResolveFontFamilyNames({milestro_skia::FontFamilyToken::Bare("body")}),
              std::vector<std::string>{"Source Han Sans VF"});
}

TEST_F(SkiaFontCollectionCacheTest, MappingSetAndNonemptyResetInvalidateShapedParagraphs) {
    auto initial = BuildParagraph();
    ASSERT_EQ(cache->count(), 1);
    collection->SetFontFamilyKeywordMapping("heading", {
            milestro_skia::FontFamilyToken::Exact("Source Han Sans VF"),
    });
    EXPECT_EQ(cache->count(), 0);
    EXPECT_EQ(collection->ResolveFontFamilyNames({milestro_skia::FontFamilyToken::Bare("heading")}),
              std::vector<std::string>{"Source Han Sans VF"});
    auto mapped = BuildParagraph();
    ASSERT_EQ(cache->count(), 1);
    EXPECT_EQ(MilestroSkiaFontRegistryResetFontFamilyKeywordMappings(), 0);
    EXPECT_EQ(cache->count(), 0);
    EXPECT_EQ(collection->ResolveFontFamilyNames({milestro_skia::FontFamilyToken::Bare("heading")}),
              std::vector<std::string>{"heading"});
    auto reset = BuildParagraph();
    EXPECT_EQ(cache->count(), 1);
    EXPECT_EQ(cacheHits, 0);
}

TEST_F(SkiaFontCollectionCacheTest, ExplicitCacheClearStillInvalidatesWithoutMappings) {
    auto initial = BuildParagraph();
    ASSERT_EQ(cache->count(), 1);
    EXPECT_EQ(MilestroSkiaFontCollectionClearCaches(), 0);
    EXPECT_EQ(cache->count(), 0);
    auto rebuilt = BuildParagraph();
    EXPECT_EQ(cache->count(), 1);
    EXPECT_EQ(cacheHits, 0);
}

TEST_F(SkiaFontCollectionCacheTest, SuccessfulRegistrationStillInvalidatesWithoutMappings) {
    auto initial = BuildParagraph();
    ASSERT_EQ(cache->count(), 1);
    std::string path = MILESTRO_TEST_FONT_PATH;
    EXPECT_NE(MilestroSkiaFontRegistryRegisterFontFromFile(
                      reinterpret_cast<uint8_t*>(path.data()), path.size()),
              static_cast<int64_t>(milestro_skia::MilestroRegisteredFontMgr::RegisterResult::Failed));
    EXPECT_EQ(cache->count(), 0);
    auto rebuilt = BuildParagraph();
    EXPECT_EQ(cache->count(), 1);
    EXPECT_EQ(cacheHits, 0);
}
