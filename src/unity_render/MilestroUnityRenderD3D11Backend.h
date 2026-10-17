#ifndef MILESTRO_UNITY_RENDER_D3D11_BACKEND_H
#define MILESTRO_UNITY_RENDER_D3D11_BACKEND_H

#include "unity_render/MilestroUnityRenderSubmission.h"

#include <IUnityGraphics.h>
#include <IUnityInterface.h>

#include <cstdint>

namespace milestro::unity_render::d3d11 {

void OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType,
                           IUnityInterfaces* unityInterfaces,
                           UnityGfxRenderer renderer);
int64_t Render(const MilestroUnityRenderSubmission& submission);
int64_t DestroyTarget(void* nativeTexture);

} // namespace milestro::unity_render::d3d11

#endif // MILESTRO_UNITY_RENDER_D3D11_BACKEND_H
