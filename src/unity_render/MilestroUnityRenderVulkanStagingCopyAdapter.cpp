#include "unity_render/MilestroUnityRenderVulkanAdapter.h"

#include "unity_render/MilestroUnityRenderSubmissionDraw.h"

#include <array>
#include <cstddef>
#include <cstdint>
#include <cstring>
#include <limits>
#include <memory>
#include <new>
#include <vector>

#include "unity_render/MilestroUnityRenderLog.h"

#include "include/core/SkAlphaType.h"
#include "include/core/SkColorSpace.h"
#include "include/core/SkImageInfo.h"
#include "include/core/SkSurface.h"

namespace milestro::unity_render::vulkan {

namespace {

constexpr std::size_t kStagingSlotCount = 3;
constexpr std::size_t kMaximumPendingRetirements = 256;
constexpr std::size_t kMaximumTargetBytes = 192U * 1024U * 1024U;
constexpr std::size_t kMaximumGlobalBytes = 512U * 1024U * 1024U;

struct StageAccess {
    VkPipelineStageFlags stage = VK_PIPELINE_STAGE_ALL_COMMANDS_BIT;
    VkAccessFlags access = VK_ACCESS_MEMORY_READ_BIT | VK_ACCESS_MEMORY_WRITE_BIT;
};

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

struct StagingAllocation {
    VkBuffer buffer = VK_NULL_HANDLE;
    VkDeviceMemory memory = VK_NULL_HANDLE;
    void* mapped = nullptr;
    VkDeviceSize size = 0;
    bool coherent = false;
};

class StagingTargetState final : public VulkanTargetState {
public:
    StagingTargetState(uint64_t generation, std::size_t pixelBytes) : pixelBytes(pixelBytes) {
        valid = ring.BeginGeneration(generation, pixelBytes);
    }

    StagingRingLifecycle<kStagingSlotCount> ring;
    std::array<StagingAllocation, kStagingSlotCount> slots{};
    std::vector<uint8_t> pixels;
    std::size_t pixelBytes = 0;
    std::size_t accountedBytes = 0;
    bool valid = false;
};

class StagingCopyVulkanBackend final : public VulkanRenderBackend {
public:
    [[nodiscard]] VulkanBackendKind Kind() const noexcept override {
        return VulkanBackendKind::StagingCopy;
    }
    [[nodiscard]] bool RequiresSubmitEvent() const noexcept override {
        return false;
    }

    void Initialize(IUnityGraphicsVulkan* vulkan, const UnityVulkanInstance& instance) override {
        Shutdown();
        vulkan_ = vulkan;
        instance_ = instance;
        try {
            retired_.reserve(kMaximumPendingRetirements);
        } catch (const std::bad_alloc&) {
            MILESTROLOG_ERROR("Milestro Vulkan staging retirement registry allocation failed.");
            Shutdown();
            return;
        }
        LoadFunctions();
    }

    void Shutdown() override {
        CollectRetired(0, true);
        retired_.clear();
        allocatedBytes_ = 0;
        lastSafeFrame_ = 0;
        functionsReady_ = false;
        instance_ = {};
        vulkan_ = nullptr;
        getDeviceProcAddr_ = nullptr;
        getMemoryProperties_ = nullptr;
        createBuffer_ = nullptr;
        destroyBuffer_ = nullptr;
        getBufferMemoryRequirements_ = nullptr;
        allocateMemory_ = nullptr;
        freeMemory_ = nullptr;
        bindBufferMemory_ = nullptr;
        mapMemory_ = nullptr;
        unmapMemory_ = nullptr;
        flushMappedMemoryRanges_ = nullptr;
        cmdCopyBufferToImage_ = nullptr;
    }

    [[nodiscard]] std::unique_ptr<VulkanTargetState> CreateTarget(uint64_t generation,
                                                                  std::size_t pixelBytes) override {
        if (!functionsReady_ || pixelBytes == 0 || pixelBytes > kMaximumTargetBytes / (kStagingSlotCount + 1U)) {
            MILESTROLOG_ERROR("Milestro Vulkan staging target exceeds the per-target memory bound.");
            return nullptr;
        }
        auto state = std::make_unique<StagingTargetState>(generation, pixelBytes);
        if (!state->valid) {
            return nullptr;
        }
        return state;
    }

    bool RetireTarget(std::unique_ptr<VulkanTargetState>& state) override {
        if (state == nullptr) {
            return true;
        }
        auto* staging = static_cast<StagingTargetState*>(state.get());
        if (staging->accountedBytes == 0) {
            state.reset();
            return true;
        }
        if (retired_.size() >= kMaximumPendingRetirements) {
            return false;
        }
        retired_.push_back(std::unique_ptr<StagingTargetState>(static_cast<StagingTargetState*>(state.release())));
        return true;
    }

    void DestroyTargetImmediately(std::unique_ptr<VulkanTargetState> state) override {
        if (state == nullptr) {
            return;
        }
        auto* staging = static_cast<StagingTargetState*>(state.get());
        DestroyState(*staging);
    }

    [[nodiscard]] std::size_t PendingRetirementCount() const noexcept override {
        return retired_.size();
    }
    [[nodiscard]] bool HasPendingRetirements() const noexcept override {
        return !retired_.empty();
    }

    bool CollectRetired(uint64_t safeFrameNumber, bool force) override {
        lastSafeFrame_ = safeFrameNumber;
        auto write = retired_.begin();
        for (auto read = retired_.begin(); read != retired_.end(); ++read) {
            StagingTargetState& state = **read;
            state.ring.Collect(safeFrameNumber);
            if (force || !state.ring.HasInFlight()) {
                DestroyState(state);
                continue;
            }
            if (write != read) {
                *write = std::move(*read);
            }
            ++write;
        }
        retired_.erase(write, retired_.end());
        return !retired_.empty();
    }

    VulkanPrepareResult
    Prepare(VulkanTarget& target, MilestroUnityRenderSubmission& submission, PreparedVulkanSubmission&) override {
        auto* state = static_cast<StagingTargetState*>(target.state.get());
        if (state == nullptr || !state->valid || !functionsReady_ || vulkan_ == nullptr ||
            vulkan_->AccessTexture == nullptr || vulkan_->CommandRecordingState == nullptr) {
            return {};
        }

        UnityVulkanRecordingState beforeAccess{};
        if (!vulkan_->CommandRecordingState(&beforeAccess, kUnityVulkanGraphicsQueueAccess_DontCare) ||
            beforeAccess.commandBuffer == VK_NULL_HANDLE || beforeAccess.renderPass != VK_NULL_HANDLE) {
            MILESTROLOG_ERROR("Milestro Vulkan staging copy requires an outside-render-pass command buffer.");
            return {};
        }
        lastSafeFrame_ = beforeAccess.safeFrameNumber;
        CollectRetired(lastSafeFrame_, false);

        std::size_t slotIndex = kStagingSlotCount;
        bool needsAllocation = false;
        const StagingAcquireResult acquired =
                state->ring.Acquire(target.generation, beforeAccess.safeFrameNumber, slotIndex, needsAllocation);
        if (acquired == StagingAcquireResult::QueueFull) {
            return {MilestroUnityRenderSubmissionStatus::Skipped, false};
        }
        if (acquired != StagingAcquireResult::Acquired || slotIndex >= kStagingSlotCount) {
            return {};
        }

        if (!EnsureCpuPixels(*state) || (needsAllocation && !CreateSlot(*state, slotIndex))) {
            state->ring.Cancel(slotIndex, target.generation);
            return {};
        }
        StagingAllocation& slot = state->slots[slotIndex];
        if (!state->ring.MarkAllocated(slotIndex, target.generation, static_cast<std::size_t>(slot.size))) {
            state->ring.Cancel(slotIndex, target.generation);
            return {};
        }

        UnityVulkanImage observed{};
        if (!AccessTexture(target.nativeTexture,
                           VK_IMAGE_LAYOUT_UNDEFINED,
                           VK_PIPELINE_STAGE_TOP_OF_PIPE_BIT,
                           0,
                           kUnityVulkanResourceAccess_ObserveOnly,
                           observed) ||
            !ValidateImage(target, observed)) {
            state->ring.Cancel(slotIndex, target.generation);
            return {};
        }

        const SkImageInfo imageInfo = SkImageInfo::Make(target.width,
                                                        target.height,
                                                        ColorTypeForFormat(observed.format),
                                                        kPremul_SkAlphaType,
                                                        ColorSpaceForTarget(submission.target, observed.format));
        std::memcpy(slot.mapped, state->pixels.data(), state->pixelBytes);
        sk_sp<SkSurface> surface =
                SkSurfaces::WrapPixels(imageInfo, slot.mapped, static_cast<std::size_t>(target.width) * 4U);
        if (surface == nullptr) {
            state->ring.Cancel(slotIndex, target.generation);
            return {};
        }
        DrawSubmission(surface->getCanvas(), submission);
        if (!slot.coherent && !FlushSlot(slot)) {
            state->ring.Cancel(slotIndex, target.generation);
            return {};
        }

        UnityVulkanImage transferTarget{};
        if (!AccessTexture(target.nativeTexture,
                           VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                           VK_PIPELINE_STAGE_TRANSFER_BIT,
                           VK_ACCESS_TRANSFER_WRITE_BIT,
                           kUnityVulkanResourceAccess_PipelineBarrier,
                           transferTarget) ||
            !ValidateImage(target, transferTarget) || transferTarget.image != observed.image ||
            transferTarget.format != observed.format) {
            state->ring.Cancel(slotIndex, target.generation);
            return {};
        }

        const VkImageLayout restoreLayout = observed.layout == VK_IMAGE_LAYOUT_UNDEFINED
                                                    ? VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL
                                                    : observed.layout;
        UnityVulkanRecordingState recording{};
        if (!vulkan_->CommandRecordingState(&recording, kUnityVulkanGraphicsQueueAccess_DontCare) ||
            recording.commandBuffer == VK_NULL_HANDLE || recording.renderPass != VK_NULL_HANDLE) {
            RestoreTexture(target.nativeTexture, restoreLayout);
            state->ring.Cancel(slotIndex, target.generation);
            return {};
        }

        VkBufferImageCopy copy{};
        copy.bufferOffset = 0;
        copy.bufferRowLength = 0;
        copy.bufferImageHeight = 0;
        copy.imageSubresource.aspectMask = transferTarget.aspect & VK_IMAGE_ASPECT_COLOR_BIT;
        copy.imageSubresource.mipLevel = 0;
        copy.imageSubresource.baseArrayLayer = 0;
        copy.imageSubresource.layerCount = 1;
        copy.imageExtent = {static_cast<uint32_t>(target.width), static_cast<uint32_t>(target.height), 1};
        if (!state->ring.Commit(slotIndex, target.generation, recording.currentFrameNumber)) {
            RestoreTexture(target.nativeTexture, restoreLayout);
            state->ring.Cancel(slotIndex, target.generation);
            return {};
        }
        cmdCopyBufferToImage_(recording.commandBuffer,
                              slot.buffer,
                              transferTarget.image,
                              VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                              1,
                              &copy);
        std::memcpy(state->pixels.data(), slot.mapped, state->pixelBytes);
        if (!RestoreTexture(target.nativeTexture, restoreLayout)) {
            return {};
        }
        return {MilestroUnityRenderSubmissionStatus::Drawn, false};
    }

    MilestroUnityRenderSubmissionStatus Submit(PreparedVulkanSubmission&) override {
        return MilestroUnityRenderSubmissionStatus::Failed;
    }

private:
    template <typename Function>
    Function LoadDeviceFunction(const char* name) const {
        return getDeviceProcAddr_ != nullptr ? reinterpret_cast<Function>(getDeviceProcAddr_(instance_.device, name))
                                             : nullptr;
    }

    bool LoadFunctions() {
        functionsReady_ = false;
        if (instance_.getInstanceProcAddr == nullptr || instance_.instance == VK_NULL_HANDLE ||
            instance_.device == VK_NULL_HANDLE) {
            return false;
        }
        getDeviceProcAddr_ = reinterpret_cast<PFN_vkGetDeviceProcAddr>(
                instance_.getInstanceProcAddr(instance_.instance, "vkGetDeviceProcAddr"));
        getMemoryProperties_ = reinterpret_cast<PFN_vkGetPhysicalDeviceMemoryProperties>(
                instance_.getInstanceProcAddr(instance_.instance, "vkGetPhysicalDeviceMemoryProperties"));
        createBuffer_ = LoadDeviceFunction<PFN_vkCreateBuffer>("vkCreateBuffer");
        destroyBuffer_ = LoadDeviceFunction<PFN_vkDestroyBuffer>("vkDestroyBuffer");
        getBufferMemoryRequirements_ =
                LoadDeviceFunction<PFN_vkGetBufferMemoryRequirements>("vkGetBufferMemoryRequirements");
        allocateMemory_ = LoadDeviceFunction<PFN_vkAllocateMemory>("vkAllocateMemory");
        freeMemory_ = LoadDeviceFunction<PFN_vkFreeMemory>("vkFreeMemory");
        bindBufferMemory_ = LoadDeviceFunction<PFN_vkBindBufferMemory>("vkBindBufferMemory");
        mapMemory_ = LoadDeviceFunction<PFN_vkMapMemory>("vkMapMemory");
        unmapMemory_ = LoadDeviceFunction<PFN_vkUnmapMemory>("vkUnmapMemory");
        flushMappedMemoryRanges_ = LoadDeviceFunction<PFN_vkFlushMappedMemoryRanges>("vkFlushMappedMemoryRanges");
        cmdCopyBufferToImage_ = LoadDeviceFunction<PFN_vkCmdCopyBufferToImage>("vkCmdCopyBufferToImage");
        functionsReady_ =
                getDeviceProcAddr_ != nullptr && getMemoryProperties_ != nullptr && createBuffer_ != nullptr &&
                destroyBuffer_ != nullptr && getBufferMemoryRequirements_ != nullptr && allocateMemory_ != nullptr &&
                freeMemory_ != nullptr && bindBufferMemory_ != nullptr && mapMemory_ != nullptr &&
                unmapMemory_ != nullptr && flushMappedMemoryRanges_ != nullptr && cmdCopyBufferToImage_ != nullptr;
        return functionsReady_;
    }

    bool EnsureCpuPixels(StagingTargetState& state) {
        if (state.pixels.size() == state.pixelBytes) {
            return true;
        }
        if (state.accountedBytes > kMaximumTargetBytes - state.pixelBytes ||
            allocatedBytes_ > kMaximumGlobalBytes - state.pixelBytes) {
            return false;
        }
        try {
            state.pixels.resize(state.pixelBytes);
        } catch (const std::bad_alloc&) {
            return false;
        }
        state.accountedBytes += state.pixelBytes;
        allocatedBytes_ += state.pixelBytes;
        return true;
    }

    bool CreateSlot(StagingTargetState& state, std::size_t slotIndex) {
        if (slotIndex >= state.slots.size()) {
            return false;
        }
        StagingAllocation& slot = state.slots[slotIndex];
        if (slot.mapped != nullptr && slot.size >= state.pixelBytes) {
            return true;
        }

        VkBufferCreateInfo bufferInfo{};
        bufferInfo.sType = VK_STRUCTURE_TYPE_BUFFER_CREATE_INFO;
        bufferInfo.size = static_cast<VkDeviceSize>(state.pixelBytes);
        bufferInfo.usage = VK_BUFFER_USAGE_TRANSFER_SRC_BIT;
        bufferInfo.sharingMode = VK_SHARING_MODE_EXCLUSIVE;
        if (createBuffer_(instance_.device, &bufferInfo, nullptr, &slot.buffer) != VK_SUCCESS) {
            return false;
        }

        VkMemoryRequirements requirements{};
        getBufferMemoryRequirements_(instance_.device, slot.buffer, &requirements);
        if (requirements.size > std::numeric_limits<std::size_t>::max() || requirements.size > kMaximumTargetBytes ||
            requirements.size > kMaximumGlobalBytes ||
            state.accountedBytes > kMaximumTargetBytes - static_cast<std::size_t>(requirements.size) ||
            allocatedBytes_ > kMaximumGlobalBytes - static_cast<std::size_t>(requirements.size)) {
            DestroySlot(slot);
            return false;
        }

        VkPhysicalDeviceMemoryProperties properties{};
        getMemoryProperties_(instance_.physicalDevice, &properties);
        uint32_t memoryType = UINT32_MAX;
        bool coherent = false;
        for (uint32_t i = 0; i < properties.memoryTypeCount; ++i) {
            const bool allowed = (requirements.memoryTypeBits & (1U << i)) != 0;
            const VkMemoryPropertyFlags flags = properties.memoryTypes[i].propertyFlags;
            if (!allowed || (flags & VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT) == 0) {
                continue;
            }
            if (memoryType == UINT32_MAX || (flags & VK_MEMORY_PROPERTY_HOST_COHERENT_BIT) != 0) {
                memoryType = i;
                coherent = (flags & VK_MEMORY_PROPERTY_HOST_COHERENT_BIT) != 0;
            }
            if (coherent) {
                break;
            }
        }
        if (memoryType == UINT32_MAX) {
            DestroySlot(slot);
            return false;
        }

        VkMemoryAllocateInfo allocation{};
        allocation.sType = VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO;
        allocation.allocationSize = requirements.size;
        allocation.memoryTypeIndex = memoryType;
        if (allocateMemory_(instance_.device, &allocation, nullptr, &slot.memory) != VK_SUCCESS ||
            bindBufferMemory_(instance_.device, slot.buffer, slot.memory, 0) != VK_SUCCESS ||
            mapMemory_(instance_.device, slot.memory, 0, requirements.size, 0, &slot.mapped) != VK_SUCCESS) {
            DestroySlot(slot);
            return false;
        }
        slot.size = requirements.size;
        slot.coherent = coherent;
        state.accountedBytes += static_cast<std::size_t>(requirements.size);
        allocatedBytes_ += static_cast<std::size_t>(requirements.size);
        return true;
    }

    void DestroySlot(StagingAllocation& slot) {
        if (instance_.device != VK_NULL_HANDLE) {
            if (slot.mapped != nullptr && unmapMemory_ != nullptr && slot.memory != VK_NULL_HANDLE) {
                unmapMemory_(instance_.device, slot.memory);
            }
            if (slot.buffer != VK_NULL_HANDLE && destroyBuffer_ != nullptr) {
                destroyBuffer_(instance_.device, slot.buffer, nullptr);
            }
            if (slot.memory != VK_NULL_HANDLE && freeMemory_ != nullptr) {
                freeMemory_(instance_.device, slot.memory, nullptr);
            }
        }
        slot = {};
    }

    void DestroyState(StagingTargetState& state) {
        for (StagingAllocation& slot: state.slots) {
            DestroySlot(slot);
        }
        if (state.accountedBytes <= allocatedBytes_) {
            allocatedBytes_ -= state.accountedBytes;
        } else {
            allocatedBytes_ = 0;
        }
        state.accountedBytes = 0;
        state.pixels.clear();
        state.ring.Shutdown();
        state.valid = false;
    }

    bool FlushSlot(const StagingAllocation& slot) const {
        VkMappedMemoryRange range{};
        range.sType = VK_STRUCTURE_TYPE_MAPPED_MEMORY_RANGE;
        range.memory = slot.memory;
        range.offset = 0;
        range.size = VK_WHOLE_SIZE;
        return flushMappedMemoryRanges_(instance_.device, 1, &range) == VK_SUCCESS;
    }

    bool AccessTexture(void* nativeTexture,
                       VkImageLayout layout,
                       VkPipelineStageFlags stage,
                       VkAccessFlags access,
                       UnityVulkanResourceAccessMode mode,
                       UnityVulkanImage& image) const {
        image = {};
        return vulkan_ != nullptr && vulkan_->AccessTexture != nullptr &&
               vulkan_->AccessTexture(nativeTexture, UnityVulkanWholeImage, layout, stage, access, mode, &image);
    }

    bool RestoreTexture(void* nativeTexture, VkImageLayout layout) const {
        const StageAccess access = StageAccessForLayout(layout);
        UnityVulkanImage restored{};
        return AccessTexture(nativeTexture,
                             layout,
                             access.stage,
                             access.access,
                             kUnityVulkanResourceAccess_PipelineBarrier,
                             restored);
    }

    static bool IsSupportedFormat(VkFormat format) {
        return format == VK_FORMAT_R8G8B8A8_UNORM || format == VK_FORMAT_R8G8B8A8_SRGB ||
               format == VK_FORMAT_B8G8R8A8_UNORM || format == VK_FORMAT_B8G8R8A8_SRGB;
    }

    static bool ValidateImage(const VulkanTarget& target, const UnityVulkanImage& image) {
        return image.image != VK_NULL_HANDLE && IsSupportedFormat(image.format) &&
               image.samples == VK_SAMPLE_COUNT_1_BIT && image.type == VK_IMAGE_TYPE_2D && image.layers >= 1 &&
               image.mipCount >= 1 && image.extent.width == static_cast<uint32_t>(target.width) &&
               image.extent.height == static_cast<uint32_t>(target.height) &&
               (image.usage & VK_IMAGE_USAGE_TRANSFER_DST_BIT) != 0 && (image.aspect & VK_IMAGE_ASPECT_COLOR_BIT) != 0;
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

    IUnityGraphicsVulkan* vulkan_ = nullptr;
    UnityVulkanInstance instance_{};
    bool functionsReady_ = false;
    uint64_t lastSafeFrame_ = 0;
    std::size_t allocatedBytes_ = 0;
    std::vector<std::unique_ptr<StagingTargetState>> retired_;

    PFN_vkGetDeviceProcAddr getDeviceProcAddr_ = nullptr;
    PFN_vkGetPhysicalDeviceMemoryProperties getMemoryProperties_ = nullptr;
    PFN_vkCreateBuffer createBuffer_ = nullptr;
    PFN_vkDestroyBuffer destroyBuffer_ = nullptr;
    PFN_vkGetBufferMemoryRequirements getBufferMemoryRequirements_ = nullptr;
    PFN_vkAllocateMemory allocateMemory_ = nullptr;
    PFN_vkFreeMemory freeMemory_ = nullptr;
    PFN_vkBindBufferMemory bindBufferMemory_ = nullptr;
    PFN_vkMapMemory mapMemory_ = nullptr;
    PFN_vkUnmapMemory unmapMemory_ = nullptr;
    PFN_vkFlushMappedMemoryRanges flushMappedMemoryRanges_ = nullptr;
    PFN_vkCmdCopyBufferToImage cmdCopyBufferToImage_ = nullptr;
};

StagingCopyVulkanBackend gStagingCopyBackend;

} // namespace

VulkanRenderBackend& StagingCopyBackend() {
    return gStagingCopyBackend;
}

} // namespace milestro::unity_render::vulkan
