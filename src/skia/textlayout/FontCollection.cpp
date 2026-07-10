#include <algorithm>
#include <memory>
#include <string>
#include <utility>
#include <vector>
#include "Milestro/skia/textlayout/FontCollection.h"
#include "Milestro/common/milestro_result.h"
#include "Milestro/log/log.h"
#include "Milestro/skia/FontRegistry.h"
#include "include/core/SkData.h"
#include "include/core/SkFontStyle.h"
#include "include/core/SkStream.h"
#include "include/core/SkTypeface.h"
#include <cstring>

namespace milestro::skia::textlayout {

namespace {

constexpr char kSystemDefaultFamily[] = "\x1Fmilestro-system-default";

constexpr int32_t NormalizeFontWeight(int32_t weight) {
    return std::clamp(weight,
                      static_cast<int32_t>(SkFontStyle::kThin_Weight),
                      static_cast<int32_t>(SkFontStyle::kExtraBlack_Weight));
}

std::vector<SkString> ToSkiaFamilies(const std::vector<std::string> &families) {
    std::vector<SkString> ret;
    ret.reserve(families.size());
    for (const auto &family: families) {
        ret.emplace_back(family.c_str());
    }
    return ret;
}

sk_sp<SkTypeface> MatchExactTypeface(SkFontMgr *fontMgr,
                                     const std::string &family,
                                     const SkFontStyle &style) {
    if (fontMgr == nullptr || family.empty()) {
        return nullptr;
    }

    auto styleSet = fontMgr->matchFamily(family.c_str());
    return styleSet == nullptr ? nullptr : styleSet->matchStyle(style);
}

sk_sp<SkTypeface> MatchDefaultTypeface(SkFontMgr *fontMgr, const SkFontStyle &style) {
    return fontMgr == nullptr ? nullptr : fontMgr->matchFamilyStyle(nullptr, style);
}

bool IsSystemDefaultFamily(const char *familyName) {
    return familyName != nullptr && std::strcmp(familyName, kSystemDefaultFamily) == 0;
}

class SystemDefaultFontStyleSet final : public SkFontStyleSet {
public:
    explicit SystemDefaultFontStyleSet(sk_sp<SkFontMgr> systemFontMgr)
        : systemFontMgr(std::move(systemFontMgr)) {
    }

    int count() override {
        return systemFontMgr == nullptr ? 0 : 1;
    }

    void getStyle(int index, SkFontStyle *style, SkString *name) override {
        if (index != 0) {
            return;
        }
        auto typeface = this->matchStyle(SkFontStyle());
        if (typeface == nullptr) {
            return;
        }
        if (style != nullptr) {
            *style = typeface->fontStyle();
        }
        if (name != nullptr) {
            typeface->getFamilyName(name);
        }
    }

    sk_sp<SkTypeface> createTypeface(int index) override {
        return index == 0 ? this->matchStyle(SkFontStyle()) : nullptr;
    }

    sk_sp<SkTypeface> matchStyle(const SkFontStyle &pattern) override {
        return MatchDefaultTypeface(systemFontMgr.get(), pattern);
    }

private:
    sk_sp<SkFontMgr> systemFontMgr;
};

class SystemDefaultFontMgr final : public SkFontMgr {
public:
    explicit SystemDefaultFontMgr(sk_sp<SkFontMgr> systemFontMgr)
        : systemFontMgr(std::move(systemFontMgr)) {
    }

protected:
    int onCountFamilies() const override {
        return systemFontMgr == nullptr ? 0 : 1;
    }

    void onGetFamilyName(int index, SkString *familyName) const override {
        if (index == 0 && familyName != nullptr) {
            familyName->set(kSystemDefaultFamily);
        }
    }

    sk_sp<SkFontStyleSet> onCreateStyleSet(int index) const override {
        return index == 0 && systemFontMgr != nullptr
                ? sk_make_sp<SystemDefaultFontStyleSet>(systemFontMgr)
                : SkFontStyleSet::CreateEmpty();
    }

    sk_sp<SkFontStyleSet> onMatchFamily(const char familyName[]) const override {
        return IsSystemDefaultFamily(familyName) && systemFontMgr != nullptr
                ? sk_make_sp<SystemDefaultFontStyleSet>(systemFontMgr)
                : SkFontStyleSet::CreateEmpty();
    }

    sk_sp<SkTypeface> onMatchFamilyStyle(const char familyName[],
                                         const SkFontStyle &style) const override {
        return IsSystemDefaultFamily(familyName)
                ? MatchDefaultTypeface(systemFontMgr.get(), style)
                : nullptr;
    }

    sk_sp<SkTypeface> onMatchFamilyStyleCharacter(const char familyName[],
                                                  const SkFontStyle &style,
                                                  const char *bcp47[],
                                                  int bcp47Count,
                                                  SkUnichar character) const override {
        if (!IsSystemDefaultFamily(familyName) || systemFontMgr == nullptr) {
            return nullptr;
        }
        return systemFontMgr->matchFamilyStyleCharacter(
                nullptr, style, bcp47, bcp47Count, character);
    }

    sk_sp<SkTypeface> onMakeFromData(sk_sp<SkData>, int) const override {
        return nullptr;
    }

    sk_sp<SkTypeface> onMakeFromStreamIndex(std::unique_ptr<SkStreamAsset>, int) const override {
        return nullptr;
    }

    sk_sp<SkTypeface> onMakeFromStreamArgs(std::unique_ptr<SkStreamAsset>,
                                           const SkFontArguments &) const override {
        return nullptr;
    }

    sk_sp<SkTypeface> onMakeFromFile(const char[], int) const override {
        return nullptr;
    }

    sk_sp<SkTypeface> onLegacyMakeTypeface(const char familyName[],
                                           SkFontStyle style) const override {
        return IsSystemDefaultFamily(familyName)
                ? MatchDefaultTypeface(systemFontMgr.get(), style)
                : nullptr;
    }

private:
    sk_sp<SkFontMgr> systemFontMgr;
};

} // namespace

static std::unique_ptr<FontCollection> FontCollectionInstance = nullptr;

FontCollection::FontCollection(sk_sp<::skia::textlayout::FontCollection> fontCollection,
                               sk_sp<SkFontMgr> registeredFontMgr,
                               sk_sp<SkFontMgr> systemFontMgr)
    : fontCollection(std::move(fontCollection)),
      registeredFontMgr(std::move(registeredFontMgr)),
      systemFontMgr(std::move(systemFontMgr)) {
    if (this->systemFontMgr != nullptr) {
        this->fontCollection->setDynamicFontManager(
                sk_make_sp<SystemDefaultFontMgr>(this->systemFontMgr));
    }
    this->fontCollection->setAssetFontManager(this->registeredFontMgr);
    this->fontCollection->setDefaultFontManager(
            this->systemFontMgr != nullptr ? this->systemFontMgr : SkFontMgr::RefEmpty());
}

Result<void, std::string> InitialFontCollection() {
    auto fontCollection = sk_make_sp<::skia::textlayout::FontCollection>();
    if (fontCollection == nullptr) {
        return Err(std::string("fail to create ::skia::textlayout::FontCollection"));
    }

    auto fontRegistry = milestro::skia::GetFontRegistry();
    FontCollectionInstance = std::make_unique<FontCollection>(
            std::move(fontCollection),
            fontRegistry->GetRegisteredFontMgr(),
            fontRegistry->GetSystemFontMgr());
    return Ok();
}

FontCollection *GetFontCollection() {
    if (FontCollectionInstance == nullptr) {
        auto result = InitialFontCollection();
        if (result.isErr()) {
            MILESTROLOG_ERROR("{}", result.unwrapErr());
        }
    }

    assert(FontCollectionInstance != nullptr && "FontCollectionInstance is null");
    return FontCollectionInstance.get();
}

void FontCollection::ClearFontFamilyKeywordMappings() {
    fontFamilyKeywordMap.ClearUserMappings();
    fontCollection->clearCaches();
}

void FontCollection::SetFontFamilyKeywordMapping(std::string keyword,
                                                 std::vector<FontFamilyToken> mapping) {
    fontFamilyKeywordMap.SetUserMapping(std::move(keyword), std::move(mapping));
    fontCollection->clearCaches();
}

std::vector<FontFamilyCandidate> FontCollection::ResolveFontFamilyCandidates(
        const std::vector<FontFamilyToken> &tokens) const {
    return fontFamilyKeywordMap.Resolve(tokens);
}

std::vector<std::string> FontCollection::ResolveFontFamilyNames(
        const std::vector<FontFamilyToken> &tokens) const {
    std::vector<std::string> names;
    for (const auto &candidate: ResolveFontFamilyCandidates(tokens)) {
        if (candidate.kind == FontFamilyCandidateKind::SystemDefault) {
            names.emplace_back(kSystemDefaultFamily);
        } else if (!candidate.value.empty()) {
            names.emplace_back(candidate.value);
        }
    }
    return names;
}

sk_sp<SkTypeface> FontCollection::ResolveTypeface(const std::vector<FontFamilyToken> &tokens,
                                                  int32_t weight,
                                                  bool fallbackToSystem) const {
    const SkFontStyle style(NormalizeFontWeight(weight),
                            SkFontStyle::kNormal_Width,
                            SkFontStyle::kUpright_Slant);

    for (const auto &candidate: ResolveFontFamilyCandidates(tokens)) {
        if (candidate.kind == FontFamilyCandidateKind::SystemDefault) {
            if (fallbackToSystem) {
                auto typeface = MatchDefaultTypeface(systemFontMgr.get(), style);
                if (typeface != nullptr) {
                    return typeface;
                }
            }
            continue;
        }

        auto typeface = MatchExactTypeface(registeredFontMgr.get(), candidate.value, style);
        if (typeface != nullptr) {
            return typeface;
        }

        if (fallbackToSystem) {
            typeface = MatchExactTypeface(systemFontMgr.get(), candidate.value, style);
            if (typeface != nullptr) {
                return typeface;
            }
        }
    }

    if (fallbackToSystem) {
        auto typeface = MatchDefaultTypeface(systemFontMgr.get(), style);
        if (typeface != nullptr) {
            return typeface;
        }
    }

    return SkTypeface::MakeEmpty();
}

::skia::textlayout::TextStyle FontCollection::ResolveTextStyle(const TextStyle &style) const {
    auto resolved = style.spawn();
    if (style.hasFontFamilyTokens()) {
        resolved.setFontFamilies(ToSkiaFamilies(ResolveFontFamilyNames(style.getFontFamilyTokens())));
    }
    return resolved;
}

::skia::textlayout::StrutStyle FontCollection::ResolveStrutStyle(const StrutStyle &style) const {
    auto resolved = style.spawn();
    if (style.hasFontFamilyTokens()) {
        resolved.setFontFamilies(ToSkiaFamilies(ResolveFontFamilyNames(style.getFontFamilyTokens())));
    }
    return resolved;
}

::skia::textlayout::ParagraphStyle FontCollection::ResolveParagraphStyle(const ParagraphStyle &style) const {
    auto resolved = style.unwrap();
    if (style.hasTextFontFamilyTokens()) {
        auto textStyle = resolved.getTextStyle();
        textStyle.setFontFamilies(ToSkiaFamilies(ResolveFontFamilyNames(style.getTextFontFamilyTokens())));
        resolved.setTextStyle(textStyle);
    }
    if (style.hasStrutFontFamilyTokens()) {
        auto strutStyle = resolved.getStrutStyle();
        strutStyle.setFontFamilies(ToSkiaFamilies(ResolveFontFamilyNames(style.getStrutFontFamilyTokens())));
        resolved.setStrutStyle(strutStyle);
    }
    return resolved;
}

std::unique_ptr<::skia::textlayout::ParagraphBuilder> FontCollection::MakeParagraphBuilder(
        const ParagraphStyle &style,
        sk_sp<SkUnicode> unicode) const {
    return ::skia::textlayout::ParagraphBuilder::make(
            ResolveParagraphStyle(style),
            fontCollection,
            std::move(unicode));
}

std::unique_ptr<::skia::textlayout::ParagraphBuilder> FontCollection::MakeInputParagraphBuilder(
        const ::skia::textlayout::ParagraphStyle &style,
        const TextStyle &textStyle,
        sk_sp<SkUnicode> unicode) const {
    auto resolvedParagraphStyle = style;
    auto resolvedTextStyle = ResolveTextStyle(textStyle);
    resolvedParagraphStyle.setTextStyle(resolvedTextStyle);

    auto resolvedStrutStyle = resolvedParagraphStyle.getStrutStyle();
    resolvedStrutStyle.setFontFamilies(resolvedTextStyle.getFontFamilies());
    resolvedParagraphStyle.setStrutStyle(std::move(resolvedStrutStyle));

    auto builder = ::skia::textlayout::ParagraphBuilder::make(
            std::move(resolvedParagraphStyle),
            fontCollection,
            std::move(unicode));
    builder->pushStyle(std::move(resolvedTextStyle));
    return builder;
}

void FontCollection::PushStyle(::skia::textlayout::ParagraphBuilder *builder,
                               const TextStyle &style) const {
    if (builder != nullptr) {
        builder->pushStyle(ResolveTextStyle(style));
    }
}

}
