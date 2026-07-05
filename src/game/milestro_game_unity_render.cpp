#include "milestro_game_unity_render.h"

#include "unity_render/MilestroUnityRenderDispatcher.h"

#include <Milestro/common/milestro_export_macros.h>

#include <cstdint>

namespace milestro::game::unity_render {

void Load(IUnityInterfaces *unityInterfaces) {
    milestro::unity_render::Load(unityInterfaces);
}

void Unload() {
    milestro::unity_render::Unload();
}

} // namespace milestro::game::unity_render

extern "C" {

MILESTRO_API void *MilestroUnityRenderGetRenderEventAndDataFunc() {
    return milestro::unity_render::GetRenderEventFuncForExport();
}

MILESTRO_API int64_t MilestroUnityRenderGetMetalRenderEventId(int32_t &eventId) {
    return milestro::unity_render::GetMetalRenderEventIdForExport(eventId);
}

MILESTRO_API int64_t MilestroUnityRenderGetRenderTextureEventId(int32_t graphicsBackend, int32_t &eventId) {
    return milestro::unity_render::GetRenderTextureEventIdForExport(graphicsBackend, eventId);
}

MILESTRO_API int64_t MilestroUnityRenderEnqueueSubmission(int32_t graphicsBackend, void *submission) {
    return milestro::unity_render::EnqueueSubmissionForExport(graphicsBackend, submission);
}

MILESTRO_API int64_t MilestroUnityRenderCreateD3D12ExternalTexture(int32_t width,
                                                                   int32_t height,
                                                                   int32_t storageSrgb,
                                                                   int32_t preferredFormat,
                                                                   void *&texture) {
    return milestro::unity_render::CreateD3D12ExternalTextureForExport(width,
                                                                       height,
                                                                       storageSrgb,
                                                                       preferredFormat,
                                                                       texture);
}

MILESTRO_API int64_t MilestroUnityRenderDestroyD3D12ExternalTexture(void *&texture) {
    return milestro::unity_render::DestroyD3D12ExternalTextureForExport(texture);
}

}
