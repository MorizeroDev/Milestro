#include "unity_render/MilestroUnityRenderDispatcher.h"

#include "game/milestro_game_retcode.h"
#include "unity_render/MilestroUnityGraphicsBackend.h"
#include "unity_render/MilestroUnityRenderTargetPayload.h"

#include <IUnityGraphics.h>
#include <Milestro/log/log.h>

#include <atomic>

#if defined(__APPLE__)
#include "unity_render/MilestroUnityRenderMetalBackend.h"
#endif

#if defined(_WIN32)
#include "unity_render/MilestroUnityRenderD3D12Backend.h"
#endif

namespace milestro::unity_render {

namespace {

constexpr int kMetalDrawEventOffset = 0;
constexpr int kD3D12DrawEventOffset = 1;
constexpr int kReservedEventCount = 2;

IUnityInterfaces *gUnityInterfaces = nullptr;
IUnityGraphics *gUnityGraphics = nullptr;
UnityGfxRenderer gRenderer = kUnityGfxRendererNull;
int gEventBase = -1;

void MarkPayloadCompleted(MilestroUnityRenderTargetPayload *payload) {
    if (payload == nullptr) {
        return;
    }

    std::atomic_ref<int32_t> completed(payload->completed);
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
    (void)eventType;
#endif

#if defined(_WIN32)
    d3d12::OnGraphicsDeviceEvent(eventType,
                                 gUnityInterfaces,
                                 gRenderer,
                                 gEventBase >= 0 ? gEventBase + kD3D12DrawEventOffset : -1);
#endif
}

void UNITY_INTERFACE_API OnRenderEvent(int eventId, void *data) {
    if (gEventBase < 0) {
        MILESTROLOG_WARN("Ignoring unknown Milestro Unity render event: {}", eventId);
        MarkPayloadCompleted(static_cast<MilestroUnityRenderTargetPayload *>(data));
        return;
    }

    if (data == nullptr) {
        MILESTROLOG_ERROR("Milestro Unity render event received null payload.");
        return;
    }

    auto *payload = static_cast<MilestroUnityRenderTargetPayload *>(data);
    const int eventOffset = eventId - gEventBase;
    if (eventOffset == kMetalDrawEventOffset) {
        if (payload->graphicsBackend != static_cast<int32_t>(MilestroUnityGraphicsBackend::Metal)) {
            MILESTROLOG_ERROR("Milestro Metal render event received backend {}.", payload->graphicsBackend);
            MarkPayloadCompleted(payload);
            return;
        }

        if (gRenderer != kUnityGfxRendererMetal) {
            MILESTROLOG_ERROR("Milestro Metal render event invoked while Unity renderer is {}.",
                              static_cast<int>(gRenderer));
            MarkPayloadCompleted(payload);
            return;
        }

#if defined(__APPLE__)
        const auto status = metal::Render(*payload);
        if (status < 0) {
            MILESTROLOG_ERROR("Milestro Metal render event failed: {}", status);
        }
        MarkPayloadCompleted(payload);
#else
        MILESTROLOG_ERROR("Milestro Metal render event is only available on Apple platforms.");
        MarkPayloadCompleted(payload);
#endif
        return;
    }

    if (eventOffset == kD3D12DrawEventOffset) {
        if (payload->graphicsBackend != static_cast<int32_t>(MilestroUnityGraphicsBackend::Direct3D12)) {
            MILESTROLOG_ERROR("Milestro D3D12 render event received backend {}.", payload->graphicsBackend);
            MarkPayloadCompleted(payload);
            return;
        }

        if (gRenderer != kUnityGfxRendererD3D12) {
            MILESTROLOG_ERROR("Milestro D3D12 render event invoked while Unity renderer is {}.",
                              static_cast<int>(gRenderer));
            MarkPayloadCompleted(payload);
            return;
        }

#if defined(_WIN32)
        const auto status = d3d12::Render(*payload);
        if (status < 0) {
            MILESTROLOG_ERROR("Milestro D3D12 render event failed: {}", status);
        }
        MarkPayloadCompleted(payload);
#else
        MILESTROLOG_ERROR("Milestro D3D12 render event is only available on Windows.");
        MarkPayloadCompleted(payload);
#endif
        return;
    }

    MILESTROLOG_WARN("Ignoring unknown Milestro Unity render event: {}", eventId);
    MarkPayloadCompleted(payload);
}

void *RenderEventFunc() {
    return reinterpret_cast<void *>(&OnRenderEvent);
}

int64_t MetalRenderEventId(int32_t &eventId) {
    if (gEventBase < 0) {
        eventId = -1;
        return MILESTRO_API_RET_FAILED;
    }

    eventId = gEventBase + kMetalDrawEventOffset;
    return MILESTRO_API_RET_OK;
}

int64_t RenderTextureEventId(int32_t graphicsBackend, int32_t &eventId) {
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
        case MilestroUnityGraphicsBackend::Vulkan:
        case MilestroUnityGraphicsBackend::OpenGL:
        case MilestroUnityGraphicsBackend::OpenGLES:
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

void *GetRenderEventFuncForExport() {
    return RenderEventFunc();
}

int64_t GetMetalRenderEventIdForExport(int32_t &eventId) {
    return MetalRenderEventId(eventId);
}

int64_t GetRenderTextureEventIdForExport(int32_t graphicsBackend, int32_t &eventId) {
    return RenderTextureEventId(graphicsBackend, eventId);
}

int64_t CreateD3D12ExternalTextureForExport(int32_t width,
                                            int32_t height,
                                            int32_t srgb,
                                            int32_t preferredFormat,
                                            void *&texture) {
#if defined(_WIN32)
    if (gRenderer != kUnityGfxRendererD3D12) {
        texture = nullptr;
        MILESTROLOG_ERROR("Milestro D3D12 external texture requested while Unity renderer is {}.",
                          static_cast<int>(gRenderer));
        return MILESTRO_API_RET_FAILED;
    }

    return d3d12::CreateExternalTexture(width, height, srgb, preferredFormat, texture);
#else
    (void)width;
    (void)height;
    (void)srgb;
    (void)preferredFormat;
    texture = nullptr;
    MILESTROLOG_ERROR("Milestro D3D12 external texture is only available on Windows.");
    return MILESTRO_API_RET_FAILED;
#endif
}

int64_t DestroyD3D12ExternalTextureForExport(void *&texture) {
#if defined(_WIN32)
    return d3d12::DestroyExternalTexture(texture);
#else
    texture = nullptr;
    MILESTROLOG_ERROR("Milestro D3D12 external texture is only available on Windows.");
    return MILESTRO_API_RET_FAILED;
#endif
}

void Load(IUnityInterfaces *unityInterfaces) {
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
