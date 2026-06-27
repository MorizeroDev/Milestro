#ifndef MILESTRO_GAME_UNITY_RENDER_H
#define MILESTRO_GAME_UNITY_RENDER_H

#include <IUnityGraphics.h>
#include <IUnityInterface.h>

#include <Milestro/game/milestro_game_types.h>

#include <cstdint>

struct MilestroUnityRenderTargetPayload {
    void *colorRenderBufferHandle = nullptr;
    int32_t width = 0;
    int32_t height = 0;
    int32_t srgb = 0;
    int32_t clearBeforeDraw = 1;

    milestro::skia::textlayout::Paragraph *paragraph = nullptr;
    float paragraphX = 0.0f;
    float paragraphY = 0.0f;

    milestro::skia::Image *image = nullptr;
    float imageX = 0.0f;
    float imageY = 0.0f;
    float imageWidth = 0.0f;
    float imageHeight = 0.0f;
};

namespace milestro::game::unity_render {

void Load(IUnityInterfaces *unityInterfaces);
void Unload();

} // namespace milestro::game::unity_render

#if defined(__APPLE__)
namespace milestro::game::unity_render::metal {
void OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType,
                           IUnityInterfaces *unityInterfaces,
                           UnityGfxRenderer renderer);
int64_t Render(const MilestroUnityRenderTargetPayload &payload);
} // namespace milestro::game::unity_render::metal
#endif

#endif // MILESTRO_GAME_UNITY_RENDER_H
