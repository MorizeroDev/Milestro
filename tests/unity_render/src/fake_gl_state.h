#ifndef MILESTRO_TEST_FAKE_GL_STATE_H
#define MILESTRO_TEST_FAKE_GL_STATE_H

// A state-only GL host, not a renderer. Enum values match GLES3; native NDK
// compilation separately checks the production guard against the actual headers.
#include <algorithm>
#include <array>
#include <map>
#include <stdexcept>
#include <vector>

#define GL_ES_VERSION_3_0 1

using GLenum = unsigned int;
using GLuint = unsigned int;
using GLint = int;
using GLboolean = unsigned char;
using GLfloat = float;

#define GL_ACTIVE_TEXTURE 0x84E0
#define GL_ARRAY_BUFFER 0x8892
#define GL_ARRAY_BUFFER_BINDING 0x8894
#define GL_BACK 0x0405
#define GL_BLEND 0x0BE2
#define GL_BLEND_COLOR 0x8005
#define GL_BLEND_DST_ALPHA 0x80CA
#define GL_BLEND_DST_RGB 0x80C8
#define GL_BLEND_EQUATION_ALPHA 0x883D
#define GL_BLEND_EQUATION_RGB 0x8009
#define GL_BLEND_SRC_ALPHA 0x80CB
#define GL_BLEND_SRC_RGB 0x80C9
#define GL_COLOR_WRITEMASK 0x0C23
#define GL_CULL_FACE 0x0B44
#define GL_CULL_FACE_MODE 0x0B45
#define GL_CURRENT_PROGRAM 0x8B8D
#define GL_DEPTH_FUNC 0x0B74
#define GL_DEPTH_TEST 0x0B71
#define GL_DEPTH_WRITEMASK 0x0B72
#define GL_DRAW_FRAMEBUFFER 0x8CA9
#define GL_DRAW_FRAMEBUFFER_BINDING 0x8CA6
#define GL_ELEMENT_ARRAY_BUFFER 0x8893
#define GL_ELEMENT_ARRAY_BUFFER_BINDING 0x8895
#define GL_FALSE 0
#define GL_FRAMEBUFFER 0x8D40
#define GL_FRAMEBUFFER_BINDING 0x8CA6
#define GL_FRONT 0x0404
#define GL_FRONT_FACE 0x0B46
#define GL_MAX_COMBINED_TEXTURE_IMAGE_UNITS 0x8B4D
#define GL_PACK_ALIGNMENT 0x0D05
#define GL_PACK_ROW_LENGTH 0x0D02
#define GL_PACK_SKIP_PIXELS 0x0D04
#define GL_PACK_SKIP_ROWS 0x0D03
#define GL_PIXEL_PACK_BUFFER 0x88EB
#define GL_PIXEL_PACK_BUFFER_BINDING 0x88ED
#define GL_PIXEL_UNPACK_BUFFER 0x88EC
#define GL_PIXEL_UNPACK_BUFFER_BINDING 0x88EF
#define GL_READ_FRAMEBUFFER 0x8CA8
#define GL_READ_FRAMEBUFFER_BINDING 0x8CAA
#define GL_SAMPLER_BINDING 0x8919
#define GL_SCISSOR_BOX 0x0C10
#define GL_SCISSOR_TEST 0x0C11
#define GL_STENCIL_BACK_FAIL 0x8801
#define GL_STENCIL_BACK_FUNC 0x8800
#define GL_STENCIL_BACK_PASS_DEPTH_FAIL 0x8802
#define GL_STENCIL_BACK_PASS_DEPTH_PASS 0x8803
#define GL_STENCIL_BACK_REF 0x8CA3
#define GL_STENCIL_BACK_VALUE_MASK 0x8CA4
#define GL_STENCIL_BACK_WRITEMASK 0x8CA5
#define GL_STENCIL_FAIL 0x0B94
#define GL_STENCIL_FUNC 0x0B92
#define GL_STENCIL_PASS_DEPTH_FAIL 0x0B95
#define GL_STENCIL_PASS_DEPTH_PASS 0x0B96
#define GL_STENCIL_REF 0x0B97
#define GL_STENCIL_TEST 0x0B90
#define GL_STENCIL_VALUE_MASK 0x0B93
#define GL_STENCIL_WRITEMASK 0x0B98
#define GL_TEXTURE0 0x84C0
#define GL_TEXTURE_2D 0x0DE1
#define GL_TEXTURE_BINDING_2D 0x8069
#define GL_TRUE 1
#define GL_UNPACK_ALIGNMENT 0x0CF5
#define GL_UNPACK_IMAGE_HEIGHT 0x806E
#define GL_UNPACK_ROW_LENGTH 0x0CF2
#define GL_UNPACK_SKIP_IMAGES 0x806D
#define GL_UNPACK_SKIP_PIXELS 0x0CF4
#define GL_UNPACK_SKIP_ROWS 0x0CF3
#define GL_VERTEX_ARRAY_BINDING 0x85B5
#define GL_VIEWPORT 0x0BA2

namespace fake_gl {
struct State {
    std::map<GLenum, std::vector<GLint>> values;
    std::array<GLfloat, 4> blendColor{0.125f, 0.25f, 0.5f, 0.75f};
    std::array<GLuint, 3> textures{11, 22, 33};
    std::array<GLuint, 3> samplers{44, 55, 66};
    GLint active = 1;
    bool operator==(const State&) const = default;
};
inline State state;
inline std::vector<GLint>& Values(GLenum name, size_t size = 1) {
    auto [entry, inserted] = state.values.try_emplace(name, size);
    if (inserted) {
        for (size_t i = 0; i < size; ++i) {
            entry->second[i] = static_cast<GLint>((name + i) % 97 + 1);
        }
    }
    return entry->second;
}
inline void Set(GLenum name, GLint value) {
    Values(name)[0] = value;
}
inline GLenum StencilName(GLenum face, GLenum front, GLenum back) {
    if (face != GL_FRONT && face != GL_BACK) {
        throw std::logic_error("unexpected stencil face");
    }
    return face == GL_FRONT ? front : back;
}
} // namespace fake_gl

inline void glGetIntegerv(GLenum name, GLint* out) {
    using namespace fake_gl;
    switch (name) {
        case GL_ACTIVE_TEXTURE:
            *out = GL_TEXTURE0 + state.active;
            return;
        case GL_MAX_COMBINED_TEXTURE_IMAGE_UNITS:
            *out = 3;
            return;
        case GL_TEXTURE_BINDING_2D:
            *out = state.textures.at(state.active);
            return;
        case GL_SAMPLER_BINDING:
            *out = state.samplers.at(state.active);
            return;
        default:
            break;
    }
    const auto& values = Values(name, name == GL_VIEWPORT || name == GL_SCISSOR_BOX ? 4 : 1);
    std::copy(values.begin(), values.end(), out);
}
inline void glGetBooleanv(GLenum name, GLboolean* out) {
    auto& values = fake_gl::Values(name, name == GL_COLOR_WRITEMASK ? 4 : 1);
    for (size_t i = 0; i < values.size(); ++i) {
        values[i] &= 1;
        out[i] = static_cast<GLboolean>(values[i]);
    }
}
inline void glGetFloatv(GLenum name, GLfloat* out) {
    if (name != GL_BLEND_COLOR)
        throw std::logic_error("unexpected float query");
    std::copy(fake_gl::state.blendColor.begin(), fake_gl::state.blendColor.end(), out);
}
inline GLboolean glIsEnabled(GLenum name) {
    auto& value = fake_gl::Values(name)[0];
    value &= 1;
    return static_cast<GLboolean>(value);
}
inline void glEnable(GLenum name) {
    fake_gl::Set(name, 1);
}
inline void glDisable(GLenum name) {
    fake_gl::Set(name, 0);
}
inline void glActiveTexture(GLenum value) {
    fake_gl::state.active = value - GL_TEXTURE0;
}
inline void glBindTexture(GLenum target, GLuint texture) {
    if (target != GL_TEXTURE_2D)
        throw std::logic_error("unexpected texture target");
    fake_gl::state.textures.at(fake_gl::state.active) = texture;
}
inline void glBindSampler(GLuint unit, GLuint sampler) {
    fake_gl::state.samplers.at(unit) = sampler;
}
inline void glUseProgram(GLuint program) {
    fake_gl::Set(GL_CURRENT_PROGRAM, program);
}
inline void glBindVertexArray(GLuint array) {
    fake_gl::Set(GL_VERTEX_ARRAY_BINDING, array);
}
inline void glBindBuffer(GLenum target, GLuint buffer) {
    switch (target) {
        case GL_ARRAY_BUFFER:
            fake_gl::Set(GL_ARRAY_BUFFER_BINDING, buffer);
            break;
        case GL_ELEMENT_ARRAY_BUFFER:
            fake_gl::Set(GL_ELEMENT_ARRAY_BUFFER_BINDING, buffer);
            break;
        case GL_PIXEL_PACK_BUFFER:
            fake_gl::Set(GL_PIXEL_PACK_BUFFER_BINDING, buffer);
            break;
        case GL_PIXEL_UNPACK_BUFFER:
            fake_gl::Set(GL_PIXEL_UNPACK_BUFFER_BINDING, buffer);
            break;
        default:
            throw std::logic_error("unexpected buffer target");
    }
}
inline void glBindFramebuffer(GLenum target, GLuint framebuffer) {
    if (target == GL_DRAW_FRAMEBUFFER || target == GL_FRAMEBUFFER)
        fake_gl::Set(GL_DRAW_FRAMEBUFFER_BINDING, framebuffer);
    if (target == GL_READ_FRAMEBUFFER || target == GL_FRAMEBUFFER)
        fake_gl::Set(GL_READ_FRAMEBUFFER_BINDING, framebuffer);
}
inline void glViewport(GLint x, GLint y, GLint width, GLint height) {
    fake_gl::Values(GL_VIEWPORT) = {x, y, width, height};
}
inline void glScissor(GLint x, GLint y, GLint width, GLint height) {
    fake_gl::Values(GL_SCISSOR_BOX) = {x, y, width, height};
}
inline void glColorMask(GLboolean r, GLboolean g, GLboolean b, GLboolean a) {
    fake_gl::Values(GL_COLOR_WRITEMASK) = {r, g, b, a};
}
inline void glDepthMask(GLboolean value) {
    fake_gl::Set(GL_DEPTH_WRITEMASK, value);
}
inline void glBlendFuncSeparate(GLenum sr, GLenum dr, GLenum sa, GLenum da) {
    fake_gl::Set(GL_BLEND_SRC_RGB, sr);
    fake_gl::Set(GL_BLEND_DST_RGB, dr);
    fake_gl::Set(GL_BLEND_SRC_ALPHA, sa);
    fake_gl::Set(GL_BLEND_DST_ALPHA, da);
}
inline void glBlendEquationSeparate(GLenum rgb, GLenum alpha) {
    fake_gl::Set(GL_BLEND_EQUATION_RGB, rgb);
    fake_gl::Set(GL_BLEND_EQUATION_ALPHA, alpha);
}
inline void glBlendColor(GLfloat r, GLfloat g, GLfloat b, GLfloat a) {
    fake_gl::state.blendColor = {r, g, b, a};
}
inline void glDepthFunc(GLenum value) {
    fake_gl::Set(GL_DEPTH_FUNC, value);
}
inline void glCullFace(GLenum value) {
    fake_gl::Set(GL_CULL_FACE_MODE, value);
}
inline void glFrontFace(GLenum value) {
    fake_gl::Set(GL_FRONT_FACE, value);
}
inline void glStencilFuncSeparate(GLenum face, GLenum func, GLint ref, GLuint mask) {
    using namespace fake_gl;
    Set(StencilName(face, GL_STENCIL_FUNC, GL_STENCIL_BACK_FUNC), func);
    Set(StencilName(face, GL_STENCIL_REF, GL_STENCIL_BACK_REF), ref);
    Set(StencilName(face, GL_STENCIL_VALUE_MASK, GL_STENCIL_BACK_VALUE_MASK), mask);
}
inline void glStencilMaskSeparate(GLenum face, GLuint mask) {
    fake_gl::Set(fake_gl::StencilName(face, GL_STENCIL_WRITEMASK, GL_STENCIL_BACK_WRITEMASK), mask);
}
inline void glStencilOpSeparate(GLenum face, GLenum fail, GLenum depthFail, GLenum depthPass) {
    using namespace fake_gl;
    Set(StencilName(face, GL_STENCIL_FAIL, GL_STENCIL_BACK_FAIL), fail);
    Set(StencilName(face, GL_STENCIL_PASS_DEPTH_FAIL, GL_STENCIL_BACK_PASS_DEPTH_FAIL), depthFail);
    Set(StencilName(face, GL_STENCIL_PASS_DEPTH_PASS, GL_STENCIL_BACK_PASS_DEPTH_PASS), depthPass);
}
inline void glPixelStorei(GLenum name, GLint value) {
    fake_gl::Set(name, value);
}

#endif // MILESTRO_TEST_FAKE_GL_STATE_H
