#include "unity_render/MilestroUnityRenderVulkanBackend.h"

#include "game/milestro_game_retcode.h"
#include "unity_render/MilestroUnityRenderTextureHandleKind.h"
#include "unity_render/MilestroUnityRenderSubmissionDraw.h"

#include <IUnityGraphicsVulkan.h>
#include <atomic>
#include <cstdint>
#include <cstring>
#include <functional>
#include <mutex>
#include <new>

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
bool gEventConfigured = false;
std::mutex gVulkanQueueMutex;

struct VulkanQueueSubmission;
VulkanQueueSubmission* gPendingQueueHead = nullptr;
bool gVulkanDeviceActive = false;

template <typename T>
unsigned long long NonDispatchableHandle(T handle) {
#if defined(VK_USE_64_BIT_PTR_DEFINES) && VK_USE_64_BIT_PTR_DEFINES
    return static_cast<unsigned long long>(reinterpret_cast<std::uintptr_t>(handle));
#else
    return static_cast<unsigned long long>(handle);
#endif
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
        MILESTROLOG_ERROR("Milestro Vulkan AccessTexture is unavailable during {} event {}.",
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
    MILESTRO_RENDER_LOG_INFO("Milestro Vulkan {} AccessTexture event={} ok={} mode={} image={} layout={} format={} {}x{}.",
                             label,
                             renderSerial,
                             ok ? 1 : 0,
                             AccessName(mode),
                             NonDispatchableHandle(image.image),
                             static_cast<int>(image.layout),
                             static_cast<int>(image.format),
                             image.extent.width,
                             image.extent.height);
    return ok;
}

bool ConfigureEvent(int renderEventId) {
    if (gVulkan == nullptr || gVulkan->ConfigureEvent == nullptr || renderEventId < 0) {
        MILESTROLOG_ERROR("Milestro Vulkan render event cannot be configured: interface or event id is unavailable.");
        return false;
    }

    UnityVulkanPluginEventConfig config = {};
    config.renderPassPrecondition = kUnityVulkanRenderPass_EnsureOutside;
    config.graphicsQueueAccess = kUnityVulkanGraphicsQueueAccess_DontCare;
    config.flags = kUnityVulkanEventConfigFlag_EnsurePreviousFrameSubmission |
                   kUnityVulkanEventConfigFlag_ModifiesCommandBuffersState;
    gVulkan->ConfigureEvent(renderEventId, &config);
    MILESTRO_RENDER_LOG_INFO("Configured Milestro Vulkan render event {} for Unity resource access.", renderEventId);
    return true;
}

void LogHeaderContract() {
    if (gLoggedHeaderContract) {
        return;
    }

    MILESTRO_RENDER_LOG_INFO("Milestro Vulkan PluginAPI contract active.");
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
            MILESTROLOG_ERROR("EnsureInitialized failed: Unity getInstanceProcAddr is null.");
            return false;
        }

        backendCtx.fMemoryAllocator = sk_make_sp<SimpleVulkanMemoryAllocator>(instance.device, instance.physicalDevice, getProc, instance.instance);
        MILESTRO_RENDER_LOG_INFO("SimpleVulkanMemoryAllocator created successfully.");

        // Unity's interface supplies the loader entry point on every supported
        // platform. Do not open a platform-specific Vulkan shared library here.
        PFN_vkGetInstanceProcAddr systemGetInstanceProcAddr = getProc;

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
                MILESTRO_RENDER_LOG_INFO("Providing fallback vkEnumerateInstanceVersion for Vulkan 1.0 compatibility.");
                return reinterpret_cast<PFN_vkVoidFunction>(+FallbackEnumerateInstanceVersion);
            }
            if (result == nullptr) {
                MILESTROLOG_ERROR("Vulkan proc '{}' is unavailable (instance={}, device={}).",
                                  name != nullptr ? name : "null",
                                  static_cast<void*>(inst),
                                  static_cast<void*>(dev));
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

        MILESTRO_RENDER_LOG_INFO("Skia Vulkan GrDirectContext created successfully.");
    }

    return true;
}

namespace {

struct VulkanQueueSubmission {
    MilestroUnityRenderSubmission* submission = nullptr;
    UnityVulkanImage colorTarget{};
    UnityVulkanInstance instance{};
    uint64_t renderSerial = 0;
    RenderCompletionCallback completionCallback = nullptr;
    void* completionUserData = nullptr;
    std::atomic<bool> completionSent{false};
    bool canceled = false;
    bool linked = false;
    VulkanQueueSubmission* previous = nullptr;
    VulkanQueueSubmission* next = nullptr;
};

void LinkPendingQueueSubmission(VulkanQueueSubmission* queued) {
    if (queued == nullptr || queued->linked) {
        return;
    }

    queued->previous = nullptr;
    queued->next = gPendingQueueHead;
    if (gPendingQueueHead != nullptr) {
        gPendingQueueHead->previous = queued;
    }
    gPendingQueueHead = queued;
    queued->linked = true;
}

void UnlinkPendingQueueSubmission(VulkanQueueSubmission* queued) {
    if (queued == nullptr || !queued->linked) {
        return;
    }

    if (queued->previous != nullptr) {
        queued->previous->next = queued->next;
    } else {
        gPendingQueueHead = queued->next;
    }
    if (queued->next != nullptr) {
        queued->next->previous = queued->previous;
    }

    queued->previous = nullptr;
    queued->next = nullptr;
    queued->linked = false;
}

bool DrawVulkanSubmission(const VulkanQueueSubmission& queued, GrDirectContext* context) {
    if (queued.submission == nullptr || context == nullptr || context->abandoned()) {
        return false;
    }

    const auto& payload = queued.submission->target;
    context->resetContext();

    GrVkImageInfo vkInfo;
    vkInfo.fImage = queued.colorTarget.image;
    vkInfo.fAlloc = {};
    vkInfo.fImageTiling = queued.colorTarget.tiling;
    vkInfo.fImageLayout = VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;
    vkInfo.fFormat = queued.colorTarget.format;
    vkInfo.fImageUsageFlags = queued.colorTarget.usage;
    vkInfo.fSampleCount = 1;
    vkInfo.fLevelCount = 1;
    vkInfo.fCurrentQueueFamily = queued.instance.queueFamilyIndex;
    vkInfo.fSharingMode = VK_SHARING_MODE_EXCLUSIVE;

    GrBackendRenderTarget backendRT =
        GrBackendRenderTargets::MakeVk(payload.width, payload.height, vkInfo);

    SkColorType colorType = kRGBA_8888_SkColorType;
    if (queued.colorTarget.format == VK_FORMAT_B8G8R8A8_UNORM ||
        queued.colorTarget.format == VK_FORMAT_B8G8R8A8_SRGB) {
        colorType = kBGRA_8888_SkColorType;
    } else if (queued.colorTarget.format != VK_FORMAT_R8G8B8A8_UNORM &&
               queued.colorTarget.format != VK_FORMAT_R8G8B8A8_SRGB) {
        MILESTROLOG_ERROR("Unexpected Vulkan RenderTexture format {}; defaulting to RGBA8888.",
                          static_cast<int>(queued.colorTarget.format));
    }

    sk_sp<SkColorSpace> colorSpace;
    if (queued.colorTarget.format == VK_FORMAT_R8G8B8A8_SRGB ||
        queued.colorTarget.format == VK_FORMAT_B8G8R8A8_SRGB) {
        colorSpace = SkColorSpace::MakeSRGB();
    } else if (payload.colorSpace == 1) {
        colorSpace = SkColorSpace::MakeSRGBLinear();
    } else {
        colorSpace = SkColorSpace::MakeSRGB();
    }

    sk_sp<SkSurface> surface = SkSurfaces::WrapBackendRenderTarget(
        context, backendRT, kTopLeft_GrSurfaceOrigin, colorType, colorSpace, nullptr);
    if (surface == nullptr) {
        MILESTROLOG_ERROR("Failed to wrap Unity Vulkan RenderTexture as a Skia surface ({}x{}, format={}).",
                          payload.width,
                          payload.height,
                          static_cast<int>(queued.colorTarget.format));
        return false;
    }

    milestro::unity_render::DrawSubmission(surface->getCanvas(), *queued.submission);
    context->flushAndSubmit(surface.get());
    MILESTRO_RENDER_LOG_INFO("Skia Vulkan draw completed for render event {}.", queued.renderSerial);
    return true;
}

void PublishQueueCompletion(VulkanQueueSubmission* queued,
                            MilestroUnityRenderSubmissionStatus status) {
    if (queued == nullptr) {
        return;
    }

    bool expected = false;
    if (!queued->completionSent.compare_exchange_strong(expected,
                                                        true,
                                                        std::memory_order_acq_rel)) {
        return;
    }

    if (queued->completionCallback != nullptr) {
        queued->completionCallback(queued->submission, status, queued->completionUserData);
    }
}

void UNITY_INTERFACE_API OnVulkanQueueAccess(int eventId, void* userData) {
    (void) eventId;
    auto* queued = static_cast<VulkanQueueSubmission*>(userData);
    if (queued == nullptr) {
        return;
    }

    // Ganesh contexts are not thread-safe. Keep drawing, completion publication,
    // and pending-list removal serialized with device shutdown so Unload cannot
    // return while a queue callback is still using plugin state.
    std::lock_guard queueLock(gVulkanQueueMutex);
    if (!queued->completionSent.load(std::memory_order_acquire)) {
        MilestroUnityRenderSubmissionStatus status = MilestroUnityRenderSubmissionStatus::Failed;
        if (!queued->canceled && gVulkanDeviceActive && gVulkan != nullptr &&
            EnsureInitialized(nullptr, -1) && gDirectContext != nullptr) {
            const UnityVulkanInstance current = gVulkan->Instance();
            if (current.device == queued->instance.device &&
                current.graphicsQueue == queued->instance.graphicsQueue) {
                if (DrawVulkanSubmission(*queued, gDirectContext.get())) {
                    status = MilestroUnityRenderSubmissionStatus::Drawn;
                }
            } else {
                MILESTROLOG_ERROR("Unity Vulkan device or graphics queue changed before queued submission {}.",
                                  queued->renderSerial);
            }
        } else {
            MILESTROLOG_ERROR("Unity Vulkan queue callback ran without an initialized Skia context.");
        }

        PublishQueueCompletion(queued, status);
    }

    UnlinkPendingQueueSubmission(queued);
    delete queued;
}

} // namespace

void OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType,
                           IUnityInterfaces* unityInterfaces,
                           UnityGfxRenderer renderer,
                           int renderEventId) {
    if (eventType == kUnityGfxDeviceEventShutdown) {
        std::lock_guard queueLock(gVulkanQueueMutex);
        gVulkanDeviceActive = false;

        // AccessQueue has no cancellation API. Publish failure now, but keep
        // each callback record alive so a callback already accepted by Unity
        // can safely observe completionSent, unlink itself, and delete it.
        int pendingCount = 0;
        for (VulkanQueueSubmission* queued = gPendingQueueHead;
             queued != nullptr;
             queued = queued->next) {
            queued->canceled = true;
            if (!queued->completionSent.load(std::memory_order_acquire)) {
                PublishQueueCompletion(queued, MilestroUnityRenderSubmissionStatus::Failed);
                ++pendingCount;
            }
        }
        if (pendingCount != 0) {
            MILESTRO_RENDER_LOG_WARN("Canceled {} pending Milestro Vulkan queue submission(s) during device shutdown.",
                                     pendingCount);
        }

        if (gDirectContext) {
            gDirectContext->abandonContext();
            gDirectContext.reset();
        }
        gCachedInstance = {};
        gVulkan = nullptr;
        gUnityInterfacesCache = nullptr;
        gRenderEventIdCache = -1;
        gEventConfigured = false;
        gLoggedHeaderContract = false;
        return;
    }

    if (eventType != kUnityGfxDeviceEventInitialize || renderer != kUnityGfxRendererVulkan) {
        return;
    }

    std::lock_guard queueLock(gVulkanQueueMutex);
    gVulkanDeviceActive = false;
    if (unityInterfaces != nullptr) {
        gUnityInterfacesCache = unityInterfaces;
        gVulkan = unityInterfaces->Get<IUnityGraphicsVulkan>();
    }
    if (renderEventId >= 0) {
        gRenderEventIdCache = renderEventId;
    }

    if (gVulkan == nullptr) {
        MILESTROLOG_ERROR("Unity Vulkan graphics interface is unavailable during device initialization.");
        return;
    }

    // ConfigureEvent is required during initialization. The event itself only
    // records Unity resource barriers; Skia submission happens later through
    // AccessQueue, which supplies the queue ownership and flush guarantee.
    gEventConfigured = ConfigureEvent(gRenderEventIdCache);
    gVulkanDeviceActive = gEventConfigured;
    LogHeaderContract();
}

int64_t Render(MilestroUnityRenderSubmission& submission,
               RenderCompletionCallback completionCallback,
               void* completionUserData) {
    const auto& payload = submission.target;
    const uint64_t renderSerial = ++gRenderSerial;

    if (completionCallback == nullptr) {
        MILESTROLOG_ERROR("Milestro Vulkan render submission has no completion callback.");
        return MILESTRO_API_RET_FAILED;
    }
    if (!gEventConfigured || gRenderEventIdCache < 0) {
        MILESTROLOG_ERROR("Milestro Vulkan render event was not configured during device initialization.");
        return MILESTRO_API_RET_FAILED;
    }
    if (gVulkan == nullptr && gUnityInterfacesCache != nullptr) {
        gVulkan = gUnityInterfacesCache->Get<IUnityGraphicsVulkan>();
    }
    if (gVulkan == nullptr || gVulkan->AccessTexture == nullptr || gVulkan->AccessQueue == nullptr) {
        MILESTROLOG_ERROR("Unity Vulkan resource-access or queue-access API is unavailable.");
        return MILESTRO_API_RET_FAILED;
    }

    if (payload.width <= 0 || payload.height <= 0) {
        MILESTROLOG_ERROR("Invalid Vulkan render payload size ({}x{}).", payload.width, payload.height);
        return MILESTRO_API_RET_FAILED;
    }
    if (payload.msaaSamples != 1) {
        MILESTROLOG_ERROR("Vulkan RenderTexture MSAA is not implemented ({} samples).", payload.msaaSamples);
        return MILESTRO_API_RET_FAILED;
    }
    if (payload.handleKind != static_cast<int32_t>(MilestroUnityRenderTextureHandleKind::NativeTexture)) {
        MILESTROLOG_ERROR("Vulkan render target requires NativeTexture handle kind, got {}.", payload.handleKind);
        return MILESTRO_API_RET_FAILED;
    }
    if (payload.nativeTextureHandle == nullptr) {
        MILESTROLOG_ERROR("Vulkan render target native texture handle is null.");
        return MILESTRO_API_RET_FAILED;
    }

    const UnityVulkanInstance instance = gVulkan->Instance();
    if (instance.device == VK_NULL_HANDLE || instance.graphicsQueue == VK_NULL_HANDLE) {
        MILESTROLOG_ERROR("Unity Vulkan device or graphics queue is unavailable for render event {}.",
                          renderSerial);
        return MILESTRO_API_RET_FAILED;
    }

    MILESTRO_RENDER_LOG_INFO("Scheduling Vulkan render event {} for {}x{} (device={}, queue={}, family={}).",
                             renderSerial,
                             payload.width,
                             payload.height,
                             static_cast<void*>(instance.device),
                             static_cast<void*>(instance.graphicsQueue),
                             instance.queueFamilyIndex);

    UnityVulkanImage colorTarget = {};
    if (!AccessNativeTexture(payload.nativeTextureHandle,
                             VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                             VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT,
                             VK_ACCESS_COLOR_ATTACHMENT_READ_BIT | VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT,
                             kUnityVulkanResourceAccess_PipelineBarrier,
                             colorTarget,
                             renderSerial,
                             "color-attachment")) {
        MILESTROLOG_ERROR("Unity Vulkan color-attachment resource access failed for render event {}.",
                          renderSerial);
        return MILESTRO_API_RET_FAILED;
    }

    auto* queued = new (std::nothrow) VulkanQueueSubmission();
    if (queued == nullptr) {
        MILESTROLOG_ERROR("Failed to allocate Vulkan queue submission for render event {}.", renderSerial);
        return MILESTRO_API_RET_FAILED;
    }
    queued->submission = &submission;
    queued->colorTarget = colorTarget;
    queued->instance = instance;
    queued->renderSerial = renderSerial;
    queued->completionCallback = completionCallback;
    queued->completionUserData = completionUserData;

    {
        std::lock_guard queueLock(gVulkanQueueMutex);
        if (!gVulkanDeviceActive) {
            delete queued;
            MILESTROLOG_ERROR("Unity Vulkan device became unavailable before render event {} could access the queue.",
                              renderSerial);
            return MILESTRO_API_RET_FAILED;
        }
        LinkPendingQueueSubmission(queued);
    }

    // AccessTexture above records the transition in Unity's command buffer and
    // updates Unity's tracked layout. flush=true submits that buffer before
    // this callback, and AccessQueue grants exclusive graphics-queue access to
    // Ganesh. The target intentionally remains in COLOR_ATTACHMENT_OPTIMAL;
    // Unity will insert the next transition when it uses the texture again.
    gVulkan->AccessQueue(&OnVulkanQueueAccess, gRenderEventIdCache, queued, true);
    return kRenderDeferred;
}

} // namespace milestro::unity_render::vulkan
