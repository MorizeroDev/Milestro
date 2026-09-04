#include "unity_render/MilestroUnityRenderVulkanBackend.h"

#include "game/milestro_game_retcode.h"
#include "unity_render/MilestroUnityGraphicsBackend.h"
#include "unity_render/MilestroUnityRenderTextureHandleKind.h"
#include "unity_render/MilestroUnityRenderVulkanAdapter.h"

#include <IUnityGraphicsVulkan.h>

#include <algorithm>
#include <cstddef>
#include <cstdint>
#include <limits>
#include <memory>
#include <new>
#include <vector>

#include "unity_render/MilestroUnityRenderLog.h"

namespace milestro::unity_render::vulkan {

namespace {

constexpr std::size_t kMaximumRegisteredTargets = 256;

IUnityGraphicsVulkan* gVulkan = nullptr;
UnityVulkanInstance gInstance{};
std::vector<std::unique_ptr<VulkanTarget>> gTargets;
std::vector<PreparedVulkanSubmission> gPreparedDirect;
uint64_t gNextTargetGeneration = 0;

bool ConfigureEvent(int eventId, UnityVulkanGraphicsQueueAccess queueAccess, uint32_t flags) {
    if (gVulkan == nullptr || gVulkan->ConfigureEvent == nullptr || eventId < 0) {
        return false;
    }

    UnityVulkanPluginEventConfig config{};
    config.renderPassPrecondition = kUnityVulkanRenderPass_EnsureOutside;
    config.graphicsQueueAccess = queueAccess;
    config.flags = flags;
    gVulkan->ConfigureEvent(eventId, &config);
    return true;
}

VulkanTarget* FindTarget(void* handle) {
    const auto found = std::find_if(gTargets.begin(), gTargets.end(), [handle](const auto& target) {
        return target.get() == handle;
    });
    return found == gTargets.end() ? nullptr : found->get();
}

bool TryPixelBytes(int32_t width, int32_t height, std::size_t& bytes) {
    bytes = 0;
    if (width <= 0 || height <= 0) {
        return false;
    }
    const auto unsignedWidth = static_cast<std::size_t>(width);
    const auto unsignedHeight = static_cast<std::size_t>(height);
    if (unsignedWidth > std::numeric_limits<std::size_t>::max() / unsignedHeight) {
        return false;
    }
    const std::size_t pixels = unsignedWidth * unsignedHeight;
    if (pixels > std::numeric_limits<std::size_t>::max() / 4U) {
        return false;
    }
    bytes = pixels * 4U;
    return bytes != 0;
}

VulkanTarget* ValidatedTarget(const MilestroUnityRenderSubmission& submission) {
    const MilestroUnityRenderTargetPayload& payload = submission.target;
    if (payload.graphicsBackend != static_cast<int32_t>(MilestroUnityGraphicsBackend::Vulkan) ||
        payload.handleKind != static_cast<int32_t>(MilestroUnityRenderTextureHandleKind::NativeTexture) ||
        payload.nativeTextureHandle == nullptr || payload.vulkanTarget == nullptr ||
        payload.vulkanTargetGeneration == 0 || payload.width <= 0 || payload.height <= 0 || payload.msaaSamples != 1) {
        return nullptr;
    }

    VulkanBackendKind kind = VulkanBackendKind::Direct;
    if (!VulkanBackendKindFromRaw(payload.vulkanBackend, kind)) {
        return nullptr;
    }

    VulkanTarget* target = FindTarget(payload.vulkanTarget);
    if (target == nullptr || target->generation != payload.vulkanTargetGeneration ||
        target->deviceEpoch != payload.deviceEpoch || target->nativeTexture != payload.nativeTextureHandle ||
        target->width != payload.width || target->height != payload.height || !target->backend.Matches(kind)) {
        return nullptr;
    }
    return target;
}

void DestroyAllTargetsImmediately() {
    for (auto& target: gTargets) {
        VulkanBackendKind kind = VulkanBackendKind::Direct;
        if (target->backend.Matches(VulkanBackendKind::StagingCopy)) {
            kind = VulkanBackendKind::StagingCopy;
        }
        BackendForKind(kind).DestroyTargetImmediately(std::move(target->state));
    }
    gTargets.clear();
}

VulkanSubmissionResult PrepareWithBackend(MilestroUnityRenderSubmission* submission, VulkanBackendKind expectedKind) {
    if (submission == nullptr) {
        return {};
    }
    VulkanTarget* target = ValidatedTarget(*submission);
    if (target == nullptr || !target->backend.Matches(expectedKind)) {
        MILESTROLOG_ERROR("Milestro Vulkan rejected a stale, mismatched, or invalid target registration.");
        return {submission, MilestroUnityRenderSubmissionStatus::Failed};
    }

    PreparedVulkanSubmission prepared;
    VulkanPrepareResult result = BackendForKind(expectedKind).Prepare(*target, *submission, prepared);
    if (!result.requiresSubmit) {
        return {submission, result.status};
    }

    if (gPreparedDirect.size() >= kMaximumRegisteredTargets) {
        return {submission, MilestroUnityRenderSubmissionStatus::Failed};
    }
    gPreparedDirect.push_back(prepared);
    return {nullptr, MilestroUnityRenderSubmissionStatus::Pending};
}

} // namespace

void OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType,
                           IUnityInterfaces* unityInterfaces,
                           UnityGfxRenderer renderer,
                           int stagingEventId,
                           int directPrepareEventId,
                           int directSubmitEventId) {
    const bool shutdown = eventType == kUnityGfxDeviceEventShutdown || eventType == kUnityGfxDeviceEventBeforeReset;
    const bool initialize = eventType == kUnityGfxDeviceEventInitialize || eventType == kUnityGfxDeviceEventAfterReset;
    if (!shutdown && !initialize) {
        return;
    }

    if (shutdown || renderer != kUnityGfxRendererVulkan) {
        DestroyAllTargetsImmediately();
        DirectBackend().Shutdown();
        StagingCopyBackend().Shutdown();
        gPreparedDirect.clear();
        gVulkan = nullptr;
        gInstance = {};
        return;
    }

    if (gVulkan != nullptr || !gTargets.empty() || !gPreparedDirect.empty()) {
        DestroyAllTargetsImmediately();
        DirectBackend().Shutdown();
        StagingCopyBackend().Shutdown();
        gPreparedDirect.clear();
        gVulkan = nullptr;
        gInstance = {};
    }

    gVulkan = unityInterfaces != nullptr ? unityInterfaces->Get<IUnityGraphicsVulkan>() : nullptr;
    if (gVulkan == nullptr || gVulkan->Instance == nullptr) {
        MILESTROLOG_ERROR("Unity Vulkan graphics interface is unavailable.");
        return;
    }

    gInstance = gVulkan->Instance();
    if (gInstance.instance == VK_NULL_HANDLE || gInstance.physicalDevice == VK_NULL_HANDLE ||
        gInstance.device == VK_NULL_HANDLE || gInstance.graphicsQueue == VK_NULL_HANDLE ||
        gInstance.getInstanceProcAddr == nullptr) {
        MILESTROLOG_ERROR("Unity Vulkan instance is incomplete.");
        gVulkan = nullptr;
        gInstance = {};
        return;
    }

    try {
        gTargets.reserve(kMaximumRegisteredTargets);
        gPreparedDirect.reserve(kMaximumRegisteredTargets);
    } catch (const std::bad_alloc&) {
        MILESTROLOG_ERROR("Milestro Vulkan target registry allocation failed.");
        gVulkan = nullptr;
        gInstance = {};
        return;
    }

    const uint32_t prepareFlags = kUnityVulkanEventConfigFlag_EnsurePreviousFrameSubmission |
                                  kUnityVulkanEventConfigFlag_ModifiesCommandBuffersState;
    const uint32_t submitFlags = kUnityVulkanEventConfigFlag_EnsurePreviousFrameSubmission |
                                 kUnityVulkanEventConfigFlag_FlushCommandBuffers |
                                 kUnityVulkanEventConfigFlag_SyncWorkerThreads;
    if (!ConfigureEvent(stagingEventId, kUnityVulkanGraphicsQueueAccess_DontCare, prepareFlags) ||
        !ConfigureEvent(directPrepareEventId, kUnityVulkanGraphicsQueueAccess_DontCare, prepareFlags) ||
        !ConfigureEvent(directSubmitEventId, kUnityVulkanGraphicsQueueAccess_Allow, submitFlags)) {
        MILESTROLOG_ERROR("Milestro Vulkan event configuration failed.");
        gVulkan = nullptr;
        gInstance = {};
        return;
    }

    DirectBackend().Initialize(gVulkan, gInstance);
    StagingCopyBackend().Initialize(gVulkan, gInstance);
}

int64_t CreateTarget(void* nativeTexture,
                     int32_t width,
                     int32_t height,
                     int32_t backend,
                     uint64_t deviceEpoch,
                     void*& target,
                     uint64_t& generation) {
    target = nullptr;
    generation = 0;
    VulkanBackendKind kind = VulkanBackendKind::Direct;
    std::size_t pixelBytes = 0;
    if (gVulkan == nullptr || nativeTexture == nullptr || deviceEpoch == 0 ||
        !VulkanBackendKindFromRaw(backend, kind) || !TryPixelBytes(width, height, pixelBytes) ||
        gTargets.size() + DirectBackend().PendingRetirementCount() + StagingCopyBackend().PendingRetirementCount() >=
                kMaximumRegisteredTargets ||
        gNextTargetGeneration == std::numeric_limits<uint64_t>::max()) {
        return MILESTRO_API_RET_FAILED;
    }

    std::unique_ptr<VulkanTarget> registered;
    try {
        registered = std::make_unique<VulkanTarget>();
        if (!registered->backend.Bind(kind)) {
            return MILESTRO_API_RET_FAILED;
        }
        registered->nativeTexture = nativeTexture;
        registered->width = width;
        registered->height = height;
        registered->deviceEpoch = deviceEpoch;
        registered->generation = gNextTargetGeneration + 1;
        registered->state = BackendForKind(kind).CreateTarget(registered->generation, pixelBytes);
        if (registered->state == nullptr) {
            return MILESTRO_API_RET_FAILED;
        }
        gTargets.push_back(std::move(registered));
    } catch (const std::bad_alloc&) {
        return MILESTRO_API_RET_FAILED;
    }

    VulkanTarget* created = gTargets.back().get();
    gNextTargetGeneration = created->generation;
    target = created;
    generation = created->generation;
    return MILESTRO_API_RET_OK;
}

int64_t DestroyTarget(void*& target, int32_t& retirementPending) {
    retirementPending = 0;
    if (target == nullptr) {
        return MILESTRO_API_RET_OK;
    }

    const auto found = std::find_if(gTargets.begin(), gTargets.end(), [handle = target](const auto& candidate) {
        return candidate.get() == handle;
    });
    if (found == gTargets.end()) {
        target = nullptr;
        return MILESTRO_API_RET_OK;
    }

    VulkanBackendKind kind = (*found)->backend.Matches(VulkanBackendKind::StagingCopy) ? VulkanBackendKind::StagingCopy
                                                                                       : VulkanBackendKind::Direct;
    if (!BackendForKind(kind).RetireTarget((*found)->state)) {
        return MILESTRO_API_RET_FAILED;
    }
    gTargets.erase(found);
    target = nullptr;
    retirementPending = HasPendingRetirements() ? 1 : 0;
    return MILESTRO_API_RET_OK;
}

bool IsSubmissionTargetValid(const MilestroUnityRenderSubmission& submission) {
    return ValidatedTarget(submission) != nullptr;
}

VulkanSubmissionResult RenderStaging(MilestroUnityRenderSubmission* submission) {
    return PrepareWithBackend(submission, VulkanBackendKind::StagingCopy);
}

VulkanSubmissionResult PrepareDirect(MilestroUnityRenderSubmission* submission) {
    return PrepareWithBackend(submission, VulkanBackendKind::Direct);
}

void SubmitDirectPrepared(VulkanSubmissionCompletion complete) {
    for (PreparedVulkanSubmission& prepared: gPreparedDirect) {
        MilestroUnityRenderSubmissionStatus status = MilestroUnityRenderSubmissionStatus::Failed;
        if (prepared.target != nullptr && prepared.submission != nullptr &&
            ValidatedTarget(*prepared.submission) == prepared.target) {
            status = DirectBackend().Submit(prepared);
        }
        if (complete != nullptr) {
            complete(prepared.submission, status);
        }
    }
    gPreparedDirect.clear();
}

void FailDirectPrepared(VulkanSubmissionCompletion complete) {
    for (PreparedVulkanSubmission& prepared: gPreparedDirect) {
        if (complete != nullptr) {
            complete(prepared.submission, MilestroUnityRenderSubmissionStatus::Failed);
        }
    }
    gPreparedDirect.clear();
}

bool CollectRetiredTargets() {
    if (gVulkan == nullptr || gVulkan->CommandRecordingState == nullptr) {
        return HasPendingRetirements();
    }
    UnityVulkanRecordingState recording{};
    if (!gVulkan->CommandRecordingState(&recording, kUnityVulkanGraphicsQueueAccess_DontCare)) {
        return HasPendingRetirements();
    }
    DirectBackend().CollectRetired(recording.safeFrameNumber, false);
    StagingCopyBackend().CollectRetired(recording.safeFrameNumber, false);
    return HasPendingRetirements();
}

bool HasPendingRetirements() {
    return DirectBackend().HasPendingRetirements() || StagingCopyBackend().HasPendingRetirements();
}

} // namespace milestro::unity_render::vulkan
