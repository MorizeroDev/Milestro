#include "unity_render/MilestroUnityRenderDispatcher.h"

#include "game/milestro_game_retcode.h"
#include "unity_render/MilestroUnityGraphicsBackend.h"
#include "unity_render/MilestroUnityRenderSubmission.h"
#include "unity_render/MilestroUnityRenderSubmissionDraw.h"

#include <IUnityGraphics.h>

#include <array>
#include <atomic>
#include <cstring>
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
constexpr int kVulkanDrawEventOffset = 3;
constexpr int kReservedEventCount = 4;
constexpr int32_t kRenderDrainMagic = 0x4D524451; // MRDQ
constexpr int kSubmissionQueueCount = 6;

IUnityInterfaces* gUnityInterfaces = nullptr;
IUnityGraphics* gUnityGraphics = nullptr;
UnityGfxRenderer gRenderer = kUnityGfxRendererNull;
int gEventBase = -1;
std::mutex gSubmissionQueueMutex;
std::array<std::vector<MilestroUnityRenderSubmission*>, kSubmissionQueueCount> gSubmissionQueues;
std::mutex gRenderSystemMutex;

struct MilestroUnityRenderDrain {
    int32_t magic = 0;
    int32_t graphicsBackend = 0;
    int32_t completed = 0;
};

void MarkSubmissionCompleted(MilestroUnityRenderSubmission* submission,
                             MilestroUnityRenderSubmissionStatus status = MilestroUnityRenderSubmissionStatus::Drawn) {
    if (submission == nullptr) {
        return;
    }

    ReleaseSubmissionOwnedResources(submission);
#if defined(__cpp_lib_atomic_ref) && __cpp_lib_atomic_ref >= 201806L
    std::atomic_ref<int32_t> completed(submission->completed);
    completed.store(static_cast<int32_t>(status), std::memory_order_release);
#else
    reinterpret_cast<std::atomic<int32_t>*>(&submission->completed)->store(static_cast<int32_t>(status), std::memory_order_release);
#endif
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

void MarkDrainCompleted(MilestroUnityRenderDrain* drain) {
    if (drain == nullptr) {
        return;
    }

#if defined(__cpp_lib_atomic_ref) && __cpp_lib_atomic_ref >= 201806L
    std::atomic_ref<int32_t> completed(drain->completed);
    completed.store(1, std::memory_order_release);
#else
    reinterpret_cast<std::atomic<int32_t>*>(&drain->completed)->store(1, std::memory_order_release);
#endif
}

// A Vulkan AccessQueue callback may run after the render-event callback has
// returned. Keep the drain payload alive until every deferred submission has
// published its completion status.
class DrainCompletionState {
public:
    explicit DrainCompletionState(MilestroUnityRenderDrain* drain)
        : drain_(drain) {
    }

    void AddPending() {
        refs_.fetch_add(1, std::memory_order_relaxed);
        pending_.fetch_add(1, std::memory_order_release);
    }

    void CompletePending() {
        const int32_t previous = pending_.fetch_sub(1, std::memory_order_acq_rel);
        if (previous == 1) {
            TryComplete();
        }
        Release();
    }

    void FinishDispatch() {
        dispatching_.store(false, std::memory_order_release);
        TryComplete();
        Release();
    }

private:
    void TryComplete() {
        if (dispatching_.load(std::memory_order_acquire) ||
            pending_.load(std::memory_order_acquire) != 0) {
            return;
        }

        if (!drainCompleted_.exchange(true, std::memory_order_acq_rel)) {
            MarkDrainCompleted(drain_);
        }
    }

    void Release() {
        if (refs_.fetch_sub(1, std::memory_order_acq_rel) == 1) {
            delete this;
        }
    }

    MilestroUnityRenderDrain* drain_ = nullptr;
    std::atomic<int32_t> pending_{0};
    std::atomic<int32_t> refs_{1};
    std::atomic<bool> dispatching_{true};
    std::atomic<bool> drainCompleted_{false};
};

bool IsRenderDrainPayload(void* data) {
    if (data == nullptr) {
        return false;
    }

    int32_t magic = 0;
    std::memcpy(&magic, data, sizeof(magic));
    return magic == kRenderDrainMagic;
}

int SubmissionQueueIndex(int32_t graphicsBackend) {
    switch (static_cast<MilestroUnityGraphicsBackend>(graphicsBackend)) {
        case MilestroUnityGraphicsBackend::Metal:
        case MilestroUnityGraphicsBackend::Direct3D12:
        case MilestroUnityGraphicsBackend::Vulkan:
        case MilestroUnityGraphicsBackend::OpenGL:
        case MilestroUnityGraphicsBackend::OpenGLES:
            return graphicsBackend;
        default:
            return -1;
    }
}

std::vector<MilestroUnityRenderSubmission*> DrainQueuedSubmissions(int32_t graphicsBackend) {
    std::lock_guard lock(gSubmissionQueueMutex);

    const int queueIndex = SubmissionQueueIndex(graphicsBackend);
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
    {
        std::lock_guard lock(gSubmissionQueueMutex);
        for (std::vector<MilestroUnityRenderSubmission*>& queue: gSubmissionQueues) {
            submissions.insert(submissions.end(), queue.begin(), queue.end());
            queue.clear();
        }
    }

    for (MilestroUnityRenderSubmission* submission: submissions) {
        MarkSubmissionCompleted(submission, MilestroUnityRenderSubmissionStatus::Failed);
    }
}

int64_t EnqueueSubmission(int32_t graphicsBackend, MilestroUnityRenderSubmission* submission) {
    if (submission == nullptr) {
        MILESTROLOG_ERROR("Milestro Unity render enqueue received null submission.");
        return MILESTRO_API_RET_FAILED;
    }

    if (submission->target.graphicsBackend != graphicsBackend) {
        MILESTROLOG_ERROR("Milestro Unity render enqueue backend mismatch: requested={}, submission={}.",
                          graphicsBackend,
                          submission->target.graphicsBackend);
        return MILESTRO_API_RET_FAILED;
    }

    const int queueIndex = SubmissionQueueIndex(graphicsBackend);
    if (queueIndex < 0) {
        MILESTROLOG_ERROR("Milestro Unity render enqueue received unknown backend {}.", graphicsBackend);
        return MILESTRO_API_RET_FAILED;
    }

    std::vector<MilestroUnityRenderSubmission*> supersededSubmissions;
    {
        std::lock_guard lock(gSubmissionQueueMutex);
#if defined(__APPLE__)
        if (static_cast<MilestroUnityGraphicsBackend>(graphicsBackend) == MilestroUnityGraphicsBackend::Metal) {
            DropSupersededQueuedSubmissionsLocked(queueIndex, submission, supersededSubmissions);
        }
#endif
        gSubmissionQueues[queueIndex].push_back(submission);
    }
    for (MilestroUnityRenderSubmission* superseded: supersededSubmissions) {
        MILESTRO_RENDER_LOG_WARN("Dropping superseded Milestro Metal render submission before queue drain.");
        MarkSubmissionCompleted(superseded, MilestroUnityRenderSubmissionStatus::Failed);
    }
    return MILESTRO_API_RET_OK;
}

void CompleteVulkanSubmission(MilestroUnityRenderSubmission* submission,
                              MilestroUnityRenderSubmissionStatus status,
                              void* userData) {
    auto* drainCompletion = static_cast<DrainCompletionState*>(userData);
    MarkSubmissionCompleted(submission, status);
    if (drainCompletion != nullptr) {
        drainCompletion->CompletePending();
    }
}

void RenderQueuedSubmission(int eventOffset,
                            MilestroUnityRenderSubmission* submission,
                            DrainCompletionState* drainCompletion) {
    if (submission == nullptr) {
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

    if (eventOffset == kVulkanDrawEventOffset) {
        if (target.graphicsBackend != static_cast<int32_t>(MilestroUnityGraphicsBackend::Vulkan)) {
            MILESTROLOG_ERROR("Milestro Vulkan render event received backend {}.", target.graphicsBackend);
            MarkSubmissionCompleted(submission, MilestroUnityRenderSubmissionStatus::Failed);
            return;
        }

        if (gUnityGraphics != nullptr) {
            gRenderer = gUnityGraphics->GetRenderer();
        }
        if (gRenderer != kUnityGfxRendererVulkan) {
            MILESTROLOG_ERROR("Milestro Vulkan render event invoked while Unity renderer is {}.",
                              static_cast<int>(gRenderer));
            MarkSubmissionCompleted(submission, MilestroUnityRenderSubmissionStatus::Failed);
            return;
        }

#if defined(MILESTRO_ENABLE_UNITY_VULKAN_RENDER)
        if (drainCompletion == nullptr) {
            MILESTROLOG_ERROR("Milestro Vulkan render event has no drain completion state.");
            MarkSubmissionCompleted(submission, MilestroUnityRenderSubmissionStatus::Failed);
            return;
        }

        drainCompletion->AddPending();
        const auto status = vulkan::Render(*submission, &CompleteVulkanSubmission, drainCompletion);
        if (status == vulkan::kRenderDeferred) {
            return;
        }

        drainCompletion->CompletePending();
        if (status < 0) {
            MILESTROLOG_ERROR("Milestro Vulkan render event failed: {}", status);
            MarkSubmissionCompleted(submission, MilestroUnityRenderSubmissionStatus::Failed);
            return;
        }
        MarkSubmissionCompleted(submission);
#else
        MILESTROLOG_ERROR("Milestro Vulkan render backend is not enabled in this Milestro build.");
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
    std::vector<MilestroUnityRenderSubmission*> submissions = DrainQueuedSubmissions(drain->graphicsBackend);
    auto* drainCompletion = new (std::nothrow) DrainCompletionState(drain);
    if (drainCompletion == nullptr) {
        MILESTROLOG_ERROR("Failed to allocate Milestro render-drain completion state.");
        for (MilestroUnityRenderSubmission* submission: submissions) {
            MarkSubmissionCompleted(submission, MilestroUnityRenderSubmissionStatus::Failed);
        }
        MarkDrainCompleted(drain);
        return;
    }

    for (MilestroUnityRenderSubmission* submission: submissions) {
        RenderQueuedSubmission(eventOffset, submission, drainCompletion);
    }
    drainCompletion->FinishDispatch();
}

void UNITY_INTERFACE_API OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType) {
    std::lock_guard renderLock(gRenderSystemMutex);

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
                                  gEventBase >= 0 ? gEventBase + kVulkanDrawEventOffset : -1);
#endif
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
            eventId = gEventBase + kVulkanDrawEventOffset;
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

int64_t EnqueueSubmissionForExport(int32_t graphicsBackend, void* submission) {
    return EnqueueSubmission(graphicsBackend, static_cast<MilestroUnityRenderSubmission*>(submission));
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
    CompleteQueuedSubmissions();
    OnGraphicsDeviceEvent(kUnityGfxDeviceEventShutdown);
    gEventBase = -1;
    gUnityGraphics = nullptr;
    gUnityInterfaces = nullptr;
}

} // namespace milestro::unity_render
