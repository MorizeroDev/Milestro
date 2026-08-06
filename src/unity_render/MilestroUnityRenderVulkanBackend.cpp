#include "unity_render/MilestroUnityRenderVulkanBackend.h"

#include "game/milestro_game_retcode.h"
#include "unity_render/MilestroUnityRenderTextureHandleKind.h"
#include "unity_render/MilestroUnityRenderSubmissionDraw.h"

#include <IUnityGraphicsVulkan.h>
#include <cstdint>
#include <functional>

#include "unity_render/MilestroUnityRenderLog.h"

#include "include/core/SkCanvas.h"
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

#include <android/log.h>
#include <dlfcn.h>

#define ALOGE(...) __android_log_print(ANDROID_LOG_ERROR, "MilestroVulkan", __VA_ARGS__)
#define ALOGI(...) __android_log_print(ANDROID_LOG_INFO, "MilestroVulkan", __VA_ARGS__)

namespace milestro::unity_render::vulkan {

namespace {

IUnityGraphicsVulkan* gVulkan = nullptr;
bool gLoggedHeaderContract = false;
uint64_t gRenderSerial = 0;

sk_sp<GrDirectContext> gDirectContext;
UnityVulkanInstance gCachedInstance{};
skgpu::VulkanExtensions gVkExtensions;
IUnityInterfaces* gUnityInterfacesCache = nullptr;
int gRenderEventIdCache = -1;

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
        ALOGE("AccessTexture unavailable during %s event %llu", label, (unsigned long long)renderSerial);
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
    ALOGI("%s AccessTexture event=%llu ok=%d image=%llu layout=%d format=%d %ux%u",
          label, (unsigned long long)renderSerial, ok ? 1 : 0, NonDispatchableHandle(image.image),
          (int)image.layout, (int)image.format, image.extent.width, image.extent.height);
    return ok;
}

bool LogRecordingState(uint64_t renderSerial, const char* label) {
    if (gVulkan == nullptr || gVulkan->CommandRecordingState == nullptr) {
        ALOGE("CommandRecordingState unavailable during %s event %llu", label, (unsigned long long)renderSerial);
        return false;
    }

    UnityVulkanRecordingState state = {};
    const bool ok = gVulkan->CommandRecordingState(&state, kUnityVulkanGraphicsQueueAccess_DontCare);
    ALOGI("%s CommandRecordingState event=%llu ok=%d cmdBuffer=%p", label, (unsigned long long)renderSerial, ok ? 1 : 0, (void*)state.commandBuffer);
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
    ALOGI("Configured Vulkan render event %d", renderEventId);
}

void LogHeaderContract() {
    if (gLoggedHeaderContract) {
        return;
    }

    ALOGI("Milestro Vulkan PluginAPI contract active.");
    gLoggedHeaderContract = true;
}

} // namespace

#include "include/gpu/vk/VulkanMemoryAllocator.h"

class SimpleVulkanMemoryAllocator : public skgpu::VulkanMemoryAllocator {
public:
    SimpleVulkanMemoryAllocator(VkDevice device, VkPhysicalDevice physDev, PFN_vkGetInstanceProcAddr getProc, VkInstance inst)
        : fDevice(device), fPhysicalDevice(physDev) {
        fAllocateMemory = reinterpret_cast<PFN_vkAllocateMemory>(getProc(inst, "vkAllocateMemory"));
        fFreeMemory = reinterpret_cast<PFN_vkFreeMemory>(getProc(inst, "vkFreeMemory"));
        fMapMemory = reinterpret_cast<PFN_vkMapMemory>(getProc(inst, "vkMapMemory"));
        fUnmapMemory = reinterpret_cast<PFN_vkUnmapMemory>(getProc(inst, "vkUnmapMemory"));
        fBindImageMemory = reinterpret_cast<PFN_vkBindImageMemory>(getProc(inst, "vkBindImageMemory"));
        fBindBufferMemory = reinterpret_cast<PFN_vkBindBufferMemory>(getProc(inst, "vkBindBufferMemory"));
        fGetImageMemoryRequirements = reinterpret_cast<PFN_vkGetImageMemoryRequirements>(getProc(inst, "vkGetImageMemoryRequirements"));
        fGetBufferMemoryRequirements = reinterpret_cast<PFN_vkGetBufferMemoryRequirements>(getProc(inst, "vkGetBufferMemoryRequirements"));
        fGetPhysicalDeviceMemoryProperties = reinterpret_cast<PFN_vkGetPhysicalDeviceMemoryProperties>(getProc(inst, "vkGetPhysicalDeviceMemoryProperties"));

        if (fGetPhysicalDeviceMemoryProperties) {
            fGetPhysicalDeviceMemoryProperties(physDev, &fMemProps);
        }
    }

    uint32_t findMemoryType(uint32_t typeFilter, VkMemoryPropertyFlags properties) const {
        for (uint32_t i = 0; i < fMemProps.memoryTypeCount; i++) {
            if ((typeFilter & (1 << i)) && (fMemProps.memoryTypes[i].propertyFlags & properties) == properties) {
                return i;
            }
        }
        for (uint32_t i = 0; i < fMemProps.memoryTypeCount; i++) {
            if (typeFilter & (1 << i)) {
                return i;
            }
        }
        return 0;
    }

    struct AllocBlock {
        VkDeviceMemory memory = VK_NULL_HANDLE;
        VkDeviceSize size = 0;
        uint32_t memoryTypeIndex = 0;
        VkMemoryPropertyFlags flags = 0;
        void* mapped = nullptr;
    };

    VkResult allocateImageMemory(VkImage image, uint32_t allocationPropertyFlags, skgpu::VulkanBackendMemory* memoryHandle) override {
        VkMemoryRequirements memReqs;
        fGetImageMemoryRequirements(fDevice, image, &memReqs);

        VkMemoryPropertyFlags reqFlags = VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT;
        uint32_t memType = findMemoryType(memReqs.memoryTypeBits, reqFlags);

        VkMemoryAllocateInfo allocInfo = {};
        allocInfo.sType = VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO;
        allocInfo.allocationSize = memReqs.size;
        allocInfo.memoryTypeIndex = memType;

        VkDeviceMemory mem = VK_NULL_HANDLE;
        VkResult res = fAllocateMemory(fDevice, &allocInfo, nullptr, &mem);
        if (res != VK_SUCCESS) return res;

        res = fBindImageMemory(fDevice, image, mem, 0);
        if (res != VK_SUCCESS) {
            fFreeMemory(fDevice, mem, nullptr);
            return res;
        }

        auto block = new AllocBlock();
        block->memory = mem;
        block->size = memReqs.size;
        block->memoryTypeIndex = memType;
        block->flags = fMemProps.memoryTypes[memType].propertyFlags;
        *memoryHandle = reinterpret_cast<skgpu::VulkanBackendMemory>(block);
        return VK_SUCCESS;
    }

    VkResult allocateBufferMemory(VkBuffer buffer, BufferUsage usage, uint32_t allocationPropertyFlags, skgpu::VulkanBackendMemory* memoryHandle) override {
        VkMemoryRequirements memReqs;
        fGetBufferMemoryRequirements(fDevice, buffer, &memReqs);

        VkMemoryPropertyFlags reqFlags = 0;
        if (usage == BufferUsage::kGpuOnly) {
            reqFlags = VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT;
        } else {
            reqFlags = VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT | VK_MEMORY_PROPERTY_HOST_COHERENT_BIT;
        }

        uint32_t memType = findMemoryType(memReqs.memoryTypeBits, reqFlags);

        VkMemoryAllocateInfo allocInfo = {};
        allocInfo.sType = VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO;
        allocInfo.allocationSize = memReqs.size;
        allocInfo.memoryTypeIndex = memType;

        VkDeviceMemory mem = VK_NULL_HANDLE;
        VkResult res = fAllocateMemory(fDevice, &allocInfo, nullptr, &mem);
        if (res != VK_SUCCESS) return res;

        res = fBindBufferMemory(fDevice, buffer, mem, 0);
        if (res != VK_SUCCESS) {
            fFreeMemory(fDevice, mem, nullptr);
            return res;
        }

        auto block = new AllocBlock();
        block->memory = mem;
        block->size = memReqs.size;
        block->memoryTypeIndex = memType;
        block->flags = fMemProps.memoryTypes[memType].propertyFlags;
        *memoryHandle = reinterpret_cast<skgpu::VulkanBackendMemory>(block);
        return VK_SUCCESS;
    }

    void getAllocInfo(const skgpu::VulkanBackendMemory& memoryHandle, skgpu::VulkanAlloc* alloc) const override {
        auto block = reinterpret_cast<AllocBlock*>(memoryHandle);
        if (!block) return;
        alloc->fMemory = block->memory;
        alloc->fOffset = 0;
        alloc->fSize = block->size;
        alloc->fFlags = 0;
        if (block->flags & VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT) {
            alloc->fFlags |= skgpu::VulkanAlloc::kMappable_Flag;
        }
        if (!(block->flags & VK_MEMORY_PROPERTY_HOST_COHERENT_BIT)) {
            alloc->fFlags |= skgpu::VulkanAlloc::kNoncoherent_Flag;
        }
        alloc->fBackendMemory = memoryHandle;
    }

    VkResult mapMemory(const skgpu::VulkanBackendMemory& memoryHandle, void** data) override {
        auto block = reinterpret_cast<AllocBlock*>(memoryHandle);
        if (!block || !data) return VK_ERROR_INITIALIZATION_FAILED;
        if (block->mapped) {
            *data = block->mapped;
            return VK_SUCCESS;
        }
        VkResult res = fMapMemory(fDevice, block->memory, 0, block->size, 0, &block->mapped);
        if (res == VK_SUCCESS) {
            *data = block->mapped;
        }
        return res;
    }

    void unmapMemory(const skgpu::VulkanBackendMemory& memoryHandle) override {
        auto block = reinterpret_cast<AllocBlock*>(memoryHandle);
        if (!block || !block->mapped) return;
        fUnmapMemory(fDevice, block->memory);
        block->mapped = nullptr;
    }

    void freeMemory(const skgpu::VulkanBackendMemory& memoryHandle) override {
        auto block = reinterpret_cast<AllocBlock*>(memoryHandle);
        if (!block) return;
        if (block->mapped) {
            fUnmapMemory(fDevice, block->memory);
        }
        fFreeMemory(fDevice, block->memory, nullptr);
        delete block;
    }

    std::pair<uint64_t, uint64_t> totalAllocatedAndUsedMemory() const override {
        return {0, 0};
    }

private:
    VkDevice fDevice = VK_NULL_HANDLE;
    VkPhysicalDevice fPhysicalDevice = VK_NULL_HANDLE;
    VkPhysicalDeviceMemoryProperties fMemProps = {};
    PFN_vkAllocateMemory fAllocateMemory = nullptr;
    PFN_vkFreeMemory fFreeMemory = nullptr;
    PFN_vkMapMemory fMapMemory = nullptr;
    PFN_vkUnmapMemory fUnmapMemory = nullptr;
    PFN_vkBindImageMemory fBindImageMemory = nullptr;
    PFN_vkBindBufferMemory fBindBufferMemory = nullptr;
    PFN_vkGetImageMemoryRequirements fGetImageMemoryRequirements = nullptr;
    PFN_vkGetBufferMemoryRequirements fGetBufferMemoryRequirements = nullptr;
    PFN_vkGetPhysicalDeviceMemoryProperties fGetPhysicalDeviceMemoryProperties = nullptr;
};

bool EnsureInitialized(IUnityInterfaces* unityInterfaces, int renderEventId) {
    if (unityInterfaces != nullptr) {
        gUnityInterfacesCache = unityInterfaces;
    }
    if (renderEventId >= 0) {
        gRenderEventIdCache = renderEventId;
    }

    if (gVulkan == nullptr && gUnityInterfacesCache != nullptr) {
        gVulkan = gUnityInterfacesCache->Get<IUnityGraphicsVulkan>();
    }

    if (gVulkan == nullptr) {
        MILESTROLOG_ERROR("EnsureInitialized failed: gVulkan is null (interfacesCache={})", static_cast<void*>(gUnityInterfacesCache));
        return false;
    }

    UnityVulkanInstance instance = gVulkan->Instance();
    if (instance.instance == VK_NULL_HANDLE || instance.device == VK_NULL_HANDLE) {
        MILESTROLOG_ERROR("EnsureInitialized failed: inst={} dev={} queue={}", static_cast<void*>(instance.instance), static_cast<void*>(instance.device), static_cast<void*>(instance.graphicsQueue));
        return false;
    }

    if (!gDirectContext || gCachedInstance.instance != instance.instance || gCachedInstance.device != instance.device) {
        if (gDirectContext) {
            gDirectContext->abandonContext();
            gDirectContext.reset();
        }

        gCachedInstance = instance;

        skgpu::VulkanBackendContext backendCtx;
        backendCtx.fInstance = instance.instance;
        backendCtx.fPhysicalDevice = instance.physicalDevice;
        backendCtx.fDevice = instance.device;
        backendCtx.fQueue = instance.graphicsQueue;
        backendCtx.fGraphicsQueueIndex = instance.queueFamilyIndex;
        backendCtx.fMaxAPIVersion = 0;

        auto getProc = instance.getInstanceProcAddr;
        if (getProc == nullptr) {
            ALOGE("EnsureInitialized failed: getInstanceProcAddr is null!");
            return false;
        }

        backendCtx.fMemoryAllocator = sk_make_sp<SimpleVulkanMemoryAllocator>(instance.device, instance.physicalDevice, getProc, instance.instance);
        ALOGI("SimpleVulkanMemoryAllocator created successfully.");

        PFN_vkGetInstanceProcAddr systemGetInstanceProcAddr = nullptr;
        void* vulkanLib = dlopen("libvulkan.so", RTLD_NOW | RTLD_LOCAL);
        if (vulkanLib) {
            systemGetInstanceProcAddr = reinterpret_cast<PFN_vkGetInstanceProcAddr>(
                dlsym(vulkanLib, "vkGetInstanceProcAddr"));
        }
        if (systemGetInstanceProcAddr == nullptr) {
            systemGetInstanceProcAddr = getProc;
        }

        auto vkGetDeviceProcAddr = reinterpret_cast<PFN_vkGetDeviceProcAddr>(getProc(instance.instance, "vkGetDeviceProcAddr"));

        static auto FallbackEnumerateInstanceVersion = [](uint32_t* pApiVersion) -> VkResult {
            if (pApiVersion) {
                *pApiVersion = VK_API_VERSION_1_1;
            }
            return VK_SUCCESS;
        };

        backendCtx.fGetProc = [getProc, systemGetInstanceProcAddr, vkGetDeviceProcAddr, cachedInst = instance.instance, cachedDev = instance.device](const char* name, VkInstance inst, VkDevice dev) -> PFN_vkVoidFunction {
            PFN_vkVoidFunction result = nullptr;
            VkDevice targetDev = (dev != VK_NULL_HANDLE) ? dev : cachedDev;
            if (targetDev != VK_NULL_HANDLE && vkGetDeviceProcAddr != nullptr) {
                result = vkGetDeviceProcAddr(targetDev, name);
            }
            if (result == nullptr && inst == VK_NULL_HANDLE && dev == VK_NULL_HANDLE && systemGetInstanceProcAddr != nullptr) {
                result = systemGetInstanceProcAddr(VK_NULL_HANDLE, name);
            }
            if (result == nullptr) {
                VkInstance targetInst = (inst != VK_NULL_HANDLE) ? inst : cachedInst;
                if (targetInst != VK_NULL_HANDLE && getProc != nullptr) {
                    result = getProc(targetInst, name);
                }
            }
            if (result == nullptr && systemGetInstanceProcAddr != nullptr) {
                result = systemGetInstanceProcAddr(cachedInst, name);
            }
            if (result == nullptr && name != nullptr && strcmp(name, "vkEnumerateInstanceVersion") == 0) {
                ALOGI("Providing FallbackEnumerateInstanceVersion for Vulkan 1.0 compatibility");
                return reinterpret_cast<PFN_vkVoidFunction>(+FallbackEnumerateInstanceVersion);
            }
            if (result == nullptr) {
                ALOGE("fGetProc MISSING: '%s' (inst=%p, dev=%p)", name ? name : "null", static_cast<void*>(inst), static_cast<void*>(dev));
            }
            return result;
        };

        gVkExtensions.init(backendCtx.fGetProc, instance.instance, instance.physicalDevice, 0, nullptr, 0, nullptr);
        backendCtx.fVkExtensions = &gVkExtensions;

        gDirectContext = GrDirectContexts::MakeVulkan(backendCtx);
        if (!gDirectContext) {
            MILESTROLOG_ERROR("GrDirectContexts::MakeVulkan returned NULL!");
            return false;
        }

        MILESTROLOG_ERROR("Skia Vulkan GrDirectContext created successfully!");
        ConfigureEvent(gRenderEventIdCache);
    }

    return true;
}

void OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType,
                           IUnityInterfaces* unityInterfaces,
                           UnityGfxRenderer renderer,
                           int renderEventId) {
    if (eventType == kUnityGfxDeviceEventShutdown) {
        if (gDirectContext) {
            gDirectContext->abandonContext();
            gDirectContext.reset();
        }
        gCachedInstance = {};
        gVulkan = nullptr;
        gRenderEventIdCache = -1;
        return;
    }

    if (eventType != kUnityGfxDeviceEventInitialize) {
        return;
    }

    // Cache the render event ID for deferred initialization.
    // Do NOT call EnsureInitialized here — Unity's Vulkan device
    // may not be fully ready during UnityPluginLoad / early init.
    // Initialization is deferred to the first Render() call.
    if (renderEventId >= 0) {
        gRenderEventIdCache = renderEventId;
    }
    if (unityInterfaces != nullptr && gVulkan == nullptr) {
        gVulkan = unityInterfaces->Get<IUnityGraphicsVulkan>();
    }
}

int64_t Render(const MilestroUnityRenderSubmission& submission) {
    const auto& payload = submission.target;
    const uint64_t renderSerial = ++gRenderSerial;

    if (payload.width <= 0 || payload.height <= 0) {
        ALOGE("Failed: payload width or height <= 0 (%dx%d)", payload.width, payload.height);
        return MILESTRO_API_RET_FAILED;
    }

    if (payload.msaaSamples != 1) {
        ALOGE("Failed: msaaSamples != 1 (%d)", payload.msaaSamples);
        return MILESTRO_API_RET_FAILED;
    }

    if (payload.handleKind != static_cast<int32_t>(MilestroUnityRenderTextureHandleKind::NativeTexture)) {
        ALOGE("Failed: handleKind != NativeTexture (%d)", payload.handleKind);
        return MILESTRO_API_RET_FAILED;
    }

    if (payload.nativeTextureHandle == nullptr) {
        ALOGE("Failed: nativeTextureHandle is null");
        return MILESTRO_API_RET_FAILED;
    }
    
    if (!gDirectContext) {
        if (!EnsureInitialized(nullptr, -1)) {
            ALOGE("Failed: gDirectContext is null after EnsureInitialized");
            return MILESTRO_API_RET_FAILED;
        }
    }

    LogHeaderContract();
    UnityVulkanInstance instance = gVulkan != nullptr ? gVulkan->Instance() : UnityVulkanInstance {};
    ALOGI("Render event=%llu payloadSize=%dx%d, handle=%p, dev=%p, queue=%p, family=%u",
          (unsigned long long)renderSerial, payload.width, payload.height,
          payload.nativeTextureHandle, (void*)instance.device, (void*)instance.graphicsQueue, instance.queueFamilyIndex);

    UnityVulkanImage observed = {};
    if (!AccessNativeTexture(payload.nativeTextureHandle,
                             VK_IMAGE_LAYOUT_UNDEFINED,
                             VK_PIPELINE_STAGE_TOP_OF_PIPE_BIT,
                             0,
                             kUnityVulkanResourceAccess_ObserveOnly,
                             observed,
                             renderSerial,
                             "observe")) {
        ALOGE("Failed: AccessNativeTexture observe failed");
        return MILESTRO_API_RET_FAILED;
    }

    UnityVulkanImage colorTarget = {};
    if (!AccessNativeTexture(payload.nativeTextureHandle,
                             VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                             VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT,
                             VK_ACCESS_COLOR_ATTACHMENT_READ_BIT | VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT,
                             kUnityVulkanResourceAccess_PipelineBarrier,
                             colorTarget,
                             renderSerial,
                             "color-attachment")) {
        ALOGE("Failed: AccessNativeTexture color-attachment failed");
        return MILESTRO_API_RET_FAILED;
    }

    if (!LogRecordingState(renderSerial, "after-color-attachment-access")) {
        ALOGE("Failed: LogRecordingState failed");
        return MILESTRO_API_RET_FAILED;
    }

    gDirectContext->resetContext();

    GrVkImageInfo vkInfo;
    vkInfo.fImage = colorTarget.image;
    vkInfo.fAlloc = {};
    vkInfo.fImageTiling = colorTarget.tiling;
    vkInfo.fImageLayout = VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;
    vkInfo.fFormat = colorTarget.format;
    vkInfo.fImageUsageFlags = colorTarget.usage;
    vkInfo.fSampleCount = 1;
    vkInfo.fLevelCount = 1;
    vkInfo.fCurrentQueueFamily = instance.queueFamilyIndex;
    vkInfo.fSharingMode = VK_SHARING_MODE_EXCLUSIVE;

    GrBackendRenderTarget backendRT = GrBackendRenderTargets::MakeVk(payload.width, payload.height, vkInfo);

    SkColorType colorType = kRGBA_8888_SkColorType;
    sk_sp<SkColorSpace> colorSpace;
    if (colorTarget.format == VK_FORMAT_B8G8R8A8_UNORM || colorTarget.format == VK_FORMAT_B8G8R8A8_SRGB) {
        colorType = kBGRA_8888_SkColorType;
    } else if (colorTarget.format != VK_FORMAT_R8G8B8A8_UNORM && colorTarget.format != VK_FORMAT_R8G8B8A8_SRGB) {
        ALOGE("Unexpected format %d, defaulting to RGBA8888", (int)colorTarget.format);
    }

    if (colorTarget.format == VK_FORMAT_R8G8B8A8_SRGB || colorTarget.format == VK_FORMAT_B8G8R8A8_SRGB) {
        colorSpace = SkColorSpace::MakeSRGB();
    } else {
        if (payload.colorSpace == 1) {
            colorSpace = SkColorSpace::MakeSRGBLinear();
        } else {
            colorSpace = SkColorSpace::MakeSRGB();
        }
    }

    sk_sp<SkSurface> surface = SkSurfaces::WrapBackendRenderTarget(
            gDirectContext.get(), backendRT, kTopLeft_GrSurfaceOrigin, colorType, colorSpace, nullptr);

    bool drawSuccess = false;
    if (surface) {
        milestro::unity_render::DrawSubmission(surface->getCanvas(), submission);
        gDirectContext->flushAndSubmit(surface.get());
        drawSuccess = true;
        ALOGI("Skia Vulkan draw SUCCESS!");
    } else {
        ALOGE("Failed: SkSurfaces::WrapBackendRenderTarget returned NULL! size=%dx%d format=%d", payload.width, payload.height, (int)colorTarget.format);
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
        ALOGE("Failed: restore AccessNativeTexture failed");
        return MILESTRO_API_RET_FAILED;
    }

    if (!LogRecordingState(renderSerial, "after-restore-access")) {
        ALOGE("Failed: restore LogRecordingState failed");
        return MILESTRO_API_RET_FAILED;
    }

    return drawSuccess ? MILESTRO_API_RET_OK : MILESTRO_API_RET_FAILED;
}

} // namespace milestro::unity_render::vulkan
