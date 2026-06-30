#ifndef MILESTRO_UNITY_RENDER_VULKAN_BACKEND_H
#define MILESTRO_UNITY_RENDER_VULKAN_BACKEND_H

#include "unity_render/MilestroUnityRenderSubmission.h"

#include <IUnityGraphics.h>
#include <IUnityInterface.h>

#include <cstdint>

namespace milestro::unity_render::vulkan {

void OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType,
                           IUnityInterfaces* unityInterfaces,
                           UnityGfxRenderer renderer,
                           int renderEventId);
int64_t Render(const MilestroUnityRenderSubmission& submission);

} // namespace milestro::unity_render::vulkan

#endif // MILESTRO_UNITY_RENDER_VULKAN_BACKEND_H
