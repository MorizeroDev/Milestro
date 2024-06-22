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
#include "MilestroFontManager.h"
#include "TypeFace.h"
#include <memory>

#if WIN32

#include "include/ports/SkTypeface_win.h"

#endif

class SkData;
namespace milestro::skia {

MilestroFontStyleSet::MilestroFontStyleSet(SkString familyName)
    : fFamilyName(std::move(familyName)) {}

void MilestroFontStyleSet::appendTypeface(sk_sp<SkTypeface> typeface) {
    fStyles.emplace_back(std::move(typeface));
}

int MilestroFontStyleSet::count() {
    return fStyles.size();
}

void MilestroFontStyleSet::getStyle(int index, SkFontStyle *style, SkString *name) {
    SkASSERT(index < fStyles.size());
    if (style) {
        *style = fStyles[index]->fontStyle();
    }
    if (name) {
        name->reset();
    }
}

sk_sp<SkTypeface> MilestroFontStyleSet::createTypeface(int index) {
    SkASSERT(index < fStyles.size());
    return fStyles[index];
}

sk_sp<SkTypeface> MilestroFontStyleSet::matchStyle(const SkFontStyle &pattern) {
    return this->matchStyleCSS3(pattern);
}

SkString MilestroFontStyleSet::getFamilyName() { return fFamilyName; }

MilestroFontManager::MilestroFontManager() : fDefaultFamily(nullptr) {
#if WIN32
    backend = SkFontMgr_New_DirectWrite();
#else
#error No SkFontMgr Provider
#endif
}

int MilestroFontManager::onCountFamilies() const {
    return fFamilies.size();
}

void MilestroFontManager::onGetFamilyName(int index, SkString *familyName) const {
    SkASSERT(index < fFamilies.size());
    familyName->set(fFamilies[index]->getFamilyName());
}

sk_sp<SkFontStyleSet> MilestroFontManager::onCreateStyleSet(int index) const {
    SkASSERT(index < fFamilies.size());
    return fFamilies[index];
}

sk_sp<SkFontStyleSet> MilestroFontManager::onMatchFamily(const char familyName[]) const {
    for (int i = 0; i < fFamilies.size(); ++i) {
        if (fFamilies[i]->getFamilyName().equals(familyName)) {
            return fFamilies[i];
        }
    }
    return nullptr;
}

sk_sp<SkTypeface> MilestroFontManager::onMatchFamilyStyle(const char familyName[],
                                                          const SkFontStyle &fontStyle) const {
    sk_sp<SkFontStyleSet> sset(this->matchFamily(familyName));
    return sset->matchStyle(fontStyle);
}

sk_sp<SkTypeface> MilestroFontManager::onMatchFamilyStyleCharacter(
    const char familyName[], const SkFontStyle &,
    const char *bcp47[], int bcp47Count,
    SkUnichar) const {
    return nullptr;
}

sk_sp<SkTypeface> MilestroFontManager::onMakeFromData(sk_sp<SkData> data, int ttcIndex) const {
    auto ret = backend->makeFromData(data, ttcIndex);
    return ret;
}

sk_sp<SkTypeface> MilestroFontManager::onMakeFromStreamIndex(std::unique_ptr<SkStreamAsset> stream,
                                                             int ttcIndex) const {
    auto ret = backend->makeFromStream(std::move(stream), ttcIndex);
    return ret;
}

sk_sp<SkTypeface> MilestroFontManager::onMakeFromStreamArgs(std::unique_ptr<SkStreamAsset> stream,
                                                            const SkFontArguments &args) const {
    auto ret = backend->makeFromStream(std::move(stream), args);
    return ret;
}

sk_sp<SkTypeface> MilestroFontManager::onMakeFromFile(const char path[], int ttcIndex) const {
    auto ret = backend->makeFromFile(path, ttcIndex);
    return ret;
}

sk_sp<SkTypeface> MilestroFontManager::onLegacyMakeTypeface(const char familyName[],
                                                            SkFontStyle style) const {
    sk_sp<SkTypeface> tf;

    if (familyName) {
        tf = this->onMatchFamilyStyle(familyName, style);
    }

    if (!tf && fDefaultFamily) {
        tf = fDefaultFamily->matchStyle(style);
    }
    return tf;
}

bool MilestroFontManager::isValidAndUniqueFontName(const SkString name) {
    size_t i = 0;
    for (i = 0; i < name.size(); i++) {
        auto rune = name[i];
        if (rune != '_' && rune != ' ' && rune != '!') {
            break;
        }
    }
    if (i == name.size()) {
        return false;
    }

    for (const auto &family : fFamilies) {
        if (family->getFamilyName() == name) {
            return false;
        }
    }

    return true;
}

void MilestroFontManager::registerTypeFace(sk_sp<SkTypeface> typeFace) {
    auto familyNames = typeFace->createFamilyNameIterator();
    SkTypeface::LocalizedString famName;

    while (familyNames->next(&famName)) {
        auto name = famName.fString;
        if (!isValidAndUniqueFontName(name)) {
            continue;
        }

        auto family = sk_make_sp<MilestroFontStyleSet>(name);
        family->appendTypeface(typeFace);
        fFamilies.push_back(family);
    }

    familyNames->unref();
}

}
