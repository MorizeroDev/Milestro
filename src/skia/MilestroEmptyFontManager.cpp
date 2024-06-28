#include "include/core/SkFontArguments.h"
#include "include/core/SkFontMgr.h"
#include "include/core/SkFontStyle.h"
#include "include/core/SkRefCnt.h"
#include "include/core/SkStream.h"
#include "include/core/SkString.h"
#include "include/core/SkTypeface.h"
#include "include/core/SkTypes.h"
#include "include/private/base/SkTemplates.h"
#include "src/core/SkFontDescriptor.h"
#include "Milestro/skia/MilestroEmptyFontManager.h"
#include "Milestro/skia/Typeface.h"
#include <memory>
#include <sstream>

namespace milestro::skia {

MilestroEmptyFontManager::MilestroEmptyFontManager() {
}

int MilestroEmptyFontManager::onCountFamilies() const {
    return 0;
}

void MilestroEmptyFontManager::onGetFamilyName(int index, SkString *familyName) const {
    familyName->set("");
}

sk_sp<SkFontStyleSet> MilestroEmptyFontManager::onCreateStyleSet(int index) const {
    return nullptr;
}

sk_sp<SkFontStyleSet> MilestroEmptyFontManager::onMatchFamily(const char familyName[]) const {
    MILESTROLOG_INFO("MilestroEmptyFontManager::onMatchFamily: familyName: {}", familyName);
    return nullptr;
}

sk_sp<SkTypeface> MilestroEmptyFontManager::onMatchFamilyStyle(const char familyName[],
                                                               const SkFontStyle &fontStyle) const {
    MILESTROLOG_INFO(
        "MilestroEmptyFontManager::onMatchFamilyStyle: familyName: {}, fontStyle.weight: {}, fontStyle.width: {}, fontStyle.slant: {}",
        familyName,
        (int) fontStyle.weight(),
        (int) fontStyle.width(),
        (int) fontStyle.slant());
    return nullptr;
}

sk_sp<SkTypeface> MilestroEmptyFontManager::onMatchFamilyStyleCharacter(const char familyName[],
                                                                        const SkFontStyle &fontStyle,
                                                                        const char *bcp47[],
                                                                        int bcp47Count,
                                                                        SkUnichar character) const {
    std::stringstream bcp47Builder;
    for (int i = 0; i < bcp47Count; i++) {
        if (i != 0) {
            bcp47Builder << ",";
        }
        bcp47Builder << bcp47[i];
    }


    MILESTROLOG_INFO(
        "MilestroEmptyFontManager::onMatchFamilyStyleCharacter: familyName: {}, fontStyle.weight: {}, fontStyle.width: {}, fontStyle.slant: {}, bcp47: {}, character: {}",
        familyName,
        (int) fontStyle.weight(),
        (int) fontStyle.width(),
        (int) fontStyle.slant(),
        bcp47Builder.str(),
        character
        );
    return nullptr;
}

sk_sp<SkTypeface> MilestroEmptyFontManager::onMakeFromData(sk_sp<SkData> data, int ttcIndex) const {
    return nullptr;
}

sk_sp<SkTypeface> MilestroEmptyFontManager::onMakeFromStreamIndex(std::unique_ptr<SkStreamAsset> stream,
                                                                  int ttcIndex) const {
    return nullptr;
}

sk_sp<SkTypeface> MilestroEmptyFontManager::onMakeFromStreamArgs(std::unique_ptr<SkStreamAsset> stream,
                                                                 const SkFontArguments &args) const {
    return nullptr;
}

sk_sp<SkTypeface> MilestroEmptyFontManager::onMakeFromFile(const char path[], int ttcIndex) const {
    return nullptr;
}

sk_sp<SkTypeface> MilestroEmptyFontManager::onLegacyMakeTypeface(const char familyName[],
                                                                 SkFontStyle style) const {
    return nullptr;
}

}
