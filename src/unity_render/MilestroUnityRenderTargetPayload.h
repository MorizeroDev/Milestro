#ifndef MILESTRO_UNITY_RENDER_TARGET_PAYLOAD_H
#define MILESTRO_UNITY_RENDER_TARGET_PAYLOAD_H

#include <Milestro/game/milestro_game_types.h>

#include <cstdint>

struct MilestroUnityRenderTargetPayload {
    int32_t graphicsBackend = 0;
    int32_t handleKind = 0;
    void *colorRenderBufferHandle = nullptr;
    void *nativeTextureHandle = nullptr;
    int32_t width = 0;
    int32_t height = 0;
    int32_t srgb = 0;
    int32_t clearBeforeDraw = 1;
    int32_t msaaSamples = 1;
    int32_t resolveStrategy = 0;
    int32_t preferredFormat = 0;

    milestro::skia::textlayout::Paragraph *paragraph = nullptr;
    float paragraphX = 0.0f;
    float paragraphY = 0.0f;

    milestro::skia::Image *image = nullptr;
    float imageX = 0.0f;
    float imageY = 0.0f;
    float imageWidth = 0.0f;
    float imageHeight = 0.0f;

    // Written by the render-thread callback so managed code can free the per-event payload.
    int32_t completed = 0;
};

#endif // MILESTRO_UNITY_RENDER_TARGET_PAYLOAD_H
