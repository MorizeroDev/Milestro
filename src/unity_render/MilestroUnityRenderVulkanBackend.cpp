#include "unity_render/MilestroUnityRenderVulkanBackend.h"

#include "game/milestro_game_retcode.h"
#include "unity_render/MilestroUnityRenderTextureHandleKind.h"

#include <IUnityGraphicsVulkan.h>

#include <cstdint>

#include "unity_render/MilestroUnityRenderLog.h"

namespace milestro::unity_render::vulkan {

namespace {

IUnityGraphicsVulkan* gVulkan = nullptr;
bool gLoggedHeaderContract = false;
uint64_t gRenderSerial = 0;

struct StageAccess {
    VkPipelineStageFlags stage = VK_PIPELINE_STAGE_ALL_COMMANDS_BIT;
    VkAccessFlags access = VK_ACCESS_MEMORY_READ_BIT | VK_ACCESS_MEMORY_WRITE_BIT;
};

template <typename T>
unsigned long long NonDispatchableHandle(T handle) {
#if defined(VK_USE_64_BIT_PTR_DEFINES) && VK_USE_64_BIT_PTR_DEFINES
    return static_cast<unsigned long long>(reinterpret_cast<std::uintptr_t>(handle));
#else
    return static_cast<unsigned long long>(handle);
#endif
}

StageAccess StageAccessForLayout(VkImageLayout layout) {
    switch (layout) {
        case VK_IMAGE_LAYOUT_UNDEFINED:
            return {VK_PIPELINE_STAGE_TOP_OF_PIPE_BIT, 0};
        case VK_IMAGE_LAYOUT_GENERAL:
            return {VK_PIPELINE_STAGE_ALL_COMMANDS_BIT, VK_ACCESS_MEMORY_READ_BIT | VK_ACCESS_MEMORY_WRITE_BIT};
        case VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL:
            return {VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT,
                    VK_ACCESS_COLOR_ATTACHMENT_READ_BIT | VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT};
        case VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL:
            return {VK_PIPELINE_STAGE_FRAGMENT_SHADER_BIT, VK_ACCESS_SHADER_READ_BIT};
        case VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL:
            return {VK_PIPELINE_STAGE_TRANSFER_BIT, VK_ACCESS_TRANSFER_READ_BIT};
        case VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL:
            return {VK_PIPELINE_STAGE_TRANSFER_BIT, VK_ACCESS_TRANSFER_WRITE_BIT};
        case VK_IMAGE_LAYOUT_PRESENT_SRC_KHR:
            return {VK_PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT, 0};
        default:
            return {VK_PIPELINE_STAGE_ALL_COMMANDS_BIT, VK_ACCESS_MEMORY_READ_BIT | VK_ACCESS_MEMORY_WRITE_BIT};
    }
}

const char* AccessName(UnityVulkanResourceAccessMode mode) {
    switch (mode) {
        case kUnityVulkanResourceAccess_ObserveOnly:
            return "ObserveOnly";
        case kUnityVulkanResourceAccess_PipelineBarrier:
            return "PipelineBarrier";
        case kUnityVulkanResourceAccess_Recreate:
            return "Recreate";
        default:
            return "Unknown";
    }
}

bool AccessNativeTexture(void* nativeTexture,
                         VkImageLayout layout,
                         VkPipelineStageFlags stage,
                         VkAccessFlags access,
                         UnityVulkanResourceAccessMode mode,
                         UnityVulkanImage& image,
                         uint64_t renderSerial,
                         const char* label) {
    if (gVulkan == nullptr || gVulkan->AccessTexture == nullptr) {
        MILESTROLOG_ERROR("Milestro Vulkan AccessTexture is unavailable during {} on event {}.",
                          label,
                          renderSerial);
        return false;
    }

    image = {};
    const bool ok = gVulkan->AccessTexture(nativeTexture,
                                           UnityVulkanWholeImage,
                                           layout,
                                           stage,
                                           access,
                                           mode,
                                           &image);
    MILESTRO_RENDER_LOG_INFO("Milestro Vulkan {} AccessTexture event={} ok={} mode={} requestedLayout={} "
                     "requestedStage=0x{:x} requestedAccess=0x{:x} image=0x{:x} returnedLayout={} "
                     "format={} extent={}x{}x{} samples={} mipCount={} layers={}.",
                     label,
                     renderSerial,
                     ok ? 1 : 0,
                     AccessName(mode),
                     static_cast<int>(layout),
                     static_cast<unsigned int>(stage),
                     static_cast<unsigned int>(access),
                     NonDispatchableHandle(image.image),
                     static_cast<int>(image.layout),
                     static_cast<int>(image.format),
                     static_cast<unsigned int>(image.extent.width),
                     static_cast<unsigned int>(image.extent.height),
                     static_cast<unsigned int>(image.extent.depth),
                     static_cast<int>(image.samples),
                     image.mipCount,
                     image.layers);
    return ok;
}

bool LogRecordingState(uint64_t renderSerial, const char* label) {
    if (gVulkan == nullptr || gVulkan->CommandRecordingState == nullptr) {
        MILESTROLOG_ERROR("Milestro Vulkan CommandRecordingState is unavailable during {} on event {}.",
                          label,
                          renderSerial);
        return false;
    }

    UnityVulkanRecordingState state = {};
    const bool ok = gVulkan->CommandRecordingState(&state, kUnityVulkanGraphicsQueueAccess_DontCare);
    MILESTRO_RENDER_LOG_INFO("Milestro Vulkan {} CommandRecordingState event={} ok={} commandBuffer={} level={} "
                     "renderPass={} framebuffer={} subPass={} frame={} safeFrame={}.",
                     label,
                     renderSerial,
                     ok ? 1 : 0,
                     static_cast<void*>(state.commandBuffer),
                     static_cast<int>(state.commandBufferLevel),
                     NonDispatchableHandle(state.renderPass),
                     NonDispatchableHandle(state.framebuffer),
                     state.subPassIndex,
                     static_cast<unsigned long long>(state.currentFrameNumber),
                     static_cast<unsigned long long>(state.safeFrameNumber));
    return ok;
}

void ConfigureEvent(int renderEventId) {
    if (gVulkan == nullptr || renderEventId < 0) {
        return;
    }

    UnityVulkanPluginEventConfig config = {};
    config.renderPassPrecondition = kUnityVulkanRenderPass_EnsureOutside;
    config.graphicsQueueAccess = kUnityVulkanGraphicsQueueAccess_DontCare;
    config.flags = kUnityVulkanEventConfigFlag_EnsurePreviousFrameSubmission |
                   kUnityVulkanEventConfigFlag_ModifiesCommandBuffersState;
    gVulkan->ConfigureEvent(renderEventId, &config);
    MILESTRO_RENDER_LOG_INFO("Configured Milestro Vulkan render event {}: renderPassPrecondition=EnsureOutside, "
                     "graphicsQueueAccess=DontCare, flags=0x{:x}.",
                     renderEventId,
                     static_cast<unsigned int>(config.flags));
}

void LogHeaderContract() {
    if (gLoggedHeaderContract) {
        return;
    }

    MILESTRO_RENDER_LOG_INFO("Milestro Vulkan PluginAPI contract: IUnityGraphicsVulkan::Instance() returns "
                     "UnityVulkanInstance with instance/physicalDevice/device/graphicsQueue/queueFamilyIndex; "
                     "AccessTexture requires desired layout/stage/access/mode and invalidates "
                     "CommandRecordingState; resource access must not run in graphicsQueueAccess=Allow or "
                     "AccessQueue callbacks.");
    gLoggedHeaderContract = true;
}

} // namespace

void OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType,
                           IUnityInterfaces* unityInterfaces,
                           UnityGfxRenderer renderer,
                           int renderEventId) {
    if (eventType == kUnityGfxDeviceEventShutdown || renderer != kUnityGfxRendererVulkan) {
        gVulkan = nullptr;
        return;
    }

    if (eventType != kUnityGfxDeviceEventInitialize) {
        return;
    }

    gVulkan = unityInterfaces != nullptr ? unityInterfaces->Get<IUnityGraphicsVulkan>() : nullptr;
    if (gVulkan == nullptr) {
        MILESTROLOG_ERROR("Unity Vulkan graphics interface is unavailable.");
        return;
    }

    LogHeaderContract();
    UnityVulkanInstance instance = gVulkan->Instance();
    MILESTRO_RENDER_LOG_INFO("Milestro Vulkan instance identity: instance={}, physicalDevice={}, device={}, queue={}, "
                     "queueFamilyIndex={}, hasGetInstanceProcAddr={}.",
                     static_cast<void*>(instance.instance),
                     static_cast<void*>(instance.physicalDevice),
                     static_cast<void*>(instance.device),
                     static_cast<void*>(instance.graphicsQueue),
                     instance.queueFamilyIndex,
                     instance.getInstanceProcAddr != nullptr ? 1 : 0);
    ConfigureEvent(renderEventId);
}

int64_t Render(const MilestroUnityRenderSubmission& submission) {
    const auto& payload = submission.target;
    const uint64_t renderSerial = ++gRenderSerial;

    if (payload.width <= 0 || payload.height <= 0) {
        MILESTROLOG_ERROR("Invalid Milestro Vulkan render payload size on event {}.", renderSerial);
        return MILESTRO_API_RET_FAILED;
    }

    if (payload.msaaSamples != 1) {
        MILESTROLOG_ERROR("Milestro Vulkan RenderTexture MSAA is not implemented yet: {} samples.",
                          payload.msaaSamples);
        return MILESTRO_API_RET_FAILED;
    }

    if (payload.handleKind != static_cast<int32_t>(MilestroUnityRenderTextureHandleKind::NativeTexture)) {
        MILESTROLOG_ERROR("Milestro Vulkan render target requires NativeTexture handleKind, got {}.",
                          payload.handleKind);
        return MILESTRO_API_RET_FAILED;
    }

    if (payload.nativeTextureHandle == nullptr) {
        MILESTROLOG_ERROR("Milestro Vulkan render target native texture handle is null.");
        return MILESTRO_API_RET_FAILED;
    }

    LogHeaderContract();
    UnityVulkanInstance instance = gVulkan != nullptr ? gVulkan->Instance() : UnityVulkanInstance {};
    MILESTRO_RENDER_LOG_INFO("Milestro Vulkan contract spike event={} payloadSize={}x{}, nativeTexture={}, device={}, "
                     "queue={}, queueFamilyIndex={}.",
                     renderSerial,
                     payload.width,
                     payload.height,
                     payload.nativeTextureHandle,
                     static_cast<void*>(instance.device),
                     static_cast<void*>(instance.graphicsQueue),
                     instance.queueFamilyIndex);

    UnityVulkanImage observed = {};
    if (!AccessNativeTexture(payload.nativeTextureHandle,
                             VK_IMAGE_LAYOUT_UNDEFINED,
                             VK_PIPELINE_STAGE_TOP_OF_PIPE_BIT,
                             0,
                             kUnityVulkanResourceAccess_ObserveOnly,
                             observed,
                             renderSerial,
                             "observe")) {
        return MILESTRO_API_RET_FAILED;
    }

    UnityVulkanImage transferTarget = {};
    if (!AccessNativeTexture(payload.nativeTextureHandle,
                             VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                             VK_PIPELINE_STAGE_TRANSFER_BIT,
                             VK_ACCESS_TRANSFER_WRITE_BIT,
                             kUnityVulkanResourceAccess_PipelineBarrier,
                             transferTarget,
                             renderSerial,
                             "transfer-dst")) {
        return MILESTRO_API_RET_FAILED;
    }

    if (!LogRecordingState(renderSerial, "after-transfer-dst-access")) {
        return MILESTRO_API_RET_FAILED;
    }

    const VkImageLayout restoreLayout = observed.layout == VK_IMAGE_LAYOUT_UNDEFINED
                                            ? VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL
                                            : observed.layout;
    StageAccess restoreAccess = StageAccessForLayout(restoreLayout);
    UnityVulkanImage restored = {};
    if (!AccessNativeTexture(payload.nativeTextureHandle,
                             restoreLayout,
                             restoreAccess.stage,
                             restoreAccess.access,
                             kUnityVulkanResourceAccess_PipelineBarrier,
                             restored,
                             renderSerial,
                             "restore-observed-layout")) {
        return MILESTRO_API_RET_FAILED;
    }

    if (!LogRecordingState(renderSerial, "after-restore-access")) {
        return MILESTRO_API_RET_FAILED;
    }

    MILESTRO_RENDER_LOG_WARN("Milestro Vulkan contract spike completed without Skia drawing or vkCmdCopyImage on event {}.",
                     renderSerial);
    return MILESTRO_API_RET_OK;
}

} // namespace milestro::unity_render::vulkan
