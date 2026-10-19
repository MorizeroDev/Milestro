#include "unity_render/MilestroUnityRenderD3D12Backend.h"

#include "game/milestro_game_retcode.h"
#include "unity_render/MilestroUnityGraphicsBackend.h"
#include "unity_render/MilestroUnityRenderTextureHandleKind.h"

#include <Milestro/skia/Image.h>
#include <gtest/gtest.h>

#ifndef NOMINMAX
#define NOMINMAX
#endif
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif

#include <d3d12.h>
#include <dxgi1_4.h>

#include <IUnityGraphicsD3D12.h>
#include <wrl/client.h>

#include <algorithm>
#include <array>
#include <cstdint>
#include <memory>
#include <vector>

#include "include/core/SkBitmap.h"
#include "include/core/SkColor.h"
#include "include/core/SkData.h"
#include "include/core/SkImageInfo.h"
#include "include/encode/SkPngEncoder.h"

namespace {

using Microsoft::WRL::ComPtr;

IUnityGraphicsD3D12v8 gUnityD3D12v8 = {};
ID3D12Device* gDevice = nullptr;
ID3D12CommandQueue* gQueue = nullptr;
ID3D12Fence* gFrameFence = nullptr;
UINT64 gNextFrameFenceValue = 1;
bool gConfigureEventCalled = false;

ID3D12Device* UNITY_INTERFACE_API GetDevice() {
    return gDevice;
}

IDXGISwapChain* UNITY_INTERFACE_API GetSwapChain() {
    return nullptr;
}

UINT32 UNITY_INTERFACE_API GetSyncInterval() {
    return 0;
}

UINT UNITY_INTERFACE_API GetPresentFlags() {
    return 0;
}

ID3D12Fence* UNITY_INTERFACE_API GetFrameFence() {
    return gFrameFence;
}

UINT64 UNITY_INTERFACE_API GetNextFrameFenceValue() {
    return gNextFrameFenceValue;
}

UINT64 UNITY_INTERFACE_API ExecuteCommandList(ID3D12GraphicsCommandList* commandList,
                                              int stateCount,
                                              UnityGraphicsD3D12ResourceState* states) {
    (void) stateCount;
    (void) states;
    if (commandList == nullptr || gQueue == nullptr) {
        return 0;
    }
    ID3D12CommandList* lists[] = {commandList};
    gQueue->ExecuteCommandLists(1, lists);
    return gNextFrameFenceValue;
}

void UNITY_INTERFACE_API
SetPhysicalVideoMemoryControlValues(const UnityGraphicsD3D12PhysicalVideoMemoryControlValues* memInfo) {
    (void) memInfo;
}

ID3D12CommandQueue* UNITY_INTERFACE_API GetCommandQueue() {
    return gQueue;
}

ID3D12Resource* UNITY_INTERFACE_API TextureFromRenderBuffer(UnityRenderBuffer renderBuffer) {
    (void) renderBuffer;
    return nullptr;
}

ID3D12Resource* UNITY_INTERFACE_API TextureFromNativeTexture(UnityTextureID texture) {
    (void) texture;
    return nullptr;
}

void UNITY_INTERFACE_API ConfigureEvent(int eventID, const UnityD3D12PluginEventConfig* pluginEventConfig) {
    (void) eventID;
    (void) pluginEventConfig;
    gConfigureEventCalled = true;
}

bool UNITY_INTERFACE_API GetCommandRecordingState(UnityGraphicsD3D12RecordingState* outCommandRecordingState) {
    (void) outCommandRecordingState;
    return false;
}

void UNITY_INTERFACE_API RequestResourceState(ID3D12Resource* resource, D3D12_RESOURCE_STATES state) {
    (void) resource;
    (void) state;
}

void UNITY_INTERFACE_API NotifyResourceState(ID3D12Resource* resource, D3D12_RESOURCE_STATES state, bool UAVAccess) {
    (void) resource;
    (void) state;
    (void) UAVAccess;
}

IUnityInterface* UNITY_INTERFACE_API GetInterface(UnityInterfaceGUID guid) {
    if (guid == GetUnityInterfaceGUID<IUnityGraphicsD3D12v8>()) {
        return &gUnityD3D12v8;
    }
    return nullptr;
}

class D3D12BackendGuard {
public:
    D3D12BackendGuard() {
        gUnityD3D12v8.GetDevice = &GetDevice;
        gUnityD3D12v8.GetSwapChain = &GetSwapChain;
        gUnityD3D12v8.GetSyncInterval = &GetSyncInterval;
        gUnityD3D12v8.GetPresentFlags = &GetPresentFlags;
        gUnityD3D12v8.GetFrameFence = &GetFrameFence;
        gUnityD3D12v8.GetNextFrameFenceValue = &GetNextFrameFenceValue;
        gUnityD3D12v8.ExecuteCommandList = &ExecuteCommandList;
        gUnityD3D12v8.SetPhysicalVideoMemoryControlValues = &SetPhysicalVideoMemoryControlValues;
        gUnityD3D12v8.GetCommandQueue = &GetCommandQueue;
        gUnityD3D12v8.TextureFromRenderBuffer = &TextureFromRenderBuffer;
        gUnityD3D12v8.TextureFromNativeTexture = &TextureFromNativeTexture;
        gUnityD3D12v8.ConfigureEvent = &ConfigureEvent;
        gUnityD3D12v8.CommandRecordingState = &GetCommandRecordingState;
        gUnityD3D12v8.RequestResourceState = &RequestResourceState;
        gUnityD3D12v8.NotifyResourceState = &NotifyResourceState;
        interfaces_.GetInterface = &GetInterface;
        milestro::unity_render::d3d12::OnGraphicsDeviceEvent(kUnityGfxDeviceEventInitialize,
                                                             &interfaces_,
                                                             kUnityGfxRendererD3D12,
                                                             1);
    }

    ~D3D12BackendGuard() {
        milestro::unity_render::d3d12::OnGraphicsDeviceEvent(kUnityGfxDeviceEventShutdown,
                                                             &interfaces_,
                                                             kUnityGfxRendererD3D12,
                                                             -1);
        gDevice = nullptr;
        gQueue = nullptr;
        gFrameFence = nullptr;
        gUnityD3D12v8 = {};
    }

private:
    IUnityInterfaces interfaces_ = {};
};

struct D3D12TestRig {
    ComPtr<ID3D12Device> device;
    ComPtr<ID3D12CommandQueue> queue;
    ComPtr<ID3D12Fence> fence;
    HANDLE fenceEvent = nullptr;
};

bool CreateRig(D3D12TestRig& rig) {
    ComPtr<IDXGIFactory4> factory;
    if (FAILED(CreateDXGIFactory1(IID_PPV_ARGS(&factory)))) {
        return false;
    }

    ComPtr<IDXGIAdapter1> adapter;
    for (UINT index = 0; factory->EnumAdapters1(index, adapter.ReleaseAndGetAddressOf()) != DXGI_ERROR_NOT_FOUND;
         ++index) {
        if (SUCCEEDED(D3D12CreateDevice(adapter.Get(), D3D_FEATURE_LEVEL_11_0, IID_PPV_ARGS(&rig.device)))) {
            break;
        }
    }
    if (rig.device == nullptr) {
        return false;
    }

    D3D12_COMMAND_QUEUE_DESC queueDesc = {};
    queueDesc.Type = D3D12_COMMAND_LIST_TYPE_DIRECT;
    if (FAILED(rig.device->CreateCommandQueue(&queueDesc, IID_PPV_ARGS(&rig.queue)))) {
        return false;
    }
    if (FAILED(rig.device->CreateFence(0, D3D12_FENCE_FLAG_NONE, IID_PPV_ARGS(&rig.fence)))) {
        return false;
    }
    rig.fenceEvent = CreateEvent(nullptr, FALSE, FALSE, nullptr);
    return rig.fenceEvent != nullptr;
}

void SignalFrameFence(D3D12TestRig& rig) {
    rig.queue->Signal(rig.fence.Get(), gNextFrameFenceValue);
    ++gNextFrameFenceValue;
}

void WaitForGpu(D3D12TestRig& rig) {
    const UINT64 value = gNextFrameFenceValue;
    rig.queue->Signal(rig.fence.Get(), value);
    ++gNextFrameFenceValue;
    if (rig.fence->GetCompletedValue() < value) {
        rig.fence->SetEventOnCompletion(value, rig.fenceEvent);
        WaitForSingleObject(rig.fenceEvent, INFINITE);
    }
}

struct SolidImage {
    sk_sp<SkData> encoded;
    std::unique_ptr<milestro::skia::Image> image;
};

SolidImage MakeSolidImage(SkColor color) {
    SkBitmap bitmap;
    bitmap.allocPixels(SkImageInfo::MakeN32Premul(1, 1));
    bitmap.eraseColor(color);
    SolidImage result;
    result.encoded = SkPngEncoder::Encode(bitmap.pixmap(), SkPngEncoder::Options{});
    EXPECT_NE(result.encoded, nullptr);
    if (result.encoded != nullptr) {
        result.image = std::make_unique<milestro::skia::Image>(const_cast<void*>(result.encoded->data()),
                                                               result.encoded->size());
    }
    return result;
}

MilestroUnityRenderSubmission MakeSubmission(ID3D12Resource* texture,
                                             int32_t clearBeforeDraw,
                                             MilestroUnityDrawCommand* commands,
                                             int32_t commandCount) {
    MilestroUnityRenderSubmission submission;
    submission.target.graphicsBackend = static_cast<int32_t>(MilestroUnityGraphicsBackend::Direct3D12);
    submission.target.handleKind = static_cast<int32_t>(MilestroUnityRenderTextureHandleKind::NativeTexture);
    submission.target.nativeTextureHandle = texture;
    submission.target.width = 4;
    submission.target.height = 4;
    submission.target.clearBeforeDraw = clearBeforeDraw;
    submission.target.msaaSamples = 1;
    submission.target.effectiveScale = 1.0f;
    submission.commands = commands;
    submission.commandCount = commandCount;
    return submission;
}

MilestroUnityDrawCommand MakeImageCommand(milestro::skia::Image* image, float x) {
    MilestroUnityDrawCommand command;
    command.kind = static_cast<int32_t>(MilestroUnityDrawCommandKind::Image);
    command.resource = image;
    command.x = x;
    command.y = 0.0f;
    command.width = 2.0f;
    command.height = 4.0f;
    return command;
}

std::vector<uint8_t> ReadPixels(D3D12TestRig& rig, ID3D12Resource* texture) {
    D3D12_RESOURCE_DESC desc = texture->GetDesc();
    D3D12_PLACED_SUBRESOURCE_FOOTPRINT footprint = {};
    UINT64 totalBytes = 0;
    rig.device->GetCopyableFootprints(&desc, 0, 1, 0, &footprint, nullptr, nullptr, &totalBytes);

    D3D12_HEAP_PROPERTIES heapProps = {};
    heapProps.Type = D3D12_HEAP_TYPE_READBACK;
    D3D12_RESOURCE_DESC bufferDesc = {};
    bufferDesc.Dimension = D3D12_RESOURCE_DIMENSION_BUFFER;
    bufferDesc.Width = totalBytes;
    bufferDesc.Height = 1;
    bufferDesc.DepthOrArraySize = 1;
    bufferDesc.MipLevels = 1;
    bufferDesc.Format = DXGI_FORMAT_UNKNOWN;
    bufferDesc.SampleDesc.Count = 1;
    bufferDesc.Layout = D3D12_TEXTURE_LAYOUT_ROW_MAJOR;
    ComPtr<ID3D12Resource> readback;
    if (FAILED(rig.device->CreateCommittedResource(&heapProps,
                                                   D3D12_HEAP_FLAG_NONE,
                                                   &bufferDesc,
                                                   D3D12_RESOURCE_STATE_COPY_DEST,
                                                   nullptr,
                                                   IID_PPV_ARGS(&readback)))) {
        return {};
    }

    ComPtr<ID3D12CommandAllocator> allocator;
    ComPtr<ID3D12GraphicsCommandList> commandList;
    if (FAILED(rig.device->CreateCommandAllocator(D3D12_COMMAND_LIST_TYPE_DIRECT, IID_PPV_ARGS(&allocator))) ||
        FAILED(rig.device->CreateCommandList(0,
                                             D3D12_COMMAND_LIST_TYPE_DIRECT,
                                             allocator.Get(),
                                             nullptr,
                                             IID_PPV_ARGS(&commandList)))) {
        return {};
    }

    D3D12_RESOURCE_BARRIER barrier = {};
    barrier.Type = D3D12_RESOURCE_BARRIER_TYPE_TRANSITION;
    barrier.Transition.pResource = texture;
    barrier.Transition.Subresource = D3D12_RESOURCE_BARRIER_ALL_SUBRESOURCES;
    barrier.Transition.StateBefore = D3D12_RESOURCE_STATE_PIXEL_SHADER_RESOURCE;
    barrier.Transition.StateAfter = D3D12_RESOURCE_STATE_COPY_SOURCE;
    commandList->ResourceBarrier(1, &barrier);

    D3D12_TEXTURE_COPY_LOCATION destination = {};
    destination.pResource = readback.Get();
    destination.Type = D3D12_TEXTURE_COPY_TYPE_PLACED_FOOTPRINT;
    destination.PlacedFootprint = footprint;
    D3D12_TEXTURE_COPY_LOCATION source = {};
    source.pResource = texture;
    source.Type = D3D12_TEXTURE_COPY_TYPE_SUBRESOURCE_INDEX;
    source.SubresourceIndex = 0;
    commandList->CopyTextureRegion(&destination, 0, 0, 0, &source, nullptr);
    if (FAILED(commandList->Close())) {
        return {};
    }

    ID3D12CommandList* lists[] = {commandList.Get()};
    rig.queue->ExecuteCommandLists(1, lists);
    WaitForGpu(rig);

    void* mapped = nullptr;
    D3D12_RANGE range = {0, static_cast<SIZE_T>(totalBytes)};
    if (FAILED(readback->Map(0, &range, &mapped)) || mapped == nullptr) {
        return {};
    }

    const std::size_t rowBytes = static_cast<std::size_t>(desc.Width) * 4U;
    std::vector<uint8_t> pixels(rowBytes * desc.Height);
    for (UINT row = 0; row < desc.Height; ++row) {
        const auto* sourceRow = static_cast<const uint8_t*>(mapped) + row * footprint.Footprint.RowPitch;
        std::copy_n(sourceRow, rowBytes, pixels.data() + row * rowBytes);
    }
    readback->Unmap(0, nullptr);
    return pixels;
}

void ExpectColor(const std::vector<uint8_t>& pixels, int32_t x, std::array<uint8_t, 4> expected) {
    ASSERT_EQ(pixels.size(), 4U * 4U * 4U);
    for (int32_t y = 0; y < 4; ++y) {
        const std::size_t offset = (static_cast<std::size_t>(y) * 4U + static_cast<std::size_t>(x)) * 4U;
        for (std::size_t channel = 0; channel < expected.size(); ++channel) {
            EXPECT_EQ(pixels[offset + channel], expected[channel]) << "x=" << x << ", y=" << y;
        }
    }
}

TEST(UnityRenderD3D12, AsyncSubmitRendersAndPreservesIncrementalPixels) {
    D3D12TestRig rig;
    if (!CreateRig(rig)) {
        GTEST_SKIP() << "No D3D12-capable adapter is available.";
    }

    gDevice = rig.device.Get();
    gQueue = rig.queue.Get();
    gFrameFence = rig.fence.Get();
    gNextFrameFenceValue = 1;
    gConfigureEventCalled = false;

    D3D12BackendGuard guard;
    EXPECT_TRUE(gConfigureEventCalled);

    void* textureHandle = nullptr;
    ASSERT_EQ(milestro::unity_render::d3d12::CreateExternalTexture(4, 4, 0, 0, textureHandle), MILESTRO_API_RET_OK);
    ASSERT_NE(textureHandle, nullptr);
    auto* texture = static_cast<ID3D12Resource*>(textureHandle);

    SolidImage red = MakeSolidImage(SK_ColorRED);
    SolidImage blue = MakeSolidImage(SK_ColorBLUE);
    ASSERT_NE(red.image, nullptr);
    ASSERT_NE(blue.image, nullptr);

    MilestroUnityDrawCommand leftRed = MakeImageCommand(red.image.get(), 0.0f);
    MilestroUnityDrawCommand rightRed = MakeImageCommand(red.image.get(), 2.0f);
    std::array<MilestroUnityDrawCommand, 2> initialCommands = {leftRed, rightRed};
    MilestroUnityRenderSubmission initial = MakeSubmission(texture, 1, initialCommands.data(), 2);
    ASSERT_EQ(milestro::unity_render::d3d12::Render(initial), MILESTRO_API_RET_OK);

    SignalFrameFence(rig);
    WaitForGpu(rig);
    std::vector<uint8_t> pixels = ReadPixels(rig, texture);
    ExpectColor(pixels, 0, {0, 0, 255, 255});
    ExpectColor(pixels, 3, {0, 0, 255, 255});

    MilestroUnityDrawCommand rightBlue = MakeImageCommand(blue.image.get(), 2.0f);
    MilestroUnityRenderSubmission incremental = MakeSubmission(texture, 0, &rightBlue, 1);
    ASSERT_EQ(milestro::unity_render::d3d12::Render(incremental), MILESTRO_API_RET_OK);

    SignalFrameFence(rig);
    WaitForGpu(rig);
    pixels = ReadPixels(rig, texture);
    ExpectColor(pixels, 0, {0, 0, 255, 255});
    ExpectColor(pixels, 1, {0, 0, 255, 255});
    ExpectColor(pixels, 2, {255, 0, 0, 255});
    ExpectColor(pixels, 3, {255, 0, 0, 255});

    ASSERT_EQ(milestro::unity_render::d3d12::DestroyExternalTexture(textureHandle), MILESTRO_API_RET_OK);
    EXPECT_EQ(textureHandle, nullptr);
    SignalFrameFence(rig);
    WaitForGpu(rig);

    CloseHandle(rig.fenceEvent);
}

} // namespace
