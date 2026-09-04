#ifndef MILESTRO_UNITY_RENDER_SUBMISSION_H
#define MILESTRO_UNITY_RENDER_SUBMISSION_H

#include "unity_render/MilestroUnityRenderTargetPayload.h"

#include <Milestro/game/milestro_game_types.h>

#include <cmath>
#include <cstddef>
#include <cstdint>
#include <limits>

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
    uint32_t abiVersion = 0;
    uint32_t structSize = 0;
    MilestroUnityRenderTargetPayload target;
    MilestroUnityDrawCommand* commands = nullptr;
    int32_t commandCount = 0;

    // Written by the render-thread callback so managed code can free the per-event submission.
    // Values use MilestroUnityRenderSubmissionStatus, with 0 reserved for pending.
    int32_t completed = 0;
};

static_assert(sizeof(MilestroUnityRenderSubmission) <= std::numeric_limits<uint32_t>::max());
inline constexpr uint32_t kMilestroUnityRenderSubmissionSize =
        static_cast<uint32_t>(sizeof(MilestroUnityRenderSubmission));
inline constexpr uint32_t kMilestroUnityRenderSubmissionTargetOffset = offsetof(MilestroUnityRenderSubmission, target);
inline constexpr uint32_t kMilestroUnityRenderSubmissionCompletedOffset =
        offsetof(MilestroUnityRenderSubmission, completed);

static_assert(offsetof(MilestroUnityRenderSubmission, abiVersion) == 0);
static_assert(offsetof(MilestroUnityRenderSubmission, structSize) == sizeof(uint32_t));

constexpr uint64_t MilestroUnityRenderLayoutFingerprintMix(uint64_t hash, uint64_t value) noexcept {
    return (hash ^ value) * 1099511628211ULL;
}

constexpr uint64_t MilestroUnityRenderPayloadLayoutFingerprint() noexcept {
    uint64_t hash = 14695981039346656037ULL;
#define MILESTRO_MIX_LAYOUT_VALUE(value) hash = MilestroUnityRenderLayoutFingerprintMix(hash, (value))
#define MILESTRO_MIX_MEMBER(type, member) MILESTRO_MIX_LAYOUT_VALUE(offsetof(type, member))
    MILESTRO_MIX_LAYOUT_VALUE(kMilestroUnityRenderTargetPayloadSize);
    MILESTRO_MIX_MEMBER(MilestroUnityRenderTargetPayload, abiVersion);
    MILESTRO_MIX_MEMBER(MilestroUnityRenderTargetPayload, structSize);
    MILESTRO_MIX_MEMBER(MilestroUnityRenderTargetPayload, graphicsBackend);
    MILESTRO_MIX_MEMBER(MilestroUnityRenderTargetPayload, handleKind);
    MILESTRO_MIX_MEMBER(MilestroUnityRenderTargetPayload, colorRenderBufferHandle);
    MILESTRO_MIX_MEMBER(MilestroUnityRenderTargetPayload, nativeTextureHandle);
    MILESTRO_MIX_MEMBER(MilestroUnityRenderTargetPayload, width);
    MILESTRO_MIX_MEMBER(MilestroUnityRenderTargetPayload, height);
    MILESTRO_MIX_MEMBER(MilestroUnityRenderTargetPayload, colorSpace);
    MILESTRO_MIX_MEMBER(MilestroUnityRenderTargetPayload, storageSrgb);
    MILESTRO_MIX_MEMBER(MilestroUnityRenderTargetPayload, clearBeforeDraw);
    MILESTRO_MIX_MEMBER(MilestroUnityRenderTargetPayload, msaaSamples);
    MILESTRO_MIX_MEMBER(MilestroUnityRenderTargetPayload, resolveStrategy);
    MILESTRO_MIX_MEMBER(MilestroUnityRenderTargetPayload, preferredFormat);
    MILESTRO_MIX_MEMBER(MilestroUnityRenderTargetPayload, vulkanBackend);
    MILESTRO_MIX_MEMBER(MilestroUnityRenderTargetPayload, vulkanTarget);
    MILESTRO_MIX_MEMBER(MilestroUnityRenderTargetPayload, vulkanTargetGeneration);
    MILESTRO_MIX_MEMBER(MilestroUnityRenderTargetPayload, effectiveScale);
    MILESTRO_MIX_MEMBER(MilestroUnityRenderTargetPayload, deviceEpoch);
    MILESTRO_MIX_LAYOUT_VALUE(kMilestroUnityRenderSubmissionSize);
    MILESTRO_MIX_MEMBER(MilestroUnityRenderSubmission, abiVersion);
    MILESTRO_MIX_MEMBER(MilestroUnityRenderSubmission, structSize);
    MILESTRO_MIX_MEMBER(MilestroUnityRenderSubmission, target);
    MILESTRO_MIX_MEMBER(MilestroUnityRenderSubmission, commands);
    MILESTRO_MIX_MEMBER(MilestroUnityRenderSubmission, commandCount);
    MILESTRO_MIX_MEMBER(MilestroUnityRenderSubmission, completed);
#undef MILESTRO_MIX_MEMBER
#undef MILESTRO_MIX_LAYOUT_VALUE
    return hash;
}

inline constexpr uint64_t kMilestroUnityRenderPayloadLayoutFingerprint = MilestroUnityRenderPayloadLayoutFingerprint();
static_assert(kMilestroUnityRenderPayloadLayoutFingerprint != 0);

#if INTPTR_MAX == INT64_MAX
static_assert(kMilestroUnityRenderTargetPayloadSize == 104);
static_assert(kMilestroUnityRenderSubmissionSize == 128);
static_assert(kMilestroUnityRenderTargetEffectiveScaleOffset == 88);
static_assert(kMilestroUnityRenderTargetDeviceEpochOffset == 96);
static_assert(kMilestroUnityRenderSubmissionTargetOffset == 8);
static_assert(kMilestroUnityRenderSubmissionCompletedOffset == 124);
static_assert(kMilestroUnityRenderPayloadLayoutFingerprint == 11249664113689606655ULL);
#elif defined(__arm__) && !defined(__aarch64__)
// Android armeabi-v7a uses the AAPCS eight-byte alignment for uint64_t.
static_assert(kMilestroUnityRenderTargetPayloadSize == 88);
static_assert(kMilestroUnityRenderSubmissionSize == 108);
static_assert(kMilestroUnityRenderTargetEffectiveScaleOffset == 72);
static_assert(kMilestroUnityRenderTargetDeviceEpochOffset == 80);
static_assert(kMilestroUnityRenderSubmissionTargetOffset == 8);
static_assert(kMilestroUnityRenderSubmissionCompletedOffset == 104);
static_assert(kMilestroUnityRenderPayloadLayoutFingerprint == 15803143509196474059ULL);
#endif

inline bool MilestroUnityRenderSubmissionHasCurrentAbi(const MilestroUnityRenderSubmission* submission,
                                                       uint64_t expectedDeviceEpoch) noexcept {
    if (submission == nullptr || expectedDeviceEpoch == 0 ||
        submission->abiVersion != kMilestroUnityRenderPayloadAbiVersion ||
        submission->structSize != kMilestroUnityRenderSubmissionSize) {
        return false;
    }

    const MilestroUnityRenderTargetPayload& target = submission->target;
    return target.abiVersion == kMilestroUnityRenderPayloadAbiVersion &&
           target.structSize == kMilestroUnityRenderTargetPayloadSize && std::isfinite(target.effectiveScale) &&
           target.effectiveScale > 0.0f && target.deviceEpoch == expectedDeviceEpoch;
}

inline bool
MilestroUnityRenderSubmissionCanReplaceQueuedContent(const MilestroUnityRenderSubmission* submission) noexcept {
    return submission != nullptr && submission->target.clearBeforeDraw != 0;
}

#endif // MILESTRO_UNITY_RENDER_SUBMISSION_H
