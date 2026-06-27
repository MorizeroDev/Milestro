#ifndef MILESTRO_UNITY_RENDER_GRAPHICS_BACKEND_H
#define MILESTRO_UNITY_RENDER_GRAPHICS_BACKEND_H

#include <cstdint>

enum class MilestroUnityGraphicsBackend : int32_t {
    Metal = 1,
    Direct3D12 = 2,
    Vulkan = 3,
    OpenGL = 4,
    OpenGLES = 5,
};

#endif // MILESTRO_UNITY_RENDER_GRAPHICS_BACKEND_H
