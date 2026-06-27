#include "milestro_game_unity_render.h"

#include "milestro_game_retcode.h"

#include <Milestro/game/milestro_game_interface.h>
#include <Milestro/log/log.h>

namespace milestro::game::unity_render {

namespace {

constexpr int kMetalDrawEventOffset = 0;
constexpr int kReservedEventCount = 1;

IUnityInterfaces *gUnityInterfaces = nullptr;
IUnityGraphics *gUnityGraphics = nullptr;
UnityGfxRenderer gRenderer = kUnityGfxRendererNull;
int gEventBase = -1;

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
}

void UNITY_INTERFACE_API OnRenderEvent(int eventId, void *data) {
    if (gEventBase < 0 || eventId != gEventBase + kMetalDrawEventOffset) {
        MILESTROLOG_WARN("Ignoring unknown Milestro Unity render event: {}", eventId);
        return;
    }

    if (data == nullptr) {
        MILESTROLOG_ERROR("Milestro Unity render event received null payload.");
        return;
    }

    if (gRenderer != kUnityGfxRendererMetal) {
        MILESTROLOG_ERROR("Milestro Metal render event invoked while Unity renderer is {}.", static_cast<int>(gRenderer));
        return;
    }

    auto *payload = static_cast<MilestroUnityRenderTargetPayload *>(data);
#if defined(__APPLE__)
    const auto status = metal::Render(*payload);
    if (status < 0) {
        MILESTROLOG_ERROR("Milestro Metal render event failed: {}", status);
    }
#else
    (void)payload;
    MILESTROLOG_ERROR("Milestro Metal render event is only available on Apple platforms.");
#endif
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

} // namespace

void *GetRenderEventFuncForExport() {
    return RenderEventFunc();
}

int64_t GetMetalRenderEventIdForExport(int32_t &eventId) {
    return MetalRenderEventId(eventId);
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

} // namespace milestro::game::unity_render

extern "C" {

MILESTRO_API void *MilestroUnityRenderGetRenderEventAndDataFunc() {
    return milestro::game::unity_render::GetRenderEventFuncForExport();
}

MILESTRO_API int64_t MilestroUnityRenderGetMetalRenderEventId(int32_t &eventId) {
    return milestro::game::unity_render::GetMetalRenderEventIdForExport(eventId);
}

}
