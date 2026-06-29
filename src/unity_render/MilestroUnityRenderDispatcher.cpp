#include "unity_render/MilestroUnityRenderDispatcher.h"

#include "game/milestro_game_retcode.h"
#include "unity_render/MilestroUnityGraphicsBackend.h"
#include "unity_render/MilestroUnityRenderSubmission.h"

#include <IUnityGraphics.h>
#include <Milestro/log/log.h>

#include <atomic>

#if defined(__APPLE__)
#include "unity_render/MilestroUnityRenderMetalBackend.h"
#endif

#if defined(_WIN32)
#include "unity_render/MilestroUnityRenderD3D12Backend.h"
#endif

#if defined(MILESTRO_ENABLE_UNITY_GL_RENDER)
#include "unity_render/MilestroUnityRenderGLBackend.h"
#endif

namespace milestro::unity_render {

namespace {

constexpr int kMetalDrawEventOffset = 0;
constexpr int kD3D12DrawEventOffset = 1;
constexpr int kGLDrawEventOffset = 2;
constexpr int kReservedEventCount = 3;

IUnityInterfaces* gUnityInterfaces = nullptr;
IUnityGraphics* gUnityGraphics = nullptr;
UnityGfxRenderer gRenderer = kUnityGfxRendererNull;
int gEventBase = -1;

void MarkSubmissionCompleted(MilestroUnityRenderSubmission* submission) {
    if (submission == nullptr) {
        return;
    }

    std::atomic_ref<int32_t> completed(submission->completed);
    completed.store(1, std::memory_order_release);
}

void UNITY_INTERFACE_API OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType) {
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
}

void UNITY_INTERFACE_API OnRenderEvent(int eventId, void* data) {
    if (gEventBase < 0) {
        MILESTROLOG_WARN("Ignoring unknown Milestro Unity render event: {}", eventId);
        MarkSubmissionCompleted(static_cast<MilestroUnityRenderSubmission*>(data));
        return;
    }

    if (data == nullptr) {
        MILESTROLOG_ERROR("Milestro Unity render event received null payload.");
        return;
    }

    auto* submission = static_cast<MilestroUnityRenderSubmission*>(data);
    const MilestroUnityRenderTargetPayload& target = submission->target;
    const int eventOffset = eventId - gEventBase;
    if (eventOffset == kMetalDrawEventOffset) {
        if (target.graphicsBackend != static_cast<int32_t>(MilestroUnityGraphicsBackend::Metal)) {
            MILESTROLOG_ERROR("Milestro Metal render event received backend {}.", target.graphicsBackend);
            MarkSubmissionCompleted(submission);
            return;
        }

        if (gRenderer != kUnityGfxRendererMetal) {
            MILESTROLOG_ERROR("Milestro Metal render event invoked while Unity renderer is {}.",
                              static_cast<int>(gRenderer));
            MarkSubmissionCompleted(submission);
            return;
        }

#if defined(__APPLE__)
        const auto status = metal::Render(*submission);
        if (status < 0) {
            MILESTROLOG_ERROR("Milestro Metal render event failed: {}", status);
        }
        MarkSubmissionCompleted(submission);
#else
        MILESTROLOG_ERROR("Milestro Metal render event is only available on Apple platforms.");
        MarkSubmissionCompleted(submission);
#endif
        return;
    }

    if (eventOffset == kD3D12DrawEventOffset) {
        if (target.graphicsBackend != static_cast<int32_t>(MilestroUnityGraphicsBackend::Direct3D12)) {
            MILESTROLOG_ERROR("Milestro D3D12 render event received backend {}.", target.graphicsBackend);
            MarkSubmissionCompleted(submission);
            return;
        }

        if (gRenderer != kUnityGfxRendererD3D12) {
            MILESTROLOG_ERROR("Milestro D3D12 render event invoked while Unity renderer is {}.",
                              static_cast<int>(gRenderer));
            MarkSubmissionCompleted(submission);
            return;
        }

#if defined(_WIN32)
        const auto status = d3d12::Render(*submission);
        if (status < 0) {
            MILESTROLOG_ERROR("Milestro D3D12 render event failed: {}", status);
        }
        MarkSubmissionCompleted(submission);
#else
        MILESTROLOG_ERROR("Milestro D3D12 render event is only available on Windows.");
        MarkSubmissionCompleted(submission);
#endif
        return;
    }

    if (eventOffset == kGLDrawEventOffset) {
        if (target.graphicsBackend != static_cast<int32_t>(MilestroUnityGraphicsBackend::OpenGL) &&
            target.graphicsBackend != static_cast<int32_t>(MilestroUnityGraphicsBackend::OpenGLES)) {
            MILESTROLOG_ERROR("Milestro GL render event received backend {}.", target.graphicsBackend);
            MarkSubmissionCompleted(submission);
            return;
        }

        if (gRenderer != kUnityGfxRendererOpenGLES30 && gRenderer != kUnityGfxRendererOpenGLCore) {
            MILESTROLOG_ERROR("Milestro GL render event invoked while Unity renderer is {}.",
                              static_cast<int>(gRenderer));
            MarkSubmissionCompleted(submission);
            return;
        }

        if ((target.graphicsBackend == static_cast<int32_t>(MilestroUnityGraphicsBackend::OpenGLES) &&
             gRenderer != kUnityGfxRendererOpenGLES30) ||
            (target.graphicsBackend == static_cast<int32_t>(MilestroUnityGraphicsBackend::OpenGL) &&
             gRenderer != kUnityGfxRendererOpenGLCore)) {
            MILESTROLOG_ERROR("Milestro GL render event backend {} does not match Unity renderer {}.",
                              target.graphicsBackend,
                              static_cast<int>(gRenderer));
            MarkSubmissionCompleted(submission);
            return;
        }

#if defined(MILESTRO_ENABLE_UNITY_GL_RENDER)
        const auto status = gl::Render(*submission, gRenderer);
        if (status < 0) {
            MILESTROLOG_ERROR("Milestro GL render event failed: {}", status);
        }
        MarkSubmissionCompleted(submission);
#else
        MILESTROLOG_ERROR("Milestro GL render event is not enabled in this Milestro build.");
        MarkSubmissionCompleted(submission);
#endif
        return;
    }

    MILESTROLOG_WARN("Ignoring unknown Milestro Unity render event: {}", eventId);
    MarkSubmissionCompleted(submission);
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
            eventId = -1;
            MILESTROLOG_ERROR("Milestro Unity render backend {} is reserved but not implemented.", graphicsBackend);
            return MILESTRO_API_RET_FAILED;
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

int64_t CreateD3D12ExternalTextureForExport(int32_t width,
                                            int32_t height,
                                            int32_t srgb,
                                            int32_t preferredFormat,
                                            void*& texture) {
#if defined(_WIN32)
    if (gRenderer != kUnityGfxRendererD3D12) {
        texture = nullptr;
        MILESTROLOG_ERROR("Milestro D3D12 external texture requested while Unity renderer is {}.",
                          static_cast<int>(gRenderer));
        return MILESTRO_API_RET_FAILED;
    }

    return d3d12::CreateExternalTexture(width, height, srgb, preferredFormat, texture);
#else
    (void) width;
    (void) height;
    (void) srgb;
    (void) preferredFormat;
    texture = nullptr;
    MILESTROLOG_ERROR("Milestro D3D12 external texture is only available on Windows.");
    return MILESTRO_API_RET_FAILED;
#endif
}

int64_t DestroyD3D12ExternalTextureForExport(void*& texture) {
#if defined(_WIN32)
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
        MILESTROLOG_WARN("IUnityGraphics is unavailable; Unity render PoC is disabled.");
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
