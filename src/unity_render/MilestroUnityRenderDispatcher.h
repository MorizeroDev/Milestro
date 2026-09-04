#ifndef MILESTRO_UNITY_RENDER_DISPATCHER_H
#define MILESTRO_UNITY_RENDER_DISPATCHER_H

#include <IUnityInterface.h>

#include <cstdint>

namespace milestro::unity_render {

void Load(IUnityInterfaces *unityInterfaces);
void Unload();

void *GetRenderEventFuncForExport();
int64_t GetMetalRenderEventIdForExport(int32_t &eventId);
int64_t GetRenderTextureEventIdForExport(int32_t graphicsBackend, int32_t &eventId);
int64_t GetVulkanRenderEventIdsForExport(int32_t vulkanBackend, int32_t &firstEventId, int32_t &secondEventId);
int64_t EnqueueSubmissionForExport(int32_t graphicsBackend, void *submission);
int64_t CreateVulkanTargetForExport(void *nativeTexture,
                                    int32_t width,
                                    int32_t height,
                                    int32_t vulkanBackend,
                                    uint64_t deviceEpoch,
                                    void *&target,
                                    uint64_t &generation);
int64_t DestroyVulkanTargetForExport(void *&target, int32_t &retirementPending);
int64_t GetPayloadAbiInfoForExport(uint32_t& abiVersion,
                                   uint64_t& layoutFingerprint,
                                   uint32_t& targetSize,
                                   uint32_t& submissionSize,
                                   uint32_t& targetEffectiveScaleOffset,
                                   uint32_t& targetDeviceEpochOffset,
                                   uint32_t& submissionTargetOffset,
                                   uint32_t& submissionCompletedOffset);
int64_t GetDeviceEpochForExport(uint64_t& deviceEpoch);
int64_t GetDiagnosticsSnapshotForExport(uint32_t& abiVersion,
                                        uint32_t& structSize,
                                        uint64_t& acceptedSubmissionCount,
                                        uint64_t& rejectedSubmissionCount,
                                        int32_t& hasLastAcceptedSubmission,
                                        int32_t& lastAcceptedGraphicsBackend,
                                        int32_t& lastAcceptedRasterWidth,
                                        int32_t& lastAcceptedRasterHeight,
                                        float& lastAcceptedEffectiveScale,
                                        uint64_t& lastAcceptedDeviceEpoch,
                                        uint64_t& currentDeviceEpoch);
int64_t CreateD3D12ExternalTextureForExport(int32_t width,
                                            int32_t height,
                                            int32_t storageSrgb,
                                            int32_t preferredFormat,
                                            void *&texture);
int64_t DestroyD3D12ExternalTextureForExport(void *&texture);

} // namespace milestro::unity_render

#endif // MILESTRO_UNITY_RENDER_DISPATCHER_H
