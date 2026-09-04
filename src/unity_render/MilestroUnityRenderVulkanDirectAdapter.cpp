#include "unity_render/MilestroUnityRenderVulkanAdapter.h"

#include "unity_render/MilestroUnityRenderSubmissionDraw.h"

#include <cstdint>
#include <cstring>
#include <memory>

#include "unity_render/MilestroUnityRenderLog.h"

#include "include/core/SkColorSpace.h"
#include "include/core/SkSurface.h"
#include "include/gpu/ganesh/GrBackendSurface.h"
#include "include/gpu/ganesh/GrDirectContext.h"
#include "include/gpu/ganesh/SkSurfaceGanesh.h"
#include "include/gpu/ganesh/vk/GrVkBackendSurface.h"
#include "include/gpu/ganesh/vk/GrVkDirectContext.h"
#include "include/gpu/ganesh/vk/GrVkTypes.h"
#include "include/gpu/vk/VulkanBackendContext.h"
#include "include/gpu/vk/VulkanExtensions.h"
#include "src/gpu/GpuTypesPriv.h"
#include "src/gpu/vk/vulkanmemoryallocator/VulkanMemoryAllocatorPriv.h"

namespace milestro::unity_render::vulkan {

namespace {

VkResult VKAPI_PTR EnumerateVulkan10InstanceVersion(uint32_t* apiVersion) {
    if (apiVersion != nullptr) {
        *apiVersion = VK_API_VERSION_1_0;
    }
    return VK_SUCCESS;
}

} // namespace

PFN_vkVoidFunction ResolveInstanceProcWithVulkan10Fallback(const UnityVulkanInstance& instance, const char* name) {
    if (instance.getInstanceProcAddr == nullptr || name == nullptr) {
        return nullptr;
    }
    PFN_vkVoidFunction result = instance.getInstanceProcAddr(instance.instance, name);
    if (result == nullptr) {
        result = instance.getInstanceProcAddr(VK_NULL_HANDLE, name);
    }
    if (result == nullptr && std::strcmp(name, "vkEnumerateInstanceVersion") == 0) {
        return reinterpret_cast<PFN_vkVoidFunction>(&EnumerateVulkan10InstanceVersion);
    }
    return result;
}

namespace {

class DirectTargetState final : public VulkanTargetState {};

class DirectVulkanBackend final : public VulkanRenderBackend {
public:
    [[nodiscard]] VulkanBackendKind Kind() const noexcept override {
        return VulkanBackendKind::Direct;
    }
    [[nodiscard]] bool RequiresSubmitEvent() const noexcept override {
        return true;
    }

    void Initialize(IUnityGraphicsVulkan* vulkan, const UnityVulkanInstance& instance) override {
        Shutdown();
        vulkan_ = vulkan;
        instance_ = instance;
    }

    void Shutdown() override {
        if (directContext_ != nullptr) {
            // Direct mode deliberately preserves the existing no-wait abandon behavior.
            // It remains an explicit higher-performance/higher-lifecycle-risk choice.
            directContext_->abandonContext();
            directContext_.reset();
        }
        instance_ = {};
        vulkan_ = nullptr;
    }

    [[nodiscard]] std::unique_ptr<VulkanTargetState> CreateTarget(uint64_t, std::size_t) override {
        return std::make_unique<DirectTargetState>();
    }

    bool RetireTarget(std::unique_ptr<VulkanTargetState>& state) override {
        state.reset();
        return true;
    }
    void DestroyTargetImmediately(std::unique_ptr<VulkanTargetState>) override {
    }
    [[nodiscard]] std::size_t PendingRetirementCount() const noexcept override {
        return 0;
    }
    [[nodiscard]] bool HasPendingRetirements() const noexcept override {
        return false;
    }
    bool CollectRetired(uint64_t, bool) override {
        return false;
    }

    VulkanPrepareResult Prepare(VulkanTarget& target,
                                MilestroUnityRenderSubmission& submission,
                                PreparedVulkanSubmission& prepared) override {
        if (vulkan_ == nullptr || vulkan_->AccessTexture == nullptr || vulkan_->Instance == nullptr) {
            return {};
        }

        UnityVulkanImage image{};
        if (!vulkan_->AccessTexture(target.nativeTexture,
                                    UnityVulkanWholeImage,
                                    VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                                    VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT,
                                    VK_ACCESS_COLOR_ATTACHMENT_READ_BIT | VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT,
                                    kUnityVulkanResourceAccess_PipelineBarrier,
                                    &image) ||
            image.image == VK_NULL_HANDLE || image.samples != VK_SAMPLE_COUNT_1_BIT ||
            image.extent.width != static_cast<uint32_t>(target.width) ||
            image.extent.height != static_cast<uint32_t>(target.height) ||
            (image.usage & VK_IMAGE_USAGE_COLOR_ATTACHMENT_BIT) == 0 || !IsSupportedFormat(image.format)) {
            MILESTROLOG_ERROR("Milestro Vulkan direct target is unsupported or AccessTexture failed.");
            return {};
        }

        const UnityVulkanInstance current = vulkan_->Instance();
        if (!SameDevice(current, instance_)) {
            MILESTROLOG_ERROR("Milestro Vulkan device changed while preparing a direct submission.");
            return {};
        }

        prepared.target = &target;
        prepared.submission = &submission;
        prepared.image = image;
        prepared.instance = current;
        return {MilestroUnityRenderSubmissionStatus::Pending, true};
    }

    MilestroUnityRenderSubmissionStatus Submit(PreparedVulkanSubmission& prepared) override {
        if (prepared.submission == nullptr || prepared.target == nullptr || !SameDevice(prepared.instance, instance_) ||
            !EnsureContext()) {
            return MilestroUnityRenderSubmissionStatus::Failed;
        }

        const MilestroUnityRenderTargetPayload& target = prepared.submission->target;
        directContext_->resetContext();
        GrVkImageInfo imageInfo{};
        imageInfo.fImage = prepared.image.image;
        imageInfo.fAlloc = {};
        imageInfo.fImageTiling = prepared.image.tiling;
        imageInfo.fImageLayout = VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;
        imageInfo.fFormat = prepared.image.format;
        imageInfo.fImageUsageFlags = prepared.image.usage;
        imageInfo.fSampleCount = 1;
        imageInfo.fLevelCount = 1;
        imageInfo.fCurrentQueueFamily = prepared.instance.queueFamilyIndex;
        imageInfo.fSharingMode = VK_SHARING_MODE_EXCLUSIVE;

        const GrBackendRenderTarget backendTarget =
                GrBackendRenderTargets::MakeVk(target.width, target.height, imageInfo);
        sk_sp<SkSurface> surface =
                SkSurfaces::WrapBackendRenderTarget(directContext_.get(),
                                                    backendTarget,
                                                    kTopLeft_GrSurfaceOrigin,
                                                    ColorTypeForFormat(prepared.image.format),
                                                    ColorSpaceForTarget(target, prepared.image.format),
                                                    nullptr);
        if (surface == nullptr) {
            MILESTROLOG_ERROR("Milestro failed to wrap a Unity Vulkan direct target.");
            return MilestroUnityRenderSubmissionStatus::Failed;
        }

        DrawSubmission(surface->getCanvas(), *prepared.submission);
        directContext_->flush(surface.get());
        if (!directContext_->submit(GrSyncCpu::kNo)) {
            MILESTROLOG_ERROR("Milestro Vulkan direct queue submission failed.");
            return MilestroUnityRenderSubmissionStatus::Failed;
        }
        return MilestroUnityRenderSubmissionStatus::Drawn;
    }

private:
    static bool SameDevice(const UnityVulkanInstance& left, const UnityVulkanInstance& right) {
        return left.instance != VK_NULL_HANDLE && left.instance == right.instance &&
               left.physicalDevice == right.physicalDevice && left.device == right.device &&
               left.graphicsQueue == right.graphicsQueue && left.queueFamilyIndex == right.queueFamilyIndex;
    }

    static bool IsSupportedFormat(VkFormat format) {
        return format == VK_FORMAT_R8G8B8A8_UNORM || format == VK_FORMAT_R8G8B8A8_SRGB ||
               format == VK_FORMAT_B8G8R8A8_UNORM || format == VK_FORMAT_B8G8R8A8_SRGB;
    }

    static SkColorType ColorTypeForFormat(VkFormat format) {
        return format == VK_FORMAT_B8G8R8A8_UNORM || format == VK_FORMAT_B8G8R8A8_SRGB ? kBGRA_8888_SkColorType
                                                                                       : kRGBA_8888_SkColorType;
    }

    static sk_sp<SkColorSpace> ColorSpaceForTarget(const MilestroUnityRenderTargetPayload& target, VkFormat format) {
        if (format == VK_FORMAT_R8G8B8A8_SRGB || format == VK_FORMAT_B8G8R8A8_SRGB) {
            return SkColorSpace::MakeSRGB();
        }
        return target.colorSpace == 1 ? SkColorSpace::MakeSRGBLinear() : SkColorSpace::MakeSRGB();
    }

    bool EnsureContext() {
        if (directContext_ != nullptr && !directContext_->abandoned()) {
            return true;
        }
        if (instance_.getInstanceProcAddr == nullptr || instance_.device == VK_NULL_HANDLE) {
            return false;
        }

        skgpu::VulkanBackendContext context;
        context.fInstance = instance_.instance;
        context.fPhysicalDevice = instance_.physicalDevice;
        context.fDevice = instance_.device;
        context.fQueue = instance_.graphicsQueue;
        context.fGraphicsQueueIndex = instance_.queueFamilyIndex;
        context.fMaxAPIVersion = 0;

        const auto getInstanceProc = instance_.getInstanceProcAddr;
        const auto getDeviceProc =
                reinterpret_cast<PFN_vkGetDeviceProcAddr>(getInstanceProc(instance_.instance, "vkGetDeviceProcAddr"));
        const VkInstance cachedInstance = instance_.instance;
        const VkDevice cachedDevice = instance_.device;
        context.fGetProc = [getInstanceProc, getDeviceProc, cachedInstance, cachedDevice](
                                   const char* name,
                                   VkInstance requestedInstance,
                                   VkDevice requestedDevice) -> PFN_vkVoidFunction {
            const VkDevice device = requestedDevice != VK_NULL_HANDLE ? requestedDevice : cachedDevice;
            PFN_vkVoidFunction result =
                    device != VK_NULL_HANDLE && getDeviceProc != nullptr ? getDeviceProc(device, name) : nullptr;
            if (result == nullptr && requestedInstance == VK_NULL_HANDLE && requestedDevice == VK_NULL_HANDLE) {
                result = getInstanceProc(VK_NULL_HANDLE, name);
            }
            if (result == nullptr) {
                const VkInstance instance = requestedInstance != VK_NULL_HANDLE ? requestedInstance : cachedInstance;
                result = getInstanceProc(instance, name);
            }
            if (result == nullptr) {
                result = getInstanceProc(cachedInstance, name);
            }
            if (result == nullptr) {
                UnityVulkanInstance fallbackInstance{};
                fallbackInstance.instance = cachedInstance;
                fallbackInstance.getInstanceProcAddr = getInstanceProc;
                return ResolveInstanceProcWithVulkan10Fallback(fallbackInstance, name);
            }
            return result;
        };

        extensions_.init(context.fGetProc, instance_.instance, instance_.physicalDevice, 0, nullptr, 0, nullptr);
        context.fVkExtensions = &extensions_;
        context.fMemoryAllocator = skgpu::VulkanMemoryAllocators::Make(context, skgpu::ThreadSafe::kNo);
        if (context.fMemoryAllocator == nullptr) {
            MILESTROLOG_ERROR("Milestro Vulkan direct allocator creation failed.");
            return false;
        }

        directContext_ = GrDirectContexts::MakeVulkan(context);
        if (directContext_ == nullptr) {
            MILESTROLOG_ERROR("Milestro Vulkan direct context creation failed.");
            return false;
        }
        return true;
    }

    IUnityGraphicsVulkan* vulkan_ = nullptr;
    UnityVulkanInstance instance_{};
    skgpu::VulkanExtensions extensions_;
    sk_sp<GrDirectContext> directContext_;
};

DirectVulkanBackend gDirectBackend;

} // namespace

VulkanRenderBackend& DirectBackend() {
    return gDirectBackend;
}

} // namespace milestro::unity_render::vulkan
