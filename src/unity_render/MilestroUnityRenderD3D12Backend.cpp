#include "unity_render/MilestroUnityRenderD3D12Backend.h"

#include "game/milestro_game_retcode.h"
#include "unity_render/MilestroUnityRenderSubmissionDraw.h"
#include "unity_render/MilestroUnityRenderTextureHandleKind.h"

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif

#include <algorithm>
#include <atomic>
#include <cstdio>
#include <string>
#include <vector>

#include "unity_render/MilestroUnityRenderLog.h"
#include "include/core/SkCanvas.h"
#include "include/core/SkColorSpace.h"
#include "include/core/SkSurface.h"
#include "include/gpu/ganesh/GrBackendSurface.h"
#include "include/gpu/ganesh/GrDirectContext.h"
#include "include/gpu/ganesh/SkSurfaceGanesh.h"
#include "include/gpu/ganesh/d3d/GrD3DBackendContext.h"
#include "include/gpu/ganesh/d3d/GrD3DBackendSurface.h"
#include "include/gpu/ganesh/d3d/GrD3DDirectContext.h"

#include <IUnityGraphicsD3D12.h>
#include <d3d12.h>
#include <dxgi1_4.h>

namespace milestro::unity_render::d3d12 {

namespace {

IUnityGraphicsD3D12v8* gD3D12v8 = nullptr;
gr_cp<ID3D12Device> gUnityDevice;
sk_sp<GrDirectContext> gDirectContext;
ID3D12Device* gDirectContextDevice = nullptr;
ID3D12CommandQueue* gDirectContextQueue = nullptr;
bool gLoggedQueueModeStateApiSkip = false;
bool gLoggedCommandRecordingProbe = false;
std::atomic<uint64_t> gRenderSerial = 0;

constexpr bool kUseUnityStateTrackerInQueueMode = false;
constexpr bool kProbeDirectRenderTargetView = false;
constexpr bool kSyncSkiaSubmitForDiagnostics = true;

struct PendingResourceRetain {
    gr_cp<ID3D12Resource> resource;
    gr_cp<ID3D12Fence> fence;
    uint64_t fenceValue = 0;
};

struct PendingCommandListRetain {
    gr_cp<ID3D12CommandAllocator> allocator;
    gr_cp<ID3D12GraphicsCommandList> commandList;
    gr_cp<ID3D12Fence> fence;
    uint64_t fenceValue = 0;
};

struct CachedRenderTarget {
    gr_cp<ID3D12Resource> resource;
    GrBackendRenderTarget renderTarget;
    sk_sp<SkSurface> surface;
    ID3D12Device* device = nullptr;
    ID3D12CommandQueue* queue = nullptr;
    uint64_t width = 0;
    uint32_t height = 0;
    DXGI_FORMAT format = DXGI_FORMAT_UNKNOWN;
    uint32_t sampleCount = 1;
    uint32_t levelCount = 1;
    unsigned int sampleQuality = 0;
    int32_t colorSpace = 0;
    int32_t storageSrgb = 0;
};

std::vector<PendingResourceRetain> gPendingResources;
std::vector<PendingCommandListRetain> gPendingCommandLists;
std::vector<CachedRenderTarget> gCachedRenderTargets;

enum class TextureSource { None, RenderBuffer, NativeTexture };

struct TextureLookup {
    ID3D12Resource* resource = nullptr;
    TextureSource source = TextureSource::None;
};

const char* TextureSourceName(TextureSource source) {
    switch (source) {
        case TextureSource::RenderBuffer:
            return "RenderBuffer";
        case TextureSource::NativeTexture:
            return "NativeTexture";
        case TextureSource::None:
        default:
            return "None";
    }
}

std::string FormatLuid(LUID luid) {
    char buffer[32];
    std::snprintf(buffer,
                  sizeof(buffer),
                  "%08x:%08x",
                  static_cast<unsigned int>(luid.HighPart),
                  static_cast<unsigned int>(luid.LowPart));
    return buffer;
}

ID3D12Device* Device() {
    if (gUnityDevice.get() != nullptr) {
        return gUnityDevice.get();
    }
    return nullptr;
}

ID3D12CommandQueue* CommandQueue() {
    if (gD3D12v8 != nullptr) {
        return gD3D12v8->GetCommandQueue();
    }
    return nullptr;
}

ID3D12Fence* FrameFence() {
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

void LogCommandRecordingStateProbe() {
    if (gLoggedCommandRecordingProbe || gD3D12v8 == nullptr || gD3D12v8->CommandRecordingState == nullptr) {
        return;
    }

    UnityGraphicsD3D12RecordingState recordingState = {};
    bool available = gD3D12v8->CommandRecordingState(&recordingState);
    MILESTRO_RENDER_LOG_INFO("Milestro D3D12 v8 event contract probe: queueAccess=Allow, CommandRecordingState available={}, "
                     "commandList={}.",
                     available ? 1 : 0,
                     static_cast<void*>(recordingState.commandList));
    gLoggedCommandRecordingProbe = true;
}

void CollectPendingResources() {
    for (auto it = gPendingResources.begin(); it != gPendingResources.end();) {
        if (it->fence.get() != nullptr && it->fence->GetCompletedValue() >= it->fenceValue) {
            it = gPendingResources.erase(it);
        } else {
            ++it;
        }
    }

    for (auto it = gPendingCommandLists.begin(); it != gPendingCommandLists.end();) {
        if (it->fence.get() != nullptr && it->fence->GetCompletedValue() >= it->fenceValue) {
            it = gPendingCommandLists.erase(it);
        } else {
            ++it;
        }
    }
}

void ClearCachedRenderTargets() {
    gCachedRenderTargets.clear();
}

void ClearCachedRenderTarget(ID3D12Resource* resource) {
    if (resource == nullptr) {
        return;
    }

    gCachedRenderTargets.erase(std::remove_if(gCachedRenderTargets.begin(),
                                              gCachedRenderTargets.end(),
                                              [resource](const CachedRenderTarget& cached) {
                                                  return cached.resource.get() == resource;
                                              }),
                               gCachedRenderTargets.end());
}

void RetainResourceUntilShutdown(ID3D12Resource* resource) {
    PendingResourceRetain resources;
    resources.resource.retain(resource);
    gPendingResources.push_back(resources);
}

bool RetainResourceUntilFrameFence(ID3D12Resource* resource) {
    ID3D12Fence* fence = FrameFence();
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

ID3D12Resource* TextureFromRenderBuffer(void* renderBufferHandle) {
    if (renderBufferHandle == nullptr) {
        return nullptr;
    }

    UnityRenderBuffer renderBuffer = reinterpret_cast<UnityRenderBuffer>(renderBufferHandle);
    if (gD3D12v8 != nullptr) {
        return gD3D12v8->TextureFromRenderBuffer(renderBuffer);
    }
    return nullptr;
}

ID3D12Resource* TextureFromNativeTexture(void* nativeTextureHandle) {
    return static_cast<ID3D12Resource*>(nativeTextureHandle);
}

gr_cp<IDXGIAdapter1> AdapterForDevice(ID3D12Device* device) {
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

        if (desc.AdapterLuid.HighPart == deviceLuid.HighPart && desc.AdapterLuid.LowPart == deviceLuid.LowPart) {
            return adapter;
        }
    }

    MILESTROLOG_ERROR("Failed to find DXGI adapter matching Unity D3D12 device.");
    return gr_cp<IDXGIAdapter1>();
}

GrDirectContext* DirectContext() {
    ID3D12Device* device = Device();
    ID3D12CommandQueue* queue = CommandQueue();
    if (device == nullptr || queue == nullptr) {
        MILESTROLOG_ERROR("Unity D3D12 device or command queue is unavailable.");
        return nullptr;
    }

    if (gDirectContext != nullptr && gDirectContextDevice == device && gDirectContextQueue == queue) {
        MILESTRO_RENDER_LOG_INFO("Reusing Skia D3D12 direct context={}, unityDevice={}, queue={}, adapterLuid={}.",
                         static_cast<void*>(gDirectContext.get()),
                         static_cast<void*>(device),
                         static_cast<void*>(queue),
                         FormatLuid(device->GetAdapterLuid()));
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

    if (gDirectContext != nullptr) {
        ClearCachedRenderTargets();
    }

    DXGI_ADAPTER_DESC1 adapterDesc = {};
    adapter->GetDesc1(&adapterDesc);
    MILESTRO_RENDER_LOG_INFO("Creating Skia D3D12 direct context: previousContext={}, previousDevice={}, previousQueue={}, "
                     "unityDevice={}, queue={}, deviceLuid={}, adapter={}, adapterLuid={}.",
                     static_cast<void*>(gDirectContext.get()),
                     static_cast<void*>(gDirectContextDevice),
                     static_cast<void*>(gDirectContextQueue),
                     static_cast<void*>(device),
                     static_cast<void*>(queue),
                     FormatLuid(device->GetAdapterLuid()),
                     static_cast<void*>(adapter.get()),
                     FormatLuid(adapterDesc.AdapterLuid));

    gDirectContext = GrDirectContexts::MakeD3D(backendContext);
    gDirectContextDevice = device;
    gDirectContextQueue = queue;
    if (gDirectContext == nullptr) {
        MILESTROLOG_ERROR("Failed to create Skia D3D12 direct context.");
    } else {
        MILESTRO_RENDER_LOG_INFO("Created Skia D3D12 direct context={}.", static_cast<void*>(gDirectContext.get()));
    }
    return gDirectContext.get();
}

TextureLookup TextureFromPayload(const MilestroUnityRenderTargetPayload& payload) {
    TextureLookup result;

    if (payload.handleKind == static_cast<int32_t>(MilestroUnityRenderTextureHandleKind::RenderBuffer)) {
        result.resource = TextureFromRenderBuffer(payload.colorRenderBufferHandle);
        result.source = result.resource != nullptr ? TextureSource::RenderBuffer : TextureSource::None;
    } else if (payload.handleKind == static_cast<int32_t>(MilestroUnityRenderTextureHandleKind::NativeTexture)) {
        result.resource = TextureFromNativeTexture(payload.nativeTextureHandle);
        result.source = result.resource != nullptr ? TextureSource::NativeTexture : TextureSource::None;
    }

    return result;
}

constexpr int32_t kUnityColorSpaceLinear = 1;

sk_sp<SkColorSpace> ColorSpaceForUnityColorSpace(int32_t colorSpace) {
    if (colorSpace == kUnityColorSpaceLinear) {
        return SkColorSpace::MakeSRGBLinear();
    }
    return SkColorSpace::MakeSRGB();
}

DXGI_FORMAT NormalizeDxgiFormat(DXGI_FORMAT format, int32_t storageSrgb, int32_t preferredFormat) {
    switch (format) {
        case DXGI_FORMAT_B8G8R8A8_TYPELESS:
            return storageSrgb != 0 ? DXGI_FORMAT_B8G8R8A8_UNORM_SRGB : DXGI_FORMAT_B8G8R8A8_UNORM;
        case DXGI_FORMAT_R8G8B8A8_TYPELESS:
            return storageSrgb != 0 ? DXGI_FORMAT_R8G8B8A8_UNORM_SRGB : DXGI_FORMAT_R8G8B8A8_UNORM;
        case DXGI_FORMAT_UNKNOWN:
            if (preferredFormat == 2) {
                return storageSrgb != 0 ? DXGI_FORMAT_R8G8B8A8_UNORM_SRGB : DXGI_FORMAT_R8G8B8A8_UNORM;
            }
            return storageSrgb != 0 ? DXGI_FORMAT_B8G8R8A8_UNORM_SRGB : DXGI_FORMAT_B8G8R8A8_UNORM;
        default:
            return format;
    }
}

bool IsSupportedDxgiFormat(DXGI_FORMAT format) {
    switch (format) {
        case DXGI_FORMAT_B8G8R8A8_UNORM:
        case DXGI_FORMAT_B8G8R8A8_UNORM_SRGB:
        case DXGI_FORMAT_R8G8B8A8_UNORM:
        case DXGI_FORMAT_R8G8B8A8_UNORM_SRGB:
            return true;
        default:
            return false;
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
            MILESTRO_RENDER_LOG_WARN("Unexpected D3D12 texture format {}; trying BGRA_8888.",
                             static_cast<unsigned int>(format));
            return kBGRA_8888_SkColorType;
    }
}

sk_sp<SkColorSpace> ColorSpaceForFormat(DXGI_FORMAT format, int32_t colorSpace) {
    if (format == DXGI_FORMAT_B8G8R8A8_UNORM_SRGB || format == DXGI_FORMAT_R8G8B8A8_UNORM_SRGB) {
        return SkColorSpace::MakeSRGB();
    }
    return ColorSpaceForUnityColorSpace(colorSpace);
}

DXGI_FORMAT PreferredDxgiFormat(int32_t storageSrgb, int32_t preferredFormat) {
    return NormalizeDxgiFormat(DXGI_FORMAT_UNKNOWN, storageSrgb, preferredFormat);
}

bool CreateD3D12TextureResource(ID3D12Device* device,
                                int32_t width,
                                int32_t height,
                                int32_t storageSrgb,
                                int32_t preferredFormat,
                                gr_cp<ID3D12Resource>& resource) {
    if (device == nullptr || width <= 0 || height <= 0) {
        return false;
    }

    DXGI_FORMAT format = PreferredDxgiFormat(storageSrgb, preferredFormat);
    if (!IsSupportedDxgiFormat(format)) {
        MILESTROLOG_ERROR("Unsupported Milestro D3D12 external texture format {}.", static_cast<unsigned int>(format));
        return false;
    }

    D3D12_HEAP_PROPERTIES heapProps = {};
    heapProps.Type = D3D12_HEAP_TYPE_DEFAULT;
    heapProps.CPUPageProperty = D3D12_CPU_PAGE_PROPERTY_UNKNOWN;
    heapProps.MemoryPoolPreference = D3D12_MEMORY_POOL_UNKNOWN;
    heapProps.CreationNodeMask = 1;
    heapProps.VisibleNodeMask = 1;

    D3D12_RESOURCE_DESC desc = {};
    desc.Dimension = D3D12_RESOURCE_DIMENSION_TEXTURE2D;
    desc.Width = static_cast<UINT64>(width);
    desc.Height = static_cast<UINT>(height);
    desc.DepthOrArraySize = 1;
    desc.MipLevels = 1;
    desc.Format = format;
    desc.SampleDesc.Count = 1;
    desc.SampleDesc.Quality = 0;
    desc.Layout = D3D12_TEXTURE_LAYOUT_UNKNOWN;
    desc.Flags = D3D12_RESOURCE_FLAG_ALLOW_RENDER_TARGET | D3D12_RESOURCE_FLAG_ALLOW_SIMULTANEOUS_ACCESS;

    D3D12_CLEAR_VALUE clearValue = {};
    clearValue.Format = format;
    clearValue.Color[0] = 0.0f;
    clearValue.Color[1] = 0.0f;
    clearValue.Color[2] = 0.0f;
    clearValue.Color[3] = 0.0f;

    HRESULT hr = device->CreateCommittedResource(&heapProps,
                                                 D3D12_HEAP_FLAG_NONE,
                                                 &desc,
                                                 D3D12_RESOURCE_STATE_PIXEL_SHADER_RESOURCE,
                                                 &clearValue,
                                                 IID_PPV_ARGS(&resource));
    if (FAILED(hr) || resource.get() == nullptr) {
        MILESTROLOG_ERROR("Failed to create Milestro D3D12 external texture resource {}x{}, format={}: 0x{:08x}.",
                          width,
                          height,
                          static_cast<unsigned int>(format),
                          static_cast<unsigned int>(hr));
        return false;
    }

    MILESTRO_RENDER_LOG_INFO(
            "Created Milestro D3D12 external texture resource={}, device={}, size={}x{}, format={}, flags=0x{:x}.",
            static_cast<void*>(resource.get()),
            static_cast<void*>(device),
            width,
            height,
            static_cast<unsigned int>(format),
            static_cast<unsigned int>(desc.Flags));
    return true;
}

bool TransitionExternalTextureForUnity(ID3D12Device* device,
                                       ID3D12Resource* resource,
                                       D3D12_RESOURCE_STATES before,
                                       D3D12_RESOURCE_STATES after,
                                       uint64_t renderSerial) {
    if (resource == nullptr || before == after) {
        return true;
    }

    if (gD3D12v8 == nullptr || gD3D12v8->ExecuteCommandList == nullptr) {
        MILESTROLOG_ERROR("Unity D3D12 ExecuteCommandList is unavailable for external texture state transition.");
        return false;
    }

    gr_cp<ID3D12CommandAllocator> allocator;
    HRESULT hr = device->CreateCommandAllocator(D3D12_COMMAND_LIST_TYPE_DIRECT, IID_PPV_ARGS(&allocator));
    if (FAILED(hr) || allocator.get() == nullptr) {
        MILESTROLOG_ERROR("Failed to create Milestro D3D12 external texture transition allocator: 0x{:08x}.",
                          static_cast<unsigned int>(hr));
        return false;
    }

    gr_cp<ID3D12GraphicsCommandList> commandList;
    hr = device->CreateCommandList(0,
                                   D3D12_COMMAND_LIST_TYPE_DIRECT,
                                   allocator.get(),
                                   nullptr,
                                   IID_PPV_ARGS(&commandList));
    if (FAILED(hr) || commandList.get() == nullptr) {
        MILESTROLOG_ERROR("Failed to create Milestro D3D12 external texture transition command list: 0x{:08x}.",
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
        MILESTROLOG_ERROR("Failed to close Milestro D3D12 external texture transition command list: 0x{:08x}.",
                          static_cast<unsigned int>(hr));
        return false;
    }

    UnityGraphicsD3D12ResourceState state = {};
    state.resource = resource;
    state.expected = before;
    state.current = after;
    UINT64 fenceValue = gD3D12v8->ExecuteCommandList(commandList.get(), 1, &state);
    MILESTRO_RENDER_LOG_INFO("Submitted Milestro D3D12 external texture transition: event={}, resource={}, before={}, "
                     "after={}, fenceValue={}.",
                     renderSerial,
                     static_cast<void*>(resource),
                     static_cast<unsigned int>(before),
                     static_cast<unsigned int>(after),
                     static_cast<unsigned long long>(fenceValue));
    if (fenceValue == 0) {
        return false;
    }

    ID3D12Fence* fence = FrameFence();
    if (fence == nullptr) {
        MILESTRO_RENDER_LOG_WARN("Unity D3D12 frame fence is unavailable after external texture transition; retaining command "
                         "list until shutdown.");
    }

    PendingCommandListRetain pending;
    pending.allocator = std::move(allocator);
    pending.commandList = std::move(commandList);
    pending.fence.retain(fence);
    pending.fenceValue = fenceValue;
    gPendingCommandLists.push_back(std::move(pending));
    return true;
}

bool ProbeRenderTargetView(ID3D12Device* device, ID3D12Resource* resource, uint64_t renderSerial) {
    D3D12_DESCRIPTOR_HEAP_DESC heapDesc = {};
    heapDesc.Type = D3D12_DESCRIPTOR_HEAP_TYPE_RTV;
    heapDesc.NumDescriptors = 1;
    heapDesc.Flags = D3D12_DESCRIPTOR_HEAP_FLAG_NONE;

    gr_cp<ID3D12DescriptorHeap> heap;
    HRESULT hr = device->CreateDescriptorHeap(&heapDesc, IID_PPV_ARGS(&heap));
    if (FAILED(hr) || heap.get() == nullptr) {
        MILESTROLOG_ERROR("Milestro D3D12 direct RTV probe failed to create descriptor heap on event {}: 0x{:08x}.",
                          renderSerial,
                          static_cast<unsigned int>(hr));
        return false;
    }

    D3D12_CPU_DESCRIPTOR_HANDLE handle = heap->GetCPUDescriptorHandleForHeapStart();
    MILESTRO_RENDER_LOG_INFO("Milestro D3D12 direct RTV probe before CreateRenderTargetView: event={}, device={}, resource={}, "
                     "heap={}, cpuHandle=0x{:x}.",
                     renderSerial,
                     static_cast<void*>(device),
                     static_cast<void*>(resource),
                     static_cast<void*>(heap.get()),
                     static_cast<unsigned long long>(handle.ptr));
    device->CreateRenderTargetView(resource, nullptr, handle);
    MILESTRO_RENDER_LOG_INFO("Milestro D3D12 direct RTV probe succeeded: event={}, heap={}, cpuHandle=0x{:x}.",
                     renderSerial,
                     static_cast<void*>(heap.get()),
                     static_cast<unsigned long long>(handle.ptr));
    return true;
}

bool CachedRenderTargetMatches(const CachedRenderTarget& cached,
                               ID3D12Device* device,
                               ID3D12CommandQueue* queue,
                               ID3D12Resource* resource,
                               const D3D12_RESOURCE_DESC& desc,
                               DXGI_FORMAT format,
                               uint32_t sampleCount,
                               uint32_t levelCount,
                               unsigned int sampleQuality,
                               int32_t colorSpace,
                               int32_t storageSrgb) {
    return cached.surface != nullptr && cached.resource.get() == resource && cached.device == device &&
           cached.queue == queue && cached.width == desc.Width && cached.height == desc.Height &&
           cached.format == format && cached.sampleCount == sampleCount && cached.levelCount == levelCount &&
           cached.sampleQuality == sampleQuality && cached.colorSpace == colorSpace &&
           cached.storageSrgb == storageSrgb;
}

CachedRenderTarget* FindCachedRenderTarget(ID3D12Device* device,
                                           ID3D12CommandQueue* queue,
                                           ID3D12Resource* resource,
                                           const D3D12_RESOURCE_DESC& desc,
                                           DXGI_FORMAT format,
                                           uint32_t sampleCount,
                                           uint32_t levelCount,
                                           unsigned int sampleQuality,
                                           int32_t colorSpace,
                                           int32_t storageSrgb) {
    for (CachedRenderTarget& cached: gCachedRenderTargets) {
        if (CachedRenderTargetMatches(cached,
                                      device,
                                      queue,
                                      resource,
                                      desc,
                                      format,
                                      sampleCount,
                                      levelCount,
                                      sampleQuality,
                                      colorSpace,
                                      storageSrgb)) {
            return &cached;
        }
    }
    return nullptr;
}

CachedRenderTarget* GetOrCreateCachedRenderTarget(GrDirectContext* context,
                                                  ID3D12Device* device,
                                                  ID3D12CommandQueue* queue,
                                                  ID3D12Resource* resource,
                                                  const D3D12_RESOURCE_DESC& desc,
                                                  DXGI_FORMAT format,
                                                  uint32_t sampleCount,
                                                  uint32_t levelCount,
                                                  unsigned int sampleQuality,
                                                  int32_t colorSpace,
                                                  int32_t storageSrgb,
                                                  D3D12_RESOURCE_STATES initialState,
                                                  uint64_t renderSerial) {
    CachedRenderTarget* cached = FindCachedRenderTarget(device,
                                                        queue,
                                                        resource,
                                                        desc,
                                                        format,
                                                        sampleCount,
                                                        levelCount,
                                                        sampleQuality,
                                                        colorSpace,
                                                        storageSrgb);
    if (cached != nullptr) {
        MILESTRO_RENDER_LOG_INFO("Reusing cached Skia D3D12 render target: event={}, resource={}, surface={}.",
                         renderSerial,
                         static_cast<void*>(resource),
                         static_cast<void*>(cached->surface.get()));
        return cached;
    }

    gCachedRenderTargets.emplace_back();
    CachedRenderTarget& created = gCachedRenderTargets.back();
    created.resource.retain(resource);
    created.device = device;
    created.queue = queue;
    created.width = desc.Width;
    created.height = desc.Height;
    created.format = format;
    created.sampleCount = sampleCount;
    created.levelCount = levelCount;
    created.sampleQuality = sampleQuality;
    created.colorSpace = colorSpace;
    created.storageSrgb = storageSrgb;

    GrD3DTextureResourceInfo textureInfo;
    textureInfo.fResource.retain(resource);
    textureInfo.fResourceState = initialState;
    textureInfo.fFormat = format;
    textureInfo.fSampleCount = sampleCount;
    textureInfo.fLevelCount = levelCount;
    textureInfo.fSampleQualityPattern = sampleQuality;
    created.renderTarget = GrBackendRenderTargets::MakeD3D(static_cast<int>(desc.Width), desc.Height, textureInfo);
    created.surface = SkSurfaces::WrapBackendRenderTarget(context,
                                                          created.renderTarget,
                                                          kTopLeft_GrSurfaceOrigin,
                                                          ColorTypeForFormat(format),
                                                          ColorSpaceForFormat(format, colorSpace),
                                                          nullptr);
    if (created.surface == nullptr) {
        MILESTROLOG_ERROR("Failed to wrap Unity ID3D12Resource as Skia render target.");
        gCachedRenderTargets.pop_back();
        return nullptr;
    }

    MILESTRO_RENDER_LOG_INFO("Created cached Skia D3D12 render target: event={}, resource={}, surface={}, initialState={}.",
                     renderSerial,
                     static_cast<void*>(resource),
                     static_cast<void*>(created.surface.get()),
                     static_cast<unsigned int>(initialState));
    return &created;
}

void RequestResourceState(ID3D12Resource* resource, D3D12_RESOURCE_STATES state) {
    if (gD3D12v8 == nullptr || gD3D12v8->RequestResourceState == nullptr) {
        return;
    }

    if (!kUseUnityStateTrackerInQueueMode) {
        if (!gLoggedQueueModeStateApiSkip) {
            MILESTRO_RENDER_LOG_INFO("Skipping Unity D3D12 active-command-list resource state APIs in queueAccess=Allow mode; "
                             "Skia owns queue submission on this path.");
            gLoggedQueueModeStateApiSkip = true;
        }
        return;
    }

    if (!gLoggedQueueModeStateApiSkip) {
        MILESTRO_RENDER_LOG_INFO(
                "Using Unity D3D12 active-command-list resource state APIs in queueAccess=Allow mode for diagnostics.");
        gLoggedQueueModeStateApiSkip = true;
    }

    gD3D12v8->RequestResourceState(resource, state);
}

void NotifyResourceState(ID3D12Resource* resource, D3D12_RESOURCE_STATES state) {
    if (gD3D12v8 == nullptr || gD3D12v8->NotifyResourceState == nullptr) {
        return;
    }

    if (!kUseUnityStateTrackerInQueueMode) {
        return;
    }

    gD3D12v8->NotifyResourceState(resource, state, false);
}

void ConfigureRenderEvent(int32_t renderEventId) {
    if (renderEventId < 0) {
        return;
    }

    UnityD3D12PluginEventConfig config = {};
    // Ganesh D3D builds its context from Unity's queue. This does not
    // statically satisfy the v8 active-command-list state tracker contract;
    // Windows debug-layer/reload runtime validation is still required.
    config.graphicsQueueAccess = kUnityD3D12GraphicsQueueAccess_Allow;
    config.flags = kUnityD3D12EventConfigFlag_FlushCommandBuffers | kUnityD3D12EventConfigFlag_SyncWorkerThreads |
                   kUnityD3D12EventConfigFlag_ModifiesCommandBuffersState;
    config.ensureActiveRenderTextureIsBound = false;

    if (gD3D12v8 != nullptr) {
        gD3D12v8->ConfigureEvent(renderEventId, &config);
        MILESTRO_RENDER_LOG_INFO("Configured Milestro D3D12 render event {}: graphicsQueueAccess=Allow, flags=0x{:x}, "
                         "ensureActiveRenderTextureIsBound={}.",
                         renderEventId,
                         static_cast<unsigned int>(config.flags),
                         config.ensureActiveRenderTextureIsBound ? 1 : 0);
    }
}

} // namespace

void OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType,
                           IUnityInterfaces* unityInterfaces,
                           UnityGfxRenderer renderer,
                           int32_t renderEventId) {
    if (eventType == kUnityGfxDeviceEventShutdown || renderer != kUnityGfxRendererD3D12) {
        ClearCachedRenderTargets();
        gPendingResources.clear();
        gPendingCommandLists.clear();
        gDirectContext.reset();
        gDirectContextDevice = nullptr;
        gDirectContextQueue = nullptr;
        gLoggedQueueModeStateApiSkip = false;
        gLoggedCommandRecordingProbe = false;
        gUnityDevice.reset();
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

    gUnityDevice.retain(gD3D12v8->GetDevice());
    if (gUnityDevice.get() == nullptr) {
        MILESTROLOG_ERROR("Unity D3D12 device is unavailable during graphics initialization.");
        return;
    }

    ConfigureRenderEvent(renderEventId);
}

int64_t CreateExternalTexture(int32_t width,
                              int32_t height,
                              int32_t storageSrgb,
                              int32_t preferredFormat,
                              void*& texture) {
    texture = nullptr;

    if (width <= 0 || height <= 0) {
        MILESTROLOG_ERROR("Invalid Milestro D3D12 external texture size {}x{}.", width, height);
        return MILESTRO_API_RET_FAILED;
    }

    ID3D12Device* device = Device();
    if (device == nullptr) {
        MILESTROLOG_ERROR("Unity D3D12 device is unavailable while creating external texture.");
        return MILESTRO_API_RET_FAILED;
    }

    gr_cp<ID3D12Resource> resource;
    if (!CreateD3D12TextureResource(device, width, height, storageSrgb, preferredFormat, resource)) {
        return MILESTRO_API_RET_FAILED;
    }

    texture = resource.release();
    return MILESTRO_API_RET_OK;
}

int64_t DestroyExternalTexture(void*& texture) {
    ID3D12Resource* resource = static_cast<ID3D12Resource*>(texture);
    texture = nullptr;
    if (resource == nullptr) {
        return MILESTRO_API_RET_OK;
    }

    RetainResourceUntilFrameFence(resource);
    MILESTRO_RENDER_LOG_INFO("Destroying Milestro D3D12 external texture resource={}.", static_cast<void*>(resource));
    ClearCachedRenderTarget(resource);
    resource->Release();
    return MILESTRO_API_RET_OK;
}

int64_t Render(const MilestroUnityRenderSubmission& submission) {
    const MilestroUnityRenderTargetPayload& payload = submission.target;
    const uint64_t renderSerial = gRenderSerial.fetch_add(1, std::memory_order_relaxed) + 1;

    LogCommandRecordingStateProbe();

    if (payload.width <= 0 || payload.height <= 0) {
        MILESTROLOG_ERROR("Invalid Milestro D3D12 render payload size on event {}.", renderSerial);
        return MILESTRO_API_RET_FAILED;
    }

    CollectPendingResources();

    if (payload.msaaSamples != 1) {
        MILESTROLOG_ERROR("Milestro D3D12 RenderTexture MSAA is not implemented yet: {} samples.", payload.msaaSamples);
        return MILESTRO_API_RET_FAILED;
    }

    ID3D12Device* device = Device();
    GrDirectContext* context = DirectContext();
    if (device == nullptr || context == nullptr) {
        return MILESTRO_API_RET_FAILED;
    }

    TextureLookup texture = TextureFromPayload(payload);
    ID3D12Resource* resource = texture.resource;
    if (resource == nullptr) {
        MILESTROLOG_ERROR("Failed to resolve Unity texture to ID3D12Resource on event {}. handleKind={}, "
                          "renderBufferHandle={}, nativeTextureHandle={}.",
                          renderSerial,
                          payload.handleKind,
                          payload.colorRenderBufferHandle,
                          payload.nativeTextureHandle);
        return MILESTRO_API_RET_FAILED;
    }

    gr_cp<ID3D12Resource> resourceRef;
    resourceRef.retain(resource);

    gr_cp<ID3D12Device> resourceDevice;
    HRESULT resourceDeviceResult = resource->GetDevice(IID_PPV_ARGS(&resourceDevice));
    if (FAILED(resourceDeviceResult) || resourceDevice.get() == nullptr) {
        MILESTROLOG_ERROR("Failed to read Unity D3D12 RenderTexture resource device: 0x{:08x}.",
                          static_cast<unsigned int>(resourceDeviceResult));
        return MILESTRO_API_RET_FAILED;
    }

    if (resourceDevice.get() != device) {
        MILESTROLOG_ERROR("Unity D3D12 RenderTexture resource device {} does not match Unity device {}.",
                          static_cast<void*>(resourceDevice.get()),
                          static_cast<void*>(device));
        return MILESTRO_API_RET_FAILED;
    }

    MILESTRO_RENDER_LOG_INFO("Milestro D3D12 resource/context identity: event={}, unityDevice={}, resourceDevice={}, queue={}, "
                     "skiaContext={}, contextDevice={}, contextQueue={}, deviceLuid={}, resourceDeviceLuid={}.",
                     renderSerial,
                     static_cast<void*>(device),
                     static_cast<void*>(resourceDevice.get()),
                     static_cast<void*>(gDirectContextQueue),
                     static_cast<void*>(context),
                     static_cast<void*>(gDirectContextDevice),
                     static_cast<void*>(gDirectContextQueue),
                     FormatLuid(device->GetAdapterLuid()),
                     FormatLuid(resourceDevice->GetAdapterLuid()));

    D3D12_RESOURCE_DESC desc = resource->GetDesc();
    DXGI_FORMAT format = NormalizeDxgiFormat(desc.Format, payload.storageSrgb, payload.preferredFormat);
    const uint32_t sampleCount = desc.SampleDesc.Count == 0 ? 1 : desc.SampleDesc.Count;
    const uint32_t levelCount = desc.MipLevels == 0 ? 1 : desc.MipLevels;
    const unsigned int sampleQuality = desc.SampleDesc.Quality;

    MILESTRO_RENDER_LOG_INFO("Milestro D3D12 wrap target: event={}, source={}, resource={}, unityDevice={}, queue={}, "
                     "payload={}x{}, desc={}x{}, "
                     "dimension={}, format={}, normalizedFormat={}, sampleCount={}, sampleQuality={}, mipLevels={}, "
                     "flags=0x{:x}, "
                     "colorSpace={}, storageSrgb={}, preferredFormat={}, renderBufferHandle={}, nativeTextureHandle={}.",
                     renderSerial,
                     TextureSourceName(texture.source),
                     static_cast<void*>(resource),
                     static_cast<void*>(device),
                     static_cast<void*>(gDirectContextQueue),
                     payload.width,
                     payload.height,
                     static_cast<unsigned long long>(desc.Width),
                     desc.Height,
                     static_cast<unsigned int>(desc.Dimension),
                     static_cast<unsigned int>(desc.Format),
                     static_cast<unsigned int>(format),
                     sampleCount,
                     sampleQuality,
                     levelCount,
                     static_cast<unsigned int>(desc.Flags),
                     payload.colorSpace,
                     payload.storageSrgb,
                     payload.preferredFormat,
                     payload.colorRenderBufferHandle,
                     payload.nativeTextureHandle);

    if (desc.Dimension != D3D12_RESOURCE_DIMENSION_TEXTURE2D) {
        MILESTROLOG_ERROR("Milestro D3D12 RenderTexture resource must be TEXTURE2D, got dimension {}.",
                          static_cast<unsigned int>(desc.Dimension));
        return MILESTRO_API_RET_FAILED;
    }

    if ((desc.Flags & D3D12_RESOURCE_FLAG_ALLOW_RENDER_TARGET) == 0) {
        MILESTROLOG_ERROR("Milestro D3D12 RenderTexture resource is missing D3D12_RESOURCE_FLAG_ALLOW_RENDER_TARGET.");
        return MILESTRO_API_RET_FAILED;
    }

    if (sampleCount != 1) {
        MILESTROLOG_ERROR("Milestro D3D12 RenderTexture resource MSAA is not implemented yet: {} samples.",
                          sampleCount);
        return MILESTRO_API_RET_FAILED;
    }

    if (!IsSupportedDxgiFormat(format)) {
        MILESTROLOG_ERROR("Unsupported D3D12 RenderTexture format {} normalized to {}.",
                          static_cast<unsigned int>(desc.Format),
                          static_cast<unsigned int>(format));
        return MILESTRO_API_RET_FAILED;
    }

    if (payload.width != static_cast<int32_t>(desc.Width) || payload.height != static_cast<int32_t>(desc.Height)) {
        MILESTRO_RENDER_LOG_WARN("Milestro D3D12 payload size {}x{} differs from resource desc {}x{}; using resource desc.",
                         payload.width,
                         payload.height,
                         static_cast<unsigned long long>(desc.Width),
                         desc.Height);
    }

    if (kProbeDirectRenderTargetView && !ProbeRenderTargetView(device, resource, renderSerial)) {
        return MILESTRO_API_RET_FAILED;
    }

    const D3D12_RESOURCE_STATES displayState = D3D12_RESOURCE_STATE_PIXEL_SHADER_RESOURCE;
    const D3D12_RESOURCE_STATES skiaRenderState = D3D12_RESOURCE_STATE_RENDER_TARGET;
    RequestResourceState(resource, skiaRenderState);

    const D3D12_RESOURCE_STATES skiaInitialState = kUseUnityStateTrackerInQueueMode ? skiaRenderState : displayState;
    CachedRenderTarget* cached = GetOrCreateCachedRenderTarget(context,
                                                               device,
                                                               gDirectContextQueue,
                                                               resource,
                                                               desc,
                                                               format,
                                                               sampleCount,
                                                               levelCount,
                                                               sampleQuality,
                                                               payload.colorSpace,
                                                               payload.storageSrgb,
                                                               skiaInitialState,
                                                               renderSerial);
    if (cached == nullptr || cached->surface == nullptr) {
        RequestResourceState(resource, displayState);
        NotifyResourceState(resource, displayState);
        return MILESTRO_API_RET_FAILED;
    }

    GrBackendRenderTargets::SetD3DResourceState(&cached->renderTarget,
                                                static_cast<GrD3DResourceStateEnum>(skiaInitialState));
    milestro::unity_render::DrawSubmission(cached->surface->getCanvas(), submission);
    if (kSyncSkiaSubmitForDiagnostics) {
        context->flushAndSubmit(cached->surface.get(), GrSyncCpu::kYes);
    } else {
        context->flushAndSubmit(cached->surface.get());
    }

    GrD3DTextureResourceInfo finalInfo = GrBackendRenderTargets::GetD3DTextureResourceInfo(cached->renderTarget);
    NotifyResourceState(resource, finalInfo.fResourceState);
    if (texture.source == TextureSource::NativeTexture) {
        const auto finalState = static_cast<D3D12_RESOURCE_STATES>(finalInfo.fResourceState);
        if (!TransitionExternalTextureForUnity(device, resource, finalState, displayState, renderSerial)) {
            MILESTROLOG_ERROR("Failed to transition Milestro D3D12 external texture for Unity sampling.");
            return MILESTRO_API_RET_FAILED;
        }
        GrBackendRenderTargets::SetD3DResourceState(&cached->renderTarget,
                                                    static_cast<GrD3DResourceStateEnum>(displayState));
    } else {
        RequestResourceState(resource, displayState);
        GrBackendRenderTargets::SetD3DResourceState(&cached->renderTarget,
                                                    static_cast<GrD3DResourceStateEnum>(displayState));
        NotifyResourceState(resource, displayState);
    }
    RetainResourceUntilFrameFence(resource);
    return MILESTRO_API_RET_OK;
}

} // namespace milestro::unity_render::d3d12
