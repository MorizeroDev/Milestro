#ifndef MILESTRO_UNITY_RENDER_SUBMISSION_H
#define MILESTRO_UNITY_RENDER_SUBMISSION_H

#include "unity_render/MilestroUnityRenderTargetPayload.h"

#include <Milestro/game/milestro_game_types.h>

#include <cstdint>

enum class MilestroUnityDrawCommandKind : int32_t {
    Paragraph = 1,
    Image = 2,
};

struct MilestroUnityDrawCommand {
    int32_t kind = 0;
    void* resource = nullptr;
    float x = 0.0f;
    float y = 0.0f;
    float width = 0.0f;
    float height = 0.0f;
};

struct MilestroUnityRenderSubmission {
    MilestroUnityRenderTargetPayload target;
    MilestroUnityDrawCommand* commands = nullptr;
    int32_t commandCount = 0;

    // Written by the render-thread callback so managed code can free the per-event submission.
    int32_t completed = 0;
};

#endif // MILESTRO_UNITY_RENDER_SUBMISSION_H
