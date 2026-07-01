#include <Milestro/game/milestro_game_retcode.h>
#include <Milestro/unicode/milestro_unicode_segments.h>
#include <IUnityLog.h>
#include <Milestro/game/milestro_game_interface.h>
#include <Milestro/game/milestro_game_model.h>
#include <Milestro/log/log.h>

extern "C" {

MILESTRO_API int64_t MilestroUnicodeSegmenterCreate(milestro::unicode::Segmenter *&ret,
                                     uint8_t *locale,
                                     uint8_t *text
) try {
    ret = new milestro::unicode::Segmenter(
            std::string(reinterpret_cast<char *>(locale)),
            std::string(reinterpret_cast<char *>(text))
    );
    return MILESTRO_API_RET_OK;
} catch (std::runtime_error &e) {
    MILESTROLOG_ERROR("MilestroUnicodeSegmenterCreate: {}", e.what());
    return MILESTRO_API_RET_FAILED;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

MILESTRO_API int64_t MilestroUnicodeSegmenterDestroy(milestro::unicode::Segmenter *&ret) try {
    delete ret;
    ret = nullptr;
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

MILESTRO_API int64_t MilestroUnicodeSegmenterFirst(milestro::unicode::Segmenter *seg,
                                               int32_t &ret) try {
    ret = seg->first();
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

MILESTRO_API int64_t MilestroUnicodeSegmenterNext(milestro::unicode::Segmenter *seg,
                                   int32_t &ret
) try {
    ret = seg->next();
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}


MILESTRO_API int64_t MilestroUnicodeSegmenterCurrent(milestro::unicode::Segmenter *seg,
                                                 int32_t &ret
) try {
    ret = seg->current();
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}


MILESTRO_API int64_t MilestroUnicodeSegmenterPrevious(milestro::unicode::Segmenter *seg,
                                                  int32_t &ret
) try {
    ret = seg->previous();
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}


MILESTRO_API int64_t MilestroUnicodeSegmenterSubString(milestro::unicode::Segmenter *seg,
                                                   milestro::game::model::BytesWrapper *&ret,
                                                   int32_t start,
                                                   int32_t len
) try {
    ret = new milestro::game::model::BytesWrapper(seg->subString(start, len));
    return MILESTRO_API_RET_OK;
} catch (...) {
    return MILESTRO_API_RET_FAILED;
}

}
