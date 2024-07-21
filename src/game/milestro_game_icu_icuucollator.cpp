#include "Milestro/game/milestro_game_retcode.h"
#include "Milestro/icu/IcuUCollator.h"
#include <IUnityLog.h>
#include <Milestro/game/milestro_game_interface.h>
#include <Milestro/log/log.h>

extern "C" {
int64_t MilestroIcuIcuUCollatorCreate(milestro::icu::IcuUCollator*& ret, uint8_t* collation) try {
    ret = new milestro::icu::IcuUCollator(std::move(std::string(reinterpret_cast<char*>(collation))));
    return MILESTRO_API_RET_OK;
} catch (std::runtime_error& e) {
    MILESTROLOG_ERROR("MilestroIcuIcuUCollatorCreate: {}", e.what());
    return MILESTRO_API_RET_FAILED;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroIcuIcuUCollatorDestroy(milestro::icu::IcuUCollator*& ret) try {
    delete ret;
    ret = nullptr;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroIcuIcuUCollatorCompare(milestro::icu::IcuUCollator* cmp, int32_t& result, uint8_t* a, uint8_t* b) try {
    result = cmp->compare(reinterpret_cast<char*>(a), reinterpret_cast<char*>(b));
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

int64_t MilestroIcuIcuUCollatorSetAttribute(milestro::icu::IcuUCollator* collator, int32_t attr, int32_t value) try {
    collator->setAttribute((UColAttribute)attr, (UColAttributeValue)value);
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}
}
