#include <Milestro/game/milestro_game_retcode.h>
#include <Milestro/unicode/milestro_unicode_transliterator.h>
#include <Milestro/game/milestro_game_interface.h>
#include <Milestro/game/milestro_game_model.h>

extern "C" {

MILESTRO_API int64_t MilestroUnicodeTransliteratorCreate(milestro::unicode::Transliterator *&ret,
                                                     uint8_t *id,
                                                     int32_t direction
) try {
    ret = new milestro::unicode::Transliterator(
            std::string(reinterpret_cast<char *>(id)), direction
    );
    return MILESTRO_API_RET_OK;
} catch (std::runtime_error &e) {
    MILESTROLOG_ERROR("MilestroUnicodeTransliteratorCreate: {}", e.what());
    return MILESTRO_API_RET_FAILED;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

MILESTRO_API int64_t MilestroUnicodeTransliteratorDestroy(milestro::unicode::Transliterator *&ret) try {
    delete ret;
    ret = nullptr;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

MILESTRO_API int64_t MilestroUnicodeTransliteratorTransliterate(milestro::unicode::Transliterator *t,
                                                            milestro::game::model::BytesWrapper *&output,
                                                            uint8_t *input
) try {
    output = new milestro::game::model::BytesWrapper(
            t->transliterate(std::string(reinterpret_cast<char *>(input)))
    );
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

}
