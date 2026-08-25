#ifndef MILESTRO_UNITY_RENDER_TARGET_PAYLOAD_H
#define MILESTRO_UNITY_RENDER_TARGET_PAYLOAD_H

#include <cstddef>
#include <cstdint>
#include <limits>

struct MilestroUnityRenderTargetPayload {
    uint32_t abiVersion = 0;
    uint32_t structSize = 0;
    int32_t graphicsBackend = 0;
    int32_t handleKind = 0;
    void* colorRenderBufferHandle = nullptr;
    void* nativeTextureHandle = nullptr;
    int32_t width = 0;
    int32_t height = 0;
    int32_t colorSpace = 0;
    int32_t storageSrgb = 0;
    int32_t clearBeforeDraw = 1;
    int32_t msaaSamples = 1;
    int32_t resolveStrategy = 0;
    int32_t preferredFormat = 0;
    float effectiveScale = 1.0f;
    uint64_t deviceEpoch = 0;
};

inline constexpr uint32_t kMilestroUnityRenderPayloadAbiVersion = 1;
static_assert(sizeof(MilestroUnityRenderTargetPayload) <= std::numeric_limits<uint32_t>::max());
inline constexpr uint32_t kMilestroUnityRenderTargetPayloadSize =
        static_cast<uint32_t>(sizeof(MilestroUnityRenderTargetPayload));
inline constexpr uint32_t kMilestroUnityRenderTargetEffectiveScaleOffset =
        offsetof(MilestroUnityRenderTargetPayload, effectiveScale);
inline constexpr uint32_t kMilestroUnityRenderTargetDeviceEpochOffset =
        offsetof(MilestroUnityRenderTargetPayload, deviceEpoch);

constexpr uint64_t MilestroUnityRenderNextDeviceEpoch(uint64_t current) noexcept {
    if (current == 0) {
        return 1;
    }
    if (current == std::numeric_limits<uint64_t>::max()) {
        return current;
    }
    return current + 1;
}

static_assert(offsetof(MilestroUnityRenderTargetPayload, abiVersion) == 0);
static_assert(offsetof(MilestroUnityRenderTargetPayload, structSize) == sizeof(uint32_t));
static_assert(MilestroUnityRenderNextDeviceEpoch(0) == 1);
static_assert(MilestroUnityRenderNextDeviceEpoch(1) == 2);
static_assert(MilestroUnityRenderNextDeviceEpoch(std::numeric_limits<uint64_t>::max()) ==
              std::numeric_limits<uint64_t>::max());

#endif // MILESTRO_UNITY_RENDER_TARGET_PAYLOAD_H
