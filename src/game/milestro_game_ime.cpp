#include <Milestro/game/milestro_game_interface.h>
#include <Milestro/game/milestro_game_retcode.h>
#include <Milestro/ime/ImeSession.h>

extern "C" {

MILESTRO_API int64_t MilestroImeCancelComposition(int32_t& result) {
    result = static_cast<int32_t>(milestro::ime::CancelComposition());
    return MILESTRO_API_RET_OK;
}
}
