#ifndef MILESTRO_UNITY_RENDER_D3D12_BACKEND_H
#define MILESTRO_UNITY_RENDER_D3D12_BACKEND_H

#include "unity_render/MilestroUnityRenderSubmission.h"

#include <IUnityGraphics.h>
#include <IUnityInterface.h>

#include <cstdint>

namespace milestro::unity_render::d3d12 {

void OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType,
                           IUnityInterfaces* unityInterfaces,
                           UnityGfxRenderer renderer,
                           int32_t renderEventId);
int64_t Render(const MilestroUnityRenderSubmission& submission);
int64_t CreateExternalTexture(int32_t width, int32_t height, int32_t srgb, int32_t preferredFormat, void*& texture);
int64_t DestroyExternalTexture(void*& texture);

} // namespace milestro::unity_render::d3d12

#endif // MILESTRO_UNITY_RENDER_D3D12_BACKEND_H
