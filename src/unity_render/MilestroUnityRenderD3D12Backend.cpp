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

namespace milestro::unity_render::d3d12 {

namespace {

IUnityGraphicsD3D12v7 *gD3D12v7 = nullptr;
IUnityGraphicsD3D12v6 *gD3D12v6 = nullptr;
IUnityGraphicsD3D12v5 *gD3D12v5 = nullptr;
sk_sp<GrDirectContext> gDirectContext;
ID3D12Device *gDirectContextDevice = nullptr;
ID3D12CommandQueue *gDirectContextQueue = nullptr;

struct PendingCommandResources {
    gr_cp<ID3D12Resource> resource;
    gr_cp<ID3D12CommandAllocator> commandAllocator;
    gr_cp<ID3D12GraphicsCommandList> commandList;
    gr_cp<ID3D12Fence> fence;
    uint64_t fenceValue = 0;
};

std::vector<PendingCommandResources> gPendingCommandResources;

ID3D12Device *Device() {
    if (gD3D12v7 != nullptr) {
        return gD3D12v7->GetDevice();
    }
    if (gD3D12v6 != nullptr) {
        return gD3D12v6->GetDevice();
    }
    if (gD3D12v5 != nullptr) {
        return gD3D12v5->GetDevice();
    }
    return nullptr;
}

ID3D12CommandQueue *CommandQueue() {
    if (gD3D12v7 != nullptr) {
        return gD3D12v7->GetCommandQueue();
    }
    if (gD3D12v6 != nullptr) {
        return gD3D12v6->GetCommandQueue();
    }
    if (gD3D12v5 != nullptr) {
        return gD3D12v5->GetCommandQueue();
    }
    return nullptr;
}

ID3D12Fence *FrameFence() {
    if (gD3D12v7 != nullptr) {
        return gD3D12v7->GetFrameFence();
    }
    if (gD3D12v6 != nullptr) {
        return gD3D12v6->GetFrameFence();
    }
    if (gD3D12v5 != nullptr) {
        return gD3D12v5->GetFrameFence();
    }
    return nullptr;
}

uint64_t ExecuteCommandList(ID3D12GraphicsCommandList *commandList,
                            int stateCount,
                            UnityGraphicsD3D12ResourceState *states) {
    if (gD3D12v7 != nullptr) {
        return gD3D12v7->ExecuteCommandList(commandList, stateCount, states);
    }
    if (gD3D12v6 != nullptr) {
        return gD3D12v6->ExecuteCommandList(commandList, stateCount, states);
    }
    if (gD3D12v5 != nullptr) {
        return gD3D12v5->ExecuteCommandList(commandList, stateCount, states);
    }
    return 0;
}

void CollectPendingCommandResources() {
    for (auto it = gPendingCommandResources.begin(); it != gPendingCommandResources.end();) {
        if (it->fence.get() != nullptr && it->fence->GetCompletedValue() >= it->fenceValue) {
            it = gPendingCommandResources.erase(it);
        } else {
            ++it;
        }
    }
}

void RetainCommandResourcesUntilShutdown(ID3D12Resource *resource,
                                         gr_cp<ID3D12CommandAllocator> commandAllocator,
                                         gr_cp<ID3D12GraphicsCommandList> commandList) {
    PendingCommandResources resources;
    resources.resource.retain(resource);
    resources.commandAllocator = commandAllocator;
    resources.commandList = commandList;
    gPendingCommandResources.push_back(resources);
}

bool RetainCommandResources(ID3D12Resource *resource,
                            gr_cp<ID3D12CommandAllocator> commandAllocator,
                            gr_cp<ID3D12GraphicsCommandList> commandList,
                            uint64_t fenceValue) {
    ID3D12Fence *fence = FrameFence();
    if (fence == nullptr || fenceValue == 0) {
        MILESTROLOG_ERROR("Unity D3D12 frame fence is unavailable for submitted command resources.");
        RetainCommandResourcesUntilShutdown(resource, commandAllocator, commandList);
        return false;
    }

    CollectPendingCommandResources();
    PendingCommandResources resources;
    resources.resource.retain(resource);
    resources.commandAllocator = commandAllocator;
    resources.commandList = commandList;
    resources.fence.retain(fence);
    resources.fenceValue = fenceValue;
    gPendingCommandResources.push_back(resources);
    return true;
}

ID3D12Resource *TextureFromRenderBuffer(void *renderBufferHandle) {
    if (renderBufferHandle == nullptr) {
        return nullptr;
    }

    UnityRenderBuffer renderBuffer = reinterpret_cast<UnityRenderBuffer>(renderBufferHandle);
    if (gD3D12v7 != nullptr) {
        return gD3D12v7->TextureFromRenderBuffer(renderBuffer);
    }
    if (gD3D12v6 != nullptr) {
        return gD3D12v6->TextureFromRenderBuffer(renderBuffer);
    }
    if (gD3D12v5 != nullptr) {
        return gD3D12v5->TextureFromRenderBuffer(renderBuffer);
    }
    return nullptr;
}

ID3D12Resource *TextureFromNativeTexture(void *nativeTextureHandle) {
    if (nativeTextureHandle == nullptr) {
        return nullptr;
    }

    UnityTextureID texture = reinterpret_cast<UnityTextureID>(nativeTextureHandle);
    if (gD3D12v7 != nullptr) {
        return gD3D12v7->TextureFromNativeTexture(texture);
    }
    if (gD3D12v6 != nullptr) {
        return gD3D12v6->TextureFromNativeTexture(texture);
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

bool TransitionResource(ID3D12Device *device,
                        ID3D12Resource *resource,
                        D3D12_RESOURCE_STATES before,
                        D3D12_RESOURCE_STATES after,
                        const char *label) {
    if (before == after) {
        return true;
    }

    gr_cp<ID3D12CommandAllocator> commandAllocator;
    HRESULT hr = device->CreateCommandAllocator(D3D12_COMMAND_LIST_TYPE_DIRECT,
                                                IID_PPV_ARGS(&commandAllocator));
    if (FAILED(hr)) {
        MILESTROLOG_ERROR("Failed to create D3D12 command allocator for {} transition: 0x{:08x}.",
                          label,
                          static_cast<unsigned int>(hr));
        return false;
    }

    gr_cp<ID3D12GraphicsCommandList> commandList;
    hr = device->CreateCommandList(0,
                                   D3D12_COMMAND_LIST_TYPE_DIRECT,
                                   commandAllocator.get(),
                                   nullptr,
                                   IID_PPV_ARGS(&commandList));
    if (FAILED(hr)) {
        MILESTROLOG_ERROR("Failed to create D3D12 command list for {} transition: 0x{:08x}.",
                          label,
                          static_cast<unsigned int>(hr));
        return false;
    }

    D3D12_RESOURCE_BARRIER barrier = {};
    barrier.Type = D3D12_RESOURCE_BARRIER_TYPE_TRANSITION;
    barrier.Flags = D3D12_RESOURCE_BARRIER_FLAG_NONE;
    barrier.Transition.pResource = resource;
    barrier.Transition.Subresource = D3D12_RESOURCE_BARRIER_ALL_SUBRESOURCES;
    barrier.Transition.StateBefore = before;
    barrier.Transition.StateAfter = after;
    commandList->ResourceBarrier(1, &barrier);

    hr = commandList->Close();
    if (FAILED(hr)) {
        MILESTROLOG_ERROR("Failed to close D3D12 {} transition command list: 0x{:08x}.",
                          label,
                          static_cast<unsigned int>(hr));
        return false;
    }

    UnityGraphicsD3D12ResourceState state = {};
    state.resource = resource;
    state.expected = before;
    state.current = after;
    uint64_t fenceValue = ExecuteCommandList(commandList.get(), 1, &state);
    if (fenceValue == 0) {
        MILESTROLOG_ERROR("Unity D3D12 ExecuteCommandList returned no fence for {} transition.", label);
        RetainCommandResourcesUntilShutdown(resource, commandAllocator, commandList);
        return false;
    }
    return RetainCommandResources(resource, commandAllocator, commandList, fenceValue);
}

void ConfigureRenderEvent(int32_t renderEventId) {
    if (renderEventId < 0) {
        return;
    }

    UnityD3D12PluginEventConfig config = {};
    config.graphicsQueueAccess = kUnityD3D12GraphicsQueueAccess_Allow;
    config.flags = kUnityD3D12EventConfigFlag_FlushCommandBuffers |
                   kUnityD3D12EventConfigFlag_SyncWorkerThreads |
                   kUnityD3D12EventConfigFlag_ModifiesCommandBuffersState;
    config.ensureActiveRenderTextureIsBound = false;

    if (gD3D12v7 != nullptr) {
        gD3D12v7->ConfigureEvent(renderEventId, &config);
    } else if (gD3D12v6 != nullptr) {
        gD3D12v6->ConfigureEvent(renderEventId, &config);
    }
}

} // namespace

void OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType,
                           IUnityInterfaces *unityInterfaces,
                           UnityGfxRenderer renderer,
                           int32_t renderEventId) {
    if (eventType == kUnityGfxDeviceEventShutdown || renderer != kUnityGfxRendererD3D12) {
        gPendingCommandResources.clear();
        gDirectContext.reset();
        gDirectContextDevice = nullptr;
        gDirectContextQueue = nullptr;
        gD3D12v7 = nullptr;
        gD3D12v6 = nullptr;
        gD3D12v5 = nullptr;
        return;
    }

    if (eventType != kUnityGfxDeviceEventInitialize || unityInterfaces == nullptr) {
        return;
    }

    gD3D12v7 = unityInterfaces->Get<IUnityGraphicsD3D12v7>();
    if (gD3D12v7 == nullptr) {
        gD3D12v6 = unityInterfaces->Get<IUnityGraphicsD3D12v6>();
    }
    if (gD3D12v7 == nullptr && gD3D12v6 == nullptr) {
        gD3D12v5 = unityInterfaces->Get<IUnityGraphicsD3D12v5>();
    }

    if (gD3D12v7 == nullptr && gD3D12v6 == nullptr && gD3D12v5 == nullptr) {
        if (unityInterfaces->Get<IUnityGraphicsD3D12>() != nullptr) {
            MILESTROLOG_ERROR("Unity obsolete IUnityGraphicsD3D12 is unsupported; need v5 or newer.");
        } else {
            MILESTROLOG_ERROR("Unity D3D12 graphics interface is unavailable.");
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

    CollectPendingCommandResources();

    if (payload.msaaSamples != 1) {
        MILESTROLOG_ERROR("Milestro D3D12 RenderTexture MSAA is not implemented yet: {} samples.",
                          payload.msaaSamples);
        return MILESTRO_API_RET_FAILED;
    }

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
    if (!TransitionResource(device,
                            resource,
                            displayState,
                            skiaRenderState,
                            "pre-Skia render target")) {
        return MILESTRO_API_RET_FAILED;
    }

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
        TransitionResource(device, resource, skiaRenderState, displayState, "failed-wrap restore");
        return MILESTRO_API_RET_FAILED;
    }

    milestro::unity_render::DrawPayload(surface->getCanvas(), payload);
    context->flushAndSubmit(surface.get());

    GrD3DTextureResourceInfo finalInfo = GrBackendRenderTargets::GetD3DTextureResourceInfo(renderTarget);
    if (!TransitionResource(device, resource, finalInfo.fResourceState, displayState, "post-Skia display")) {
        return MILESTRO_API_RET_FAILED;
    }
    GrBackendRenderTargets::SetD3DResourceState(&renderTarget,
                                                static_cast<GrD3DResourceStateEnum>(displayState));
    return MILESTRO_API_RET_OK;
}

} // namespace milestro::unity_render::d3d12
