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
#include <bit>
#include <cstdint>
#include <memory>
#include <mutex>
#include <tuple>
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
    auto baseTypeface = typeface;
    fStyles.push_back(RegisteredTypeface{
            std::move(typeface),
            std::move(baseTypeface),
            {},
            0,
            {},
            {},
    });
}

void MilestroRegisteredFontStyleSet::appendTypefaceWithVariations(sk_sp<SkTypeface> typeface,
                                                                  sk_sp<SkTypeface> baseTypeface,
                                                                  std::string sourcePath,
                                                                  int faceIndex,
                                                                  std::vector<VariationAxis> axes,
                                                                  std::vector<VariationCoordinate> position) {
    fStyles.push_back(RegisteredTypeface{
            std::move(typeface),
            std::move(baseTypeface),
            std::move(sourcePath),
            faceIndex,
            std::move(axes),
            std::move(position),
    });
}

int MilestroRegisteredFontStyleSet::count() {
    return static_cast<int>(fStyles.size());
}

void MilestroRegisteredFontStyleSet::getStyle(int index, SkFontStyle *style, SkString *name) {
    SkASSERT(index < fStyles.size());
    if (style) {
        *style = fStyles[index].typeface->fontStyle();
    }
    if (name) {
        name->reset();
    }
}

sk_sp<SkTypeface> MilestroRegisteredFontStyleSet::createTypeface(int index) {
    SkASSERT(index < fStyles.size());
    return fStyles[index].typeface;
}

sk_sp<SkTypeface> MilestroRegisteredFontStyleSet::matchStyle(const SkFontStyle &pattern) {
    auto matched = this->matchStyleCSS3(pattern);
    if (matched == nullptr) {
        return nullptr;
    }

    const auto record = std::find_if(fStyles.begin(), fStyles.end(), [&matched](const auto &item) {
        return item.typeface->uniqueID() == matched->uniqueID();
    });
    if (record == fStyles.end() || record->baseTypeface == nullptr) {
        return matched;
    }

    const auto weightAxis = std::find_if(record->axes.begin(), record->axes.end(), [](const auto &axis) {
        return axis.tag == SkFontArguments::VariationPosition::Coordinate::wght;
    });
    if (weightAxis == record->axes.end()) {
        return matched;
    }

    std::vector<VariationCoordinate> coordinates;
    coordinates.reserve(record->axes.size());
    for (const auto &axis: record->axes) {
        const auto existing =
                std::find_if(record->position.begin(), record->position.end(), [&axis](const auto &coordinate) {
                    return coordinate.axis == axis.tag;
                });
        const SkScalar value = axis.tag == SkFontArguments::VariationPosition::Coordinate::wght
                                       ? std::clamp(static_cast<SkScalar>(pattern.weight()), axis.min, axis.max)
                               : existing == record->position.end() ? axis.def
                                                                    : existing->value;
        coordinates.push_back({axis.tag, value});
    }

    VariationCacheKey cacheKey{record->sourcePath, record->faceIndex, coordinates};
    {
        const std::lock_guard lock(fVariationCacheMutex);
        const auto cached = fVariationCache.find(cacheKey);
        if (cached != fVariationCache.end()) {
            return cached->second;
        }
    }

    SkFontArguments arguments;
    arguments.setCollectionIndex(record->faceIndex);
    arguments.setVariationDesignPosition({coordinates.data(), static_cast<int>(coordinates.size())});
    auto cloned = record->baseTypeface->makeClone(arguments);
    if (cloned == nullptr) {
        return matched;
    }

    const std::lock_guard lock(fVariationCacheMutex);
    return fVariationCache.emplace(std::move(cacheKey), std::move(cloned)).first->second;
}

bool MilestroRegisteredFontStyleSet::VariationCacheKeyLess::operator()(const VariationCacheKey &left,
                                                                       const VariationCacheKey &right) const {
    if (left.sourcePath != right.sourcePath) {
        return left.sourcePath < right.sourcePath;
    }
    if (left.faceIndex != right.faceIndex) {
        return left.faceIndex < right.faceIndex;
    }
    if (left.coordinates.size() != right.coordinates.size()) {
        return left.coordinates.size() < right.coordinates.size();
    }
    for (size_t index = 0; index < left.coordinates.size(); ++index) {
        const auto leftCoordinate = std::tuple{
                left.coordinates[index].axis,
                std::bit_cast<uint32_t>(left.coordinates[index].value),
        };
        const auto rightCoordinate = std::tuple{
                right.coordinates[index].axis,
                std::bit_cast<uint32_t>(right.coordinates[index].value),
        };
        if (leftCoordinate != rightCoordinate) {
            return leftCoordinate < rightCoordinate;
        }
    }
    return false;
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
        sk_sp<SkTypeface> baseTypeface;
        int faceIndex;
        std::vector<SkFontParameters::Variation::Axis> axes;
        std::vector<SkFontArguments::VariationPosition::Coordinate> position;
        MilestroFontFaceInfo info;
    };

    std::vector<PendingTypeface> pendingTypefaces;

    for (int faceIndex = 0; faceIndex < numFaces; ++faceIndex) {
        int numInstances;
        if (!fScanner->scanFace(stream.get(), faceIndex, &numInstances)) {
            // MILESTROLOG_DEBUG("---- failed to open <%s> as a font\n", filename.c_str());
            continue;
        }
        sk_sp<SkTypeface> baseTypeface;
        for (int instanceIndex = 0; instanceIndex <= numInstances; ++instanceIndex) {
            bool isFixedPitch;
            SkString realname;
            SkFontStyle style = SkFontStyle(); // avoid uninitialized warning
            SkFontScanner::AxisDefinitions axes;
            SkFontScanner::VariationPosition position;
            if (!fScanner->scanInstance(stream.get(),
                                        faceIndex,
                                        instanceIndex,
                                        &realname,
                                        &style,
                                        &isFixedPitch,
                                        &axes,
                                        &position)) {
                MILESTROLOG_DEBUG("---- failed to open file face as a font. file:{} face:{}", filename.c_str(), faceIndex);
                continue;
            }

            const int packedIndex = (instanceIndex << 16) + faceIndex;
            auto typeface = sk_make_sp<SkTypeface_File>(
                    style, isFixedPitch, true, realname, filename.c_str(), packedIndex);
            if (instanceIndex == 0) {
                baseTypeface = typeface;
            }

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
                std::move(typeface),
                baseTypeface,
                faceIndex,
                std::vector<SkFontParameters::Variation::Axis>(axes.begin(), axes.end()),
                std::vector<SkFontArguments::VariationPosition::Coordinate>(position.begin(), position.end()),
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
        addTo->appendTypefaceWithVariations(std::move(pending.typeface),
                                            std::move(pending.baseTypeface),
                                            pending.info.sourcePath,
                                            pending.faceIndex,
                                            std::move(pending.axes),
                                            std::move(pending.position));
        fFaces.emplace_back(std::move(pending.info));
    }
//    fStreamHolder.emplace_back(std::move(stream));
    return RegisterResult::Succeed;
}

}
