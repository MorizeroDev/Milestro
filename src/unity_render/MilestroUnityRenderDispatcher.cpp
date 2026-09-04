#include "unity_render/MilestroUnityRenderDispatcher.h"

#include "game/milestro_game_retcode.h"
#include "unity_render/MilestroUnityGraphicsBackend.h"
#include "unity_render/MilestroUnityRenderDiagnostics.h"
#include "unity_render/MilestroUnityRenderSubmission.h"
#include "unity_render/MilestroUnityRenderSubmissionDraw.h"
#include "unity_render/MilestroUnityVulkanBackendKind.h"

#include <IUnityGraphics.h>

#include <array>
#include <atomic>
#include <cstring>
#include <limits>
#include <mutex>
#include <new>
#include <vector>

#include "unity_render/MilestroUnityRenderLog.h"

#if defined(__APPLE__)
#include "unity_render/MilestroUnityRenderMetalBackend.h"
#endif

#if defined(_WIN32)
#include "unity_render/MilestroUnityRenderD3D12Backend.h"
#endif

#if defined(MILESTRO_ENABLE_UNITY_GL_RENDER)
#include "unity_render/MilestroUnityRenderGLBackend.h"
#endif

#if defined(MILESTRO_ENABLE_UNITY_VULKAN_RENDER)
#include "unity_render/MilestroUnityRenderVulkanBackend.h"
#endif

namespace milestro::unity_render {

namespace {

constexpr int kMetalDrawEventOffset = 0;
constexpr int kD3D12DrawEventOffset = 1;
constexpr int kGLDrawEventOffset = 2;
constexpr int kVulkanStagingEventOffset = 3;
constexpr int kVulkanDirectPrepareEventOffset = 4;
constexpr int kVulkanDirectSubmitEventOffset = 5;
constexpr int kReservedEventCount = 6;
constexpr int32_t kRenderDrainMagic = 0x4D524451; // MRDQ
constexpr int kSubmissionQueueCount = 5;
constexpr std::size_t kMaximumQueuedSubmissionsPerRoute = 256;

IUnityInterfaces* gUnityInterfaces = nullptr;
IUnityGraphics* gUnityGraphics = nullptr;
UnityGfxRenderer gRenderer = kUnityGfxRendererNull;
int gEventBase = -1;
std::mutex gSubmissionQueueMutex;
std::array<std::vector<MilestroUnityRenderSubmission*>, kSubmissionQueueCount> gSubmissionQueues;
std::mutex gRenderSystemMutex;
std::atomic<uint64_t> gDeviceEpoch{1};
MilestroUnityRenderDiagnostics gDiagnostics;

struct MilestroUnityRenderDrain {
    int32_t magic = 0;
    int32_t graphicsBackend = 0;
    int32_t vulkanBackend = 0;
    int32_t completed = 0;
};

uint64_t CurrentDeviceEpoch() {
    return gDeviceEpoch.load(std::memory_order_acquire);
}

void AdvanceDeviceEpoch() {
    uint64_t current = CurrentDeviceEpoch();
    while (current != std::numeric_limits<uint64_t>::max()) {
        const uint64_t next = MilestroUnityRenderNextDeviceEpoch(current);
        if (gDeviceEpoch.compare_exchange_weak(current, next, std::memory_order_acq_rel, std::memory_order_acquire)) {
            return;
        }
    }
}

bool HasCurrentPayloadAbi(const MilestroUnityRenderSubmission* submission) {
    return MilestroUnityRenderSubmissionHasCurrentAbi(submission, CurrentDeviceEpoch());
}

void MarkSubmissionCompleted(MilestroUnityRenderSubmission* submission,
                             MilestroUnityRenderSubmissionStatus status = MilestroUnityRenderSubmissionStatus::Drawn) {
    if (submission == nullptr) {
        return;
    }

    ReleaseSubmissionOwnedResources(submission);
    std::atomic_ref<int32_t> completed(submission->completed);
    completed.store(static_cast<int32_t>(status), std::memory_order_release);
}

bool IsSameRenderTarget(const MilestroUnityRenderSubmission* lhs, const MilestroUnityRenderSubmission* rhs) {
    if (lhs == nullptr || rhs == nullptr) {
        return false;
    }

    const MilestroUnityRenderTargetPayload& left = lhs->target;
    const MilestroUnityRenderTargetPayload& right = rhs->target;
    if (left.graphicsBackend != right.graphicsBackend || left.handleKind != right.handleKind) {
        return false;
    }

    if (left.graphicsBackend == static_cast<int32_t>(MilestroUnityGraphicsBackend::Vulkan)) {
        return left.vulkanBackend == right.vulkanBackend && left.vulkanTarget != nullptr &&
               left.vulkanTarget == right.vulkanTarget && left.vulkanTargetGeneration == right.vulkanTargetGeneration;
    }

    if (left.colorRenderBufferHandle != nullptr || right.colorRenderBufferHandle != nullptr) {
        return left.colorRenderBufferHandle == right.colorRenderBufferHandle;
    }
    return left.nativeTextureHandle == right.nativeTextureHandle;
}

void DropSupersededQueuedSubmissionsLocked(int queueIndex,
                                           const MilestroUnityRenderSubmission* newer,
                                           std::vector<MilestroUnityRenderSubmission*>& superseded) {
    std::vector<MilestroUnityRenderSubmission*>& queue = gSubmissionQueues[queueIndex];
    auto write = queue.begin();
    for (auto read = queue.begin(); read != queue.end(); ++read) {
        MilestroUnityRenderSubmission* queued = *read;
        if (IsSameRenderTarget(queued, newer)) {
            superseded.push_back(queued);
            continue;
        }

        *write = queued;
        ++write;
    }
    queue.erase(write, queue.end());
}

void MarkDrainCompleted(MilestroUnityRenderDrain* drain, int32_t value = 1) {
    if (drain == nullptr) {
        return;
    }

    std::atomic_ref<int32_t> completed(drain->completed);
    completed.store(value, std::memory_order_release);
}

bool IsRenderDrainPayload(void* data) {
    if (data == nullptr) {
        return false;
    }

    int32_t magic = 0;
    std::memcpy(&magic, data, sizeof(magic));
    return magic == kRenderDrainMagic;
}

int SubmissionQueueIndex(int32_t graphicsBackend, int32_t vulkanBackend = 0) {
    switch (static_cast<MilestroUnityGraphicsBackend>(graphicsBackend)) {
        case MilestroUnityGraphicsBackend::Metal:
            return 0;
        case MilestroUnityGraphicsBackend::Direct3D12:
            return 1;
        case MilestroUnityGraphicsBackend::OpenGL:
        case MilestroUnityGraphicsBackend::OpenGLES:
            return 2;
        case MilestroUnityGraphicsBackend::Vulkan:
            if (vulkanBackend == static_cast<int32_t>(vulkan::VulkanBackendKind::Direct)) {
                return 3;
            }
            if (vulkanBackend == static_cast<int32_t>(vulkan::VulkanBackendKind::StagingCopy)) {
                return 4;
            }
            return -1;
        default:
            return -1;
    }
}

std::vector<MilestroUnityRenderSubmission*> DrainQueuedSubmissions(int32_t graphicsBackend, int32_t vulkanBackend) {
    std::lock_guard lock(gSubmissionQueueMutex);

    const int queueIndex = SubmissionQueueIndex(graphicsBackend, vulkanBackend);
    if (queueIndex < 0) {
        MILESTROLOG_ERROR("Milestro Unity render drain received unknown backend {}.", graphicsBackend);
        return {};
    }

    std::vector<MilestroUnityRenderSubmission*> drained;
    drained.swap(gSubmissionQueues[queueIndex]);
    return drained;
}

void CompleteQueuedSubmissions() {
    std::vector<MilestroUnityRenderSubmission*> submissions;
    for (std::vector<MilestroUnityRenderSubmission*>& queue: gSubmissionQueues) {
        {
            std::lock_guard lock(gSubmissionQueueMutex);
            submissions.swap(queue);
        }
        for (MilestroUnityRenderSubmission* submission: submissions) {
            MarkSubmissionCompleted(submission, MilestroUnityRenderSubmissionStatus::Failed);
        }
        submissions.clear();
    }
#if defined(MILESTRO_ENABLE_UNITY_VULKAN_RENDER)
    vulkan::FailDirectPrepared(MarkSubmissionCompleted);
#endif
}

int64_t EnqueueSubmission(int32_t graphicsBackend, MilestroUnityRenderSubmission* submission) {
    if (submission == nullptr) {
        gDiagnostics.RecordRejectedSubmission();
        MILESTROLOG_ERROR("Milestro Unity render enqueue received null submission.");
        return MILESTRO_API_RET_FAILED;
    }

    std::lock_guard renderLock(gRenderSystemMutex);

    if (!HasCurrentPayloadAbi(submission)) {
        gDiagnostics.RecordRejectedSubmission();
        MILESTROLOG_ERROR("Milestro Unity render enqueue rejected an ABI, scale, or device-epoch mismatch.");
        return MILESTRO_API_RET_FAILED;
    }

    if (submission->target.graphicsBackend != graphicsBackend) {
        gDiagnostics.RecordRejectedSubmission();
        MILESTROLOG_ERROR("Milestro Unity render enqueue backend mismatch: requested={}, submission={}.",
                          graphicsBackend,
                          submission->target.graphicsBackend);
        return MILESTRO_API_RET_FAILED;
    }

#if defined(MILESTRO_ENABLE_UNITY_VULKAN_RENDER)
    if (static_cast<MilestroUnityGraphicsBackend>(graphicsBackend) == MilestroUnityGraphicsBackend::Vulkan &&
        !vulkan::IsSubmissionTargetValid(*submission)) {
        gDiagnostics.RecordRejectedSubmission();
        MILESTROLOG_ERROR("Milestro Unity render enqueue rejected an invalid Vulkan target registration.");
        return MILESTRO_API_RET_FAILED;
    }
#endif

    const int queueIndex = SubmissionQueueIndex(graphicsBackend, submission->target.vulkanBackend);
    if (queueIndex < 0) {
        gDiagnostics.RecordRejectedSubmission();
        MILESTROLOG_ERROR("Milestro Unity render enqueue received unknown backend {}.", graphicsBackend);
        return MILESTRO_API_RET_FAILED;
    }

    std::vector<MilestroUnityRenderSubmission*> supersededSubmissions;
    try {
        supersededSubmissions.reserve(kMaximumQueuedSubmissionsPerRoute);
    } catch (const std::bad_alloc&) {
        gDiagnostics.RecordRejectedSubmission();
        return MILESTRO_API_RET_FAILED;
    }
    bool queueRejected = false;
    try {
        std::lock_guard lock(gSubmissionQueueMutex);
        const bool isMetal =
                static_cast<MilestroUnityGraphicsBackend>(graphicsBackend) == MilestroUnityGraphicsBackend::Metal;
        const bool isVulkanStaging =
                static_cast<MilestroUnityGraphicsBackend>(graphicsBackend) == MilestroUnityGraphicsBackend::Vulkan &&
                submission->target.vulkanBackend == static_cast<int32_t>(vulkan::VulkanBackendKind::StagingCopy);
#if defined(__APPLE__)
        if (isMetal) {
            DropSupersededQueuedSubmissionsLocked(queueIndex, submission, supersededSubmissions);
        }
#else
        (void) isMetal;
#endif
        if (isVulkanStaging && MilestroUnityRenderSubmissionCanReplaceQueuedContent(submission)) {
            DropSupersededQueuedSubmissionsLocked(queueIndex, submission, supersededSubmissions);
        }
        if (gSubmissionQueues[queueIndex].size() >= kMaximumQueuedSubmissionsPerRoute) {
            gDiagnostics.RecordRejectedSubmission();
            MILESTROLOG_ERROR("Milestro Unity render queue {} reached its fixed capacity.", queueIndex);
            queueRejected = true;
        } else {
            gSubmissionQueues[queueIndex].push_back(submission);
        }
    } catch (const std::bad_alloc&) {
        gDiagnostics.RecordRejectedSubmission();
        queueRejected = true;
    }
    for (MilestroUnityRenderSubmission* superseded: supersededSubmissions) {
        MILESTRO_RENDER_LOG_WARN("Dropping superseded Milestro render submission before queue drain.");
        MarkSubmissionCompleted(superseded, MilestroUnityRenderSubmissionStatus::Failed);
    }
    if (queueRejected) {
        return MILESTRO_API_RET_FAILED;
    }
    const MilestroUnityRenderTargetPayload& target = submission->target;
    gDiagnostics.RecordAcceptedSubmission(graphicsBackend,
                                          target.width,
                                          target.height,
                                          target.effectiveScale,
                                          target.deviceEpoch);
    return MILESTRO_API_RET_OK;
}

void RenderQueuedSubmission(int eventOffset, MilestroUnityRenderSubmission* submission) {
    if (submission == nullptr) {
        return;
    }

    if (!HasCurrentPayloadAbi(submission)) {
        MILESTROLOG_ERROR("Milestro Unity render event rejected a stale or incompatible submission payload.");
        MarkSubmissionCompleted(submission, MilestroUnityRenderSubmissionStatus::Failed);
        return;
    }

    const MilestroUnityRenderTargetPayload& target = submission->target;
    if (eventOffset == kMetalDrawEventOffset) {
        if (target.graphicsBackend != static_cast<int32_t>(MilestroUnityGraphicsBackend::Metal)) {
            MILESTROLOG_ERROR("Milestro Metal render event received backend {}.", target.graphicsBackend);
            MarkSubmissionCompleted(submission, MilestroUnityRenderSubmissionStatus::Failed);
            return;
        }

        if (gRenderer != kUnityGfxRendererMetal) {
            MILESTROLOG_ERROR("Milestro Metal render event invoked while Unity renderer is {}.",
                              static_cast<int>(gRenderer));
            MarkSubmissionCompleted(submission, MilestroUnityRenderSubmissionStatus::Failed);
            return;
        }

#if defined(__APPLE__)
        const auto status = metal::Render(*submission);
        if (status == static_cast<int64_t>(MilestroUnityRenderSubmissionStatus::Skipped)) {
            MarkSubmissionCompleted(submission, MilestroUnityRenderSubmissionStatus::Skipped);
            return;
        }

        if (status < 0) {
            MILESTROLOG_ERROR("Milestro Metal render event failed: {}", status);
            MarkSubmissionCompleted(submission, MilestroUnityRenderSubmissionStatus::Failed);
            return;
        }
        MarkSubmissionCompleted(submission);
#else
        MILESTROLOG_ERROR("Milestro Metal render event is only available on Apple platforms.");
        MarkSubmissionCompleted(submission, MilestroUnityRenderSubmissionStatus::Failed);
#endif
        return;
    }

    if (eventOffset == kD3D12DrawEventOffset) {
        if (target.graphicsBackend != static_cast<int32_t>(MilestroUnityGraphicsBackend::Direct3D12)) {
            MILESTROLOG_ERROR("Milestro D3D12 render event received backend {}.", target.graphicsBackend);
            MarkSubmissionCompleted(submission, MilestroUnityRenderSubmissionStatus::Failed);
            return;
        }

        if (gRenderer != kUnityGfxRendererD3D12) {
            MILESTROLOG_ERROR("Milestro D3D12 render event invoked while Unity renderer is {}.",
                              static_cast<int>(gRenderer));
            MarkSubmissionCompleted(submission, MilestroUnityRenderSubmissionStatus::Failed);
            return;
        }

#if defined(_WIN32)
        const auto status = d3d12::Render(*submission);
        if (status < 0) {
            MILESTROLOG_ERROR("Milestro D3D12 render event failed: {}", status);
            MarkSubmissionCompleted(submission, MilestroUnityRenderSubmissionStatus::Failed);
            return;
        }
        MarkSubmissionCompleted(submission);
#else
        MILESTROLOG_ERROR("Milestro D3D12 render event is only available on Windows.");
        MarkSubmissionCompleted(submission, MilestroUnityRenderSubmissionStatus::Failed);
#endif
        return;
    }

    if (eventOffset == kGLDrawEventOffset) {
        if (target.graphicsBackend != static_cast<int32_t>(MilestroUnityGraphicsBackend::OpenGL) &&
            target.graphicsBackend != static_cast<int32_t>(MilestroUnityGraphicsBackend::OpenGLES)) {
            MILESTROLOG_ERROR("Milestro GL render event received backend {}.", target.graphicsBackend);
            MarkSubmissionCompleted(submission, MilestroUnityRenderSubmissionStatus::Failed);
            return;
        }

        if (gRenderer != kUnityGfxRendererOpenGLES30 && gRenderer != kUnityGfxRendererOpenGLCore) {
            MILESTROLOG_ERROR("Milestro GL render event invoked while Unity renderer is {}.",
                              static_cast<int>(gRenderer));
            MarkSubmissionCompleted(submission, MilestroUnityRenderSubmissionStatus::Failed);
            return;
        }

        if ((target.graphicsBackend == static_cast<int32_t>(MilestroUnityGraphicsBackend::OpenGLES) &&
             gRenderer != kUnityGfxRendererOpenGLES30) ||
            (target.graphicsBackend == static_cast<int32_t>(MilestroUnityGraphicsBackend::OpenGL) &&
             gRenderer != kUnityGfxRendererOpenGLCore)) {
            MILESTROLOG_ERROR("Milestro GL render event backend {} does not match Unity renderer {}.",
                              target.graphicsBackend,
                              static_cast<int>(gRenderer));
            MarkSubmissionCompleted(submission, MilestroUnityRenderSubmissionStatus::Failed);
            return;
        }

#if defined(MILESTRO_ENABLE_UNITY_GL_RENDER)
        const auto status = gl::Render(*submission, gRenderer);
        if (status < 0) {
            MILESTROLOG_ERROR("Milestro GL render event failed: {}", status);
            MarkSubmissionCompleted(submission, MilestroUnityRenderSubmissionStatus::Failed);
            return;
        }
        MarkSubmissionCompleted(submission);
#else
        MILESTROLOG_ERROR("Milestro GL render event is not enabled in this Milestro build.");
        MarkSubmissionCompleted(submission, MilestroUnityRenderSubmissionStatus::Failed);
#endif
        return;
    }

    if (eventOffset == kVulkanStagingEventOffset) {
        if (target.graphicsBackend != static_cast<int32_t>(MilestroUnityGraphicsBackend::Vulkan)) {
            MILESTROLOG_ERROR("Milestro Vulkan render event received backend {}.", target.graphicsBackend);
            MarkSubmissionCompleted(submission, MilestroUnityRenderSubmissionStatus::Failed);
            return;
        }

        if (gRenderer != kUnityGfxRendererVulkan) {
            MILESTROLOG_ERROR("Milestro Vulkan render event invoked while Unity renderer is {}.",
                              static_cast<int>(gRenderer));
            MarkSubmissionCompleted(submission, MilestroUnityRenderSubmissionStatus::Failed);
            return;
        }

#if defined(MILESTRO_ENABLE_UNITY_VULKAN_RENDER)
        const vulkan::VulkanSubmissionResult result = vulkan::RenderStaging(submission);
        MarkSubmissionCompleted(result.submission, result.status);
#else
        MILESTROLOG_ERROR("Milestro Vulkan render backend is not enabled in this Milestro build.");
        MarkSubmissionCompleted(submission, MilestroUnityRenderSubmissionStatus::Failed);
#endif
        return;
    }

    if (eventOffset == kVulkanDirectPrepareEventOffset) {
        if (target.graphicsBackend != static_cast<int32_t>(MilestroUnityGraphicsBackend::Vulkan) ||
            target.vulkanBackend != static_cast<int32_t>(vulkan::VulkanBackendKind::Direct) ||
            gRenderer != kUnityGfxRendererVulkan) {
            MILESTROLOG_ERROR("Milestro Vulkan direct prepare event received a mismatched target or renderer.");
            MarkSubmissionCompleted(submission, MilestroUnityRenderSubmissionStatus::Failed);
            return;
        }
#if defined(MILESTRO_ENABLE_UNITY_VULKAN_RENDER)
        const vulkan::VulkanSubmissionResult result = vulkan::PrepareDirect(submission);
        if (result.submission != nullptr) {
            MarkSubmissionCompleted(result.submission, result.status);
        }
#else
        MarkSubmissionCompleted(submission, MilestroUnityRenderSubmissionStatus::Failed);
#endif
        return;
    }

    MILESTRO_RENDER_LOG_WARN("Ignoring unknown Milestro Unity render event offset: {}", eventOffset);
    MarkSubmissionCompleted(submission, MilestroUnityRenderSubmissionStatus::Failed);
}

void DrainRenderQueue(int eventOffset, MilestroUnityRenderDrain* drain) {
    if (drain == nullptr || drain->magic != kRenderDrainMagic) {
        return;
    }

    std::lock_guard renderLock(gRenderSystemMutex);

#if defined(MILESTRO_ENABLE_UNITY_VULKAN_RENDER)
    if (eventOffset == kVulkanDirectSubmitEventOffset) {
        if (drain->graphicsBackend != static_cast<int32_t>(MilestroUnityGraphicsBackend::Vulkan) ||
            drain->vulkanBackend != static_cast<int32_t>(vulkan::VulkanBackendKind::Direct)) {
            MarkDrainCompleted(drain);
            return;
        }
        vulkan::SubmitDirectPrepared(MarkSubmissionCompleted);
        MarkDrainCompleted(drain);
        return;
    }

    if (eventOffset == kVulkanStagingEventOffset &&
        drain->graphicsBackend == static_cast<int32_t>(MilestroUnityGraphicsBackend::Vulkan) &&
        drain->vulkanBackend == 0) {
        const bool pending = vulkan::CollectRetiredTargets();
        MarkDrainCompleted(drain, pending ? 2 : 1);
        return;
    }
#endif

    std::vector<MilestroUnityRenderSubmission*> submissions =
            DrainQueuedSubmissions(drain->graphicsBackend, drain->vulkanBackend);
    for (MilestroUnityRenderSubmission* submission: submissions) {
        RenderQueuedSubmission(eventOffset, submission);
    }
    if (eventOffset != kVulkanDirectPrepareEventOffset) {
        MarkDrainCompleted(drain);
    }
}

void UNITY_INTERFACE_API OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType) {
    std::lock_guard renderLock(gRenderSystemMutex);

    if (eventType == kUnityGfxDeviceEventInitialize || eventType == kUnityGfxDeviceEventBeforeReset ||
        eventType == kUnityGfxDeviceEventShutdown) {
        CompleteQueuedSubmissions();
    }

    if (eventType == kUnityGfxDeviceEventInitialize && gUnityGraphics != nullptr) {
        gRenderer = gUnityGraphics->GetRenderer();
    } else if (eventType == kUnityGfxDeviceEventShutdown) {
        gRenderer = kUnityGfxRendererNull;
    }

#if defined(__APPLE__)
    metal::OnGraphicsDeviceEvent(eventType, gUnityInterfaces, gRenderer);
#else
    (void) eventType;
#endif

#if defined(_WIN32)
    d3d12::OnGraphicsDeviceEvent(eventType,
                                 gUnityInterfaces,
                                 gRenderer,
                                 gEventBase >= 0 ? gEventBase + kD3D12DrawEventOffset : -1);
#endif

#if defined(MILESTRO_ENABLE_UNITY_GL_RENDER)
    gl::OnGraphicsDeviceEvent(eventType, gUnityInterfaces, gRenderer);
#endif

#if defined(MILESTRO_ENABLE_UNITY_VULKAN_RENDER)
    vulkan::OnGraphicsDeviceEvent(eventType,
                                  gUnityInterfaces,
                                  gRenderer,
                                  gEventBase >= 0 ? gEventBase + kVulkanStagingEventOffset : -1,
                                  gEventBase >= 0 ? gEventBase + kVulkanDirectPrepareEventOffset : -1,
                                  gEventBase >= 0 ? gEventBase + kVulkanDirectSubmitEventOffset : -1);
#endif

    if (eventType == kUnityGfxDeviceEventInitialize || eventType == kUnityGfxDeviceEventBeforeReset ||
        eventType == kUnityGfxDeviceEventShutdown) {
        AdvanceDeviceEpoch();
    }
}

void UNITY_INTERFACE_API OnRenderEvent(int eventId, void* data) {
    if (gEventBase < 0) {
        MILESTRO_RENDER_LOG_WARN("Ignoring unknown Milestro Unity render event: {}", eventId);
        MarkDrainCompleted(static_cast<MilestroUnityRenderDrain*>(data));
        return;
    }

    if (data == nullptr) {
        MILESTROLOG_ERROR("Milestro Unity render event received null payload.");
        return;
    }

    const int eventOffset = eventId - gEventBase;
    if (!IsRenderDrainPayload(data)) {
        MILESTROLOG_ERROR("Milestro Unity render event received non-drain payload.");
        return;
    }

    DrainRenderQueue(eventOffset, static_cast<MilestroUnityRenderDrain*>(data));
}

void* RenderEventFunc() {
    return reinterpret_cast<void*>(&OnRenderEvent);
}

int64_t MetalRenderEventId(int32_t& eventId) {
    if (gEventBase < 0) {
        eventId = -1;
        return MILESTRO_API_RET_FAILED;
    }

    eventId = gEventBase + kMetalDrawEventOffset;
    return MILESTRO_API_RET_OK;
}

int64_t RenderTextureEventId(int32_t graphicsBackend, int32_t& eventId) {
    if (gEventBase < 0) {
        eventId = -1;
        return MILESTRO_API_RET_FAILED;
    }

    switch (static_cast<MilestroUnityGraphicsBackend>(graphicsBackend)) {
        case MilestroUnityGraphicsBackend::Metal:
            eventId = gEventBase + kMetalDrawEventOffset;
            return MILESTRO_API_RET_OK;
        case MilestroUnityGraphicsBackend::Direct3D12:
            eventId = gEventBase + kD3D12DrawEventOffset;
            return MILESTRO_API_RET_OK;
        case MilestroUnityGraphicsBackend::OpenGL:
        case MilestroUnityGraphicsBackend::OpenGLES:
#if defined(MILESTRO_ENABLE_UNITY_GL_RENDER)
            eventId = gEventBase + kGLDrawEventOffset;
            return MILESTRO_API_RET_OK;
#else
            eventId = -1;
            MILESTROLOG_ERROR("Milestro Unity GL render backend {} is not enabled in this build.", graphicsBackend);
            return MILESTRO_API_RET_FAILED;
#endif
        case MilestroUnityGraphicsBackend::Vulkan:
#if defined(MILESTRO_ENABLE_UNITY_VULKAN_RENDER)
            eventId = gEventBase + kVulkanDirectPrepareEventOffset;
            return MILESTRO_API_RET_OK;
#else
            eventId = -1;
            MILESTROLOG_ERROR("Milestro Unity Vulkan render backend is not enabled in this build.");
            return MILESTRO_API_RET_FAILED;
#endif
        default:
            eventId = -1;
            MILESTROLOG_ERROR("Milestro Unity render backend {} is unknown.", graphicsBackend);
            return MILESTRO_API_RET_FAILED;
    }
}

int64_t VulkanRenderEventIds(int32_t vulkanBackend, int32_t& firstEventId, int32_t& secondEventId) {
#if defined(MILESTRO_ENABLE_UNITY_VULKAN_RENDER)
    if (gEventBase < 0) {
        firstEventId = -1;
        secondEventId = -1;
        return MILESTRO_API_RET_FAILED;
    }
    switch (static_cast<vulkan::VulkanBackendKind>(vulkanBackend)) {
        case vulkan::VulkanBackendKind::Direct:
            firstEventId = gEventBase + kVulkanDirectPrepareEventOffset;
            secondEventId = gEventBase + kVulkanDirectSubmitEventOffset;
            return MILESTRO_API_RET_OK;
        case vulkan::VulkanBackendKind::StagingCopy:
            firstEventId = gEventBase + kVulkanStagingEventOffset;
            secondEventId = -1;
            return MILESTRO_API_RET_OK;
        default:
            firstEventId = -1;
            secondEventId = -1;
            return MILESTRO_API_RET_FAILED;
    }
#else
    (void) vulkanBackend;
    firstEventId = -1;
    secondEventId = -1;
    return MILESTRO_API_RET_FAILED;
#endif
}

} // namespace

void* GetRenderEventFuncForExport() {
    return RenderEventFunc();
}

int64_t GetMetalRenderEventIdForExport(int32_t& eventId) {
    return MetalRenderEventId(eventId);
}

int64_t GetRenderTextureEventIdForExport(int32_t graphicsBackend, int32_t& eventId) {
    return RenderTextureEventId(graphicsBackend, eventId);
}

int64_t GetVulkanRenderEventIdsForExport(int32_t vulkanBackend, int32_t& firstEventId, int32_t& secondEventId) {
    return VulkanRenderEventIds(vulkanBackend, firstEventId, secondEventId);
}

int64_t EnqueueSubmissionForExport(int32_t graphicsBackend, void* submission) {
    return EnqueueSubmission(graphicsBackend, static_cast<MilestroUnityRenderSubmission*>(submission));
}

int64_t CreateVulkanTargetForExport(void* nativeTexture,
                                    int32_t width,
                                    int32_t height,
                                    int32_t vulkanBackend,
                                    uint64_t deviceEpoch,
                                    void*& target,
                                    uint64_t& generation) {
#if defined(MILESTRO_ENABLE_UNITY_VULKAN_RENDER)
    std::lock_guard renderLock(gRenderSystemMutex);
    if (gRenderer != kUnityGfxRendererVulkan || deviceEpoch != CurrentDeviceEpoch()) {
        target = nullptr;
        generation = 0;
        return MILESTRO_API_RET_FAILED;
    }
    return vulkan::CreateTarget(nativeTexture, width, height, vulkanBackend, deviceEpoch, target, generation);
#else
    (void) nativeTexture;
    (void) width;
    (void) height;
    (void) vulkanBackend;
    (void) deviceEpoch;
    target = nullptr;
    generation = 0;
    return MILESTRO_API_RET_FAILED;
#endif
}

int64_t DestroyVulkanTargetForExport(void*& target, int32_t& retirementPending) {
#if defined(MILESTRO_ENABLE_UNITY_VULKAN_RENDER)
    std::lock_guard renderLock(gRenderSystemMutex);
    return vulkan::DestroyTarget(target, retirementPending);
#else
    target = nullptr;
    retirementPending = 0;
    return MILESTRO_API_RET_OK;
#endif
}

int64_t GetPayloadAbiInfoForExport(uint32_t& abiVersion,
                                   uint64_t& layoutFingerprint,
                                   uint32_t& targetSize,
                                   uint32_t& submissionSize,
                                   uint32_t& targetEffectiveScaleOffset,
                                   uint32_t& targetDeviceEpochOffset,
                                   uint32_t& submissionTargetOffset,
                                   uint32_t& submissionCompletedOffset) {
    abiVersion = kMilestroUnityRenderPayloadAbiVersion;
    layoutFingerprint = kMilestroUnityRenderPayloadLayoutFingerprint;
    targetSize = kMilestroUnityRenderTargetPayloadSize;
    submissionSize = kMilestroUnityRenderSubmissionSize;
    targetEffectiveScaleOffset = kMilestroUnityRenderTargetEffectiveScaleOffset;
    targetDeviceEpochOffset = kMilestroUnityRenderTargetDeviceEpochOffset;
    submissionTargetOffset = kMilestroUnityRenderSubmissionTargetOffset;
    submissionCompletedOffset = kMilestroUnityRenderSubmissionCompletedOffset;
    return MILESTRO_API_RET_OK;
}

int64_t GetDeviceEpochForExport(uint64_t& deviceEpoch) {
    deviceEpoch = CurrentDeviceEpoch();
    return deviceEpoch == 0 ? MILESTRO_API_RET_FAILED : MILESTRO_API_RET_OK;
}

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
                                        uint64_t& currentDeviceEpoch) {
    std::lock_guard renderLock(gRenderSystemMutex);
    const MilestroUnityRenderDiagnosticsSnapshot snapshot = gDiagnostics.Snapshot(CurrentDeviceEpoch());
    abiVersion = snapshot.abiVersion;
    structSize = snapshot.structSize;
    acceptedSubmissionCount = snapshot.acceptedSubmissionCount;
    rejectedSubmissionCount = snapshot.rejectedSubmissionCount;
    hasLastAcceptedSubmission = snapshot.hasLastAcceptedSubmission;
    lastAcceptedGraphicsBackend = snapshot.lastAcceptedGraphicsBackend;
    lastAcceptedRasterWidth = snapshot.lastAcceptedRasterWidth;
    lastAcceptedRasterHeight = snapshot.lastAcceptedRasterHeight;
    lastAcceptedEffectiveScale = snapshot.lastAcceptedEffectiveScale;
    lastAcceptedDeviceEpoch = snapshot.lastAcceptedDeviceEpoch;
    currentDeviceEpoch = snapshot.currentDeviceEpoch;
    return currentDeviceEpoch == 0 ? MILESTRO_API_RET_FAILED : MILESTRO_API_RET_OK;
}

int64_t CreateD3D12ExternalTextureForExport(int32_t width,
                                            int32_t height,
                                            int32_t storageSrgb,
                                            int32_t preferredFormat,
                                            void*& texture) {
#if defined(_WIN32)
    std::lock_guard renderLock(gRenderSystemMutex);

    if (gRenderer != kUnityGfxRendererD3D12) {
        texture = nullptr;
        MILESTROLOG_ERROR("Milestro D3D12 external texture requested while Unity renderer is {}.",
                          static_cast<int>(gRenderer));
        return MILESTRO_API_RET_FAILED;
    }

    return d3d12::CreateExternalTexture(width, height, storageSrgb, preferredFormat, texture);
#else
    (void) width;
    (void) height;
    (void) storageSrgb;
    (void) preferredFormat;
    texture = nullptr;
    MILESTROLOG_ERROR("Milestro D3D12 external texture is only available on Windows.");
    return MILESTRO_API_RET_FAILED;
#endif
}

int64_t DestroyD3D12ExternalTextureForExport(void*& texture) {
#if defined(_WIN32)
    std::lock_guard renderLock(gRenderSystemMutex);
    return d3d12::DestroyExternalTexture(texture);
#else
    texture = nullptr;
    MILESTROLOG_ERROR("Milestro D3D12 external texture is only available on Windows.");
    return MILESTRO_API_RET_FAILED;
#endif
}

void Load(IUnityInterfaces* unityInterfaces) {
    gUnityInterfaces = unityInterfaces;
    gUnityGraphics = unityInterfaces != nullptr ? unityInterfaces->Get<IUnityGraphics>() : nullptr;
    if (gUnityGraphics == nullptr) {
        MILESTRO_RENDER_LOG_WARN("IUnityGraphics is unavailable; Unity render PoC is disabled.");
        return;
    }

    gEventBase = gUnityGraphics->ReserveEventIDRange(kReservedEventCount);
    gUnityGraphics->RegisterDeviceEventCallback(OnGraphicsDeviceEvent);
    OnGraphicsDeviceEvent(kUnityGfxDeviceEventInitialize);
}

void Unload() {
    if (gUnityGraphics != nullptr) {
        gUnityGraphics->UnregisterDeviceEventCallback(OnGraphicsDeviceEvent);
    }
    OnGraphicsDeviceEvent(kUnityGfxDeviceEventShutdown);
    gEventBase = -1;
    gUnityGraphics = nullptr;
    gUnityInterfaces = nullptr;
}

} // namespace milestro::unity_render
