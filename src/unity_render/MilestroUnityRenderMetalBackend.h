#ifndef MILESTRO_UNITY_RENDER_METAL_BACKEND_H
#define MILESTRO_UNITY_RENDER_METAL_BACKEND_H

#include "unity_render/MilestroUnityRenderTargetPayload.h"

#include <IUnityGraphics.h>
#include <IUnityInterface.h>

#include <cstdint>

namespace milestro::unity_render::metal {

void OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType,
                           IUnityInterfaces *unityInterfaces,
                           UnityGfxRenderer renderer);
int64_t Render(const MilestroUnityRenderTargetPayload &payload);

} // namespace milestro::unity_render::metal

#endif // MILESTRO_UNITY_RENDER_METAL_BACKEND_H
