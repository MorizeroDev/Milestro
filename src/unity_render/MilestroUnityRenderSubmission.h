#ifndef MILESTRO_UNITY_RENDER_SUBMISSION_H
#define MILESTRO_UNITY_RENDER_SUBMISSION_H

#include "unity_render/MilestroUnityRenderTargetPayload.h"

#include <Milestro/game/milestro_game_types.h>

#include <cstdint>

enum class MilestroUnityDrawCommandKind : int32_t {
    Paragraph = 1,
    Image = 2,
    InputBoxSnapshot = 3,
    SlimText = 4,
};

enum class MilestroUnityDrawResourceOwnership : int32_t {
    None = 0,
    Paragraph = 1,
    InputBoxSnapshot = 2,
};

enum class MilestroUnityRenderSubmissionStatus : int32_t {
    Failed = -1,
    Pending = 0,
    Drawn = 1,
    Skipped = 2,
};

struct MilestroUnityDrawCommand {
    int32_t kind = 0;
    void* resource = nullptr;
    float x = 0.0f;
    float y = 0.0f;
    float width = 0.0f;
    float height = 0.0f;
    float clipX = 0.0f;
    float clipY = 0.0f;
    float clipWidth = 0.0f;
    float clipHeight = 0.0f;
    float visualOffsetX = 0.0f;
    float visualOffsetY = 0.0f;
    int32_t resourceOwnership = 0;
};

struct MilestroUnityRenderSubmission {
    MilestroUnityRenderTargetPayload target;
    MilestroUnityDrawCommand* commands = nullptr;
    int32_t commandCount = 0;

    // Written by the render-thread callback so managed code can free the per-event submission.
    // Values use MilestroUnityRenderSubmissionStatus, with 0 reserved for pending.
    int32_t completed = 0;
};

#endif // MILESTRO_UNITY_RENDER_SUBMISSION_H
