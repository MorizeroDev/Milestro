#include <Milestro/game/milestro_game_retcode.h>
#include <Milestro/unicode/milestro_unicode_normalize.h>
#include <IUnityLog.h>
#include <Milestro/game/milestro_game_interface.h>
#include <Milestro/game/milestro_game_model.h>
#include <Milestro/log/log.h>

extern "C" {

MILESTRO_API int64_t MilestroUnicodeNormalizerCreate(milestro::unicode::Normalizer *&ret,
                                      uint8_t *name,
                                      int32_t mode
) try {
    ret = new milestro::unicode::Normalizer(
            std::string(reinterpret_cast<char *>(name)),
            mode
    );
    return MILESTRO_API_RET_OK;
} catch (std::runtime_error &e) {
    MILESTROLOG_ERROR("MilestroUnicodeNormalizerCreate: {}", e.what());
    return MILESTRO_API_RET_FAILED;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

MILESTRO_API int64_t MilestroUnicodeNormalizerDestroy(milestro::unicode::Normalizer *&ret) try {
    delete ret;
    ret = nullptr;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

MILESTRO_API int64_t MilestroUnicodeNormalizerNormalize(milestro::unicode::Normalizer *seg,
                                         milestro::game::model::BytesWrapper *&ret,
                                         uint8_t *text
) try {
    ret = new milestro::game::model::BytesWrapper(seg->normalize(
            std::string(reinterpret_cast<char *>(text))
    ));
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}
}
