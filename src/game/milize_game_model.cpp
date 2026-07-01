#include <Milestro/game/milestro_game_interface.h>
#include <Milestro/game/milestro_game_model.h>
#include <Milestro/game/milestro_game_retcode.h>

extern "C" {

MILESTRO_API int64_t MilestroGameModelDataEnvelopDestroy(milestro::game::model::DataEnvelop *&ret) {
    if (ret == nullptr) {
        return MILESTRO_API_RET_OK;
    }
    delete ret;
    ret = nullptr;
    return MILESTRO_API_RET_OK;
}

MILESTRO_API int64_t MilestroGameModelBytesWrapperCreate(milestro::game::model::BytesWrapper *&ret,
                                                     uint8_t *ptr,
                                                     uint64_t size
) {
    ret = new milestro::game::model::BytesWrapper(ptr, size);
    return MILESTRO_API_RET_OK;
}

MILESTRO_API int64_t MilestroGameModelBytesWrapperCStr(milestro::game::model::BytesWrapper* ret,
                                                    uint8_t*& ptr,
                                                    uint64_t& size) {
    ptr = reinterpret_cast<uint8_t*>(ret->GetPtr());
    size = ret->GetSize();
    return MILESTRO_API_RET_OK;
}

MILESTRO_API int64_t MilestroGameModelNumberWrapperCreate(milestro::game::model::NumberWrapper *&ret,
                                                      double number
) {
    ret = new milestro::game::model::NumberWrapper(number);
    return MILESTRO_API_RET_OK;
}

MILESTRO_API int64_t MilestroGameModelNumberWrapperValue(milestro::game::model::NumberWrapper* ret,
                                                   double& value) {
    value = ret->GetValue();
    return MILESTRO_API_RET_OK;
}


}
