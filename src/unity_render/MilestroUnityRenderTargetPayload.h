#ifndef MILESTRO_UNITY_RENDER_TARGET_PAYLOAD_H
#define MILESTRO_UNITY_RENDER_TARGET_PAYLOAD_H

#include <cstdint>

struct MilestroUnityRenderTargetPayload {
    int32_t graphicsBackend = 0;
    int32_t handleKind = 0;
    void* colorRenderBufferHandle = nullptr;
    void* nativeTextureHandle = nullptr;
    int32_t width = 0;
    int32_t height = 0;
    int32_t srgb = 0;
    int32_t clearBeforeDraw = 1;
    int32_t msaaSamples = 1;
    int32_t resolveStrategy = 0;
    int32_t preferredFormat = 0;
};

#endif // MILESTRO_UNITY_RENDER_TARGET_PAYLOAD_H
