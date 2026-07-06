#include "unity_render/MilestroUnityRenderMetalBackend.h"

#include "game/milestro_game_retcode.h"
#include "unity_render/MilestroUnityRenderSubmissionDraw.h"
#include "unity_render/MilestroUnityRenderTextureHandleKind.h"

#include <IUnityGraphicsMetal.h>
#include <Milestro/log/log.h>

#import <Metal/Metal.h>

#include "include/core/SkCanvas.h"
#include "include/core/SkColorSpace.h"
#include "include/core/SkSurface.h"
#include "include/gpu/ganesh/GrBackendSurface.h"
#include "include/gpu/ganesh/GrDirectContext.h"
#include "include/gpu/ganesh/SkSurfaceGanesh.h"
#include "include/gpu/ganesh/mtl/GrMtlBackendContext.h"
#include "include/gpu/ganesh/mtl/GrMtlBackendSurface.h"
#include "include/gpu/ganesh/mtl/GrMtlDirectContext.h"
#include "include/gpu/ganesh/mtl/GrMtlTypes.h"

namespace milestro::unity_render::metal {

namespace {

IUnityGraphicsMetalV2* gMetalV2 = nullptr;
IUnityGraphicsMetalV1* gMetalV1 = nullptr;
sk_sp<GrDirectContext> gDirectContext;
id<MTLCommandQueue> gDirectContextQueue = nil;
bool gLoggedMissingCommandBufferSkip = false;

constexpr int32_t kUnityColorSpaceLinear = 1;

id<MTLDevice> MetalDevice() {
    if (gMetalV2 != nullptr) {
        return gMetalV2->MetalDevice();
    }
    if (gMetalV1 != nullptr) {
        return gMetalV1->MetalDevice();
    }
    return nil;
}

id<MTLCommandQueue> CommandQueue() {
    if (gMetalV2 != nullptr) {
        return gMetalV2->CommandQueue();
    }

    id<MTLCommandBuffer> commandBuffer = nil;
    if (gMetalV1 != nullptr) {
        commandBuffer = gMetalV1->CurrentCommandBuffer();
    }
    return commandBuffer != nil ? commandBuffer.commandQueue : nil;
}

id<MTLCommandBuffer> CurrentCommandBuffer() {
    if (gMetalV2 != nullptr) {
        return gMetalV2->CurrentCommandBuffer();
    }
    if (gMetalV1 != nullptr) {
        return gMetalV1->CurrentCommandBuffer();
    }
    return nil;
}

MTLRenderPassDescriptor* CurrentRenderPassDescriptor() {
    if (gMetalV2 != nullptr) {
        return gMetalV2->CurrentRenderPassDescriptor();
    }
    if (gMetalV1 != nullptr) {
        return gMetalV1->CurrentRenderPassDescriptor();
    }
    return nil;
}

bool RequiresUnityRenderBufferLookup(const MilestroUnityRenderTargetPayload& payload) {
    return payload.handleKind == static_cast<int32_t>(MilestroUnityRenderTextureHandleKind::RenderBuffer);
}

id<MTLTexture> TextureFromRenderBuffer(void* renderBufferHandle) {
    if (renderBufferHandle == nullptr) {
        return nil;
    }

    if (gMetalV2 != nullptr) {
        UnityRenderBuffer renderBuffer = gMetalV2->RenderBufferFromHandle(renderBufferHandle);
        if (renderBuffer == nullptr) {
            MILESTROLOG_WARN("Unity Metal V2 returned a null render buffer for handle {}.", renderBufferHandle);
            return nil;
        }
        return gMetalV2->TextureFromRenderBuffer(renderBuffer);
    }
    if (gMetalV1 != nullptr) {
        UnityRenderBuffer renderBuffer = gMetalV1->RenderBufferFromHandle(renderBufferHandle);
        if (renderBuffer == nullptr) {
            MILESTROLOG_WARN("Unity Metal V1 returned a null render buffer for handle {}.", renderBufferHandle);
            return nil;
        }
        return gMetalV1->TextureFromRenderBuffer(renderBuffer);
    }
    return nil;
}

id<MTLTexture> TextureFromNativeTexture(void* nativeTextureHandle) {
    if (nativeTextureHandle == nullptr) {
        return nil;
    }

    return (__bridge id<MTLTexture>) nativeTextureHandle;
}

id<MTLTexture> TextureFromPayload(const MilestroUnityRenderTargetPayload& payload) {
    if (payload.handleKind == static_cast<int32_t>(MilestroUnityRenderTextureHandleKind::RenderBuffer)) {
        return TextureFromRenderBuffer(payload.colorRenderBufferHandle);
    }
    if (payload.handleKind == static_cast<int32_t>(MilestroUnityRenderTextureHandleKind::NativeTexture)) {
        return TextureFromNativeTexture(payload.nativeTextureHandle);
    }

    return nil;
}

void EndCurrentCommandEncoder() {
    if (gMetalV2 != nullptr) {
        gMetalV2->EndCurrentCommandEncoder();
        return;
    }
    if (gMetalV1 != nullptr) {
        gMetalV1->EndCurrentCommandEncoder();
    }
}

void CommitCurrentCommandBufferIfAvailable() {
    if (gMetalV2 != nullptr) {
        gMetalV2->CommitCurrentCommandBuffer();
    }
}

GrDirectContext* DirectContext() {
    id<MTLDevice> device = MetalDevice();
    id<MTLCommandQueue> queue = CommandQueue();
    if (device == nil || queue == nil) {
        MILESTROLOG_ERROR("Unity Metal device or command queue is unavailable.");
        return nullptr;
    }

    if (gDirectContext != nullptr && gDirectContextQueue == queue) {
        return gDirectContext.get();
    }

    GrMtlBackendContext backendContext;
    backendContext.fDevice.retain((__bridge GrMTLHandle) device);
    backendContext.fQueue.retain((__bridge GrMTLHandle) queue);

    gDirectContext = GrDirectContexts::MakeMetal(backendContext);
    gDirectContextQueue = queue;
    if (gDirectContext == nullptr) {
        MILESTROLOG_ERROR("Failed to create Skia Metal direct context.");
    }
    return gDirectContext.get();
}

SkColorType ColorTypeForTexture(id<MTLTexture> texture) {
    switch (texture.pixelFormat) {
        case MTLPixelFormatBGRA8Unorm:
        case MTLPixelFormatBGRA8Unorm_sRGB:
            return kBGRA_8888_SkColorType;
        case MTLPixelFormatRGBA8Unorm:
        case MTLPixelFormatRGBA8Unorm_sRGB:
            return kRGBA_8888_SkColorType;
        default:
            MILESTROLOG_WARN("Unexpected Metal texture format {}; trying BGRA_8888.",
                             static_cast<unsigned int>(texture.pixelFormat));
            return kBGRA_8888_SkColorType;
    }
}

sk_sp<SkColorSpace> ColorSpaceForTexture(id<MTLTexture> texture, int32_t colorSpace) {
    if (texture.pixelFormat == MTLPixelFormatBGRA8Unorm_sRGB || texture.pixelFormat == MTLPixelFormatRGBA8Unorm_sRGB) {
        return SkColorSpace::MakeSRGB();
    }
    if (colorSpace == kUnityColorSpaceLinear) {
        return SkColorSpace::MakeSRGBLinear();
    }
    return SkColorSpace::MakeSRGB();
}

} // namespace

void OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType,
                           IUnityInterfaces* unityInterfaces,
                           UnityGfxRenderer renderer) {
    if (eventType == kUnityGfxDeviceEventShutdown || renderer != kUnityGfxRendererMetal) {
        gDirectContext.reset();
        gDirectContextQueue = nil;
        gMetalV2 = nullptr;
        gMetalV1 = nullptr;
        return;
    }

    if (eventType != kUnityGfxDeviceEventInitialize || unityInterfaces == nullptr) {
        return;
    }

    gMetalV2 = unityInterfaces->Get<IUnityGraphicsMetalV2>();
    if (gMetalV2 == nullptr) {
        gMetalV1 = unityInterfaces->Get<IUnityGraphicsMetalV1>();
    }

    if (gMetalV2 == nullptr && gMetalV1 == nullptr) {
        MILESTROLOG_ERROR("Unity Metal graphics interface is unavailable.");
    }
}

int64_t Render(const MilestroUnityRenderSubmission& submission) {
    const MilestroUnityRenderTargetPayload& payload = submission.target;

    if (payload.width <= 0 || payload.height <= 0) {
        MILESTROLOG_ERROR("Negative or zero width or height provided.");
        return MILESTRO_API_RET_FAILED;
    }

    if (RequiresUnityRenderBufferLookup(payload) &&
        (CurrentCommandBuffer() == nil || CurrentRenderPassDescriptor() == nil)) {
        if (!gLoggedMissingCommandBufferSkip) {
            MILESTROLOG_WARN("Skipping Milestro Metal render because Unity has no current Metal render context.");
            gLoggedMissingCommandBufferSkip = true;
        }
        return MILESTRO_API_RET_OK;
    }
    gLoggedMissingCommandBufferSkip = false;

    EndCurrentCommandEncoder();

    id<MTLTexture> texture = TextureFromPayload(payload);
    if (texture == nil) {
        MILESTROLOG_ERROR("Failed to resolve Unity Metal texture. handleKind={}, renderBufferHandle={}, "
                          "nativeTextureHandle={}.",
                          payload.handleKind,
                          payload.colorRenderBufferHandle,
                          payload.nativeTextureHandle);
        return MILESTRO_API_RET_FAILED;
    }

    GrDirectContext* context = DirectContext();
    if (context == nullptr) {
        return MILESTRO_API_RET_FAILED;
    }

    CommitCurrentCommandBufferIfAvailable();

    GrMtlTextureInfo textureInfo;
    textureInfo.fTexture.retain((__bridge GrMTLHandle) texture);
    GrBackendRenderTarget renderTarget = GrBackendRenderTargets::MakeMtl(payload.width, payload.height, textureInfo);

    sk_sp<SkSurface> surface = SkSurfaces::WrapBackendRenderTarget(context,
                                                                   renderTarget,
                                                                   kTopLeft_GrSurfaceOrigin,
                                                                   ColorTypeForTexture(texture),
                                                                   ColorSpaceForTexture(texture, payload.colorSpace),
                                                                   nullptr);
    if (surface == nullptr) {
        MILESTROLOG_ERROR("Failed to wrap Unity MTLTexture as Skia render target.");
        return MILESTRO_API_RET_FAILED;
    }

    milestro::unity_render::DrawSubmission(surface->getCanvas(), submission);
    context->flushAndSubmit(surface.get());
    return MILESTRO_API_RET_OK;
}

} // namespace milestro::unity_render::metal
