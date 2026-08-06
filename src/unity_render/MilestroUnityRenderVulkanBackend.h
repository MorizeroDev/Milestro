#ifndef MILESTRO_UNITY_RENDER_VULKAN_BACKEND_H
#define MILESTRO_UNITY_RENDER_VULKAN_BACKEND_H

#include "unity_render/MilestroUnityRenderVulkanLifecycle.h"

#include <IUnityGraphics.h>
#include <IUnityInterface.h>

#include <cstdint>

namespace milestro::unity_render::vulkan {

void OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType,
                           IUnityInterfaces* unityInterfaces,
                           UnityGfxRenderer renderer,
                           int firstRenderEventId);
void OnRenderEvent(int eventId);

int64_t GetEventInfo(int32_t& prepareEventId, int32_t& submitEventId, uint64_t& epoch);
int64_t EnqueueSubmission(uint64_t epoch, void* submission, uint64_t& serial);
int64_t CancelSubmission(uint64_t epoch, uint64_t serial);

} // namespace milestro::unity_render::vulkan

#endif // MILESTRO_UNITY_RENDER_VULKAN_BACKEND_H
