#include "../../../include/Milestro/skia/FontRegistry.h"
#include "../../../include/Milestro/skia/textlayout/FontCollection.h"
#include "../../../include/Milestro/skia/textlayout/TextStyle.h"

#include "include/core/SkFontStyle.h"
#include "include/core/SkData.h"
#include "include/core/SkStream.h"
#include "include/core/SkString.h"
#include <gtest/gtest.h>
#include <memory>
#include <utility>
#include <vector>

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
    const auto textFamilies = resolved.getTextStyle().getFontFamilies();
    ASSERT_GE(textFamilies.size(), 2u);
    EXPECT_STREQ(textFamilies[0].c_str(), "Sarasa Mono SC");
    EXPECT_STREQ(textFamilies[1].c_str(), "Source Han Sans VF");

    const auto strutFamilies = resolved.getStrutStyle().getFontFamilies();
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
    ASSERT_EQ(resolvedStyle.getFontFamilies().size(), 1u);
    EXPECT_STREQ(resolvedStyle.getFontFamilies().front().c_str(), "Source Han Sans VF");

    const auto faces = collection->unwrap()->findTypefaces(
            resolvedStyle.getFontFamilies(),
            SkFontStyle());
    ASSERT_FALSE(faces.empty());
    ASSERT_NE(faces.front(), nullptr);

    SkString family;
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
    ASSERT_EQ(resolvedStyle.getFontFamilies().size(), 1u);
    EXPECT_STREQ(resolvedStyle.getFontFamilies().front().c_str(), "Sarasa Mono SC");

    const auto faces = collection->unwrap()->findTypefaces(
            resolvedStyle.getFontFamilies(),
            fontStyle);
    ASSERT_FALSE(faces.empty());
    ASSERT_NE(faces.front(), nullptr);
    EXPECT_EQ(faces.front()->uniqueID(), expected->uniqueID());

    SkString family;
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
