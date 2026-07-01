#include <Milestro/game/milestro_game_retcode.h>
#include <Milestro/unicode/milestro_unicode_util.h>
#include <Milestro/game/milestro_game_interface.h>
#include <Milestro/game/milestro_game_model.h>
#include "Milestro/unicode/milestro_icu.h"

extern "C" {

MILESTRO_API int64_t MilestroCopyAndLoadICU(uint8_t *ptr, uint64_t size, uint8_t *path) try {
    auto ret = milestro::unicode::CopyAndLoadICU(
            ptr,
            size,
            path == nullptr ? "" : std::string(reinterpret_cast<char *>(path))
    );
    if (!ret) {
        return MILESTRO_API_RET_FAILED;
    }
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

MILESTRO_API int64_t MilestroLoadICU(uint8_t *ptr, uint8_t *path) try {
    auto ret = milestro::unicode::LoadICU(
            ptr,
            path == nullptr ? "" : std::string(reinterpret_cast<char *>(path))
    );
    if (!ret) {
        return MILESTRO_API_RET_FAILED;
    }
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

MILESTRO_API int64_t MilestroUnicodeCaseMapToUpper(
        milestro::game::model::BytesWrapper *&ret,
        uint8_t *locale,
        uint8_t *text
) try {
    ret = new milestro::game::model::BytesWrapper(
            milestro::unicode::toUpper(
                    std::string(reinterpret_cast<char *>(locale)),
                    std::string(reinterpret_cast<char *>(text))
            )
    );
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

MILESTRO_API int64_t MilestroUnicodeCaseMapToLower(
        milestro::game::model::BytesWrapper *&ret,
        uint8_t *locale,
        uint8_t *text
) try {
    ret = new milestro::game::model::BytesWrapper(
            milestro::unicode::toLower(
                    std::string(reinterpret_cast<char *>(locale)),
                    std::string(reinterpret_cast<char *>(text))
            )
    );
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}


}
