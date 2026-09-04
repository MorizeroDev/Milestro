#include "game/milestro_game_retcode.h"
#include "unity_render/MilestroUnityGraphicsBackend.h"
#include "unity_render/MilestroUnityRenderDispatcher.h"
#include "unity_render/MilestroUnityRenderSubmission.h"
#include "unity_render/MilestroUnityRenderTextureHandleKind.h"
#include "unity_render/MilestroUnityRenderVulkanAdapter.h"
#include "unity_render/MilestroUnityRenderVulkanBackend.h"
#include "unity_render/MilestroUnityVulkanBackendKind.h"
#include <Milestro/game/milestro_game_interface.h>

#include <IUnityGraphicsVulkan.h>

#include <gtest/gtest.h>

#include <cstdint>
#include <cstring>
#include <type_traits>
#include <utility>
#include <vector>

namespace {

using milestro::unity_render::vulkan::VulkanBackendKind;

IUnityGraphicsVulkan gFakeVulkan{};
IUnityGraphics gFakeGraphics{};
UnityVulkanInstance gFakeInstance{};
IUnityGraphicsDeviceEventCallback gDeviceEventCallback = nullptr;

enum class FailurePoint {
    None,
    CreateBuffer,
    AllocateMemory,
    BindMemory,
    MapMemory,
    Flush,
    ObserveAccess,
    TransferAccess,
    SecondRecordingState,
    RestoreAccess,
    MissingCopyFunction,
};

struct FakeVulkanState {
    FailurePoint failure = FailurePoint::None;
    int createCount = 0;
    int createdCount = 0;
    int destroyCount = 0;
    int allocateCount = 0;
    int allocatedCount = 0;
    int freeCount = 0;
    int bindCount = 0;
    int mapCount = 0;
    int mappedCount = 0;
    int unmapCount = 0;
    int flushCount = 0;
    int accessCount = 0;
    int recordingCount = 0;
    int copyCount = 0;
    uint64_t currentFrame = 50;
    uint64_t safeFrame = 49;
    alignas(16) uint8_t mapped[4096]{};
};

FakeVulkanState gVulkanState;

template <typename Handle>
Handle FakeHandle(uintptr_t value) {
    if constexpr (std::is_pointer_v<Handle>) {
        return reinterpret_cast<Handle>(value);
    } else {
        return static_cast<Handle>(value);
    }
}

VKAPI_ATTR VkResult VKAPI_CALL FakeCreateBuffer(VkDevice,
                                                const VkBufferCreateInfo*,
                                                const VkAllocationCallbacks*,
                                                VkBuffer* buffer) {
    ++gVulkanState.createCount;
    if (gVulkanState.failure == FailurePoint::CreateBuffer) {
        return VK_ERROR_OUT_OF_HOST_MEMORY;
    }
    *buffer = FakeHandle<VkBuffer>(0x4001);
    ++gVulkanState.createdCount;
    return VK_SUCCESS;
}

VKAPI_ATTR void VKAPI_CALL FakeDestroyBuffer(VkDevice, VkBuffer, const VkAllocationCallbacks*) {
    ++gVulkanState.destroyCount;
}

VKAPI_ATTR void VKAPI_CALL FakeGetBufferMemoryRequirements(VkDevice, VkBuffer, VkMemoryRequirements* requirements) {
    *requirements = {};
    requirements->size = 1024;
    requirements->alignment = 16;
    requirements->memoryTypeBits = 1;
}

VKAPI_ATTR VkResult VKAPI_CALL FakeAllocateMemory(VkDevice,
                                                  const VkMemoryAllocateInfo*,
                                                  const VkAllocationCallbacks*,
                                                  VkDeviceMemory* memory) {
    ++gVulkanState.allocateCount;
    if (gVulkanState.failure == FailurePoint::AllocateMemory) {
        return VK_ERROR_OUT_OF_DEVICE_MEMORY;
    }
    *memory = FakeHandle<VkDeviceMemory>(0x4002);
    ++gVulkanState.allocatedCount;
    return VK_SUCCESS;
}

VKAPI_ATTR void VKAPI_CALL FakeFreeMemory(VkDevice, VkDeviceMemory, const VkAllocationCallbacks*) {
    ++gVulkanState.freeCount;
}

VKAPI_ATTR VkResult VKAPI_CALL FakeBindBufferMemory(VkDevice, VkBuffer, VkDeviceMemory, VkDeviceSize) {
    ++gVulkanState.bindCount;
    return gVulkanState.failure == FailurePoint::BindMemory ? VK_ERROR_MEMORY_MAP_FAILED : VK_SUCCESS;
}

VKAPI_ATTR VkResult VKAPI_CALL
FakeMapMemory(VkDevice, VkDeviceMemory, VkDeviceSize, VkDeviceSize, VkMemoryMapFlags, void** mapped) {
    ++gVulkanState.mapCount;
    if (gVulkanState.failure == FailurePoint::MapMemory) {
        return VK_ERROR_MEMORY_MAP_FAILED;
    }
    *mapped = gVulkanState.mapped;
    ++gVulkanState.mappedCount;
    return VK_SUCCESS;
}

VKAPI_ATTR void VKAPI_CALL FakeUnmapMemory(VkDevice, VkDeviceMemory) {
    ++gVulkanState.unmapCount;
}

VKAPI_ATTR VkResult VKAPI_CALL FakeFlushMappedMemoryRanges(VkDevice, uint32_t, const VkMappedMemoryRange*) {
    ++gVulkanState.flushCount;
    return gVulkanState.failure == FailurePoint::Flush ? VK_ERROR_MEMORY_MAP_FAILED : VK_SUCCESS;
}

VKAPI_ATTR void VKAPI_CALL
FakeCmdCopyBufferToImage(VkCommandBuffer, VkBuffer, VkImage, VkImageLayout, uint32_t, const VkBufferImageCopy*) {
    ++gVulkanState.copyCount;
}

VKAPI_ATTR void VKAPI_CALL FakeGetPhysicalDeviceMemoryProperties(VkPhysicalDevice,
                                                                 VkPhysicalDeviceMemoryProperties* properties) {
    *properties = {};
    properties->memoryTypeCount = 1;
    properties->memoryTypes[0].propertyFlags = VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT;
}

PFN_vkVoidFunction VKAPI_PTR FakeGetDeviceProcAddr(VkDevice, const char* name) {
    if (gVulkanState.failure == FailurePoint::MissingCopyFunction && std::strcmp(name, "vkCmdCopyBufferToImage") == 0) {
        return nullptr;
    }
#define RETURN_FAKE_PROC(functionName)                                                                                 \
    if (std::strcmp(name, "vk" #functionName) == 0) {                                                                  \
        return reinterpret_cast<PFN_vkVoidFunction>(&Fake##functionName);                                              \
    }
    RETURN_FAKE_PROC(CreateBuffer)
    RETURN_FAKE_PROC(DestroyBuffer)
    RETURN_FAKE_PROC(GetBufferMemoryRequirements)
    RETURN_FAKE_PROC(AllocateMemory)
    RETURN_FAKE_PROC(FreeMemory)
    RETURN_FAKE_PROC(BindBufferMemory)
    RETURN_FAKE_PROC(MapMemory)
    RETURN_FAKE_PROC(UnmapMemory)
    RETURN_FAKE_PROC(FlushMappedMemoryRanges)
    RETURN_FAKE_PROC(CmdCopyBufferToImage)
#undef RETURN_FAKE_PROC
    return nullptr;
}

PFN_vkVoidFunction VKAPI_PTR FakeGetInstanceProcAddr(VkInstance, const char* name) {
    if (name == nullptr) {
        return nullptr;
    }
    if (std::strcmp(name, "vkGetDeviceProcAddr") == 0) {
        return reinterpret_cast<PFN_vkVoidFunction>(&FakeGetDeviceProcAddr);
    }
    if (std::strcmp(name, "vkGetPhysicalDeviceMemoryProperties") == 0) {
        return reinterpret_cast<PFN_vkVoidFunction>(&FakeGetPhysicalDeviceMemoryProperties);
    }
    return nullptr;
}

UnityVulkanInstance UNITY_INTERFACE_API FakeInstance() {
    return gFakeInstance;
}

void UNITY_INTERFACE_API FakeConfigureEvent(int, const UnityVulkanPluginEventConfig*) {
}

bool UNITY_INTERFACE_API FakeAccessTexture(void*,
                                           const VkImageSubresource*,
                                           VkImageLayout layout,
                                           VkPipelineStageFlags,
                                           VkAccessFlags,
                                           UnityVulkanResourceAccessMode,
                                           UnityVulkanImage* image) {
    ++gVulkanState.accessCount;
    if (image == nullptr || (gVulkanState.failure == FailurePoint::ObserveAccess && gVulkanState.accessCount == 1) ||
        (gVulkanState.failure == FailurePoint::TransferAccess && gVulkanState.accessCount == 2) ||
        (gVulkanState.failure == FailurePoint::RestoreAccess && gVulkanState.accessCount == 3)) {
        return false;
    }
    *image = {};
    image->image = FakeHandle<VkImage>(0x3001);
    image->layout = layout;
    image->aspect = VK_IMAGE_ASPECT_COLOR_BIT;
    image->usage = VK_IMAGE_USAGE_COLOR_ATTACHMENT_BIT | VK_IMAGE_USAGE_TRANSFER_DST_BIT;
    image->format = VK_FORMAT_R8G8B8A8_UNORM;
    image->extent = {16, 16, 1};
    image->tiling = VK_IMAGE_TILING_OPTIMAL;
    image->type = VK_IMAGE_TYPE_2D;
    image->samples = VK_SAMPLE_COUNT_1_BIT;
    image->layers = 1;
    image->mipCount = 1;
    return true;
}

bool UNITY_INTERFACE_API FakeCommandRecordingState(UnityVulkanRecordingState* state, UnityVulkanGraphicsQueueAccess) {
    ++gVulkanState.recordingCount;
    if (state == nullptr ||
        (gVulkanState.failure == FailurePoint::SecondRecordingState && gVulkanState.recordingCount == 2)) {
        return false;
    }
    *state = {};
    state->commandBuffer = FakeHandle<VkCommandBuffer>(0x5001);
    state->currentFrameNumber = gVulkanState.currentFrame;
    state->safeFrameNumber = gVulkanState.safeFrame;
    return true;
}

std::vector<std::pair<MilestroUnityRenderSubmission*, MilestroUnityRenderSubmissionStatus>> gCompletions;

void RecordCompletion(MilestroUnityRenderSubmission* submission, MilestroUnityRenderSubmissionStatus status) {
    gCompletions.emplace_back(submission, status);
}

IUnityInterface* UNITY_INTERFACE_API FakeGetInterface(UnityInterfaceGUID guid) {
    if (guid == GetUnityInterfaceGUID<IUnityGraphicsVulkan>()) {
        return &gFakeVulkan;
    }
    if (guid == GetUnityInterfaceGUID<IUnityGraphics>()) {
        return &gFakeGraphics;
    }
    return nullptr;
}

UnityGfxRenderer UNITY_INTERFACE_API FakeGetRenderer() {
    return kUnityGfxRendererVulkan;
}

void UNITY_INTERFACE_API FakeRegisterDeviceEventCallback(IUnityGraphicsDeviceEventCallback callback) {
    gDeviceEventCallback = callback;
}

void UNITY_INTERFACE_API FakeUnregisterDeviceEventCallback(IUnityGraphicsDeviceEventCallback callback) {
    if (gDeviceEventCallback == callback) {
        gDeviceEventCallback = nullptr;
    }
}

int UNITY_INTERFACE_API FakeReserveEventIdRange(int count) {
    return count == 6 ? 100 : -1;
}

class VulkanProductionTest : public testing::Test {
protected:
    void SetUp() override {
        gFakeVulkan = {};
        gFakeVulkan.ConfigureEvent = FakeConfigureEvent;
        gFakeVulkan.Instance = FakeInstance;
        gFakeVulkan.AccessTexture = FakeAccessTexture;
        gFakeVulkan.CommandRecordingState = FakeCommandRecordingState;
        gVulkanState = {};
        gCompletions.clear();

        gFakeInstance = {};
        gFakeInstance.instance = FakeHandle<VkInstance>(0x1001);
        gFakeInstance.physicalDevice = FakeHandle<VkPhysicalDevice>(0x1002);
        gFakeInstance.device = FakeHandle<VkDevice>(0x1003);
        gFakeInstance.graphicsQueue = FakeHandle<VkQueue>(0x1004);
        gFakeInstance.getInstanceProcAddr = FakeGetInstanceProcAddr;
        gFakeInstance.queueFamilyIndex = 2;

        interfaces_ = {};
        interfaces_.GetInterface = FakeGetInterface;
        milestro::unity_render::vulkan::OnGraphicsDeviceEvent(kUnityGfxDeviceEventInitialize,
                                                              &interfaces_,
                                                              kUnityGfxRendererVulkan,
                                                              103,
                                                              104,
                                                              105);
    }

    void TearDown() override {
        milestro::unity_render::vulkan::OnGraphicsDeviceEvent(kUnityGfxDeviceEventShutdown,
                                                              &interfaces_,
                                                              kUnityGfxRendererVulkan,
                                                              103,
                                                              104,
                                                              105);
    }

    IUnityInterfaces interfaces_{};
};

MilestroUnityRenderSubmission
DirectSubmission(void* nativeTexture, void* target, uint64_t generation, uint64_t deviceEpoch) {
    MilestroUnityRenderSubmission submission;
    submission.target.graphicsBackend = static_cast<int32_t>(MilestroUnityGraphicsBackend::Vulkan);
    submission.target.handleKind = static_cast<int32_t>(MilestroUnityRenderTextureHandleKind::NativeTexture);
    submission.target.nativeTextureHandle = nativeTexture;
    submission.target.width = 16;
    submission.target.height = 16;
    submission.target.vulkanBackend = static_cast<int32_t>(VulkanBackendKind::Direct);
    submission.target.vulkanTarget = target;
    submission.target.vulkanTargetGeneration = generation;
    submission.target.deviceEpoch = deviceEpoch;
    return submission;
}

void SetCurrentPayloadAbi(MilestroUnityRenderSubmission& submission) {
    submission.abiVersion = kMilestroUnityRenderPayloadAbiVersion;
    submission.structSize = kMilestroUnityRenderSubmissionSize;
    submission.target.abiVersion = kMilestroUnityRenderPayloadAbiVersion;
    submission.target.structSize = kMilestroUnityRenderTargetPayloadSize;
    submission.target.effectiveScale = 1.0F;
}

struct TestRenderDrainPayload {
    int32_t magic = 0x4D524451;
    int32_t graphicsBackend = static_cast<int32_t>(MilestroUnityGraphicsBackend::Vulkan);
    int32_t vulkanBackend = static_cast<int32_t>(VulkanBackendKind::Direct);
    int32_t completed = 0;
    uint64_t batchToken = 0;
    int32_t phase = 0;
    int32_t reserved = 0;
};

static_assert(sizeof(TestRenderDrainPayload) == 32);

class VulkanDispatcherProductionTest : public testing::Test {
protected:
    void SetUp() override {
        gFakeVulkan = {};
        gFakeVulkan.ConfigureEvent = FakeConfigureEvent;
        gFakeVulkan.Instance = FakeInstance;
        gFakeVulkan.AccessTexture = FakeAccessTexture;
        gFakeVulkan.CommandRecordingState = FakeCommandRecordingState;
        gVulkanState = {};
        gCompletions.clear();

        gFakeInstance = {};
        gFakeInstance.instance = FakeHandle<VkInstance>(0x1001);
        gFakeInstance.physicalDevice = FakeHandle<VkPhysicalDevice>(0x1002);
        gFakeInstance.device = FakeHandle<VkDevice>(0x1003);
        gFakeInstance.graphicsQueue = FakeHandle<VkQueue>(0x1004);
        gFakeInstance.getInstanceProcAddr = FakeGetInstanceProcAddr;
        gFakeInstance.queueFamilyIndex = 2;

        gFakeGraphics = {};
        gFakeGraphics.GetRenderer = FakeGetRenderer;
        gFakeGraphics.RegisterDeviceEventCallback = FakeRegisterDeviceEventCallback;
        gFakeGraphics.UnregisterDeviceEventCallback = FakeUnregisterDeviceEventCallback;
        gFakeGraphics.ReserveEventIDRange = FakeReserveEventIdRange;
        interfaces_ = {};
        interfaces_.GetInterface = FakeGetInterface;

        milestro::unity_render::Load(&interfaces_);
        ASSERT_NE(gDeviceEventCallback, nullptr);
        ASSERT_EQ(milestro::unity_render::GetDeviceEpochForExport(deviceEpoch_), MILESTRO_API_RET_OK);
    }

    void TearDown() override {
        milestro::unity_render::Unload();
        EXPECT_EQ(gDeviceEventCallback, nullptr);
    }

    IUnityInterfaces interfaces_{};
    uint64_t deviceEpoch_ = 0;
};

MilestroUnityRenderSubmission
StagingSubmission(void* nativeTexture, void* target, uint64_t generation, uint64_t deviceEpoch) {
    MilestroUnityRenderSubmission submission = DirectSubmission(nativeTexture, target, generation, deviceEpoch);
    submission.target.vulkanBackend = static_cast<int32_t>(VulkanBackendKind::StagingCopy);
    return submission;
}

void DestroyAndCollectStagingTarget(void*& target, uint64_t generation, uint64_t deviceEpoch) {
    int32_t retirementPending = -1;
    ASSERT_EQ(milestro::unity_render::vulkan::DestroyTarget(target, generation, deviceEpoch, retirementPending),
              MILESTRO_API_RET_OK);
    EXPECT_EQ(target, nullptr);
    if (retirementPending != 0) {
        gVulkanState.safeFrame = gVulkanState.currentFrame;
        EXPECT_FALSE(milestro::unity_render::vulkan::CollectRetiredTargets());
    }
}

TEST_F(VulkanProductionTest, DestroyRequiresPointerGenerationAndDeviceEpochIdentity) {
    constexpr uint64_t deviceEpoch = 7;
    void* first = nullptr;
    uint64_t firstGeneration = 0;
    ASSERT_EQ(milestro::unity_render::vulkan::CreateTarget(reinterpret_cast<void*>(0x2001),
                                                           16,
                                                           16,
                                                           static_cast<int32_t>(VulkanBackendKind::Direct),
                                                           deviceEpoch,
                                                           first,
                                                           firstGeneration),
              MILESTRO_API_RET_OK);

    void* firstDuplicate = first;
    int32_t retirementPending = -1;
    ASSERT_EQ(milestro::unity_render::vulkan::DestroyTarget(first, firstGeneration, deviceEpoch, retirementPending),
              MILESTRO_API_RET_OK);
    EXPECT_EQ(first, nullptr);

    void* second = nullptr;
    uint64_t secondGeneration = 0;
    ASSERT_EQ(milestro::unity_render::vulkan::CreateTarget(reinterpret_cast<void*>(0x2002),
                                                           16,
                                                           16,
                                                           static_cast<int32_t>(VulkanBackendKind::Direct),
                                                           deviceEpoch,
                                                           second,
                                                           secondGeneration),
              MILESTRO_API_RET_OK);
    ASSERT_NE(secondGeneration, firstGeneration);

    void* reusedAddressFromOldHandle = second;
    EXPECT_EQ(milestro::unity_render::vulkan::DestroyTarget(reusedAddressFromOldHandle,
                                                            firstGeneration,
                                                            deviceEpoch,
                                                            retirementPending),
              MILESTRO_API_RET_OK);
    EXPECT_EQ(reusedAddressFromOldHandle, nullptr);

    void* staleEpoch = second;
    EXPECT_EQ(milestro::unity_render::vulkan::DestroyTarget(staleEpoch,
                                                            secondGeneration,
                                                            deviceEpoch + 1,
                                                            retirementPending),
              MILESTRO_API_RET_OK);
    EXPECT_EQ(staleEpoch, nullptr);

    EXPECT_EQ(milestro::unity_render::vulkan::DestroyTarget(firstDuplicate,
                                                            firstGeneration,
                                                            deviceEpoch,
                                                            retirementPending),
              MILESTRO_API_RET_OK);
    EXPECT_EQ(firstDuplicate, nullptr);

    MilestroUnityRenderSubmission secondSubmission;
    secondSubmission.target.graphicsBackend = static_cast<int32_t>(MilestroUnityGraphicsBackend::Vulkan);
    secondSubmission.target.handleKind = static_cast<int32_t>(MilestroUnityRenderTextureHandleKind::NativeTexture);
    secondSubmission.target.nativeTextureHandle = reinterpret_cast<void*>(0x2002);
    secondSubmission.target.width = 16;
    secondSubmission.target.height = 16;
    secondSubmission.target.vulkanBackend = static_cast<int32_t>(VulkanBackendKind::Direct);
    secondSubmission.target.vulkanTarget = second;
    secondSubmission.target.vulkanTargetGeneration = secondGeneration;
    secondSubmission.target.deviceEpoch = deviceEpoch;
    EXPECT_TRUE(milestro::unity_render::vulkan::IsSubmissionTargetValid(secondSubmission));
    ASSERT_EQ(milestro::unity_render::vulkan::DestroyTarget(second, secondGeneration, deviceEpoch, retirementPending),
              MILESTRO_API_RET_OK);
    EXPECT_EQ(second, nullptr);
}

TEST_F(VulkanProductionTest, MissingEnumerateInstanceVersionAdvertisesOnlyVulkan10) {
    const PFN_vkVoidFunction resolved =
            milestro::unity_render::vulkan::ResolveInstanceProcWithVulkan10Fallback(gFakeInstance,
                                                                                    "vkEnumerateInstanceVersion");
    ASSERT_NE(resolved, nullptr);
    const auto enumerateVersion = reinterpret_cast<PFN_vkEnumerateInstanceVersion>(resolved);
    uint32_t apiVersion = 0;
    EXPECT_EQ(enumerateVersion(&apiVersion), VK_SUCCESS);
    EXPECT_EQ(apiVersion, VK_API_VERSION_1_0);
}

TEST_F(VulkanProductionTest, ExportedPayloadAbiInfoMatchesPinnedNativeLayout) {
    uint32_t abiVersion = 0;
    uint64_t fingerprint = 0;
    uint32_t targetSize = 0;
    uint32_t submissionSize = 0;
    uint32_t effectiveScaleOffset = 0;
    uint32_t deviceEpochOffset = 0;
    uint32_t targetOffset = 0;
    uint32_t completedOffset = 0;
    ASSERT_EQ(MilestroUnityRenderGetPayloadAbiInfo(abiVersion,
                                                   fingerprint,
                                                   targetSize,
                                                   submissionSize,
                                                   effectiveScaleOffset,
                                                   deviceEpochOffset,
                                                   targetOffset,
                                                   completedOffset),
              MILESTRO_API_RET_OK);
    EXPECT_EQ(abiVersion, kMilestroUnityRenderPayloadAbiVersion);
    EXPECT_EQ(fingerprint, kMilestroUnityRenderPayloadLayoutFingerprint);
    EXPECT_EQ(targetSize, 104U);
    EXPECT_EQ(submissionSize, 128U);
    EXPECT_EQ(effectiveScaleOffset, 88U);
    EXPECT_EQ(deviceEpochOffset, 96U);
    EXPECT_EQ(targetOffset, 8U);
    EXPECT_EQ(completedOffset, 124U);
}

TEST_F(VulkanProductionTest, DirectSubmitCompletesOnlyItsPreparedBatchExactlyOnce) {
    constexpr uint64_t deviceEpoch = 7;
    void* target = nullptr;
    uint64_t generation = 0;
    void* nativeTexture = reinterpret_cast<void*>(0x2001);
    ASSERT_EQ(milestro::unity_render::vulkan::CreateTarget(nativeTexture,
                                                           16,
                                                           16,
                                                           static_cast<int32_t>(VulkanBackendKind::Direct),
                                                           deviceEpoch,
                                                           target,
                                                           generation),
              MILESTRO_API_RET_OK);
    auto first = DirectSubmission(nativeTexture, target, generation, deviceEpoch);
    auto second = DirectSubmission(nativeTexture, target, generation, deviceEpoch);

    ASSERT_TRUE(milestro::unity_render::vulkan::BeginDirectBatch(101));
    EXPECT_EQ(milestro::unity_render::vulkan::PrepareDirect(101, &first).submission, nullptr);
    ASSERT_TRUE(milestro::unity_render::vulkan::FinishDirectBatchPrepare(101));
    ASSERT_TRUE(milestro::unity_render::vulkan::BeginDirectBatch(102));
    EXPECT_EQ(milestro::unity_render::vulkan::PrepareDirect(102, &second).submission, nullptr);
    ASSERT_TRUE(milestro::unity_render::vulkan::FinishDirectBatchPrepare(102));
    EXPECT_EQ(milestro::unity_render::vulkan::PendingDirectBatchCount(), 2U);

    UnityVulkanInstance replacementInstance = gFakeInstance;
    replacementInstance.device = FakeHandle<VkDevice>(0x1013);
    milestro::unity_render::vulkan::DirectBackend().Initialize(&gFakeVulkan, replacementInstance);

    EXPECT_TRUE(milestro::unity_render::vulkan::SubmitDirectPrepared(102, RecordCompletion));
    ASSERT_EQ(gCompletions.size(), 1U);
    EXPECT_EQ(gCompletions[0].first, &second);
    EXPECT_FALSE(milestro::unity_render::vulkan::SubmitDirectPrepared(102, RecordCompletion));
    EXPECT_FALSE(milestro::unity_render::vulkan::SubmitDirectPrepared(999, RecordCompletion));
    EXPECT_EQ(gCompletions.size(), 1U);
    EXPECT_EQ(milestro::unity_render::vulkan::PendingDirectBatchCount(), 1U);

    EXPECT_TRUE(milestro::unity_render::vulkan::SubmitDirectPrepared(101, RecordCompletion));
    ASSERT_EQ(gCompletions.size(), 2U);
    EXPECT_EQ(gCompletions[1].first, &first);
    EXPECT_EQ(milestro::unity_render::vulkan::PendingDirectBatchCount(), 0U);

    int32_t retirementPending = -1;
    ASSERT_EQ(milestro::unity_render::vulkan::DestroyTarget(target, generation, deviceEpoch, retirementPending),
              MILESTRO_API_RET_OK);
}

TEST_F(VulkanProductionTest, DirectBatchRejectsWrongPhaseAndCancelsOnTargetClose) {
    constexpr uint64_t deviceEpoch = 8;
    void* target = nullptr;
    uint64_t generation = 0;
    void* nativeTexture = reinterpret_cast<void*>(0x2011);
    ASSERT_EQ(milestro::unity_render::vulkan::CreateTarget(nativeTexture,
                                                           16,
                                                           16,
                                                           static_cast<int32_t>(VulkanBackendKind::Direct),
                                                           deviceEpoch,
                                                           target,
                                                           generation),
              MILESTRO_API_RET_OK);
    auto submission = DirectSubmission(nativeTexture, target, generation, deviceEpoch);

    EXPECT_FALSE(milestro::unity_render::vulkan::SubmitDirectPrepared(201, RecordCompletion));
    ASSERT_TRUE(milestro::unity_render::vulkan::BeginDirectBatch(201));
    EXPECT_FALSE(milestro::unity_render::vulkan::BeginDirectBatch(201));
    EXPECT_FALSE(milestro::unity_render::vulkan::SubmitDirectPrepared(201, RecordCompletion));
    EXPECT_EQ(milestro::unity_render::vulkan::PrepareDirect(201, &submission).submission, nullptr);
    ASSERT_TRUE(milestro::unity_render::vulkan::FinishDirectBatchPrepare(201));
    EXPECT_FALSE(milestro::unity_render::vulkan::FinishDirectBatchPrepare(201));

    milestro::unity_render::vulkan::FailDirectPreparedForTarget(target, generation, deviceEpoch, RecordCompletion);
    ASSERT_EQ(gCompletions.size(), 1U);
    EXPECT_EQ(gCompletions[0].first, &submission);
    EXPECT_FALSE(milestro::unity_render::vulkan::SubmitDirectPrepared(201, RecordCompletion));
    EXPECT_FALSE(milestro::unity_render::vulkan::FailDirectPrepared(201, RecordCompletion));
    EXPECT_EQ(gCompletions.size(), 1U);
    EXPECT_EQ(milestro::unity_render::vulkan::PendingDirectBatchCount(), 0U);

    int32_t retirementPending = -1;
    ASSERT_EQ(milestro::unity_render::vulkan::DestroyTarget(target, generation, deviceEpoch, retirementPending),
              MILESTRO_API_RET_OK);
}

TEST_F(VulkanProductionTest, StagingProductionPathCopiesAndReleasesExactlyOnce) {
    constexpr uint64_t deviceEpoch = 9;
    void* target = nullptr;
    uint64_t generation = 0;
    void* nativeTexture = reinterpret_cast<void*>(0x2021);
    ASSERT_EQ(milestro::unity_render::vulkan::CreateTarget(nativeTexture,
                                                           16,
                                                           16,
                                                           static_cast<int32_t>(VulkanBackendKind::StagingCopy),
                                                           deviceEpoch,
                                                           target,
                                                           generation),
              MILESTRO_API_RET_OK);
    auto submission = StagingSubmission(nativeTexture, target, generation, deviceEpoch);
    const auto result = milestro::unity_render::vulkan::RenderStaging(&submission);
    EXPECT_EQ(result.submission, &submission);
    EXPECT_EQ(result.status, MilestroUnityRenderSubmissionStatus::Drawn);
    EXPECT_EQ(gVulkanState.createCount, 1);
    EXPECT_EQ(gVulkanState.allocateCount, 1);
    EXPECT_EQ(gVulkanState.bindCount, 1);
    EXPECT_EQ(gVulkanState.mapCount, 1);
    EXPECT_EQ(gVulkanState.flushCount, 1);
    EXPECT_EQ(gVulkanState.copyCount, 1);
    EXPECT_EQ(gVulkanState.accessCount, 3);

    DestroyAndCollectStagingTarget(target, generation, deviceEpoch);
    EXPECT_EQ(gVulkanState.unmapCount, 1);
    EXPECT_EQ(gVulkanState.destroyCount, 1);
    EXPECT_EQ(gVulkanState.freeCount, 1);
    EXPECT_FALSE(milestro::unity_render::vulkan::CollectRetiredTargets());
    EXPECT_EQ(gVulkanState.unmapCount, 1);
    EXPECT_EQ(gVulkanState.destroyCount, 1);
    EXPECT_EQ(gVulkanState.freeCount, 1);
}

TEST_F(VulkanProductionTest, StagingPartialFailuresRollbackWithoutLeaksOrCopies) {
    const FailurePoint failures[] = {
            FailurePoint::CreateBuffer,
            FailurePoint::AllocateMemory,
            FailurePoint::BindMemory,
            FailurePoint::MapMemory,
            FailurePoint::Flush,
            FailurePoint::ObserveAccess,
            FailurePoint::TransferAccess,
            FailurePoint::SecondRecordingState,
    };
    uint64_t deviceEpoch = 20;
    for (const FailurePoint failure: failures) {
        SCOPED_TRACE(static_cast<int>(failure));
        gVulkanState = {};
        gVulkanState.failure = failure;
        void* target = nullptr;
        uint64_t generation = 0;
        void* nativeTexture = reinterpret_cast<void*>(0x2100 + deviceEpoch);
        ASSERT_EQ(milestro::unity_render::vulkan::CreateTarget(nativeTexture,
                                                               16,
                                                               16,
                                                               static_cast<int32_t>(VulkanBackendKind::StagingCopy),
                                                               deviceEpoch,
                                                               target,
                                                               generation),
                  MILESTRO_API_RET_OK);
        auto submission = StagingSubmission(nativeTexture, target, generation, deviceEpoch);
        const auto result = milestro::unity_render::vulkan::RenderStaging(&submission);
        EXPECT_EQ(result.submission, &submission);
        EXPECT_EQ(result.status, MilestroUnityRenderSubmissionStatus::Failed);
        EXPECT_EQ(gVulkanState.copyCount, 0);

        DestroyAndCollectStagingTarget(target, generation, deviceEpoch);
        EXPECT_EQ(gVulkanState.destroyCount, gVulkanState.createdCount);
        EXPECT_EQ(gVulkanState.freeCount, gVulkanState.allocatedCount);
        EXPECT_EQ(gVulkanState.unmapCount, gVulkanState.mappedCount);
        ++deviceEpoch;
    }
}

TEST_F(VulkanProductionTest, StagingRestoreFailureKeepsCommittedSlotUntilSafeFrame) {
    constexpr uint64_t deviceEpoch = 30;
    gVulkanState.failure = FailurePoint::RestoreAccess;
    void* target = nullptr;
    uint64_t generation = 0;
    void* nativeTexture = reinterpret_cast<void*>(0x2201);
    ASSERT_EQ(milestro::unity_render::vulkan::CreateTarget(nativeTexture,
                                                           16,
                                                           16,
                                                           static_cast<int32_t>(VulkanBackendKind::StagingCopy),
                                                           deviceEpoch,
                                                           target,
                                                           generation),
              MILESTRO_API_RET_OK);
    auto submission = StagingSubmission(nativeTexture, target, generation, deviceEpoch);
    const auto result = milestro::unity_render::vulkan::RenderStaging(&submission);
    EXPECT_EQ(result.status, MilestroUnityRenderSubmissionStatus::Failed);
    EXPECT_EQ(gVulkanState.copyCount, 1);

    int32_t retirementPending = 0;
    ASSERT_EQ(milestro::unity_render::vulkan::DestroyTarget(target, generation, deviceEpoch, retirementPending),
              MILESTRO_API_RET_OK);
    EXPECT_EQ(retirementPending, 1);
    EXPECT_TRUE(milestro::unity_render::vulkan::CollectRetiredTargets());
    EXPECT_EQ(gVulkanState.destroyCount, 0);
    gVulkanState.safeFrame = gVulkanState.currentFrame;
    EXPECT_FALSE(milestro::unity_render::vulkan::CollectRetiredTargets());
    EXPECT_EQ(gVulkanState.unmapCount, 1);
    EXPECT_EQ(gVulkanState.destroyCount, 1);
    EXPECT_EQ(gVulkanState.freeCount, 1);
}

TEST_F(VulkanProductionTest, StagingRejectsTargetWhenCopyFunctionIsUnavailable) {
    gVulkanState.failure = FailurePoint::MissingCopyFunction;
    milestro::unity_render::vulkan::OnGraphicsDeviceEvent(kUnityGfxDeviceEventInitialize,
                                                          &interfaces_,
                                                          kUnityGfxRendererVulkan,
                                                          103,
                                                          104,
                                                          105);
    void* target = reinterpret_cast<void*>(0x1);
    uint64_t generation = 99;
    EXPECT_EQ(milestro::unity_render::vulkan::CreateTarget(reinterpret_cast<void*>(0x2301),
                                                           16,
                                                           16,
                                                           static_cast<int32_t>(VulkanBackendKind::StagingCopy),
                                                           31,
                                                           target,
                                                           generation),
              MILESTRO_API_RET_FAILED);
    EXPECT_EQ(target, nullptr);
    EXPECT_EQ(generation, 0U);
    EXPECT_EQ(gVulkanState.createCount, 0);
}

TEST_F(VulkanDispatcherProductionTest, SubmitBeforePrepareFailsQueuedSubmissionAndLatePrepareIsInert) {
    void* nativeTexture = reinterpret_cast<void*>(0x2401);
    void* target = nullptr;
    uint64_t generation = 0;
    ASSERT_EQ(MilestroUnityRenderCreateVulkanTarget(nativeTexture,
                                                    16,
                                                    16,
                                                    static_cast<int32_t>(VulkanBackendKind::Direct),
                                                    deviceEpoch_,
                                                    target,
                                                    generation),
              MILESTRO_API_RET_OK);

    auto submission = DirectSubmission(nativeTexture, target, generation, deviceEpoch_);
    SetCurrentPayloadAbi(submission);
    ASSERT_EQ(milestro::unity_render::EnqueueSubmissionForExport(
                      static_cast<int32_t>(MilestroUnityGraphicsBackend::Vulkan),
                      &submission),
              MILESTRO_API_RET_OK);

    int32_t prepareEventId = -1;
    int32_t submitEventId = -1;
    ASSERT_EQ(milestro::unity_render::GetVulkanRenderEventIdsForExport(static_cast<int32_t>(VulkanBackendKind::Direct),
                                                                       prepareEventId,
                                                                       submitEventId),
              MILESTRO_API_RET_OK);
    auto renderEvent =
            reinterpret_cast<UnityRenderingEventAndData>(milestro::unity_render::GetRenderEventFuncForExport());
    ASSERT_NE(renderEvent, nullptr);

    TestRenderDrainPayload drain;
    drain.batchToken = 301;
    renderEvent(submitEventId, &drain);
    EXPECT_EQ(drain.completed, 1);
    EXPECT_EQ(drain.phase, 3);
    EXPECT_EQ(submission.completed, static_cast<int32_t>(MilestroUnityRenderSubmissionStatus::Failed));
    EXPECT_EQ(milestro::unity_render::vulkan::PendingDirectBatchCount(), 0U);

    renderEvent(prepareEventId, &drain);
    EXPECT_EQ(drain.completed, 1);
    EXPECT_EQ(drain.phase, 3);
    EXPECT_EQ(milestro::unity_render::vulkan::PendingDirectBatchCount(), 0U);

    int32_t retirementPending = -1;
    EXPECT_EQ(MilestroUnityRenderDestroyVulkanTarget(target, generation, deviceEpoch_, retirementPending),
              MILESTRO_API_RET_OK);
    EXPECT_EQ(target, nullptr);
}

TEST_F(VulkanDispatcherProductionTest, TargetCloseCancelsPreparedBatchAndLateSubmitDoesNotCompleteTwice) {
    void* nativeTexture = reinterpret_cast<void*>(0x2501);
    void* target = nullptr;
    uint64_t generation = 0;
    ASSERT_EQ(MilestroUnityRenderCreateVulkanTarget(nativeTexture,
                                                    16,
                                                    16,
                                                    static_cast<int32_t>(VulkanBackendKind::Direct),
                                                    deviceEpoch_,
                                                    target,
                                                    generation),
              MILESTRO_API_RET_OK);

    auto submission = DirectSubmission(nativeTexture, target, generation, deviceEpoch_);
    SetCurrentPayloadAbi(submission);
    ASSERT_EQ(milestro::unity_render::EnqueueSubmissionForExport(
                      static_cast<int32_t>(MilestroUnityGraphicsBackend::Vulkan),
                      &submission),
              MILESTRO_API_RET_OK);
    int32_t prepareEventId = -1;
    int32_t submitEventId = -1;
    ASSERT_EQ(milestro::unity_render::GetVulkanRenderEventIdsForExport(static_cast<int32_t>(VulkanBackendKind::Direct),
                                                                       prepareEventId,
                                                                       submitEventId),
              MILESTRO_API_RET_OK);
    auto renderEvent =
            reinterpret_cast<UnityRenderingEventAndData>(milestro::unity_render::GetRenderEventFuncForExport());
    TestRenderDrainPayload drain;
    drain.batchToken = 302;

    renderEvent(prepareEventId, &drain);
    ASSERT_EQ(drain.completed, 0);
    ASSERT_EQ(drain.phase, 2);
    ASSERT_EQ(submission.completed, static_cast<int32_t>(MilestroUnityRenderSubmissionStatus::Pending));
    ASSERT_EQ(milestro::unity_render::vulkan::PendingDirectBatchCount(), 1U);

    int32_t retirementPending = -1;
    ASSERT_EQ(MilestroUnityRenderDestroyVulkanTarget(target, generation, deviceEpoch_, retirementPending),
              MILESTRO_API_RET_OK);
    EXPECT_EQ(target, nullptr);
    EXPECT_EQ(submission.completed, static_cast<int32_t>(MilestroUnityRenderSubmissionStatus::Failed));
    EXPECT_EQ(milestro::unity_render::vulkan::PendingDirectBatchCount(), 0U);

    renderEvent(submitEventId, &drain);
    EXPECT_EQ(drain.completed, 1);
    EXPECT_EQ(drain.phase, 3);
    EXPECT_EQ(submission.completed, static_cast<int32_t>(MilestroUnityRenderSubmissionStatus::Failed));
    EXPECT_EQ(milestro::unity_render::vulkan::PendingDirectBatchCount(), 0U);
}

TEST_F(VulkanDispatcherProductionTest, SubmitCompletesOnlyTheMatchingPreparedDrain) {
    void* nativeTexture = reinterpret_cast<void*>(0x2601);
    void* target = nullptr;
    uint64_t generation = 0;
    ASSERT_EQ(MilestroUnityRenderCreateVulkanTarget(nativeTexture,
                                                    16,
                                                    16,
                                                    static_cast<int32_t>(VulkanBackendKind::Direct),
                                                    deviceEpoch_,
                                                    target,
                                                    generation),
              MILESTRO_API_RET_OK);
    auto first = DirectSubmission(nativeTexture, target, generation, deviceEpoch_);
    auto second = DirectSubmission(nativeTexture, target, generation, deviceEpoch_);
    SetCurrentPayloadAbi(first);
    SetCurrentPayloadAbi(second);

    int32_t prepareEventId = -1;
    int32_t submitEventId = -1;
    ASSERT_EQ(milestro::unity_render::GetVulkanRenderEventIdsForExport(static_cast<int32_t>(VulkanBackendKind::Direct),
                                                                       prepareEventId,
                                                                       submitEventId),
              MILESTRO_API_RET_OK);
    auto renderEvent =
            reinterpret_cast<UnityRenderingEventAndData>(milestro::unity_render::GetRenderEventFuncForExport());
    TestRenderDrainPayload firstDrain;
    firstDrain.batchToken = 401;
    TestRenderDrainPayload secondDrain;
    secondDrain.batchToken = 402;

    ASSERT_EQ(milestro::unity_render::EnqueueSubmissionForExport(
                      static_cast<int32_t>(MilestroUnityGraphicsBackend::Vulkan),
                      &first),
              MILESTRO_API_RET_OK);
    renderEvent(prepareEventId, &firstDrain);
    ASSERT_EQ(firstDrain.phase, 2);
    ASSERT_EQ(first.completed, static_cast<int32_t>(MilestroUnityRenderSubmissionStatus::Pending));

    ASSERT_EQ(milestro::unity_render::EnqueueSubmissionForExport(
                      static_cast<int32_t>(MilestroUnityGraphicsBackend::Vulkan),
                      &second),
              MILESTRO_API_RET_OK);
    renderEvent(prepareEventId, &secondDrain);
    ASSERT_EQ(secondDrain.phase, 2);
    ASSERT_EQ(second.completed, static_cast<int32_t>(MilestroUnityRenderSubmissionStatus::Pending));
    ASSERT_EQ(milestro::unity_render::vulkan::PendingDirectBatchCount(), 2U);

    UnityVulkanInstance replacementInstance = gFakeInstance;
    replacementInstance.device = FakeHandle<VkDevice>(0x1013);
    milestro::unity_render::vulkan::DirectBackend().Initialize(&gFakeVulkan, replacementInstance);

    renderEvent(submitEventId, &secondDrain);
    EXPECT_EQ(secondDrain.completed, 1);
    EXPECT_EQ(firstDrain.completed, 0);
    EXPECT_EQ(first.completed, static_cast<int32_t>(MilestroUnityRenderSubmissionStatus::Pending));
    EXPECT_NE(second.completed, static_cast<int32_t>(MilestroUnityRenderSubmissionStatus::Pending));
    EXPECT_EQ(milestro::unity_render::vulkan::PendingDirectBatchCount(), 1U);

    renderEvent(submitEventId, &secondDrain);
    EXPECT_EQ(first.completed, static_cast<int32_t>(MilestroUnityRenderSubmissionStatus::Pending));
    EXPECT_EQ(milestro::unity_render::vulkan::PendingDirectBatchCount(), 1U);

    renderEvent(submitEventId, &firstDrain);
    EXPECT_EQ(firstDrain.completed, 1);
    EXPECT_NE(first.completed, static_cast<int32_t>(MilestroUnityRenderSubmissionStatus::Pending));
    EXPECT_EQ(milestro::unity_render::vulkan::PendingDirectBatchCount(), 0U);

    int32_t retirementPending = -1;
    ASSERT_EQ(MilestroUnityRenderDestroyVulkanTarget(target, generation, deviceEpoch_, retirementPending),
              MILESTRO_API_RET_OK);
}

TEST_F(VulkanDispatcherProductionTest, DeviceResetFailsPreparedBatchAndRejectsLateSubmit) {
    void* nativeTexture = reinterpret_cast<void*>(0x2701);
    void* target = nullptr;
    uint64_t generation = 0;
    ASSERT_EQ(MilestroUnityRenderCreateVulkanTarget(nativeTexture,
                                                    16,
                                                    16,
                                                    static_cast<int32_t>(VulkanBackendKind::Direct),
                                                    deviceEpoch_,
                                                    target,
                                                    generation),
              MILESTRO_API_RET_OK);
    auto submission = DirectSubmission(nativeTexture, target, generation, deviceEpoch_);
    SetCurrentPayloadAbi(submission);
    ASSERT_EQ(milestro::unity_render::EnqueueSubmissionForExport(
                      static_cast<int32_t>(MilestroUnityGraphicsBackend::Vulkan),
                      &submission),
              MILESTRO_API_RET_OK);

    int32_t prepareEventId = -1;
    int32_t submitEventId = -1;
    ASSERT_EQ(milestro::unity_render::GetVulkanRenderEventIdsForExport(static_cast<int32_t>(VulkanBackendKind::Direct),
                                                                       prepareEventId,
                                                                       submitEventId),
              MILESTRO_API_RET_OK);
    auto renderEvent =
            reinterpret_cast<UnityRenderingEventAndData>(milestro::unity_render::GetRenderEventFuncForExport());
    TestRenderDrainPayload drain;
    drain.batchToken = 501;
    renderEvent(prepareEventId, &drain);
    ASSERT_EQ(drain.phase, 2);
    ASSERT_EQ(milestro::unity_render::vulkan::PendingDirectBatchCount(), 1U);

    gDeviceEventCallback(kUnityGfxDeviceEventBeforeReset);
    EXPECT_EQ(submission.completed, static_cast<int32_t>(MilestroUnityRenderSubmissionStatus::Failed));
    EXPECT_EQ(milestro::unity_render::vulkan::PendingDirectBatchCount(), 0U);

    renderEvent(submitEventId, &drain);
    EXPECT_EQ(drain.completed, 1);
    EXPECT_EQ(drain.phase, 3);
    EXPECT_EQ(submission.completed, static_cast<int32_t>(MilestroUnityRenderSubmissionStatus::Failed));
    gDeviceEventCallback(kUnityGfxDeviceEventAfterReset);

    int32_t retirementPending = -1;
    EXPECT_EQ(MilestroUnityRenderDestroyVulkanTarget(target, generation, deviceEpoch_, retirementPending),
              MILESTRO_API_RET_OK);
    EXPECT_EQ(target, nullptr);
}

} // namespace
