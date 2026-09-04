#ifndef MILESTRO_UNITY_VULKAN_BACKEND_KIND_H
#define MILESTRO_UNITY_VULKAN_BACKEND_KIND_H

#include <cstdint>

namespace milestro::unity_render::vulkan {

enum class VulkanBackendKind : int32_t {
    Direct = 1,
    StagingCopy = 2,
};

} // namespace milestro::unity_render::vulkan

#endif // MILESTRO_UNITY_VULKAN_BACKEND_KIND_H
