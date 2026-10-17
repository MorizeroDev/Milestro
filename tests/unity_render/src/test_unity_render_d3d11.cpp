#include "unity_render/MilestroUnityRenderD3D11Backend.h"

#include "game/milestro_game_retcode.h"
#include "unity_render/MilestroUnityGraphicsBackend.h"
#include "unity_render/MilestroUnityRenderTextureHandleKind.h"

#include <Milestro/skia/Image.h>
#include <gtest/gtest.h>

#ifndef NOMINMAX
#define NOMINMAX
#endif

#include <d3d11.h>

#include <IUnityGraphicsD3D11.h>
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

IUnityGraphicsD3D11 gUnityD3D11 = {};
ID3D11Device* gDevice = nullptr;

ID3D11Device* UNITY_INTERFACE_API GetDevice() {
    return gDevice;
}

IUnityInterface* UNITY_INTERFACE_API GetInterface(UnityInterfaceGUID guid) {
    if (guid == GetUnityInterfaceGUID<IUnityGraphicsD3D11>()) {
        return &gUnityD3D11;
    }
    return nullptr;
}

class D3D11BackendGuard {
public:
    D3D11BackendGuard() {
        gUnityD3D11.GetDevice = &GetDevice;
        interfaces_.GetInterface = &GetInterface;
        milestro::unity_render::d3d11::OnGraphicsDeviceEvent(kUnityGfxDeviceEventInitialize,
                                                             &interfaces_,
                                                             kUnityGfxRendererD3D11);
    }

    ~D3D11BackendGuard() {
        milestro::unity_render::d3d11::OnGraphicsDeviceEvent(kUnityGfxDeviceEventShutdown,
                                                             &interfaces_,
                                                             kUnityGfxRendererNull);
        gDevice = nullptr;
        gUnityD3D11 = {};
    }

private:
    IUnityInterfaces interfaces_ = {};
};

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

MilestroUnityRenderSubmission
MakeSubmission(ID3D11Texture2D* texture, int32_t clearBeforeDraw, MilestroUnityDrawCommand* command) {
    MilestroUnityRenderSubmission submission;
    submission.target.graphicsBackend = static_cast<int32_t>(MilestroUnityGraphicsBackend::Direct3D11);
    submission.target.handleKind = static_cast<int32_t>(MilestroUnityRenderTextureHandleKind::NativeTexture);
    submission.target.nativeTextureHandle = texture;
    submission.target.width = 4;
    submission.target.height = 4;
    submission.target.clearBeforeDraw = clearBeforeDraw;
    submission.target.msaaSamples = 1;
    submission.target.effectiveScale = 1.0f;
    submission.commands = command;
    submission.commandCount = command == nullptr ? 0 : 1;
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

std::vector<uint8_t> ReadPixels(ID3D11Device* device, ID3D11Texture2D* texture) {
    D3D11_TEXTURE2D_DESC desc = {};
    texture->GetDesc(&desc);
    desc.Usage = D3D11_USAGE_STAGING;
    desc.BindFlags = 0;
    desc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
    desc.MiscFlags = 0;

    ComPtr<ID3D11Texture2D> staging;
    EXPECT_HRESULT_SUCCEEDED(device->CreateTexture2D(&desc, nullptr, staging.GetAddressOf()));
    if (staging == nullptr) {
        return {};
    }

    ComPtr<ID3D11DeviceContext> context;
    device->GetImmediateContext(context.GetAddressOf());
    context->CopyResource(staging.Get(), texture);
    D3D11_MAPPED_SUBRESOURCE mapped = {};
    EXPECT_HRESULT_SUCCEEDED(context->Map(staging.Get(), 0, D3D11_MAP_READ, 0, &mapped));
    if (mapped.pData == nullptr) {
        return {};
    }

    const std::size_t rowBytes = static_cast<std::size_t>(desc.Width) * 4U;
    std::vector<uint8_t> pixels(rowBytes * desc.Height);
    for (UINT row = 0; row < desc.Height; ++row) {
        const auto* source = static_cast<const uint8_t*>(mapped.pData) + row * mapped.RowPitch;
        std::copy_n(source, rowBytes, pixels.data() + row * rowBytes);
    }
    context->Unmap(staging.Get(), 0);
    return pixels;
}

void ExpectColor(const std::vector<uint8_t>& pixels, int32_t x, std::array<uint8_t, 4> expected) {
    ASSERT_EQ(pixels.size(), 4U * 4U * 4U);
    for (int32_t y = 0; y < 4; ++y) {
        const std::size_t offset = (static_cast<std::size_t>(y) * 4U + x) * 4U;
        for (std::size_t channel = 0; channel < expected.size(); ++channel) {
            EXPECT_EQ(pixels[offset + channel], expected[channel]) << "x=" << x << ", y=" << y;
        }
    }
}

TEST(UnityRenderD3D11, UploadsAndPreservesIncrementalPixels) {
    ComPtr<ID3D11Device> device;
    ComPtr<ID3D11DeviceContext> context;
    UINT flags = D3D11_CREATE_DEVICE_BGRA_SUPPORT;
    HRESULT hr = D3D11CreateDevice(nullptr,
                                   D3D_DRIVER_TYPE_WARP,
                                   nullptr,
                                   flags,
                                   nullptr,
                                   0,
                                   D3D11_SDK_VERSION,
                                   device.GetAddressOf(),
                                   nullptr,
                                   context.GetAddressOf());
    if (FAILED(hr)) {
        GTEST_SKIP() << "WARP D3D11 device is unavailable: " << static_cast<unsigned int>(hr);
    }

    D3D11_TEXTURE2D_DESC desc = {};
    desc.Width = 4;
    desc.Height = 4;
    desc.MipLevels = 1;
    desc.ArraySize = 1;
    desc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
    desc.SampleDesc.Count = 1;
    desc.Usage = D3D11_USAGE_DEFAULT;
    desc.BindFlags = D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE;
    ComPtr<ID3D11Texture2D> texture;
    ASSERT_HRESULT_SUCCEEDED(device->CreateTexture2D(&desc, nullptr, texture.GetAddressOf()));

    gDevice = device.Get();
    D3D11BackendGuard guard;
    SolidImage red = MakeSolidImage(SK_ColorRED);
    SolidImage blue = MakeSolidImage(SK_ColorBLUE);
    ASSERT_NE(red.image, nullptr);
    ASSERT_NE(blue.image, nullptr);

    MilestroUnityDrawCommand leftRed = MakeImageCommand(red.image.get(), 0.0f);
    MilestroUnityDrawCommand rightRed = MakeImageCommand(red.image.get(), 2.0f);
    std::array<MilestroUnityDrawCommand, 2> initialCommands = {leftRed, rightRed};
    MilestroUnityRenderSubmission initial = MakeSubmission(texture.Get(), 1, initialCommands.data());
    initial.commandCount = 2;
    ASSERT_EQ(milestro::unity_render::d3d11::Render(initial), MILESTRO_API_RET_OK);
    std::vector<uint8_t> pixels = ReadPixels(device.Get(), texture.Get());
    ExpectColor(pixels, 0, {0, 0, 255, 255});
    ExpectColor(pixels, 3, {0, 0, 255, 255});

    MilestroUnityDrawCommand rightBlue = MakeImageCommand(blue.image.get(), 2.0f);
    MilestroUnityRenderSubmission incremental = MakeSubmission(texture.Get(), 0, &rightBlue);
    ASSERT_EQ(milestro::unity_render::d3d11::Render(incremental), MILESTRO_API_RET_OK);
    pixels = ReadPixels(device.Get(), texture.Get());
    ExpectColor(pixels, 0, {0, 0, 255, 255});
    ExpectColor(pixels, 1, {0, 0, 255, 255});
    ExpectColor(pixels, 2, {255, 0, 0, 255});
    ExpectColor(pixels, 3, {255, 0, 0, 255});

    ASSERT_EQ(milestro::unity_render::d3d11::DestroyTarget(texture.Get()), MILESTRO_API_RET_OK);
    MilestroUnityRenderSubmission afterDestroy = MakeSubmission(texture.Get(), 0, nullptr);
    ASSERT_EQ(milestro::unity_render::d3d11::Render(afterDestroy), MILESTRO_API_RET_OK);
    pixels = ReadPixels(device.Get(), texture.Get());
    ExpectColor(pixels, 0, {0, 0, 0, 0});
    ExpectColor(pixels, 3, {0, 0, 0, 0});
}

} // namespace
