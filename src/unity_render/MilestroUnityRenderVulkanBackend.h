#ifndef MILESTRO_UNITY_RENDER_VULKAN_BACKEND_H
#define MILESTRO_UNITY_RENDER_VULKAN_BACKEND_H

#include "unity_render/MilestroUnityRenderSubmission.h"

#include <IUnityGraphics.h>
#include <IUnityInterface.h>

#include <cstdint>

namespace milestro::unity_render::vulkan {

struct VulkanSubmissionResult {
    MilestroUnityRenderSubmission* submission = nullptr;
    MilestroUnityRenderSubmissionStatus status = MilestroUnityRenderSubmissionStatus::Failed;
};

using VulkanSubmissionCompletion = void (*)(MilestroUnityRenderSubmission*, MilestroUnityRenderSubmissionStatus);

void OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType,
                           IUnityInterfaces* unityInterfaces,
                           UnityGfxRenderer renderer,
                           int stagingEventId,
                           int directPrepareEventId,
                           int directSubmitEventId);

int64_t CreateTarget(void* nativeTexture,
                     int32_t width,
                     int32_t height,
                     int32_t backend,
                     uint64_t deviceEpoch,
                     void*& target,
                     uint64_t& generation);
int64_t DestroyTarget(void*& target, uint64_t generation, uint64_t deviceEpoch, int32_t& retirementPending);

bool IsSubmissionTargetValid(const MilestroUnityRenderSubmission& submission);
VulkanSubmissionResult RenderStaging(MilestroUnityRenderSubmission* submission);
bool BeginDirectBatch(uint64_t batchToken);
VulkanSubmissionResult PrepareDirect(uint64_t batchToken, MilestroUnityRenderSubmission* submission);
bool FinishDirectBatchPrepare(uint64_t batchToken);
bool SubmitDirectPrepared(uint64_t batchToken, VulkanSubmissionCompletion complete);
bool FailDirectPrepared(uint64_t batchToken, VulkanSubmissionCompletion complete);
void FailDirectPrepared(VulkanSubmissionCompletion complete);
void FailDirectPreparedForTarget(void* target,
                                 uint64_t generation,
                                 uint64_t deviceEpoch,
                                 VulkanSubmissionCompletion complete);
std::size_t PendingDirectBatchCount();

bool CollectRetiredTargets();
bool HasPendingRetirements();

} // namespace milestro::unity_render::vulkan

#endif // MILESTRO_UNITY_RENDER_VULKAN_BACKEND_H
