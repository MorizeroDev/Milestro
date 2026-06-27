#include "unity_render/MilestroUnityRenderD3D12Backend.h"

#include "game/milestro_game_retcode.h"
#include "unity_render/MilestroUnityRenderPayloadDraw.h"
#include "unity_render/MilestroUnityRenderTextureHandleKind.h"

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif

#include <Milestro/log/log.h>

#include <vector>

#include "include/core/SkCanvas.h"
#include "include/core/SkColorSpace.h"
#include "include/core/SkSurface.h"
#include "include/gpu/ganesh/GrBackendSurface.h"
#include "include/gpu/ganesh/GrDirectContext.h"
#include "include/gpu/ganesh/SkSurfaceGanesh.h"
#include "include/gpu/ganesh/d3d/GrD3DBackendContext.h"
#include "include/gpu/ganesh/d3d/GrD3DBackendSurface.h"
#include "include/gpu/ganesh/d3d/GrD3DDirectContext.h"

#include <d3d12.h>
#include <dxgi1_4.h>
#include <IUnityGraphicsD3D12.h>

#ifndef MILESTRO_ENABLE_D3D12_GANESH_QUEUE_EXPERIMENT
#define MILESTRO_ENABLE_D3D12_GANESH_QUEUE_EXPERIMENT 0
#endif

namespace milestro::unity_render::d3d12 {

namespace {

IUnityGraphicsD3D12v8 *gD3D12v8 = nullptr;
sk_sp<GrDirectContext> gDirectContext;
ID3D12Device *gDirectContextDevice = nullptr;
ID3D12CommandQueue *gDirectContextQueue = nullptr;

struct PendingResourceRetain {
    gr_cp<ID3D12Resource> resource;
    gr_cp<ID3D12Fence> fence;
    uint64_t fenceValue = 0;
};

std::vector<PendingResourceRetain> gPendingResources;

ID3D12Device *Device() {
    if (gD3D12v8 != nullptr) {
        return gD3D12v8->GetDevice();
    }
    return nullptr;
}

ID3D12CommandQueue *CommandQueue() {
    if (gD3D12v8 != nullptr) {
        return gD3D12v8->GetCommandQueue();
    }
    return nullptr;
}

ID3D12Fence *FrameFence() {
    if (gD3D12v8 != nullptr) {
        return gD3D12v8->GetFrameFence();
    }
    return nullptr;
}

uint64_t NextFrameFenceValue() {
    if (gD3D12v8 != nullptr) {
        return gD3D12v8->GetNextFrameFenceValue();
    }
    return 0;
}

void CollectPendingResources() {
    for (auto it = gPendingResources.begin(); it != gPendingResources.end();) {
        if (it->fence.get() != nullptr && it->fence->GetCompletedValue() >= it->fenceValue) {
            it = gPendingResources.erase(it);
        } else {
            ++it;
        }
    }
}

void RetainResourceUntilShutdown(ID3D12Resource *resource) {
    PendingResourceRetain resources;
    resources.resource.retain(resource);
    gPendingResources.push_back(resources);
}

bool RetainResourceUntilFrameFence(ID3D12Resource *resource) {
    ID3D12Fence *fence = FrameFence();
    uint64_t fenceValue = NextFrameFenceValue();
    if (fence == nullptr || fenceValue == 0) {
        MILESTROLOG_ERROR("Unity D3D12 frame fence is unavailable for rendered texture resource.");
        RetainResourceUntilShutdown(resource);
        return false;
    }

    CollectPendingResources();
    PendingResourceRetain resources;
    resources.resource.retain(resource);
    resources.fence.retain(fence);
    resources.fenceValue = fenceValue;
    gPendingResources.push_back(resources);
    return true;
}

ID3D12Resource *TextureFromRenderBuffer(void *renderBufferHandle) {
    if (renderBufferHandle == nullptr) {
        return nullptr;
    }

    UnityRenderBuffer renderBuffer = reinterpret_cast<UnityRenderBuffer>(renderBufferHandle);
    if (gD3D12v8 != nullptr) {
        return gD3D12v8->TextureFromRenderBuffer(renderBuffer);
    }
    return nullptr;
}

ID3D12Resource *TextureFromNativeTexture(void *nativeTextureHandle) {
    if (nativeTextureHandle == nullptr) {
        return nullptr;
    }

    UnityTextureID texture = reinterpret_cast<UnityTextureID>(nativeTextureHandle);
    if (gD3D12v8 != nullptr) {
        return gD3D12v8->TextureFromNativeTexture(texture);
    }
    return nullptr;
}

gr_cp<IDXGIAdapter1> AdapterForDevice(ID3D12Device *device) {
    gr_cp<IDXGIFactory4> factory;
    HRESULT hr = CreateDXGIFactory1(IID_PPV_ARGS(&factory));
    if (FAILED(hr)) {
        MILESTROLOG_ERROR("Failed to create DXGI factory for Skia D3D context: 0x{:08x}.",
                          static_cast<unsigned int>(hr));
        return gr_cp<IDXGIAdapter1>();
    }

    LUID deviceLuid = device->GetAdapterLuid();
    for (UINT adapterIndex = 0;; ++adapterIndex) {
        gr_cp<IDXGIAdapter1> adapter;
        if (factory->EnumAdapters1(adapterIndex, &adapter) == DXGI_ERROR_NOT_FOUND) {
            break;
        }

        DXGI_ADAPTER_DESC1 desc = {};
        if (FAILED(adapter->GetDesc1(&desc))) {
            continue;
        }

        if (desc.AdapterLuid.HighPart == deviceLuid.HighPart &&
            desc.AdapterLuid.LowPart == deviceLuid.LowPart) {
            return adapter;
        }
    }

    MILESTROLOG_ERROR("Failed to find DXGI adapter matching Unity D3D12 device.");
    return gr_cp<IDXGIAdapter1>();
}

GrDirectContext *DirectContext() {
    ID3D12Device *device = Device();
    ID3D12CommandQueue *queue = CommandQueue();
    if (device == nullptr || queue == nullptr) {
        MILESTROLOG_ERROR("Unity D3D12 device or command queue is unavailable.");
        return nullptr;
    }

    if (gDirectContext != nullptr && gDirectContextDevice == device && gDirectContextQueue == queue) {
        return gDirectContext.get();
    }

    gr_cp<IDXGIAdapter1> adapter = AdapterForDevice(device);
    if (adapter.get() == nullptr) {
        return nullptr;
    }

    GrD3DBackendContext backendContext;
    backendContext.fAdapter.retain(adapter.get());
    backendContext.fDevice.retain(device);
    backendContext.fQueue.retain(queue);

    gDirectContext = GrDirectContexts::MakeD3D(backendContext);
    gDirectContextDevice = device;
    gDirectContextQueue = queue;
    if (gDirectContext == nullptr) {
        MILESTROLOG_ERROR("Failed to create Skia D3D12 direct context.");
    }
    return gDirectContext.get();
}

ID3D12Resource *TextureFromPayload(const MilestroUnityRenderTargetPayload &payload) {
    ID3D12Resource *resource = nullptr;

    if (payload.handleKind == static_cast<int32_t>(MilestroUnityRenderTextureHandleKind::RenderBuffer)) {
        resource = TextureFromRenderBuffer(payload.colorRenderBufferHandle);
        if (resource == nullptr) {
            resource = TextureFromNativeTexture(payload.nativeTextureHandle);
        }
    } else if (payload.handleKind == static_cast<int32_t>(MilestroUnityRenderTextureHandleKind::NativeTexture)) {
        resource = TextureFromNativeTexture(payload.nativeTextureHandle);
    }

    return resource;
}

DXGI_FORMAT NormalizeDxgiFormat(DXGI_FORMAT format,
                                int32_t srgb,
                                int32_t preferredFormat) {
    switch (format) {
        case DXGI_FORMAT_B8G8R8A8_TYPELESS:
            return srgb != 0 ? DXGI_FORMAT_B8G8R8A8_UNORM_SRGB : DXGI_FORMAT_B8G8R8A8_UNORM;
        case DXGI_FORMAT_R8G8B8A8_TYPELESS:
            return srgb != 0 ? DXGI_FORMAT_R8G8B8A8_UNORM_SRGB : DXGI_FORMAT_R8G8B8A8_UNORM;
        case DXGI_FORMAT_UNKNOWN:
            return preferredFormat == 2 ? DXGI_FORMAT_R8G8B8A8_UNORM : DXGI_FORMAT_B8G8R8A8_UNORM;
        default:
            return format;
    }
}

SkColorType ColorTypeForFormat(DXGI_FORMAT format) {
    switch (format) {
        case DXGI_FORMAT_B8G8R8A8_UNORM:
        case DXGI_FORMAT_B8G8R8A8_UNORM_SRGB:
            return kBGRA_8888_SkColorType;
        case DXGI_FORMAT_R8G8B8A8_UNORM:
        case DXGI_FORMAT_R8G8B8A8_UNORM_SRGB:
            return kRGBA_8888_SkColorType;
        default:
            MILESTROLOG_WARN("Unexpected D3D12 texture format {}; trying BGRA_8888.",
                             static_cast<unsigned int>(format));
            return kBGRA_8888_SkColorType;
    }
}

sk_sp<SkColorSpace> ColorSpaceForFormat(DXGI_FORMAT format, int32_t srgb) {
    if (srgb != 0 ||
        format == DXGI_FORMAT_B8G8R8A8_UNORM_SRGB ||
        format == DXGI_FORMAT_R8G8B8A8_UNORM_SRGB) {
        return SkColorSpace::MakeSRGB();
    }
    return nullptr;
}

#if MILESTRO_ENABLE_D3D12_GANESH_QUEUE_EXPERIMENT
void RequestResourceState(ID3D12Resource *resource, D3D12_RESOURCE_STATES state) {
    gD3D12v8->RequestResourceState(resource, state);
}

void NotifyResourceState(ID3D12Resource *resource, D3D12_RESOURCE_STATES state) {
    gD3D12v8->NotifyResourceState(resource, state, false);
}
#endif

void ConfigureRenderEvent(int32_t renderEventId) {
    if (renderEventId < 0) {
        return;
    }

    UnityD3D12PluginEventConfig config = {};
#if MILESTRO_ENABLE_D3D12_GANESH_QUEUE_EXPERIMENT
    // Experimental only: Ganesh D3D builds its context from Unity's queue. This
    // does not statically satisfy the v8 active-command-list state tracker
    // contract; keep it behind an explicit build option for Windows validation.
    config.graphicsQueueAccess = kUnityD3D12GraphicsQueueAccess_Allow;
#else
    // Default builds do not access the Unity command queue. Render() fails fast
    // until a real active-command-list Skia path exists or the experiment is
    // explicitly enabled for runtime/debug-layer validation.
    config.graphicsQueueAccess = kUnityD3D12GraphicsQueueAccess_DontCare;
#endif
    config.flags = kUnityD3D12EventConfigFlag_FlushCommandBuffers |
                   kUnityD3D12EventConfigFlag_SyncWorkerThreads |
                   kUnityD3D12EventConfigFlag_ModifiesCommandBuffersState;
    config.ensureActiveRenderTextureIsBound = false;

    if (gD3D12v8 != nullptr) {
        gD3D12v8->ConfigureEvent(renderEventId, &config);
    }
}

} // namespace

void OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType,
                           IUnityInterfaces *unityInterfaces,
                           UnityGfxRenderer renderer,
                           int32_t renderEventId) {
    if (eventType == kUnityGfxDeviceEventShutdown || renderer != kUnityGfxRendererD3D12) {
        gPendingResources.clear();
        gDirectContext.reset();
        gDirectContextDevice = nullptr;
        gDirectContextQueue = nullptr;
        gD3D12v8 = nullptr;
        return;
    }

    if (eventType != kUnityGfxDeviceEventInitialize || unityInterfaces == nullptr) {
        return;
    }

    gD3D12v8 = unityInterfaces->Get<IUnityGraphicsD3D12v8>();
    if (gD3D12v8 == nullptr) {
        if (unityInterfaces->Get<IUnityGraphicsD3D12>() != nullptr) {
            MILESTROLOG_ERROR("Unity obsolete IUnityGraphicsD3D12 is unsupported; need v8 or newer.");
        } else {
            MILESTROLOG_ERROR("Unity D3D12 v8 graphics interface is unavailable.");
        }
        return;
    }

    ConfigureRenderEvent(renderEventId);
}

int64_t Render(const MilestroUnityRenderTargetPayload &payload) {
    if (payload.width <= 0 || payload.height <= 0) {
        MILESTROLOG_ERROR("Invalid Milestro D3D12 render payload size.");
        return MILESTRO_API_RET_FAILED;
    }

    CollectPendingResources();

    if (payload.msaaSamples != 1) {
        MILESTROLOG_ERROR("Milestro D3D12 RenderTexture MSAA is not implemented yet: {} samples.",
                          payload.msaaSamples);
        return MILESTRO_API_RET_FAILED;
    }

#if !MILESTRO_ENABLE_D3D12_GANESH_QUEUE_EXPERIMENT
    MILESTROLOG_ERROR(
            "Milestro D3D12 RenderTexture backend is disabled by default: "
            "Skia Ganesh D3D requires Unity command queue access, while Unity "
            "D3D12 v8 RequestResourceState/NotifyResourceState are scoped to "
            "the active command list. Rebuild with "
            "MILESTRO_ENABLE_D3D12_GANESH_QUEUE_EXPERIMENT=ON only for "
            "Windows runtime/debug-layer validation.");
    return MILESTRO_API_RET_FAILED;
#else
    ID3D12Device *device = Device();
    GrDirectContext *context = DirectContext();
    if (device == nullptr || context == nullptr) {
        return MILESTRO_API_RET_FAILED;
    }

    ID3D12Resource *resource = TextureFromPayload(payload);
    if (resource == nullptr) {
        MILESTROLOG_ERROR("Failed to resolve Unity RenderTexture to ID3D12Resource.");
        return MILESTRO_API_RET_FAILED;
    }

    gr_cp<ID3D12Resource> resourceRef;
    resourceRef.retain(resource);

    const D3D12_RESOURCE_STATES displayState = D3D12_RESOURCE_STATE_PIXEL_SHADER_RESOURCE;
    const D3D12_RESOURCE_STATES skiaRenderState = D3D12_RESOURCE_STATE_RENDER_TARGET;
    RequestResourceState(resource, skiaRenderState);

    D3D12_RESOURCE_DESC desc = resource->GetDesc();
    DXGI_FORMAT format = NormalizeDxgiFormat(desc.Format, payload.srgb, payload.preferredFormat);
    const uint32_t sampleCount = desc.SampleDesc.Count == 0 ? 1 : desc.SampleDesc.Count;
    const uint32_t levelCount = desc.MipLevels == 0 ? 1 : desc.MipLevels;
    const unsigned int sampleQuality =
            desc.SampleDesc.Quality == 0 ? DXGI_STANDARD_MULTISAMPLE_QUALITY_PATTERN : desc.SampleDesc.Quality;

    GrD3DTextureResourceInfo textureInfo(resource,
                                         nullptr,
                                         skiaRenderState,
                                         format,
                                         sampleCount,
                                         levelCount,
                                         sampleQuality);
    GrBackendRenderTarget renderTarget =
            GrBackendRenderTargets::MakeD3D(payload.width, payload.height, textureInfo);

    sk_sp<SkSurface> surface = SkSurfaces::WrapBackendRenderTarget(context,
                                                                   renderTarget,
                                                                   kTopLeft_GrSurfaceOrigin,
                                                                   ColorTypeForFormat(format),
                                                                   ColorSpaceForFormat(format, payload.srgb),
                                                                   nullptr);
    if (surface == nullptr) {
        MILESTROLOG_ERROR("Failed to wrap Unity ID3D12Resource as Skia render target.");
        RequestResourceState(resource, displayState);
        NotifyResourceState(resource, displayState);
        return MILESTRO_API_RET_FAILED;
    }

    milestro::unity_render::DrawPayload(surface->getCanvas(), payload);
    context->flushAndSubmit(surface.get());

    GrD3DTextureResourceInfo finalInfo = GrBackendRenderTargets::GetD3DTextureResourceInfo(renderTarget);
    NotifyResourceState(resource, finalInfo.fResourceState);
    RequestResourceState(resource, displayState);
    GrBackendRenderTargets::SetD3DResourceState(&renderTarget,
                                                static_cast<GrD3DResourceStateEnum>(displayState));
    NotifyResourceState(resource, displayState);
    RetainResourceUntilFrameFence(resource);
    return MILESTRO_API_RET_OK;
#endif
}

} // namespace milestro::unity_render::d3d12
