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
#include "Milestro/skia/MilestroFontManager.h"
#include "Milestro/skia/Typeface.h"
#include <memory>
#include <src/ports/SkFontMgr_custom.h>

#if _WIN32

#include "include/ports/SkTypeface_win.h"

#endif

class SkData;
namespace milestro::skia {

MilestroFontStyleSet::MilestroFontStyleSet(SkString familyName)
    : fFamilyName(std::move(familyName)) {
}

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

MilestroFontManager::MilestroFontManager() : fDefaultFamily(nullptr),
                                             fScanner(std::make_unique<SkFontScanner_FreeType>()) {
#if _WIN32
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

bool MilestroFontManager::registerTypeface(char *path) {
    SkString filename(path);
    std::unique_ptr<SkStreamAsset> stream = SkStream::MakeFromFile(filename.c_str());
    if (!stream) {
        MILESTROLOG_DEBUG("---- failed to open: {}", filename.c_str());
        return false;
    }

    int numFaces;
    if (!fScanner->scanFile(stream.get(), &numFaces)) {
        MILESTROLOG_DEBUG("---- failed to open file as a fount: {} ", filename.c_str());
        return false;
    }

    for (int faceIndex = 0; faceIndex < numFaces; ++faceIndex) {
        int numInstances;
        if (!fScanner->scanFace(stream.get(), faceIndex, &numInstances)) {
            // SkDebugf("---- failed to open <%s> as a font\n", filename.c_str());
            continue;
        }
        for (int instanceIndex = 0; instanceIndex <= numInstances; ++instanceIndex) {
            bool isFixedPitch;
            SkString realname;
            SkFontStyle style = SkFontStyle(); // avoid uninitialized warning
            if (!fScanner->scanInstance(stream.get(),
                                        faceIndex,
                                        instanceIndex,
                                        &realname,
                                        &style,
                                        &isFixedPitch,
                                        nullptr)) {
                SkDebugf("---- failed to open fileface as a font. file:{} face:{}", filename.c_str(), faceIndex);
                continue;
            }

            sk_sp<MilestroFontStyleSet> addTo = nullptr;
            for (auto item : fFamilies) {
                if (item->getFamilyName() == realname) {
                    addTo = item;
                }
            }
            if (!addTo) {
                addTo = sk_make_sp<MilestroFontStyleSet>(realname);
                fFamilies.push_back(addTo);
            }
            addTo->appendTypeface(sk_make_sp<SkTypeface_File>(
                style, isFixedPitch, true, realname, filename.c_str(),
                (instanceIndex << 16) + faceIndex));
        }
    }
    return true;
}

}
