#pragma once

#include <cstdint>

namespace milestro::unity_render::vulkan {

enum class VulkanHostMemorySupport {
    Unavailable,
    NonCoherent,
    Coherent,
};

template <typename MemoryProperties>
VulkanHostMemorySupport ClassifyVulkanHostMemorySupport(
    const MemoryProperties& properties,
    uint32_t memoryTypeBits,
    uint32_t hostVisibleFlag,
    uint32_t hostCoherentFlag) {
    bool hasHostVisible = false;
    for (uint32_t index = 0; index < properties.memoryTypeCount; ++index) {
        if ((memoryTypeBits & (uint32_t{1} << index)) == 0) {
            continue;
        }

        const uint32_t flags = properties.memoryTypes[index].propertyFlags;
        if ((flags & hostVisibleFlag) == 0) {
            continue;
        }

        hasHostVisible = true;
        if ((flags & hostCoherentFlag) != 0) {
            return VulkanHostMemorySupport::Coherent;
        }
    }

    return hasHostVisible
               ? VulkanHostMemorySupport::NonCoherent
               : VulkanHostMemorySupport::Unavailable;
}

} // namespace milestro::unity_render::vulkan
