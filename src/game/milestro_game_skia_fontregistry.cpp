#include <Milestro/game/milestro_game_interface.h>
#include "Milestro/skia/Font.h"
#include "Milestro/skia/FontRegistry.h"
#include "milestro_game_retcode.h"
#include "Milestro/skia/textlayout/FontCollection.h"
#include "milestro_game_skia_fontfamilytokens.h"
#include <include/core/SkFontMgr.h>
#include <include/core/SkTypeface.h>
#include <algorithm>
#include <cmath>
#include <string>
#include <utility>
#include <vector>

extern "C" {
// Keep the existing FontRegistry C ABI while TextLayout FontCollection owns resolution policy.
int64_t MilestroSkiaFontRegistryResolveTypeface(milestro::skia::Font *&ret,
                                        uint8_t *family,
                                        uint64_t familySize,
                                        int32_t weight,
                                        float size,
                                        int32_t fallbackToSystem) try {
    std::string familyName;
    if (family != nullptr && familySize > 0) {
        familyName.assign(reinterpret_cast<const char *>(family), static_cast<size_t>(familySize));
    }

    std::vector<milestro::skia::FontFamilyToken> families;
    if (!familyName.empty()) {
        families.emplace_back(milestro::skia::FontFamilyToken::Bare(std::move(familyName)));
    }

    auto fontCollection = milestro::skia::textlayout::GetFontCollection();
    auto typeface = fontCollection->ResolveTypeface(families, weight, fallbackToSystem != 0);
    SkFont font(typeface, std::max(0.0f, size));
    font.setEdging(SkFont::Edging::kAntiAlias);
    ret = new milestro::skia::Font(std::move(font));
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaFontRegistryResolveTypefaceFromFamilies(milestro::skia::Font *&ret,
                                                            uint8_t **families,
                                                            uint32_t familyCount,
                                                            int32_t weight,
                                                            float size,
                                                            int32_t fallbackToSystem) try {
    auto fontCollection = milestro::skia::textlayout::GetFontCollection();
    auto typeface = fontCollection->ResolveTypeface(ReadBareFamilyTokens(families, familyCount),
                                                    weight,
                                                    fallbackToSystem != 0);
    SkFont font(typeface, std::max(0.0f, size));
    font.setEdging(SkFont::Edging::kAntiAlias);
    ret = new milestro::skia::Font(std::move(font));
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaFontRegistryResolveTypefaceFromFamilyTokens(milestro::skia::Font *&ret,
                                                                uint8_t **families,
                                                                int32_t *familyKinds,
                                                                uint32_t familyCount,
                                                                int32_t weight,
                                                                float size,
                                                                int32_t fallbackToSystem) try {
    auto fontCollection = milestro::skia::textlayout::GetFontCollection();
    auto typeface = fontCollection->ResolveTypeface(ReadFamilyTokens(families, familyKinds, familyCount),
                                                    weight,
                                                    fallbackToSystem != 0);
    SkFont font(typeface, std::max(0.0f, size));
    font.setEdging(SkFont::Edging::kAntiAlias);
    ret = new milestro::skia::Font(std::move(font));
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaFontRegistryResetFontFamilyKeywordMappings() try {
    auto fontCollection = milestro::skia::textlayout::GetFontCollection();
    fontCollection->ClearFontFamilyKeywordMappings();
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaFontRegistrySetFontFamilyKeywordMapping(uint8_t *keyword,
                                                            uint64_t keywordSize,
                                                            uint8_t **families,
                                                            int32_t *familyKinds,
                                                            uint32_t familyCount) try {
    std::string keywordName;
    if (keyword != nullptr && keywordSize > 0) {
        keywordName.assign(reinterpret_cast<const char *>(keyword), static_cast<size_t>(keywordSize));
    }

    auto fontCollection = milestro::skia::textlayout::GetFontCollection();
    fontCollection->SetFontFamilyKeywordMapping(std::move(keywordName),
                                                ReadFamilyTokens(families, familyKinds, familyCount));
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaFontRegistryRegisterFontFromFile(uint8_t *path, uint64_t pathSize) {
    std::string pathString;
    if (path != nullptr && pathSize > 0) {
        pathString.assign(reinterpret_cast<const char *>(path), static_cast<size_t>(pathSize));
    }

    auto fontRegistry = milestro::skia::GetFontRegistry();
    const auto result = fontRegistry->RegisterFontFromFile(pathString.c_str());
    if (result != milestro::skia::MilestroRegisteredFontMgr::RegisterResult::Failed) {
        milestro::skia::textlayout::GetFontCollection()->clearCaches();
    }
    return static_cast<int64_t>(result);
}

int64_t MilestroSkiaFontRegistryGetRegisteredFontFamilyList(milestro::skia::MilestroFontFamilyList *&ret) {
    auto fontRegistry = milestro::skia::GetFontRegistry();
    ret = new milestro::skia::MilestroFontFamilyList(fontRegistry->GetRegisteredFontFamilies());
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaFontFamilyListDestroy(milestro::skia::MilestroFontFamilyList *&ret) {
    if (ret == nullptr) {
        return MILESTRO_API_RET_OK;
    }
    delete ret;
    ret = nullptr;
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaFontFamilyListGetSize(milestro::skia::MilestroFontFamilyList *list,
                                          uint64_t &size) {
    size = list->Size();
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaFontFamilyListRefElementAt(milestro::skia::MilestroFontFamilyList *list,
                                               milestro::skia::MilestroFontFamilyInfo *&ret,
                                               uint64_t index) {
    ret = list->At(index);
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaFontFamilyListGetElementAt(milestro::skia::MilestroFontFamilyList *list,
                                               milestro::skia::MilestroFontFamilyInfo *&ret,
                                               uint64_t index) {
    ret = new milestro::skia::MilestroFontFamilyInfo(list->Get(index));
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaFontFamilyInfoDestroy(milestro::skia::MilestroFontFamilyInfo *&ret) {
    if (ret == nullptr) {
        return MILESTRO_API_RET_OK;
    }
    delete ret;
    ret = nullptr;
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaFontFamilyInfoGetName(milestro::skia::MilestroFontFamilyInfo *ret,
                                          uint8_t *&ptr,
                                          uint64_t &size) {
    ptr = reinterpret_cast<uint8_t *>(ret->name.data());
    size = ret->name.size();
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaFontRegistryGetRegisteredFontFaceList(milestro::skia::MilestroFontFaceList *&ret) {
    auto fontRegistry = milestro::skia::GetFontRegistry();
    ret = new milestro::skia::MilestroFontFaceList(fontRegistry->GetRegisteredFontFaces());
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaFontFaceListDestroy(milestro::skia::MilestroFontFaceList *&ret) {
    if (ret == nullptr) {
        return MILESTRO_API_RET_OK;
    }
    delete ret;
    ret = nullptr;
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaFontFaceListGetSize(milestro::skia::MilestroFontFaceList *list,
                                        uint64_t &size) {
    size = list->Size();
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaFontFaceListRefElementAt(milestro::skia::MilestroFontFaceList *list,
                                             milestro::skia::MilestroFontFaceInfo *&ret,
                                             uint64_t index) {
    ret = list->At(index);
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaFontFaceListGetElementAt(milestro::skia::MilestroFontFaceList *list,
                                             milestro::skia::MilestroFontFaceInfo *&ret,
                                             uint64_t index) {
    ret = new milestro::skia::MilestroFontFaceInfo(list->Get(index));
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaFontFaceInfoDestroy(milestro::skia::MilestroFontFaceInfo *&ret) {
    if (ret == nullptr) {
        return MILESTRO_API_RET_OK;
    }
    delete ret;
    ret = nullptr;
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaFontFaceInfoGetSourcePath(milestro::skia::MilestroFontFaceInfo *ret,
                                              uint8_t *&ptr,
                                              uint64_t &size) {
    ptr = reinterpret_cast<uint8_t *>(ret->sourcePath.data());
    size = ret->sourcePath.size();
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaFontFaceInfoGetFamilyName(milestro::skia::MilestroFontFaceInfo *ret,
                                              uint8_t *&ptr,
                                              uint64_t &size) {
    ptr = reinterpret_cast<uint8_t *>(ret->familyName.data());
    size = ret->familyName.size();
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaFontFaceInfoGetFaceIndex(milestro::skia::MilestroFontFaceInfo *ret,
                                             int32_t &value) {
    value = ret->faceIndex;
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaFontFaceInfoGetInstanceIndex(milestro::skia::MilestroFontFaceInfo *ret,
                                                 int32_t &value) {
    value = ret->instanceIndex;
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaFontFaceInfoGetPackedIndex(milestro::skia::MilestroFontFaceInfo *ret,
                                               int32_t &value) {
    value = ret->packedIndex;
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaFontFaceInfoGetWeight(milestro::skia::MilestroFontFaceInfo *ret,
                                          int32_t &value) {
    value = ret->weight;
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaFontFaceInfoGetWidth(milestro::skia::MilestroFontFaceInfo *ret,
                                         int32_t &value) {
    value = ret->width;
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaFontFaceInfoGetSlant(milestro::skia::MilestroFontFaceInfo *ret,
                                         int32_t &value) {
    value = ret->slant;
    return MILESTRO_API_RET_OK;
}

int64_t MilestroSkiaFontFaceInfoGetFixedPitch(milestro::skia::MilestroFontFaceInfo *ret,
                                              int32_t &value) {
    value = ret->fixedPitch ? 1 : 0;
    return MILESTRO_API_RET_OK;
}
}
