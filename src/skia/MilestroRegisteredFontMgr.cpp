#include "include/core/SkFontArguments.h"
#include "include/core/SkFontMgr.h"
#include "include/core/SkFontStyle.h"
#include "include/core/SkRefCnt.h"
#include "include/core/SkStream.h"
#include "include/core/SkString.h"
#include "include/core/SkTypeface.h"
#include "include/core/SkTypes.h"
#include "include/ports/SkFontScanner_FreeType.h"
#include "src/core/SkFontDescriptor.h"
#include "Milestro/skia/MilestroRegisteredFontMgr.h"
#include "Milestro/skia/Typeface.h"
#include <algorithm>
#include <memory>
#include <utility>
#include <vector>
#include <src/ports/SkFontMgr_custom.h>

namespace milestro::skia {

MilestroFontFamilyList::MilestroFontFamilyList(std::vector<MilestroFontFamilyInfo> data)
    : data(std::move(data)) {
}

MilestroFontFamilyInfo *MilestroFontFamilyList::At(size_t position) {
    return &data[position];
}

MilestroFontFamilyInfo MilestroFontFamilyList::Get(size_t position) const {
    return data[position];
}

size_t MilestroFontFamilyList::Size() const {
    return data.size();
}

MilestroFontFaceList::MilestroFontFaceList(std::vector<MilestroFontFaceInfo> data)
    : data(std::move(data)) {
}

MilestroFontFaceInfo *MilestroFontFaceList::At(size_t position) {
    return &data[position];
}

MilestroFontFaceInfo MilestroFontFaceList::Get(size_t position) const {
    return data[position];
}

size_t MilestroFontFaceList::Size() const {
    return data.size();
}

MilestroRegisteredFontStyleSet::MilestroRegisteredFontStyleSet(SkString familyName)
    : fFamilyName(std::move(familyName)) {
}

void MilestroRegisteredFontStyleSet::appendTypeface(sk_sp<SkTypeface> typeface) {
    fStyles.emplace_back(std::move(typeface));
}

int MilestroRegisteredFontStyleSet::count() {
    return fStyles.size();
}

void MilestroRegisteredFontStyleSet::getStyle(int index, SkFontStyle *style, SkString *name) {
    SkASSERT(index < fStyles.size());
    if (style) {
        *style = fStyles[index]->fontStyle();
    }
    if (name) {
        name->reset();
    }
}

sk_sp<SkTypeface> MilestroRegisteredFontStyleSet::createTypeface(int index) {
    SkASSERT(index < fStyles.size());
    return fStyles[index];
}

sk_sp<SkTypeface> MilestroRegisteredFontStyleSet::matchStyle(const SkFontStyle &pattern) {
    return this->matchStyleCSS3(pattern);
}

SkString MilestroRegisteredFontStyleSet::getFamilyName() { return fFamilyName; }

MilestroRegisteredFontMgr::MilestroRegisteredFontMgr() : fScanner(SkFontScanner_Make_FreeType()) {
}

int MilestroRegisteredFontMgr::onCountFamilies() const {
    return fFamilies.size();
}

void MilestroRegisteredFontMgr::onGetFamilyName(int index, SkString *familyName) const {
    SkASSERT(index < fFamilies.size());
    familyName->set(fFamilies[index]->getFamilyName());
}

sk_sp<SkFontStyleSet> MilestroRegisteredFontMgr::onCreateStyleSet(int index) const {
    SkASSERT(index < fFamilies.size());
    return fFamilies[index];
}

sk_sp<SkFontStyleSet> MilestroRegisteredFontMgr::onMatchFamily(const char familyName[]) const {
    for (int i = 0; i < fFamilies.size(); ++i) {
        if (fFamilies[i]->getFamilyName().equals(familyName)) {
            return fFamilies[i];
        }
    }
    return nullptr;
}

sk_sp<SkTypeface> MilestroRegisteredFontMgr::onMatchFamilyStyle(const char familyName[],
                                                          const SkFontStyle &fontStyle) const {
    sk_sp<SkFontStyleSet> sset(this->matchFamily(familyName));
    if (!sset) {
        return nullptr;
    }
    return sset->matchStyle(fontStyle);
}

sk_sp<SkTypeface> MilestroRegisteredFontMgr::onMatchFamilyStyleCharacter(
    const char familyName[], const SkFontStyle &,
    const char *bcp47[], int bcp47Count,
    SkUnichar) const {
    return nullptr;
}

sk_sp<SkTypeface> MilestroRegisteredFontMgr::onMakeFromData(sk_sp<SkData> data, int ttcIndex) const {
    return nullptr;
}

sk_sp<SkTypeface> MilestroRegisteredFontMgr::onMakeFromStreamIndex(std::unique_ptr<SkStreamAsset> stream,
                                                             int ttcIndex) const {
    return nullptr;
}

sk_sp<SkTypeface> MilestroRegisteredFontMgr::onMakeFromStreamArgs(std::unique_ptr<SkStreamAsset> stream,
                                                            const SkFontArguments &args) const {
    return nullptr;
}

sk_sp<SkTypeface> MilestroRegisteredFontMgr::onMakeFromFile(const char path[], int ttcIndex) const {
    return nullptr;
}

sk_sp<SkTypeface> MilestroRegisteredFontMgr::onLegacyMakeTypeface(const char familyName[],
                                                            SkFontStyle style) const {
    sk_sp<SkTypeface> tf;

    if (familyName) {
        tf = this->onMatchFamilyStyle(familyName, style);
    }

    return tf;
}

std::vector<MilestroFontFaceInfo> MilestroRegisteredFontMgr::getFontFaces() const {
    return fFaces;
}

MilestroRegisteredFontMgr::RegisterResult MilestroRegisteredFontMgr::registerFont(std::unique_ptr<SkStreamAsset> stream,
                                                                      const SkString &filename) {
    bool exists =
        std::any_of(fFontRegistered.begin(), fFontRegistered.end(), [&filename](const SkString &registeredFilename) {
            return registeredFilename.equals(filename);
        });
    if (exists) {
        MILESTROLOG_DEBUG("---- already exist: {}", filename.c_str());
        return RegisterResult::Duplicated;
    }

    if (!stream) {
        MILESTROLOG_DEBUG("---- stream invalided: {}", filename.c_str());
        return RegisterResult::Failed;
    }

    int numFaces;
    if (!fScanner->scanFile(stream.get(), &numFaces)) {
        MILESTROLOG_DEBUG("---- failed to open file as a font: {}", filename.c_str());
        return RegisterResult::Failed;
    }

    struct PendingTypeface {
        SkString familyName;
        sk_sp<SkTypeface> typeface;
        MilestroFontFaceInfo info;
    };

    std::vector<PendingTypeface> pendingTypefaces;

    for (int faceIndex = 0; faceIndex < numFaces; ++faceIndex) {
        int numInstances;
        if (!fScanner->scanFace(stream.get(), faceIndex, &numInstances)) {
            // MILESTROLOG_DEBUG("---- failed to open <%s> as a font\n", filename.c_str());
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
                                        nullptr,
                                        nullptr)) {
                MILESTROLOG_DEBUG("---- failed to open file face as a font. file:{} face:{}", filename.c_str(), faceIndex);
                continue;
            }

            const int packedIndex = (instanceIndex << 16) + faceIndex;

            MilestroFontFaceInfo info;
            info.sourcePath = filename.c_str();
            info.familyName = realname.c_str();
            info.faceIndex = faceIndex;
            info.instanceIndex = instanceIndex;
            info.packedIndex = packedIndex;
            info.weight = style.weight();
            info.width = style.width();
            info.slant = style.slant();
            info.fixedPitch = isFixedPitch;

            pendingTypefaces.push_back(PendingTypeface{
                realname,
                sk_make_sp<SkTypeface_File>(
                    style, isFixedPitch, true, realname, filename.c_str(), packedIndex),
                std::move(info),
            });
        }
    }
    if (pendingTypefaces.empty()) {
        MILESTROLOG_DEBUG("---- no usable font faces in file: {}", filename.c_str());
        return RegisterResult::Failed;
    }

    fFontRegistered.push_back(filename);

    for (auto &pending : pendingTypefaces) {
        sk_sp<MilestroRegisteredFontStyleSet> addTo = nullptr;
        for (auto &item : fFamilies) {
            if (item->getFamilyName() == pending.familyName) {
                addTo = item;
                break;
            }
        }
        if (!addTo) {
            addTo = sk_make_sp<MilestroRegisteredFontStyleSet>(pending.familyName);
            fFamilies.push_back(addTo);
        }
        addTo->appendTypeface(std::move(pending.typeface));
        fFaces.emplace_back(std::move(pending.info));
    }
//    fStreamHolder.emplace_back(std::move(stream));
    return RegisterResult::Succeed;
}

}
