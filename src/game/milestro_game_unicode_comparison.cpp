#include <Milestro/game/milestro_game_retcode.h>
#include <Milestro/unicode/milestro_unicode_comparison.h>
#include <IUnityLog.h>
#include <Milestro/game/milestro_game_interface.h>
#include <Milestro/log/log.h>

extern "C" {

MILESTRO_API int64_t MilizeStringComparatorCreate(milestro::unicode::StringComparator*& ret, uint8_t* collation) try {
    ret = new milestro::unicode::StringComparator(std::string(reinterpret_cast<char*>(collation)));
    return MILESTRO_API_RET_OK;
} catch (std::runtime_error& e) {
    MILESTROLOG_ERROR("MilizeStringComparatorCreate: {}", e.what());
    return MILESTRO_API_RET_FAILED;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

MILESTRO_API int64_t MilizeStringComparatorDestroy(milestro::unicode::StringComparator*& ret) try {
    delete ret;
    ret = nullptr;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

MILESTRO_API int64_t MilizeStringComparatorCompare(milestro::unicode::StringComparator* cmp, int32_t& result, uint8_t* a, uint8_t* b) try {
    result = cmp->compare(reinterpret_cast<char*>(a), reinterpret_cast<char*>(b));
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

}
