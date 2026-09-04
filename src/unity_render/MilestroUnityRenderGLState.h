#ifndef MILESTRO_UNITY_RENDER_GL_STATE_H
#define MILESTRO_UNITY_RENDER_GL_STATE_H

// Include after the platform's GLES3/OpenGL headers. Kept independent of Skia so
// the host-state boundary can be regression-tested without a GPU or Unity.
#include <algorithm>
#include <array>
#include <cstddef>
#include <vector>

namespace milestro::unity_render::gl {

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
        glGetIntegerv(GL_BLEND_SRC_RGB, &blendSrcRgb_);
        glGetIntegerv(GL_BLEND_DST_RGB, &blendDstRgb_);
        glGetIntegerv(GL_BLEND_SRC_ALPHA, &blendSrcAlpha_);
        glGetIntegerv(GL_BLEND_DST_ALPHA, &blendDstAlpha_);
        glGetIntegerv(GL_BLEND_EQUATION_RGB, &blendEquationRgb_);
        glGetIntegerv(GL_BLEND_EQUATION_ALPHA, &blendEquationAlpha_);
        glGetFloatv(GL_BLEND_COLOR, blendColor_.data());
        glGetIntegerv(GL_DEPTH_FUNC, &depthFunc_);
        glGetIntegerv(GL_CULL_FACE_MODE, &cullFaceMode_);
        glGetIntegerv(GL_FRONT_FACE, &frontFace_);
        frontStencil_.Capture(false);
        backStencil_.Capture(true);
        glGetIntegerv(GL_PIXEL_PACK_BUFFER_BINDING, &pixelPackBuffer_);
        glGetIntegerv(GL_PIXEL_UNPACK_BUFFER_BINDING, &pixelUnpackBuffer_);
        for (size_t i = 0; i < kPixelStoreNames.size(); ++i) {
            glGetIntegerv(kPixelStoreNames[i], &pixelStore_[i]);
        }

        GLint maxTextureUnits = 0;
        glGetIntegerv(GL_MAX_COMBINED_TEXTURE_IMAGE_UNITS, &maxTextureUnits);
        textureUnitCount_ = std::max(0, maxTextureUnits);
        textureUnits_.resize(static_cast<size_t>(textureUnitCount_));
        for (int i = 0; i < textureUnitCount_; ++i) {
            glActiveTexture(static_cast<GLenum>(GL_TEXTURE0 + i));
            auto& unit = textureUnits_[static_cast<size_t>(i)];
            glGetIntegerv(GL_TEXTURE_BINDING_2D, &unit.texture2D);
#if defined(GL_ES_VERSION_3_0)
            glGetIntegerv(GL_SAMPLER_BINDING, &unit.sampler);
#endif
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
            const auto& unit = textureUnits_[static_cast<size_t>(i)];
            glBindTexture(GL_TEXTURE_2D, static_cast<GLuint>(unit.texture2D));
#if defined(GL_ES_VERSION_3_0)
            glBindSampler(static_cast<GLuint>(i), static_cast<GLuint>(unit.sampler));
#endif
        }
        glActiveTexture(static_cast<GLenum>(activeTexture_));

        glUseProgram(static_cast<GLuint>(program_));
        glBindBuffer(GL_ARRAY_BUFFER, static_cast<GLuint>(arrayBuffer_));
        glBindVertexArray(static_cast<GLuint>(vertexArray_));
        glBindBuffer(GL_ELEMENT_ARRAY_BUFFER, static_cast<GLuint>(elementArrayBuffer_));

#if defined(GL_DRAW_FRAMEBUFFER_BINDING) && defined(GL_READ_FRAMEBUFFER_BINDING)
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
        glBlendFuncSeparate(blendSrcRgb_, blendDstRgb_, blendSrcAlpha_, blendDstAlpha_);
        glBlendEquationSeparate(blendEquationRgb_, blendEquationAlpha_);
        glBlendColor(blendColor_[0], blendColor_[1], blendColor_[2], blendColor_[3]);
        glDepthFunc(depthFunc_);
        glCullFace(cullFaceMode_);
        glFrontFace(frontFace_);
        frontStencil_.Restore(GL_FRONT);
        backStencil_.Restore(GL_BACK);
        glBindBuffer(GL_PIXEL_PACK_BUFFER, static_cast<GLuint>(pixelPackBuffer_));
        glBindBuffer(GL_PIXEL_UNPACK_BUFFER, static_cast<GLuint>(pixelUnpackBuffer_));
        for (size_t i = 0; i < kPixelStoreNames.size(); ++i) {
            glPixelStorei(kPixelStoreNames[i], pixelStore_[i]);
        }
    }

private:
    struct StencilState {
        GLint func = 0;
        GLint ref = 0;
        GLint valueMask = 0;
        GLint writeMask = 0;
        GLint fail = 0;
        GLint depthFail = 0;
        GLint depthPass = 0;

        void Capture(bool back) {
            glGetIntegerv(back ? GL_STENCIL_BACK_FUNC : GL_STENCIL_FUNC, &func);
            glGetIntegerv(back ? GL_STENCIL_BACK_REF : GL_STENCIL_REF, &ref);
            glGetIntegerv(back ? GL_STENCIL_BACK_VALUE_MASK : GL_STENCIL_VALUE_MASK, &valueMask);
            glGetIntegerv(back ? GL_STENCIL_BACK_WRITEMASK : GL_STENCIL_WRITEMASK, &writeMask);
            glGetIntegerv(back ? GL_STENCIL_BACK_FAIL : GL_STENCIL_FAIL, &fail);
            glGetIntegerv(back ? GL_STENCIL_BACK_PASS_DEPTH_FAIL : GL_STENCIL_PASS_DEPTH_FAIL, &depthFail);
            glGetIntegerv(back ? GL_STENCIL_BACK_PASS_DEPTH_PASS : GL_STENCIL_PASS_DEPTH_PASS, &depthPass);
        }

        void Restore(GLenum face) const {
            glStencilFuncSeparate(face, func, ref, static_cast<GLuint>(valueMask));
            glStencilMaskSeparate(face, static_cast<GLuint>(writeMask));
            glStencilOpSeparate(face, fail, depthFail, depthPass);
        }
    };

    // GLES3 guarantees sampler objects. Do not require GL 3.3 on desktop GLCore.
    struct TextureUnitState {
        GLint texture2D = 0;
        GLint sampler = 0;
    };

    // Pixel upload state is global even when drawing into a private FBO.
    static constexpr std::array<GLenum, 10> kPixelStoreNames = {GL_PACK_ALIGNMENT,
                                                                GL_PACK_ROW_LENGTH,
                                                                GL_PACK_SKIP_PIXELS,
                                                                GL_PACK_SKIP_ROWS,
                                                                GL_UNPACK_ALIGNMENT,
                                                                GL_UNPACK_ROW_LENGTH,
                                                                GL_UNPACK_IMAGE_HEIGHT,
                                                                GL_UNPACK_SKIP_PIXELS,
                                                                GL_UNPACK_SKIP_ROWS,
                                                                GL_UNPACK_SKIP_IMAGES};

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
    std::vector<TextureUnitState> textureUnits_;
    std::array<GLint, kPixelStoreNames.size()> pixelStore_{};
    std::array<GLfloat, 4> blendColor_{};
    StencilState frontStencil_;
    StencilState backStencil_;
    GLint blendSrcRgb_ = 0;
    GLint blendDstRgb_ = 0;
    GLint blendSrcAlpha_ = 0;
    GLint blendDstAlpha_ = 0;
    GLint blendEquationRgb_ = 0;
    GLint blendEquationAlpha_ = 0;
    GLint depthFunc_ = 0;
    GLint cullFaceMode_ = 0;
    GLint frontFace_ = 0;
    GLint pixelPackBuffer_ = 0;
    GLint pixelUnpackBuffer_ = 0;
    GLint textureUnitCount_ = 0;
    GLint program_ = 0;
    GLint arrayBuffer_ = 0;
    GLint elementArrayBuffer_ = 0;
    GLint activeTexture_ = GL_TEXTURE0;
    GLint vertexArray_ = 0;
#if defined(GL_DRAW_FRAMEBUFFER_BINDING) && defined(GL_READ_FRAMEBUFFER_BINDING)
    GLint drawFramebuffer_ = 0;
    GLint readFramebuffer_ = 0;
#else
    GLint framebuffer_ = 0;
#endif
    GLboolean scissorEnabled_ = GL_FALSE;
    GLboolean blendEnabled_ = GL_FALSE;
    GLboolean cullFaceEnabled_ = GL_FALSE;
    GLboolean depthTestEnabled_ = GL_FALSE;
    GLboolean stencilTestEnabled_ = GL_FALSE;
    GLboolean depthMask_ = GL_TRUE;
};

} // namespace milestro::unity_render::gl

#endif // MILESTRO_UNITY_RENDER_GL_STATE_H
