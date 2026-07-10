#ifndef MILESTRO_SKIA_TEXTLAYOUT_FONTCOLLECTION
#define MILESTRO_SKIA_TEXTLAYOUT_FONTCOLLECTION

#include "Milestro/common/milestro_export_macros.h"
#include "Milestro/skia/FontFamilyKeywordMap.h"
#include "Milestro/util/milestro_class.h"
#include "ParagraphStyle.h"
#include "TextStyle.h"
#include "include/core/SkFontMgr.h"
#include "include/core/SkTypeface.h"
#include "modules/skparagraph/include/FontCollection.h"
#include "modules/skparagraph/include/ParagraphBuilder.h"
#include "modules/skunicode/include/SkUnicode.h"
#include <cstdint>
#include <string>
#include <utility>
#include <vector>

namespace milestro::skia::textlayout {

class MILESTRO_API FontCollection {
public:
    FontCollection(sk_sp<::skia::textlayout::FontCollection> fontCollection,
                   sk_sp<SkFontMgr> registeredFontMgr,
                   sk_sp<SkFontMgr> systemFontMgr);

    MILESTRO_DECLARE_NON_COPYABLE(FontCollection)

    sk_sp<::skia::textlayout::FontCollection> unwrap() {
        return fontCollection;
    }

    void clearCaches() {
        fontCollection->clearCaches();
    }

    bool fontFallbackEnabled() {
        return fontCollection->fontFallbackEnabled();
    }

    void setFontFallbackEnabled(bool enabled) {
        if (enabled) {
            fontCollection->enableFontFallback();
        } else {
            fontCollection->disableFontFallback();

        }
    };

    void ClearFontFamilyKeywordMappings();
    void SetFontFamilyKeywordMapping(std::string keyword, std::vector<FontFamilyToken> mapping);

    std::vector<FontFamilyCandidate> ResolveFontFamilyCandidates(
            const std::vector<FontFamilyToken> &tokens) const;
    std::vector<std::string> ResolveFontFamilyNames(const std::vector<FontFamilyToken> &tokens) const;
    sk_sp<SkTypeface> ResolveTypeface(const std::vector<FontFamilyToken> &tokens,
                                      int32_t weight,
                                      bool fallbackToSystem) const;

    ::skia::textlayout::TextStyle ResolveTextStyle(const TextStyle &style) const;
    ::skia::textlayout::StrutStyle ResolveStrutStyle(const StrutStyle &style) const;
    ::skia::textlayout::ParagraphStyle ResolveParagraphStyle(const ParagraphStyle &style) const;

    std::unique_ptr<::skia::textlayout::ParagraphBuilder> MakeParagraphBuilder(
            const ParagraphStyle &style,
            sk_sp<SkUnicode> unicode) const;
    std::unique_ptr<::skia::textlayout::ParagraphBuilder> MakeInputParagraphBuilder(
            const ::skia::textlayout::ParagraphStyle &style,
            const TextStyle &textStyle,
            sk_sp<SkUnicode> unicode) const;
    void PushStyle(::skia::textlayout::ParagraphBuilder *builder, const TextStyle &style) const;

private:
    sk_sp<::skia::textlayout::FontCollection> fontCollection;
    sk_sp<SkFontMgr> registeredFontMgr;
    sk_sp<SkFontMgr> systemFontMgr;
    FontFamilyKeywordMap fontFamilyKeywordMap;
};

MILESTRO_API FontCollection *GetFontCollection();

}

#endif //MILESTRO_SKIA_TEXTLAYOUT_FONTCOLLECTION
