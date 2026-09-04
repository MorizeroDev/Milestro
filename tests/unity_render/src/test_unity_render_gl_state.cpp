#include "fake_gl_state.h"
#include "unity_render/MilestroUnityRenderGLState.h"

#include <gtest/gtest.h>

namespace {
using milestro::unity_render::gl::GLStateGuard;

void ModifyHostState() {
    for (auto& [name, values]: fake_gl::state.values) {
        for (auto& value: values) {
            value += 101;
        }
    }
    fake_gl::state.blendColor = {1, 0, 1, 0};
    fake_gl::state.textures = {77, 88, 99};
    fake_gl::state.samplers = {100, 101, 102};
    fake_gl::state.active = 2;
}

TEST(UnityRenderGLState, RestoresAllCapturedStateAfterDrawing) {
    fake_gl::state = {};
    fake_gl::State before;
    {
        GLStateGuard guard;
        EXPECT_EQ(fake_gl::state.active, 1);
        before = fake_gl::state;
        ModifyHostState();
        EXPECT_FALSE(fake_gl::state == before);
    }
    EXPECT_TRUE(fake_gl::state == before);
}

TEST(UnityRenderGLState, RestoresStateOnExceptionAndEarlyReturn) {
    fake_gl::state = {};
    fake_gl::State before;
    EXPECT_THROW(
            {
                GLStateGuard guard;
                before = fake_gl::state;
                ModifyHostState();
                throw std::runtime_error("failed surface wrap");
            },
            std::runtime_error);
    EXPECT_TRUE(fake_gl::state == before);
    const auto failedDraw = [] {
        GLStateGuard guard;
        ModifyHostState();
        return false;
    };
    EXPECT_FALSE(failedDraw());
    EXPECT_TRUE(fake_gl::state == before);
}

TEST(UnityRenderGLState, NestedGuardsRestoreTheirOwnSnapshots) {
    fake_gl::state = {};
    fake_gl::State before;
    {
        GLStateGuard outer;
        before = fake_gl::state;
        glBlendFuncSeparate(1, 0, 0, 1);
        glStencilMaskSeparate(GL_FRONT, 0x12);
        glStencilMaskSeparate(GL_BACK, 0x34);
        glPixelStorei(GL_UNPACK_ALIGNMENT, 1);
        glBindSampler(1, 87);
        const auto innerBefore = fake_gl::state;
        {
            GLStateGuard inner;
            ModifyHostState();
        }
        EXPECT_TRUE(fake_gl::state == innerBefore);
    }
    EXPECT_TRUE(fake_gl::state == before);
}
} // namespace
