#ifndef MILESTRO_GAME_INTERFACE_H
#define MILESTRO_GAME_INTERFACE_H

#include <cstdint>
#include <cstdlib>
#include <string>
#include <vector>

#ifdef MILESTRO_BUILDING_ENV

#include "milestro_game_types.h"
#include <Milestro/common/milestro_export_macros.h>

#else

#include "milestro_export_macros.h"
#include "milestro_game_types.h"

#endif

#ifdef __clang__
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wunknown-attributes"
#endif

extern "C" {

// 返回值为大版本号 major
MILESTRO_API int64_t MilestroGetVersion(int32_t &major, int32_t &minor, int32_t &patch);

}
#ifdef __clang__
#pragma clang diagnostic pop
#endif

#endif // MILESTRO_GAME_INTERFACE_H
