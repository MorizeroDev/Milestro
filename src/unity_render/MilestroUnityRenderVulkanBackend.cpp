#include "unity_render/MilestroUnityRenderVulkanBackend.h"

#include "game/milestro_game_retcode.h"
#include "unity_render/MilestroUnityGraphicsBackend.h"
#include "unity_render/MilestroUnityRenderSubmission.h"
#include "unity_render/MilestroUnityRenderSubmissionDraw.h"
#include "unity_render/MilestroUnityRenderTextureHandleKind.h"
#include "unity_render/MilestroUnityRenderVulkanMemorySupport.h"

#include <IUnityGraphicsVulkan.h>

#include <atomic>
#include <cstdint>
#include <cstring>
#include <limits>
#include <mutex>

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
#include "src/gpu/GpuTypesPriv.h"
#include "src/gpu/vk/vulkanmemoryallocator/VulkanMemoryAllocatorPriv.h"

namespace milestro::unity_render::vulkan {

namespace {

struct VulkanSubmissionPayload {
    MilestroUnityRenderSubmission* submission = nullptr;
    UnityVulkanImage colorTarget{};
    UnityVulkanInstance instance{};
};

using VulkanLifecycle = FixedEventLifecycle<VulkanSubmissionPayload>;
using VulkanCancellationMailbox = EpochCancellationMailbox<>;

IUnityGraphicsVulkan* gVulkan = nullptr;
sk_sp<GrDirectContext> gDirectContext;
UnityVulkanInstance gCachedInstance{};
skgpu::VulkanExtensions gVkExtensions;
VulkanLifecycle gLifecycle;
VulkanCancellationMailbox gCancellationMailbox;
std::mutex gVulkanMutex;
bool gLoggedHeaderContract = false;
std::atomic<uint64_t> gActiveEpochGate{0};
DeviceTransitionState gDeviceTransitions;
uint64_t gLifecycleIntent = 0;
int gConfiguredFirstEventId = -1;

void ApplyDeviceTransitionsLocked();
void ServiceCancellationRequestsLocked();
void TryServiceCancellationRequests();
void TryServiceDeviceTransitions();

class VulkanStateLock {
public:
    VulkanStateLock()
        : lock_(gVulkanMutex) {
        ApplyDeviceTransitionsLocked();
    }

    explicit VulkanStateLock(std::try_to_lock_t)
        : lock_(gVulkanMutex, std::try_to_lock) {
        if (lock_.owns_lock()) {
            ApplyDeviceTransitionsLocked();
        }
    }

    ~VulkanStateLock() {
        if (!lock_.owns_lock()) {
            return;
        }
        ServiceCancellationRequestsLocked();
        lock_.unlock();
        TryServiceCancellationRequests();
        TryServiceDeviceTransitions();
    }

    bool OwnsLock() const {
        return lock_.owns_lock();
    }

private:
    std::unique_lock<std::mutex> lock_;
};

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
                         uint64_t serial,
                         const char* label) {
    if (gVulkan == nullptr || gVulkan->AccessTexture == nullptr) {
        MILESTROLOG_ERROR("Milestro Vulkan AccessTexture is unavailable during {} submission {}.",
                          label,
                          serial);
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
    MILESTRO_RENDER_LOG_INFO(
        "Milestro Vulkan {} AccessTexture submission={} ok={} mode={} image={} layout={} format={} {}x{}.",
        label,
        serial,
        ok ? 1 : 0,
        AccessName(mode),
        NonDispatchableHandle(image.image),
        static_cast<int>(image.layout),
        static_cast<int>(image.format),
        image.extent.width,
        image.extent.height);
    return ok;
}

bool ConfigurePrepareEvent(IUnityGraphicsVulkan* vulkan, int eventId) {
    if (vulkan == nullptr || vulkan->ConfigureEvent == nullptr || eventId < 0) {
        return false;
    }

    UnityVulkanPluginEventConfig config = {};
    config.renderPassPrecondition = kUnityVulkanRenderPass_EnsureOutside;
    config.graphicsQueueAccess = kUnityVulkanGraphicsQueueAccess_DontCare;
    config.flags = kUnityVulkanEventConfigFlag_EnsurePreviousFrameSubmission |
                   kUnityVulkanEventConfigFlag_ModifiesCommandBuffersState;
    vulkan->ConfigureEvent(eventId, &config);
    return true;
}

bool ConfigureSubmitEvent(IUnityGraphicsVulkan* vulkan, int eventId) {
    if (vulkan == nullptr || vulkan->ConfigureEvent == nullptr || eventId < 0) {
        return false;
    }

    UnityVulkanPluginEventConfig config = {};
    config.renderPassPrecondition = kUnityVulkanRenderPass_EnsureOutside;
    config.graphicsQueueAccess = kUnityVulkanGraphicsQueueAccess_Allow;
    config.flags = kUnityVulkanEventConfigFlag_EnsurePreviousFrameSubmission |
                   kUnityVulkanEventConfigFlag_FlushCommandBuffers |
                   kUnityVulkanEventConfigFlag_SyncWorkerThreads;
    vulkan->ConfigureEvent(eventId, &config);
    return true;
}

bool ConfigureReservedEvents(IUnityGraphicsVulkan* vulkan, int firstRenderEventId) {
    constexpr int kLastEventOffset = kReservedEventIdCount - 1;
    if (vulkan == nullptr || firstRenderEventId < 0 ||
        firstRenderEventId > std::numeric_limits<int>::max() - kLastEventOffset) {
        return false;
    }

    for (std::size_t epochIndex = 0; epochIndex < kEpochCapacity; ++epochIndex) {
        const int eventOffset = static_cast<int>(epochIndex) * kEventIdsPerEpoch;
        const int prepareEventId = firstRenderEventId + eventOffset;
        const int submitEventId = prepareEventId + 1;
        if (!ConfigurePrepareEvent(vulkan, prepareEventId) ||
            !ConfigureSubmitEvent(vulkan, submitEventId)) {
            return false;
        }
    }
    return true;
}

void LogHeaderContract() {
    if (gLoggedHeaderContract) {
        return;
    }

    MILESTRO_RENDER_LOG_INFO(
        "Milestro Vulkan uses a DontCare resource-access event followed by an Allow+Flush+Sync submit event.");
    gLoggedHeaderContract = true;
}

bool EnsureInitializedLocked() {
    if (gVulkan == nullptr || gVulkan->Instance == nullptr) {
        MILESTROLOG_ERROR("Milestro Vulkan interface is unavailable during context initialization.");
        return false;
    }

    const UnityVulkanInstance instance = gVulkan->Instance();
    if (instance.instance == VK_NULL_HANDLE || instance.device == VK_NULL_HANDLE ||
        instance.graphicsQueue == VK_NULL_HANDLE) {
        MILESTROLOG_ERROR("Milestro Vulkan instance is incomplete: instance={} device={} queue={}.",
                          static_cast<void*>(instance.instance),
                          static_cast<void*>(instance.device),
                          static_cast<void*>(instance.graphicsQueue));
        return false;
    }

    if (gDirectContext && gCachedInstance.instance == instance.instance &&
        gCachedInstance.device == instance.device &&
        gCachedInstance.graphicsQueue == instance.graphicsQueue) {
        return true;
    }

    if (gDirectContext) {
        gDirectContext->abandonContext();
        gDirectContext.reset();
    }
    gCachedInstance = instance;

    skgpu::VulkanBackendContext backendContext;
    backendContext.fInstance = instance.instance;
    backendContext.fPhysicalDevice = instance.physicalDevice;
    backendContext.fDevice = instance.device;
    backendContext.fQueue = instance.graphicsQueue;
    backendContext.fGraphicsQueueIndex = instance.queueFamilyIndex;
    backendContext.fMaxAPIVersion = 0;

    const auto getProc = instance.getInstanceProcAddr;
    if (getProc == nullptr) {
        MILESTROLOG_ERROR("Unity Vulkan getInstanceProcAddr is null.");
        return false;
    }

    const PFN_vkGetInstanceProcAddr systemGetInstanceProcAddr = getProc;
    const auto vkGetDeviceProcAddr = reinterpret_cast<PFN_vkGetDeviceProcAddr>(
        getProc(instance.instance, "vkGetDeviceProcAddr"));

    static auto FallbackEnumerateInstanceVersion = [](uint32_t* apiVersion) -> VkResult {
        if (apiVersion != nullptr) {
            *apiVersion = VK_API_VERSION_1_1;
        }
        return VK_SUCCESS;
    };

    backendContext.fGetProc = [getProc,
                               systemGetInstanceProcAddr,
                               vkGetDeviceProcAddr,
                               cachedInstance = instance.instance,
                               cachedDevice = instance.device](const char* name,
                                                               VkInstance requestedInstance,
                                                               VkDevice requestedDevice) -> PFN_vkVoidFunction {
        PFN_vkVoidFunction result = nullptr;
        const VkDevice targetDevice = requestedDevice != VK_NULL_HANDLE ? requestedDevice : cachedDevice;
        if (targetDevice != VK_NULL_HANDLE && vkGetDeviceProcAddr != nullptr) {
            result = vkGetDeviceProcAddr(targetDevice, name);
        }
        if (result == nullptr && requestedInstance == VK_NULL_HANDLE &&
            requestedDevice == VK_NULL_HANDLE && systemGetInstanceProcAddr != nullptr) {
            result = systemGetInstanceProcAddr(VK_NULL_HANDLE, name);
        }
        if (result == nullptr) {
            const VkInstance targetInstance =
                requestedInstance != VK_NULL_HANDLE ? requestedInstance : cachedInstance;
            if (targetInstance != VK_NULL_HANDLE) {
                result = getProc(targetInstance, name);
            }
        }
        if (result == nullptr && systemGetInstanceProcAddr != nullptr) {
            result = systemGetInstanceProcAddr(cachedInstance, name);
        }
        if (result == nullptr && name != nullptr &&
            std::strcmp(name, "vkEnumerateInstanceVersion") == 0) {
            return reinterpret_cast<PFN_vkVoidFunction>(+FallbackEnumerateInstanceVersion);
        }
        if (result == nullptr) {
            MILESTROLOG_ERROR("Vulkan proc '{}' is unavailable (instance={}, device={}).",
                              name != nullptr ? name : "null",
                              static_cast<void*>(requestedInstance),
                              static_cast<void*>(requestedDevice));
        }
        return result;
    };

    gVkExtensions.init(backendContext.fGetProc,
                       instance.instance,
                       instance.physicalDevice,
                       0,
                       nullptr,
                       0,
                       nullptr);
    backendContext.fVkExtensions = &gVkExtensions;

    const auto getMemoryProperties = reinterpret_cast<PFN_vkGetPhysicalDeviceMemoryProperties>(
        getProc(instance.instance, "vkGetPhysicalDeviceMemoryProperties"));
    if (getMemoryProperties == nullptr) {
        MILESTROLOG_ERROR("vkGetPhysicalDeviceMemoryProperties is unavailable.");
        return false;
    }

    VkPhysicalDeviceMemoryProperties memoryProperties = {};
    getMemoryProperties(instance.physicalDevice, &memoryProperties);
    const VulkanHostMemorySupport hostMemorySupport = ClassifyVulkanHostMemorySupport(
        memoryProperties,
        ~uint32_t{0},
        VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT,
        VK_MEMORY_PROPERTY_HOST_COHERENT_BIT);
    if (hostMemorySupport == VulkanHostMemorySupport::Unavailable) {
        MILESTROLOG_ERROR("Vulkan device has no host-visible memory type for Skia uploads.");
        return false;
    }
    if (hostMemorySupport == VulkanHostMemorySupport::NonCoherent) {
        MILESTRO_RENDER_LOG_INFO(
            "Vulkan host memory is non-coherent; Skia VMA manages aligned flush and invalidate operations.");
    }

    backendContext.fMemoryAllocator = skgpu::VulkanMemoryAllocators::Make(
        backendContext,
        skgpu::ThreadSafe::kNo);
    if (backendContext.fMemoryAllocator == nullptr) {
        MILESTROLOG_ERROR("Skia VMA failed to create the Vulkan memory allocator.");
        return false;
    }

    gDirectContext = GrDirectContexts::MakeVulkan(backendContext);
    if (!gDirectContext) {
        MILESTROLOG_ERROR("GrDirectContexts::MakeVulkan returned null.");
        return false;
    }

    MILESTRO_RENDER_LOG_INFO("Skia Vulkan GrDirectContext created successfully.");
    return true;
}

bool DrawVulkanSubmission(const VulkanSubmissionPayload& queued) {
    if (queued.submission == nullptr || gDirectContext == nullptr || gDirectContext->abandoned()) {
        return false;
    }

    const MilestroUnityRenderTargetPayload& target = queued.submission->target;
    gDirectContext->resetContext();

    GrVkImageInfo imageInfo;
    imageInfo.fImage = queued.colorTarget.image;
    imageInfo.fAlloc = {};
    imageInfo.fImageTiling = queued.colorTarget.tiling;
    imageInfo.fImageLayout = VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;
    imageInfo.fFormat = queued.colorTarget.format;
    imageInfo.fImageUsageFlags = queued.colorTarget.usage;
    imageInfo.fSampleCount = 1;
    imageInfo.fLevelCount = 1;
    imageInfo.fCurrentQueueFamily = queued.instance.queueFamilyIndex;
    imageInfo.fSharingMode = VK_SHARING_MODE_EXCLUSIVE;

    const GrBackendRenderTarget backendTarget =
        GrBackendRenderTargets::MakeVk(target.width, target.height, imageInfo);

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
    } else if (target.colorSpace == 1) {
        colorSpace = SkColorSpace::MakeSRGBLinear();
    } else {
        colorSpace = SkColorSpace::MakeSRGB();
    }

    sk_sp<SkSurface> surface = SkSurfaces::WrapBackendRenderTarget(
        gDirectContext.get(),
        backendTarget,
        kTopLeft_GrSurfaceOrigin,
        colorType,
        colorSpace,
        nullptr);
    if (surface == nullptr) {
        MILESTROLOG_ERROR("Failed to wrap Unity Vulkan RenderTexture as a Skia surface ({}x{}, format={}).",
                          target.width,
                          target.height,
                          static_cast<int>(queued.colorTarget.format));
        return false;
    }

    DrawSubmission(surface->getCanvas(), *queued.submission);
    gDirectContext->flushAndSubmit(surface.get());
    return true;
}

void CompleteAtLocked(std::size_t index, MilestroUnityRenderSubmissionStatus status) {
    VulkanSubmissionPayload payload = gLifecycle.TakeAt(index);
    CompleteSubmission(payload.submission, status);
}

void CompleteAllLocked(MilestroUnityRenderSubmissionStatus status) {
    while (gLifecycle.QueueSize() != 0) {
        CompleteAtLocked(0, status);
    }
}

void ShutdownGpuState() {
    gActiveEpochGate.store(0, std::memory_order_release);
    if (gDirectContext) {
        gDirectContext->abandonContext();
        gDirectContext.reset();
    }
    gCachedInstance = {};
    gVulkan = nullptr;
}

void ShutdownLifecycleLocked() {
    gActiveEpochGate.store(0, std::memory_order_release);
    gLifecycle.DetachActiveEpoch();
    CompleteAllLocked(MilestroUnityRenderSubmissionStatus::Failed);
    gLifecycleIntent = 0;
    gConfiguredFirstEventId = -1;
}

bool StartNextEpochLocked(uint64_t intent, int firstRenderEventId) {
    if (!gDeviceTransitions.IsCurrentIntent(intent) || firstRenderEventId < 0) {
        return false;
    }
    if (gLifecycle.Disabled()) {
        MILESTROLOG_ERROR("Milestro Vulkan was disabled after exhausting its bounded lifecycle capacity.");
        return false;
    }
    if (gLifecycle.EpochCount() >= kEpochCapacity) {
        gLifecycle.Disable();
        MILESTROLOG_ERROR("Milestro Vulkan has exhausted its 32 non-reused device epoch slots.");
        return false;
    }

    const int epochOffset = static_cast<int>(gLifecycle.EpochCount()) * kEventIdsPerEpoch;
    const int prepareEventId = firstRenderEventId + epochOffset;
    const int submitEventId = prepareEventId + 1;
    EventInfo eventInfo;
    if (!gLifecycle.StartEpoch(prepareEventId, submitEventId, eventInfo)) {
        MILESTROLOG_ERROR("Milestro Vulkan could not allocate a new bounded device epoch.");
        return false;
    }

    if (!gDeviceTransitions.IsCurrentIntent(intent)) {
        gLifecycle.RollbackActiveEpoch(eventInfo.epoch);
        return false;
    }
    gActiveEpochGate.store(eventInfo.epoch, std::memory_order_release);
    if (!gDeviceTransitions.IsCurrentIntent(intent)) {
        uint64_t expectedEpoch = eventInfo.epoch;
        gActiveEpochGate.compare_exchange_strong(
            expectedEpoch, 0, std::memory_order_acq_rel, std::memory_order_acquire);
        gLifecycle.RollbackActiveEpoch(eventInfo.epoch);
        return false;
    }
    LogHeaderContract();
    MILESTRO_RENDER_LOG_INFO("Milestro Vulkan epoch {} initialized with prepare={} submit={}.",
                             eventInfo.epoch,
                             eventInfo.prepareEventId,
                             eventInfo.submitEventId);
    return true;
}

void RetireActiveEpochLocked() {
    gActiveEpochGate.store(0, std::memory_order_release);
    gLifecycle.DetachActiveEpoch();
    CompleteAllLocked(MilestroUnityRenderSubmissionStatus::Failed);
    if (gDeviceTransitions.IsCurrentIntent(gLifecycleIntent) &&
        !StartNextEpochLocked(gLifecycleIntent, gConfiguredFirstEventId)) {
        MILESTRO_RENDER_LOG_WARN("Milestro Vulkan could not replace a retired submission epoch.");
    }
}

void ServiceCancellationRequestsLocked() {
    uint64_t pendingEpochs = gCancellationMailbox.TakePendingMask();
    for (uint64_t epoch = 1; pendingEpochs != 0 && epoch <= kEpochCapacity; ++epoch) {
        const bool requested = (pendingEpochs & 1U) != 0;
        pendingEpochs >>= 1U;
        if (!requested || gLifecycle.ActiveEpoch() != epoch) {
            continue;
        }

        MILESTRO_RENDER_LOG_WARN("Milestro Vulkan retiring epoch {} after a deferred cancellation.", epoch);
        RetireActiveEpochLocked();
    }
}

void InitializeLocked(const DeviceTransitionSnapshot& transition) {
    ShutdownLifecycleLocked();

    if (transition.kind != DeviceTransitionKind::Initialize ||
        !gDeviceTransitions.IsCurrentIntent(transition.intent) ||
        transition.renderer != static_cast<int32_t>(kUnityGfxRendererVulkan) ||
        transition.firstRenderEventId < 0) {
        return;
    }

    gLifecycleIntent = transition.intent;
    gConfiguredFirstEventId = transition.firstRenderEventId;
    StartNextEpochLocked(gLifecycleIntent, gConfiguredFirstEventId);
}

void ApplyDeviceTransitionsLocked() {
    gDeviceTransitions.ApplyLocked([](const DeviceTransitionSnapshot& transition) {
        if (transition.kind == DeviceTransitionKind::Initialize) {
            InitializeLocked(transition);
        } else if (transition.kind == DeviceTransitionKind::Shutdown) {
            ShutdownLifecycleLocked();
        }
    });
}

void TryServiceDeviceTransitions() {
    while (gDeviceTransitions.HasStablePendingTransition()) {
        std::unique_lock lock(gVulkanMutex, std::try_to_lock);
        if (!lock.owns_lock()) {
            return;
        }
        ApplyDeviceTransitionsLocked();
        ServiceCancellationRequestsLocked();
        lock.unlock();
    }
}

void TryServiceCancellationRequests() {
    std::unique_lock lock(gVulkanMutex, std::try_to_lock);
    if (!lock.owns_lock()) {
        return;
    }
    ApplyDeviceTransitionsLocked();
    ServiceCancellationRequestsLocked();
    lock.unlock();
    TryServiceDeviceTransitions();
}

void PrepareSubmissionLocked(const EventConsumption& event) {
    if (!event.active || gActiveEpochGate.load(std::memory_order_acquire) != event.epoch) {
        return;
    }

    if (gLifecycle.QueueSize() == 0) {
        return;
    }

    if (gLifecycle.At(0).epoch != event.epoch) {
        return;
    }
    if (gLifecycle.At(0).phase == SubmissionPhase::Prepared) {
        MILESTRO_RENDER_LOG_WARN(
            "Milestro Vulkan dropped submission {} after its Submit event was not observed.",
            gLifecycle.At(0).serial);
        RetireActiveEpochLocked();
        return;
    }

    VulkanLifecycle::Record& record = gLifecycle.At(0);
    MilestroUnityRenderSubmission* submission = record.payload.submission;
    if (submission == nullptr || gVulkan == nullptr || gVulkan->Instance == nullptr) {
        RetireActiveEpochLocked();
        return;
    }

    UnityVulkanImage colorTarget = {};
    if (!AccessNativeTexture(submission->target.nativeTextureHandle,
                             VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                             VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT,
                             VK_ACCESS_COLOR_ATTACHMENT_READ_BIT |
                                 VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT,
                             kUnityVulkanResourceAccess_PipelineBarrier,
                             colorTarget,
                             record.serial,
                             "prepare")) {
        RetireActiveEpochLocked();
        return;
    }

    const UnityVulkanInstance instance = gVulkan->Instance();
    if (instance.device == VK_NULL_HANDLE || instance.graphicsQueue == VK_NULL_HANDLE) {
        RetireActiveEpochLocked();
        return;
    }

    record.payload.colorTarget = colorTarget;
    record.payload.instance = instance;
    gLifecycle.MarkPrepared(0);
}

void SubmitPreparedLocked(const EventConsumption& event) {
    if (!event.active || gActiveEpochGate.load(std::memory_order_acquire) != event.epoch) {
        return;
    }

    if (gLifecycle.QueueSize() == 0 || gLifecycle.At(0).epoch != event.epoch) {
        return;
    }

    if (gLifecycle.At(0).phase != SubmissionPhase::Prepared) {
        MILESTRO_RENDER_LOG_WARN(
            "Milestro Vulkan dropped submission {} after its Prepare event was not observed.",
            gLifecycle.At(0).serial);
        RetireActiveEpochLocked();
        return;
    }

    VulkanLifecycle::Record& record = gLifecycle.At(0);
    if (!EnsureInitializedLocked()) {
        CompleteAtLocked(0, MilestroUnityRenderSubmissionStatus::Failed);
        return;
    }

    const UnityVulkanInstance current = gVulkan->Instance();
    if (current.device != record.payload.instance.device ||
        current.graphicsQueue != record.payload.instance.graphicsQueue ||
        current.queueFamilyIndex != record.payload.instance.queueFamilyIndex) {
        MILESTROLOG_ERROR("Unity Vulkan device changed before submission {} could be submitted.", record.serial);
        CompleteAtLocked(0, MilestroUnityRenderSubmissionStatus::Failed);
        return;
    }

    const bool drawn = DrawVulkanSubmission(record.payload);
    const uint64_t serial = record.serial;
    CompleteAtLocked(0,
                     drawn ? MilestroUnityRenderSubmissionStatus::Drawn
                           : MilestroUnityRenderSubmissionStatus::Failed);
    if (drawn) {
        MILESTRO_RENDER_LOG_INFO("Skia Vulkan submission {} completed.", serial);
    }
}

const char* EnqueueResultName(EnqueueResult result) {
    switch (result) {
        case EnqueueResult::Accepted:
            return "accepted";
        case EnqueueResult::InactiveEpoch:
            return "inactive epoch";
        case EnqueueResult::QueueFull:
            return "queue full";
        case EnqueueResult::EventBudgetExhausted:
            return "event budget exhausted";
        case EnqueueResult::SerialExhausted:
            return "serial exhausted";
        default:
            return "unknown";
    }
}

} // namespace

void OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType,
                           IUnityInterfaces* unityInterfaces,
                           UnityGfxRenderer renderer,
                           int firstRenderEventId) {
    const bool shutdown = eventType == kUnityGfxDeviceEventShutdown ||
                          eventType == kUnityGfxDeviceEventBeforeReset;
    const bool initialize = eventType == kUnityGfxDeviceEventInitialize ||
                            eventType == kUnityGfxDeviceEventAfterReset;
    if (!shutdown && !initialize) {
        return;
    }

    // Unity graphics callbacks and plugin render events are rendering-thread
    // APIs. The atomic gate prevents managed producers from entering an old
    // epoch while a transition waits for the short native state lock.
    const uint64_t intent = gDeviceTransitions.BeginTransition();
    gActiveEpochGate.store(0, std::memory_order_release);
    ShutdownGpuState();

    if (shutdown || renderer != kUnityGfxRendererVulkan) {
        gDeviceTransitions.PublishShutdown(intent);
        TryServiceDeviceTransitions();
        return;
    }

    IUnityGraphicsVulkan* vulkan =
        unityInterfaces != nullptr ? unityInterfaces->Get<IUnityGraphicsVulkan>() : nullptr;
    if (vulkan == nullptr || vulkan->ConfigureEvent == nullptr || firstRenderEventId < 0 ||
        !ConfigureReservedEvents(vulkan, firstRenderEventId)) {
        MILESTROLOG_ERROR("Unity Vulkan graphics interface or reserved event range is unavailable.");
        gDeviceTransitions.PublishShutdown(intent);
        TryServiceDeviceTransitions();
        return;
    }

    gVulkan = vulkan;

    gDeviceTransitions.PublishInitialize(intent,
                                         static_cast<int32_t>(renderer),
                                         firstRenderEventId);
    TryServiceDeviceTransitions();
}

void OnRenderEvent(int eventId) {
    VulkanStateLock lock;

    const EventConsumption prepare = gLifecycle.ConsumePrepareEvent(eventId);
    if (prepare.recognized) {
        if (prepare.hadOutstandingEvent) {
            PrepareSubmissionLocked(prepare);
        }
        return;
    }

    const EventConsumption submit = gLifecycle.ConsumeSubmitEvent(eventId);
    if (submit.recognized) {
        if (submit.hadOutstandingEvent) {
            SubmitPreparedLocked(submit);
        }
        return;
    }

    MILESTRO_RENDER_LOG_WARN("Ignoring unknown Milestro Vulkan event id {}.", eventId);
}

int64_t GetEventInfo(int32_t& prepareEventId, int32_t& submitEventId, uint64_t& epoch) {
    VulkanStateLock lock(std::try_to_lock);
    if (!lock.OwnsLock()) {
        prepareEventId = -1;
        submitEventId = -1;
        epoch = 0;
        return MILESTRO_API_RET_RETRY;
    }
    if (gActiveEpochGate.load(std::memory_order_acquire) == 0) {
        prepareEventId = -1;
        submitEventId = -1;
        epoch = 0;
        return MILESTRO_API_RET_RETRY;
    }
    EventInfo eventInfo;
    if (!gLifecycle.GetActiveEventInfo(eventInfo)) {
        prepareEventId = -1;
        submitEventId = -1;
        epoch = 0;
        return MILESTRO_API_RET_RETRY;
    }
    if (eventInfo.epoch != gActiveEpochGate.load(std::memory_order_acquire)) {
        prepareEventId = -1;
        submitEventId = -1;
        epoch = 0;
        return MILESTRO_API_RET_RETRY;
    }

    prepareEventId = eventInfo.prepareEventId;
    submitEventId = eventInfo.submitEventId;
    epoch = eventInfo.epoch;
    return MILESTRO_API_RET_OK;
}

int64_t EnqueueSubmission(uint64_t epoch, void* submissionPointer, uint64_t& serial) {
    serial = 0;
    auto* submission = static_cast<MilestroUnityRenderSubmission*>(submissionPointer);
    if (submission == nullptr) {
        MILESTROLOG_ERROR("Milestro Vulkan enqueue received a null submission.");
        return MILESTRO_API_RET_FAILED;
    }

    const MilestroUnityRenderTargetPayload& target = submission->target;
    if (target.graphicsBackend != static_cast<int32_t>(MilestroUnityGraphicsBackend::Vulkan) ||
        target.handleKind != static_cast<int32_t>(MilestroUnityRenderTextureHandleKind::NativeTexture) ||
        target.nativeTextureHandle == nullptr || target.width <= 0 || target.height <= 0 ||
        target.msaaSamples != 1 || !IsValidCommandCount(submission->commandCount)) {
        MILESTROLOG_ERROR(
            "Milestro Vulkan enqueue rejected target backend={} handleKind={} size={}x{} msaa={} commands={}.",
            target.graphicsBackend,
            target.handleKind,
            target.width,
            target.height,
            target.msaaSamples,
            submission->commandCount);
        return MILESTRO_API_RET_FAILED;
    }

    if (gActiveEpochGate.load(std::memory_order_acquire) != epoch) {
        return MILESTRO_API_RET_RETRY;
    }

    VulkanStateLock lock(std::try_to_lock);
    if (!lock.OwnsLock()) {
        return MILESTRO_API_RET_RETRY;
    }
    if (gActiveEpochGate.load(std::memory_order_acquire) != epoch) {
        return MILESTRO_API_RET_RETRY;
    }
    VulkanSubmissionPayload payload;
    payload.submission = submission;
    const EnqueueResult result = gLifecycle.TryEnqueue(epoch, payload, serial);
    if (result == EnqueueResult::Accepted) {
        return MILESTRO_API_RET_OK;
    }

    if (result == EnqueueResult::InactiveEpoch || result == EnqueueResult::QueueFull) {
        return MILESTRO_API_RET_RETRY;
    }

    MILESTROLOG_ERROR("Milestro Vulkan enqueue failed: {}.", EnqueueResultName(result));
    if (result == EnqueueResult::EventBudgetExhausted ||
        result == EnqueueResult::SerialExhausted) {
        gActiveEpochGate.store(0, std::memory_order_release);
        CompleteAllLocked(MilestroUnityRenderSubmissionStatus::Failed);
    }
    return MILESTRO_API_RET_FAILED;
}

int64_t CancelSubmission(uint64_t epoch, uint64_t serial) {
    {
        VulkanStateLock lock(std::try_to_lock);
        if (lock.OwnsLock()) {
            VulkanSubmissionPayload payload;
            if (!gLifecycle.Retire(epoch, serial, payload)) {
                return MILESTRO_API_RET_FAILED;
            }

            CompleteSubmission(payload.submission, MilestroUnityRenderSubmissionStatus::Failed);
            RetireActiveEpochLocked();
            return MILESTRO_API_RET_OK;
        }
    }

    if (!gCancellationMailbox.Publish(epoch)) {
        return MILESTRO_API_RET_FAILED;
    }
    TryServiceCancellationRequests();
    return MILESTRO_API_RET_PENDING;
}

} // namespace milestro::unity_render::vulkan
