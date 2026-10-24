#ifndef MILESTRO_GAME_RETCODE_INTERNAL_H
#define MILESTRO_GAME_RETCODE_INTERNAL_H

#include <Milestro/game/milestro_game_retcode.h>

constexpr int64_t MILESTRO_API_RET_GLYPH_NOTFOUND = -2;

template <typename T>
int64_t MilestroDestroyHandle(T*& handle) {
    T* owned = handle;
    handle = nullptr;
    if (owned == nullptr) {
        return MILESTRO_API_RET_OK;
    }

    try {
        delete owned;
        return MILESTRO_API_RET_OK;
    } catch (...) {
        return MILESTRO_API_STATUS_MAKE(MILESTRO_API_STATUS_SEVERITY_WARNING,
                                        MILESTRO_API_STATUS_FLAG_CONSUMED,
                                        MILESTRO_API_STATUS_DOMAIN_NATIVE,
                                        MILESTRO_API_STATUS_OPERATION_DESTROY,
                                        MILESTRO_API_STATUS_DETAIL_UNEXPECTED_EXCEPTION);
    }
}

#endif //MILESTRO_GAME_RETCODE_INTERNAL_H
