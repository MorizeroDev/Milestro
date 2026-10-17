#include "unity_render/MilestroUnityRenderD3D11Backend.h"

#include "game/milestro_game_retcode.h"
#include "unity_render/MilestroUnityRenderLog.h"
#include "unity_render/MilestroUnityRenderSubmissionDraw.h"
#include "unity_render/MilestroUnityRenderTextureHandleKind.h"

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#ifndef NOMINMAX
#define NOMINMAX
#endif

#include <d3d11.h>

#include <IUnityGraphicsD3D11.h>
#include <wrl/client.h>

#include <algorithm>
#include <cstdint>
#include <limits>
#include <utility>
#include <vector>

#include "include/core/SkCanvas.h"
#include "include/core/SkColor.h"
#include "include/core/SkColorSpace.h"
#include "include/core/SkImageInfo.h"
#include "include/core/SkPixmap.h"
#include "include/core/SkSurface.h"

namespace milestro::unity_render::d3d11 {

namespace {

using Microsoft::WRL::ComPtr;

struct CachedRasterTarget {
    ComPtr<ID3D11Device> device;
    ComPtr<ID3D11Texture2D> texture;
    sk_sp<SkSurface> surface;
    DXGI_FORMAT format = DXGI_FORMAT_UNKNOWN;
    int32_t colorSpace = 0;
    int32_t storageSrgb = 0;
};

IUnityGraphicsD3D11* gD3D11 = nullptr;
ComPtr<ID3D11Device> gUnityDevice;
std::vector<CachedRasterTarget> gCachedRasterTargets;

void ResetGraphicsState() {
    gCachedRasterTargets.clear();
    gUnityDevice.Reset();
    gD3D11 = nullptr;
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
        case DXGI_FORMAT_R8G8B8A8_UNORM:
        case DXGI_FORMAT_R8G8B8A8_UNORM_SRGB:
            return kRGBA_8888_SkColorType;
        case DXGI_FORMAT_B8G8R8A8_UNORM:
        case DXGI_FORMAT_B8G8R8A8_UNORM_SRGB:
        default:
            return kBGRA_8888_SkColorType;
    }
}

sk_sp<SkColorSpace> ColorSpaceForFormat(DXGI_FORMAT format, int32_t colorSpace) {
    if (format == DXGI_FORMAT_R8G8B8A8_UNORM_SRGB || format == DXGI_FORMAT_B8G8R8A8_UNORM_SRGB) {
        return SkColorSpace::MakeSRGB();
    }
    return colorSpace == 1 ? SkColorSpace::MakeSRGBLinear() : SkColorSpace::MakeSRGB();
}

ID3D11Resource* ResourceFromPayload(const MilestroUnityRenderTargetPayload& payload) {
    if (payload.handleKind == static_cast<int32_t>(MilestroUnityRenderTextureHandleKind::RenderBuffer)) {
        if (gD3D11 == nullptr || payload.colorRenderBufferHandle == nullptr) {
            return nullptr;
        }
        return gD3D11->TextureFromRenderBuffer(reinterpret_cast<UnityRenderBuffer>(payload.colorRenderBufferHandle));
    }
    if (payload.handleKind == static_cast<int32_t>(MilestroUnityRenderTextureHandleKind::NativeTexture)) {
        return static_cast<ID3D11Resource*>(payload.nativeTextureHandle);
    }
    return nullptr;
}

void ClearCachedRasterTarget(ID3D11Texture2D* texture) {
    if (texture == nullptr) {
        return;
    }
    gCachedRasterTargets.erase(std::remove_if(gCachedRasterTargets.begin(),
                                              gCachedRasterTargets.end(),
                                              [texture](const CachedRasterTarget& cached) {
                                                  return cached.texture.Get() == texture;
                                              }),
                               gCachedRasterTargets.end());
}

CachedRasterTarget* GetOrCreateRasterTarget(ID3D11Device* device,
                                            ID3D11Texture2D* texture,
                                            const D3D11_TEXTURE2D_DESC& desc,
                                            DXGI_FORMAT format,
                                            int32_t colorSpace,
                                            int32_t storageSrgb) {
    for (CachedRasterTarget& cached: gCachedRasterTargets) {
        if (cached.device.Get() == device && cached.texture.Get() == texture && cached.format == format &&
            cached.colorSpace == colorSpace && cached.storageSrgb == storageSrgb) {
            return &cached;
        }
    }

    ClearCachedRasterTarget(texture);
    const SkImageInfo imageInfo = SkImageInfo::Make(static_cast<int32_t>(desc.Width),
                                                    static_cast<int32_t>(desc.Height),
                                                    ColorTypeForFormat(format),
                                                    kPremul_SkAlphaType,
                                                    ColorSpaceForFormat(format, colorSpace));
    CachedRasterTarget created;
    created.device = device;
    created.texture = texture;
    created.format = format;
    created.colorSpace = colorSpace;
    created.storageSrgb = storageSrgb;
    created.surface = SkSurfaces::Raster(imageInfo);
    if (created.surface == nullptr) {
        MILESTROLOG_ERROR("Failed to create Milestro D3D11 CPU raster surface {}x{}.", desc.Width, desc.Height);
        return nullptr;
    }
    created.surface->getCanvas()->clear(SK_ColorTRANSPARENT);
    gCachedRasterTargets.push_back(std::move(created));
    return &gCachedRasterTargets.back();
}

} // namespace

void OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType,
                           IUnityInterfaces* unityInterfaces,
                           UnityGfxRenderer renderer) {
    if (eventType == kUnityGfxDeviceEventShutdown || eventType == kUnityGfxDeviceEventBeforeReset ||
        renderer != kUnityGfxRendererD3D11) {
        ResetGraphicsState();
        return;
    }

    if ((eventType != kUnityGfxDeviceEventInitialize && eventType != kUnityGfxDeviceEventAfterReset) ||
        unityInterfaces == nullptr) {
        return;
    }

    ResetGraphicsState();
    gD3D11 = unityInterfaces->Get<IUnityGraphicsD3D11>();
    if (gD3D11 == nullptr) {
        MILESTROLOG_ERROR("Unity D3D11 graphics interface is unavailable.");
        return;
    }

    gUnityDevice = gD3D11->GetDevice();
    if (gUnityDevice == nullptr) {
        MILESTROLOG_ERROR("Unity D3D11 device is unavailable during graphics initialization.");
        gD3D11 = nullptr;
    }
}

int64_t DestroyTarget(void* nativeTexture) {
    ClearCachedRasterTarget(static_cast<ID3D11Texture2D*>(nativeTexture));
    return MILESTRO_API_RET_OK;
}

int64_t Render(const MilestroUnityRenderSubmission& submission) {
    const MilestroUnityRenderTargetPayload& payload = submission.target;
    if (payload.width <= 0 || payload.height <= 0 || payload.msaaSamples != 1) {
        MILESTROLOG_ERROR("Invalid Milestro D3D11 render target size or MSAA configuration.");
        return MILESTRO_API_RET_FAILED;
    }
    if (gD3D11 == nullptr || gUnityDevice == nullptr) {
        MILESTROLOG_ERROR("Unity D3D11 device is unavailable while rendering.");
        return MILESTRO_API_RET_FAILED;
    }

    ID3D11Resource* resource = ResourceFromPayload(payload);
    if (resource == nullptr) {
        MILESTROLOG_ERROR("Milestro D3D11 render payload has no usable texture handle.");
        return MILESTRO_API_RET_FAILED;
    }

    ComPtr<ID3D11Texture2D> texture;
    HRESULT hr = resource->QueryInterface(IID_PPV_ARGS(texture.GetAddressOf()));
    if (FAILED(hr) || texture == nullptr) {
        MILESTROLOG_ERROR("Milestro D3D11 RenderTexture resource must be a Texture2D.");
        return MILESTRO_API_RET_FAILED;
    }

    ComPtr<ID3D11Device> resourceDevice;
    texture->GetDevice(resourceDevice.GetAddressOf());
    if (resourceDevice.Get() != gUnityDevice.Get()) {
        MILESTROLOG_ERROR("Milestro D3D11 RenderTexture belongs to a different device.");
        return MILESTRO_API_RET_FAILED;
    }

    D3D11_TEXTURE2D_DESC desc = {};
    texture->GetDesc(&desc);
    const DXGI_FORMAT format = NormalizeDxgiFormat(desc.Format, payload.storageSrgb, payload.preferredFormat);
    if (desc.Width == 0 || desc.Height == 0 || desc.Width > static_cast<UINT>(std::numeric_limits<int32_t>::max()) ||
        desc.Height > static_cast<UINT>(std::numeric_limits<int32_t>::max()) || desc.ArraySize != 1 ||
        desc.MipLevels == 0 || desc.SampleDesc.Count != 1 || desc.Usage != D3D11_USAGE_DEFAULT ||
        !IsSupportedDxgiFormat(format)) {
        MILESTROLOG_ERROR("Unsupported Milestro D3D11 RenderTexture desc: size={}x{}, array={}, mips={}, samples={}, "
                          "usage={}, format={}, normalizedFormat={}.",
                          desc.Width,
                          desc.Height,
                          desc.ArraySize,
                          desc.MipLevels,
                          desc.SampleDesc.Count,
                          static_cast<unsigned int>(desc.Usage),
                          static_cast<unsigned int>(desc.Format),
                          static_cast<unsigned int>(format));
        return MILESTRO_API_RET_FAILED;
    }

    if (payload.width != static_cast<int32_t>(desc.Width) || payload.height != static_cast<int32_t>(desc.Height)) {
        MILESTRO_RENDER_LOG_WARN(
                "Milestro D3D11 payload size {}x{} differs from resource desc {}x{}; using resource desc.",
                payload.width,
                payload.height,
                desc.Width,
                desc.Height);
    }

    CachedRasterTarget* cached = GetOrCreateRasterTarget(gUnityDevice.Get(),
                                                         texture.Get(),
                                                         desc,
                                                         format,
                                                         payload.colorSpace,
                                                         payload.storageSrgb);
    if (cached == nullptr) {
        return MILESTRO_API_RET_FAILED;
    }

    DrawSubmission(cached->surface->getCanvas(), submission);
    SkPixmap pixels;
    if (!cached->surface->peekPixels(&pixels) || pixels.addr() == nullptr || pixels.rowBytes() == 0 ||
        pixels.rowBytes() > std::numeric_limits<UINT>::max()) {
        MILESTROLOG_ERROR("Failed to access Milestro D3D11 CPU raster pixels.");
        return MILESTRO_API_RET_FAILED;
    }

    ComPtr<ID3D11DeviceContext> context;
    gUnityDevice->GetImmediateContext(context.GetAddressOf());
    if (context == nullptr) {
        MILESTROLOG_ERROR("Unity D3D11 immediate device context is unavailable.");
        return MILESTRO_API_RET_FAILED;
    }
    context->UpdateSubresource(texture.Get(), 0, nullptr, pixels.addr(), static_cast<UINT>(pixels.rowBytes()), 0);
    return MILESTRO_API_RET_OK;
}

} // namespace milestro::unity_render::d3d11
