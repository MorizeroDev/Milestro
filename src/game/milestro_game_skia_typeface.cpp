#include <Milestro/game/milestro_game_interface.h>
#include "Milestro/skia/FontRegistry.h"
#include "milestro_game_retcode.h"
#include "Milestro/skia/textlayout/FontCollection.h"

extern "C" {
int64_t MilestroSkiaTypefaceDestroy(milestro::skia::Typeface *&ret) try {
    delete ret;
    ret = nullptr;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTypefaceGetFamilyNameList(milestro::skia::Typeface *typeFace,
                                              milestro::skia::MilestroTypefaceFamilyNameList *&ret) try {
    ret = new milestro::skia::MilestroTypefaceFamilyNameList(typeFace->GetFamilyNames());
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTypefaceFamilyNameListDestroy(
        milestro::skia::MilestroTypefaceFamilyNameList *&ret) try {
    if (ret == nullptr) {
        return MILESTRO_API_RET_OK;
    }
    delete ret;
    ret = nullptr;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTypefaceFamilyNameListGetSize(
        milestro::skia::MilestroTypefaceFamilyNameList *list,
        uint64_t &size) try {
    size = list->Size();
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTypefaceFamilyNameListRefElementAt(
        milestro::skia::MilestroTypefaceFamilyNameList *list,
        milestro::skia::FontFamilyName *&ret,
        uint64_t index) try {
    ret = list->At(index);
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTypefaceFamilyNameListGetElementAt(
        milestro::skia::MilestroTypefaceFamilyNameList *list,
        milestro::skia::FontFamilyName *&ret,
        uint64_t index) try {
    ret = new milestro::skia::FontFamilyName(list->Get(index));
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTypefaceFamilyNameDestroy(milestro::skia::FontFamilyName *&ret) try {
    if (ret == nullptr) {
        return MILESTRO_API_RET_OK;
    }
    delete ret;
    ret = nullptr;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTypefaceFamilyNameGetName(milestro::skia::FontFamilyName *ret,
                                              uint8_t *&ptr,
                                              uint64_t &size) try {
    ptr = reinterpret_cast<uint8_t *>(ret->name.data());
    size = ret->name.size();
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroSkiaTypefaceFamilyNameGetLanguage(milestro::skia::FontFamilyName *ret,
                                                  uint8_t *&ptr,
                                                  uint64_t &size) try {
    ptr = reinterpret_cast<uint8_t *>(ret->language.data());
    size = ret->language.size();
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}
}
