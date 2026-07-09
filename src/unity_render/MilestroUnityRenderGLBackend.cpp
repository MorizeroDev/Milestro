#include "unity_render/MilestroUnityRenderGLBackend.h"

#include "game/milestro_game_retcode.h"
#include "unity_render/MilestroUnityRenderSubmissionDraw.h"
#include "unity_render/MilestroUnityRenderTextureHandleKind.h"

#include <algorithm>
#include <array>
#include <cstdint>
#include <limits>
#include <utility>
#include <vector>

#include "unity_render/MilestroUnityRenderLog.h"

#if defined(__ANDROID__)
#include <EGL/egl.h>
#include <GLES3/gl3.h>
#elif defined(__linux__)
#define GL_GLEXT_PROTOTYPES
#include <GL/gl.h>
#include <GL/glx.h>
#else
#error "Milestro Unity GL render backend is enabled on an unsupported platform."
#endif

#include "include/core/SkCanvas.h"
#include "include/core/SkColorSpace.h"
#include "include/core/SkSurface.h"
#include "include/gpu/ganesh/GrBackendSurface.h"
#include "include/gpu/ganesh/GrDirectContext.h"
#include "include/gpu/ganesh/SkSurfaceGanesh.h"
#include "include/gpu/ganesh/gl/GrGLBackendSurface.h"
#include "include/gpu/ganesh/gl/GrGLDirectContext.h"
#include "include/gpu/ganesh/gl/GrGLInterface.h"
#include "include/gpu/ganesh/gl/GrGLTypes.h"

#ifndef GL_RGBA8
#define GL_RGBA8 0x8058
#endif

#ifndef GL_SRGB8_ALPHA8
#define GL_SRGB8_ALPHA8 0x8C43
#endif

#ifndef GL_VERTEX_ARRAY_BINDING
#define GL_VERTEX_ARRAY_BINDING 0x85B5
#endif

namespace milestro::unity_render::gl {

namespace {

constexpr int32_t kRenderTextureFormatAuto = 0;
constexpr int32_t kRenderTextureFormatBgra32 = 1;
constexpr int32_t kRenderTextureFormatRgba32 = 2;
constexpr int32_t kUnityColorSpaceLinear = 1;

sk_sp<GrDirectContext> gDirectContext;
uint64_t gRenderSerial = 0;

struct GLContextIdentity {
    void* display = nullptr;
    void* context = nullptr;

    bool isValid() const {
        return context != nullptr;
    }

    bool operator==(const GLContextIdentity& other) const {
        return display == other.display && context == other.context;
    }
};

GLContextIdentity gDirectContextIdentity;

GLContextIdentity CurrentGLContextIdentity() {
#if defined(__ANDROID__)
    EGLDisplay display = eglGetCurrentDisplay();
    EGLContext context = eglGetCurrentContext();
    return {display != EGL_NO_DISPLAY ? static_cast<void*>(display) : nullptr,
            context != EGL_NO_CONTEXT ? static_cast<void*>(context) : nullptr};
#elif defined(__linux__)
    Display* display = glXGetCurrentDisplay();
    GLXContext context = glXGetCurrentContext();
    return {static_cast<void*>(display), reinterpret_cast<void*>(context)};
#endif
}

void ResetDirectContext(bool abandon) {
    if (gDirectContext != nullptr && abandon) {
        gDirectContext->abandonContext();
    }

    gDirectContext.reset();
    gDirectContextIdentity = {};
}

GrDirectContext* DirectContext(uint64_t renderSerial) {
    GLContextIdentity identity = CurrentGLContextIdentity();
    if (!identity.isValid()) {
        MILESTROLOG_ERROR("Milestro GL render event {} has no current GL context.", renderSerial);
        ResetDirectContext(true);
        return nullptr;
    }

    if (gDirectContext != nullptr && identity == gDirectContextIdentity) {
        return gDirectContext.get();
    }

    if (gDirectContext != nullptr) {
        MILESTRO_RENDER_LOG_WARN("Milestro GL context identity changed on event {}; abandoning cached Skia GL context.",
                         renderSerial);
        ResetDirectContext(true);
    }

#if defined(SK_DISABLE_LEGACY_GL_MAKE_NATIVE_INTERFACE)
    MILESTROLOG_ERROR("Skia was built without GrGLMakeNativeInterface; Milestro GL backend cannot create a context.");
    return nullptr;
#else
    sk_sp<const GrGLInterface> interface = GrGLMakeNativeInterface();
    if (interface == nullptr) {
        MILESTROLOG_ERROR("Failed to create Skia native GL interface on event {}.", renderSerial);
        return nullptr;
    }

    if (!interface->validate()) {
        MILESTROLOG_ERROR("Skia native GL interface validation failed on event {}.", renderSerial);
        return nullptr;
    }

    gDirectContext = GrDirectContexts::MakeGL(std::move(interface));
    gDirectContextIdentity = identity;
    if (gDirectContext == nullptr) {
        MILESTROLOG_ERROR("Failed to create Skia GL direct context on event {}.", renderSerial);
    } else {
        MILESTRO_RENDER_LOG_INFO("Created Skia GL direct context={} for Unity context={} on event {}.",
                         static_cast<void*>(gDirectContext.get()),
                         identity.context,
                         renderSerial);
    }
    return gDirectContext.get();
#endif
}

void DrainGLErrors() {
    while (glGetError() != GL_NO_ERROR) {
    }
}

bool IsRendererSupported(UnityGfxRenderer renderer) {
    return renderer == kUnityGfxRendererOpenGLES30 || renderer == kUnityGfxRendererOpenGLCore;
}

const char* RendererName(UnityGfxRenderer renderer) {
    switch (renderer) {
        case kUnityGfxRendererOpenGLES30:
            return "OpenGLES30";
        case kUnityGfxRendererOpenGLCore:
            return "OpenGLCore";
        default:
            return "unsupported";
    }
}

bool TextureNameFromPayload(const MilestroUnityRenderTargetPayload& payload, GLuint& textureName) {
    textureName = 0;

    if (payload.handleKind != static_cast<int32_t>(MilestroUnityRenderTextureHandleKind::NativeTexture)) {
        MILESTROLOG_ERROR("Milestro GL render target requires NativeTexture handleKind, got {}.", payload.handleKind);
        return false;
    }

    auto raw = reinterpret_cast<std::uintptr_t>(payload.nativeTextureHandle);
    if (raw == 0) {
        MILESTROLOG_ERROR("Milestro GL render target native texture handle is zero.");
        return false;
    }

    if (raw > static_cast<std::uintptr_t>(std::numeric_limits<GLuint>::max())) {
        MILESTROLOG_ERROR("Milestro GL texture handle 0x{:x} cannot fit in GLuint.",
                          static_cast<unsigned long long>(raw));
        return false;
    }

    textureName = static_cast<GLuint>(raw);
    return true;
}

bool FormatForPayload(const MilestroUnityRenderTargetPayload& payload, GrGLenum& format, SkColorType& colorType) {
    switch (payload.preferredFormat) {
        case kRenderTextureFormatAuto:
        case kRenderTextureFormatRgba32:
            format = payload.storageSrgb != 0 ? GL_SRGB8_ALPHA8 : GL_RGBA8;
            colorType = kRGBA_8888_SkColorType;
            return true;
        case kRenderTextureFormatBgra32:
            MILESTROLOG_ERROR("Milestro GL RenderTexture BGRA32 format is not implemented in this PoC.");
            return false;
        default:
            MILESTROLOG_ERROR("Unknown Milestro GL RenderTexture preferred format {}.", payload.preferredFormat);
            return false;
    }
}

sk_sp<SkColorSpace> ColorSpaceForPayload(const MilestroUnityRenderTargetPayload& payload) {
    if (payload.storageSrgb != 0) {
        return SkColorSpace::MakeSRGB();
    }
    if (payload.colorSpace == kUnityColorSpaceLinear) {
        return SkColorSpace::MakeSRGBLinear();
    }
    return SkColorSpace::MakeSRGB();
}

class GLStateGuard {
public:
    GLStateGuard() {
        glGetIntegerv(GL_VIEWPORT, viewport_.data());
        glGetIntegerv(GL_SCISSOR_BOX, scissorBox_.data());
        glGetIntegerv(GL_CURRENT_PROGRAM, &program_);
        glGetIntegerv(GL_ARRAY_BUFFER_BINDING, &arrayBuffer_);
        glGetIntegerv(GL_ELEMENT_ARRAY_BUFFER_BINDING, &elementArrayBuffer_);
        glGetIntegerv(GL_ACTIVE_TEXTURE, &activeTexture_);
        glGetIntegerv(GL_VERTEX_ARRAY_BINDING, &vertexArray_);

#if defined(GL_DRAW_FRAMEBUFFER_BINDING) && defined(GL_READ_FRAMEBUFFER_BINDING)
        glGetIntegerv(GL_DRAW_FRAMEBUFFER_BINDING, &drawFramebuffer_);
        glGetIntegerv(GL_READ_FRAMEBUFFER_BINDING, &readFramebuffer_);
#else
        glGetIntegerv(GL_FRAMEBUFFER_BINDING, &framebuffer_);
#endif

        scissorEnabled_ = glIsEnabled(GL_SCISSOR_TEST);
        blendEnabled_ = glIsEnabled(GL_BLEND);
        cullFaceEnabled_ = glIsEnabled(GL_CULL_FACE);
        depthTestEnabled_ = glIsEnabled(GL_DEPTH_TEST);
        stencilTestEnabled_ = glIsEnabled(GL_STENCIL_TEST);
        glGetBooleanv(GL_COLOR_WRITEMASK, colorMask_.data());
        glGetBooleanv(GL_DEPTH_WRITEMASK, &depthMask_);

        GLint maxTextureUnits = 0;
        glGetIntegerv(GL_MAX_COMBINED_TEXTURE_IMAGE_UNITS, &maxTextureUnits);
        textureUnitCount_ = std::max(0, maxTextureUnits);
        textureBindings2D_.resize(static_cast<size_t>(textureUnitCount_));
        for (int i = 0; i < textureUnitCount_; ++i) {
            glActiveTexture(static_cast<GLenum>(GL_TEXTURE0 + i));
            glGetIntegerv(GL_TEXTURE_BINDING_2D, &textureBindings2D_[static_cast<size_t>(i)]);
        }
        glActiveTexture(static_cast<GLenum>(activeTexture_));
    }

    ~GLStateGuard() {
        Restore();
    }

    GLStateGuard(const GLStateGuard&) = delete;
    GLStateGuard& operator=(const GLStateGuard&) = delete;

    void Restore() const {
        for (int i = 0; i < textureUnitCount_; ++i) {
            glActiveTexture(static_cast<GLenum>(GL_TEXTURE0 + i));
            glBindTexture(GL_TEXTURE_2D, static_cast<GLuint>(textureBindings2D_[static_cast<size_t>(i)]));
        }
        glActiveTexture(static_cast<GLenum>(activeTexture_));

        glUseProgram(static_cast<GLuint>(program_));
        glBindBuffer(GL_ARRAY_BUFFER, static_cast<GLuint>(arrayBuffer_));
        glBindVertexArray(static_cast<GLuint>(vertexArray_));
        glBindBuffer(GL_ELEMENT_ARRAY_BUFFER, static_cast<GLuint>(elementArrayBuffer_));

#if defined(GL_DRAW_FRAMEBUFFER) && defined(GL_READ_FRAMEBUFFER)
        glBindFramebuffer(GL_DRAW_FRAMEBUFFER, static_cast<GLuint>(drawFramebuffer_));
        glBindFramebuffer(GL_READ_FRAMEBUFFER, static_cast<GLuint>(readFramebuffer_));
#else
        glBindFramebuffer(GL_FRAMEBUFFER, static_cast<GLuint>(framebuffer_));
#endif

        glViewport(viewport_[0], viewport_[1], viewport_[2], viewport_[3]);
        glScissor(scissorBox_[0], scissorBox_[1], scissorBox_[2], scissorBox_[3]);
        SetEnabled(GL_SCISSOR_TEST, scissorEnabled_);
        SetEnabled(GL_BLEND, blendEnabled_);
        SetEnabled(GL_CULL_FACE, cullFaceEnabled_);
        SetEnabled(GL_DEPTH_TEST, depthTestEnabled_);
        SetEnabled(GL_STENCIL_TEST, stencilTestEnabled_);
        glColorMask(colorMask_[0], colorMask_[1], colorMask_[2], colorMask_[3]);
        glDepthMask(depthMask_);
    }

private:
    static void SetEnabled(GLenum cap, GLboolean enabled) {
        if (enabled == GL_TRUE) {
            glEnable(cap);
        } else {
            glDisable(cap);
        }
    }

    std::array<GLint, 4> viewport_ = {0, 0, 0, 0};
    std::array<GLint, 4> scissorBox_ = {0, 0, 0, 0};
    std::array<GLboolean, 4> colorMask_ = {GL_TRUE, GL_TRUE, GL_TRUE, GL_TRUE};
    std::vector<GLint> textureBindings2D_;
    GLint textureUnitCount_ = 0;
    GLint program_ = 0;
    GLint arrayBuffer_ = 0;
    GLint elementArrayBuffer_ = 0;
    GLint activeTexture_ = GL_TEXTURE0;
    GLint vertexArray_ = 0;
    GLint framebuffer_ = 0;
    GLint drawFramebuffer_ = 0;
    GLint readFramebuffer_ = 0;
    GLboolean scissorEnabled_ = GL_FALSE;
    GLboolean blendEnabled_ = GL_FALSE;
    GLboolean cullFaceEnabled_ = GL_FALSE;
    GLboolean depthTestEnabled_ = GL_FALSE;
    GLboolean stencilTestEnabled_ = GL_FALSE;
    GLboolean depthMask_ = GL_TRUE;
};

class ScopedFramebuffer {
public:
    ScopedFramebuffer() {
        glGenFramebuffers(1, &id_);
    }

    ~ScopedFramebuffer() {
        if (id_ != 0) {
            glDeleteFramebuffers(1, &id_);
        }
    }

    ScopedFramebuffer(const ScopedFramebuffer&) = delete;
    ScopedFramebuffer& operator=(const ScopedFramebuffer&) = delete;

    GLuint id() const {
        return id_;
    }

private:
    GLuint id_ = 0;
};

} // namespace

void OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType,
                           IUnityInterfaces* unityInterfaces,
                           UnityGfxRenderer renderer) {
    (void) unityInterfaces;

    if (eventType == kUnityGfxDeviceEventShutdown || eventType == kUnityGfxDeviceEventBeforeReset ||
        !IsRendererSupported(renderer)) {
        ResetDirectContext(true);
    }
}

int64_t Render(const MilestroUnityRenderSubmission& submission, UnityGfxRenderer renderer) {
    const MilestroUnityRenderTargetPayload& payload = submission.target;
    const uint64_t renderSerial = ++gRenderSerial;

    if (!IsRendererSupported(renderer)) {
        MILESTROLOG_ERROR("Milestro GL render event invoked while Unity renderer is {}.",
                          static_cast<int>(renderer));
        return MILESTRO_API_RET_FAILED;
    }

    if (payload.width <= 0 || payload.height <= 0) {
        MILESTROLOG_ERROR("Invalid Milestro GL render payload size on event {}.", renderSerial);
        return MILESTRO_API_RET_FAILED;
    }

    if (payload.msaaSamples != 1) {
        MILESTROLOG_ERROR("Milestro GL RenderTexture MSAA is not implemented yet: {} samples.", payload.msaaSamples);
        return MILESTRO_API_RET_FAILED;
    }

    GLuint textureName = 0;
    if (!TextureNameFromPayload(payload, textureName)) {
        return MILESTRO_API_RET_FAILED;
    }

    GrGLenum format = GL_RGBA8;
    SkColorType colorType = kRGBA_8888_SkColorType;
    if (!FormatForPayload(payload, format, colorType)) {
        return MILESTRO_API_RET_FAILED;
    }

    if (!CurrentGLContextIdentity().isValid()) {
        MILESTROLOG_ERROR("Milestro GL render event {} has no current GL context.", renderSerial);
        ResetDirectContext(true);
        return MILESTRO_API_RET_FAILED;
    }

    GLStateGuard stateGuard;
    DrainGLErrors();

    GrDirectContext* context = DirectContext(renderSerial);
    if (context == nullptr) {
        return MILESTRO_API_RET_FAILED;
    }

    ScopedFramebuffer framebuffer;
    if (framebuffer.id() == 0) {
        MILESTROLOG_ERROR("Failed to create temporary Milestro GL framebuffer on event {}.", renderSerial);
        return MILESTRO_API_RET_FAILED;
    }

    glBindFramebuffer(GL_FRAMEBUFFER, framebuffer.id());
    glFramebufferTexture2D(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_TEXTURE_2D, textureName, 0);
    GLenum status = glCheckFramebufferStatus(GL_FRAMEBUFFER);
    if (status != GL_FRAMEBUFFER_COMPLETE) {
        MILESTROLOG_ERROR("Milestro GL temporary framebuffer incomplete on event {}: status=0x{:x}, "
                          "texture={}, renderer={}, size={}x{}, format=0x{:x}.",
                          renderSerial,
                          static_cast<unsigned int>(status),
                          textureName,
                          RendererName(renderer),
                          payload.width,
                          payload.height,
                          static_cast<unsigned int>(format));
        return MILESTRO_API_RET_FAILED;
    }

    glViewport(0, 0, payload.width, payload.height);
    glDisable(GL_SCISSOR_TEST);
    context->resetContext();

    GrGLFramebufferInfo framebufferInfo;
    framebufferInfo.fFBOID = framebuffer.id();
    framebufferInfo.fFormat = format;
    GrBackendRenderTarget renderTarget =
            GrBackendRenderTargets::MakeGL(payload.width, payload.height, 0, 0, framebufferInfo);

    sk_sp<SkSurface> surface = SkSurfaces::WrapBackendRenderTarget(context,
                                                                   renderTarget,
                                                                   kTopLeft_GrSurfaceOrigin,
                                                                   colorType,
                                                                   ColorSpaceForPayload(payload),
                                                                   nullptr);
    if (surface == nullptr) {
        MILESTROLOG_ERROR("Failed to wrap Unity GL texture {} temporary FBO {} as Skia render target on event {}.",
                          textureName,
                          framebuffer.id(),
                          renderSerial);
        context->resetContext();
        return MILESTRO_API_RET_FAILED;
    }

    MILESTRO_RENDER_LOG_INFO("Milestro GL wrap target: event={}, renderer={}, texture={}, fbo={}, size={}x{}, "
                     "format=0x{:x}, colorSpace={}, storageSrgb={}, preferredFormat={}.",
                     renderSerial,
                     RendererName(renderer),
                     textureName,
                     framebuffer.id(),
                     payload.width,
                     payload.height,
                     static_cast<unsigned int>(format),
                     payload.colorSpace,
                     payload.storageSrgb,
                     payload.preferredFormat);

    milestro::unity_render::DrawSubmission(surface->getCanvas(), submission);
    context->flushAndSubmit(surface.get());
    context->resetContext();
    return MILESTRO_API_RET_OK;
}

} // namespace milestro::unity_render::gl
