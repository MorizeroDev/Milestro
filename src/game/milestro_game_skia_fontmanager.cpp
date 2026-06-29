#include <Milestro/game/milestro_game_interface.h>
#include "Milestro/skia/FontManager.h"
#include "milestro_game_retcode.h"
#include "Milestro/skia/textlayout/FontCollection.h"

extern "C" {
int64_t MilestroSkiaFontManagerRegisterFontFromFile(uint8_t *path) {
    auto fontMgr = milestro::skia::GetFontManager();
    return (int64_t) fontMgr->RegisterFontFromFile(reinterpret_cast<char *>(path));
}

int64_t MilestroSkiaFontManagerGetFontFamilyList(milestro::skia::MilestroFontFamilyList *&ret) {
    auto fontMgr = milestro::skia::GetFontManager();
    ret = new milestro::skia::MilestroFontFamilyList(fontMgr->GetFontFamilies());
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

int64_t MilestroSkiaFontManagerGetFontFaceList(milestro::skia::MilestroFontFaceList *&ret) {
    auto fontMgr = milestro::skia::GetFontManager();
    ret = new milestro::skia::MilestroFontFaceList(fontMgr->GetFontFaces());
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
