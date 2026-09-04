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

MILESTRO_API int64_t MilestroUnityRenderGetVulkanRenderEventIds(int32_t vulkanBackend,
                                                                int32_t& firstEventId,
                                                                int32_t& secondEventId) {
    return milestro::unity_render::GetVulkanRenderEventIdsForExport(vulkanBackend, firstEventId, secondEventId);
}

MILESTRO_API int64_t MilestroUnityRenderEnqueueSubmission(int32_t graphicsBackend, void *submission) {
    return milestro::unity_render::EnqueueSubmissionForExport(graphicsBackend, submission);
}

MILESTRO_API int64_t MilestroUnityRenderCreateVulkanTarget(void* nativeTexture,
                                                           int32_t width,
                                                           int32_t height,
                                                           int32_t vulkanBackend,
                                                           uint64_t deviceEpoch,
                                                           void*& target,
                                                           uint64_t& generation) {
    return milestro::unity_render::CreateVulkanTargetForExport(nativeTexture,
                                                               width,
                                                               height,
                                                               vulkanBackend,
                                                               deviceEpoch,
                                                               target,
                                                               generation);
}

MILESTRO_API int64_t MilestroUnityRenderDestroyVulkanTarget(void*& target,
                                                            uint64_t generation,
                                                            uint64_t deviceEpoch,
                                                            int32_t& retirementPending) {
    return milestro::unity_render::DestroyVulkanTargetForExport(target, generation, deviceEpoch, retirementPending);
}

MILESTRO_API int64_t MilestroUnityRenderGetPayloadAbiInfo(uint32_t& abiVersion,
                                                          uint64_t& layoutFingerprint,
                                                          uint32_t& targetSize,
                                                          uint32_t& submissionSize,
                                                          uint32_t& targetEffectiveScaleOffset,
                                                          uint32_t& targetDeviceEpochOffset,
                                                          uint32_t& submissionTargetOffset,
                                                          uint32_t& submissionCompletedOffset) {
    return milestro::unity_render::GetPayloadAbiInfoForExport(abiVersion,
                                                              layoutFingerprint,
                                                              targetSize,
                                                              submissionSize,
                                                              targetEffectiveScaleOffset,
                                                              targetDeviceEpochOffset,
                                                              submissionTargetOffset,
                                                              submissionCompletedOffset);
}

MILESTRO_API int64_t MilestroUnityRenderGetDeviceEpoch(uint64_t& deviceEpoch) {
    return milestro::unity_render::GetDeviceEpochForExport(deviceEpoch);
}

MILESTRO_API int64_t MilestroUnityRenderGetDiagnosticsSnapshot(uint32_t& abiVersion,
                                                               uint32_t& structSize,
                                                               uint64_t& acceptedSubmissionCount,
                                                               uint64_t& rejectedSubmissionCount,
                                                               int32_t& hasLastAcceptedSubmission,
                                                               int32_t& lastAcceptedGraphicsBackend,
                                                               int32_t& lastAcceptedRasterWidth,
                                                               int32_t& lastAcceptedRasterHeight,
                                                               float& lastAcceptedEffectiveScale,
                                                               uint64_t& lastAcceptedDeviceEpoch,
                                                               uint64_t& currentDeviceEpoch) {
    return milestro::unity_render::GetDiagnosticsSnapshotForExport(abiVersion,
                                                                    structSize,
                                                                    acceptedSubmissionCount,
                                                                    rejectedSubmissionCount,
                                                                    hasLastAcceptedSubmission,
                                                                    lastAcceptedGraphicsBackend,
                                                                    lastAcceptedRasterWidth,
                                                                    lastAcceptedRasterHeight,
                                                                    lastAcceptedEffectiveScale,
                                                                    lastAcceptedDeviceEpoch,
                                                                    currentDeviceEpoch);
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
