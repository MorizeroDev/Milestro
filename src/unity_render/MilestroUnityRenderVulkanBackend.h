#ifndef MILESTRO_UNITY_RENDER_VULKAN_BACKEND_H
#define MILESTRO_UNITY_RENDER_VULKAN_BACKEND_H

#include "unity_render/MilestroUnityRenderSubmission.h"

#include <IUnityGraphics.h>
#include <IUnityInterface.h>

#include <cstdint>

namespace milestro::unity_render::vulkan {

// Vulkan submission can be completed from Unity's AccessQueue callback, which
// may run after the original render event returns. The callback owns the
// submission completion transition and the drain's pending-count transition.
using RenderCompletionCallback = void (*)(MilestroUnityRenderSubmission* submission,
                                          MilestroUnityRenderSubmissionStatus status,
                                          void* userData);

// Returned after AccessQueue accepts a submission. The completion callback is
// responsible for publishing the final submission status in that case.
constexpr int64_t kRenderDeferred = 1;

void OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType,
                           IUnityInterfaces* unityInterfaces,
                           UnityGfxRenderer renderer,
                           int renderEventId);
int64_t Render(MilestroUnityRenderSubmission& submission,
               RenderCompletionCallback completionCallback,
               void* completionUserData);

} // namespace milestro::unity_render::vulkan

#endif // MILESTRO_UNITY_RENDER_VULKAN_BACKEND_H
