#ifndef MILESTRO_UNITY_RENDER_VULKAN_ADAPTER_H
#define MILESTRO_UNITY_RENDER_VULKAN_ADAPTER_H

#include "unity_render/MilestroUnityRenderSubmission.h"
#include "unity_render/MilestroUnityRenderVulkanLifecycle.h"

#include <IUnityGraphics.h>
#include <IUnityGraphicsVulkan.h>

#include <cstdint>
#include <memory>

namespace milestro::unity_render::vulkan {

class VulkanTargetState {
public:
    virtual ~VulkanTargetState() = default;
};

struct VulkanTarget {
    BackendBinding backend;
    void* nativeTexture = nullptr;
    int32_t width = 0;
    int32_t height = 0;
    uint64_t deviceEpoch = 0;
    uint64_t generation = 0;
    std::unique_ptr<VulkanTargetState> state;
};

struct PreparedVulkanSubmission {
    VulkanTarget* target = nullptr;
    MilestroUnityRenderSubmission* submission = nullptr;
    UnityVulkanImage image{};
    UnityVulkanInstance instance{};
};

struct VulkanPrepareResult {
    MilestroUnityRenderSubmissionStatus status = MilestroUnityRenderSubmissionStatus::Failed;
    bool requiresSubmit = false;
};

class VulkanRenderBackend {
public:
    virtual ~VulkanRenderBackend() = default;

    [[nodiscard]] virtual VulkanBackendKind Kind() const noexcept = 0;
    [[nodiscard]] virtual bool RequiresSubmitEvent() const noexcept = 0;

    virtual void Initialize(IUnityGraphicsVulkan* vulkan, const UnityVulkanInstance& instance) = 0;
    virtual void Shutdown() = 0;

    [[nodiscard]] virtual std::unique_ptr<VulkanTargetState> CreateTarget(uint64_t generation,
                                                                          std::size_t pixelBytes) = 0;
    virtual bool RetireTarget(std::unique_ptr<VulkanTargetState>& state) = 0;
    virtual void DestroyTargetImmediately(std::unique_ptr<VulkanTargetState> state) = 0;
    [[nodiscard]] virtual std::size_t PendingRetirementCount() const noexcept = 0;
    [[nodiscard]] virtual bool HasPendingRetirements() const noexcept = 0;
    virtual bool CollectRetired(uint64_t safeFrameNumber, bool force) = 0;

    virtual VulkanPrepareResult
    Prepare(VulkanTarget& target, MilestroUnityRenderSubmission& submission, PreparedVulkanSubmission& prepared) = 0;
    virtual MilestroUnityRenderSubmissionStatus Submit(PreparedVulkanSubmission& prepared) = 0;
};

VulkanRenderBackend& DirectBackend();
VulkanRenderBackend& StagingCopyBackend();
PFN_vkVoidFunction ResolveInstanceProcWithVulkan10Fallback(const UnityVulkanInstance& instance, const char* name);

inline VulkanRenderBackend& BackendForKind(VulkanBackendKind kind) {
    return kind == VulkanBackendKind::StagingCopy ? StagingCopyBackend() : DirectBackend();
}

} // namespace milestro::unity_render::vulkan

#endif // MILESTRO_UNITY_RENDER_VULKAN_ADAPTER_H
