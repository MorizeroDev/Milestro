#ifndef MILESTRO_UNITY_RENDER_VULKAN_LIFECYCLE_H
#define MILESTRO_UNITY_RENDER_VULKAN_LIFECYCLE_H

#include <array>
#include <cstddef>
#include <cstdint>

#include "unity_render/MilestroUnityVulkanBackendKind.h"

namespace milestro::unity_render::vulkan {

constexpr bool SafeFrameHasReached(uint64_t lastUsedFrame, uint64_t safeFrame) noexcept {
    // Serial ordering is defined while Unity's producer/safe-frame distance is below half
    // the sequence space. The bounded ring stops accepting work when safe-frame progress stalls.
    constexpr uint64_t kMaximumOrderedFrameSpan = UINT64_MAX / 2;
    return safeFrame - lastUsedFrame <= kMaximumOrderedFrameSpan;
}

constexpr bool VulkanBackendKindFromRaw(int32_t raw, VulkanBackendKind& kind) noexcept {
    switch (static_cast<VulkanBackendKind>(raw)) {
        case VulkanBackendKind::Direct:
            kind = VulkanBackendKind::Direct;
            return true;
        case VulkanBackendKind::StagingCopy:
            kind = VulkanBackendKind::StagingCopy;
            return true;
        default:
            return false;
    }
}

class BackendBinding {
public:
    bool Bind(VulkanBackendKind kind) noexcept {
        if (!bound_) {
            kind_ = kind;
            bound_ = true;
            return true;
        }
        return kind_ == kind;
    }

    [[nodiscard]] bool Matches(VulkanBackendKind kind) const noexcept {
        return bound_ && kind_ == kind;
    }

    void Reset() noexcept {
        bound_ = false;
        kind_ = VulkanBackendKind::Direct;
    }

private:
    VulkanBackendKind kind_ = VulkanBackendKind::Direct;
    bool bound_ = false;
};

enum class StagingAcquireResult : uint8_t {
    Acquired,
    QueueFull,
    StaleGeneration,
};

enum class StagingSlotPhase : uint8_t {
    Empty,
    Ready,
    Reserved,
    InFlight,
};

template <std::size_t SlotCount>
class StagingRingLifecycle {
public:
    static_assert(SlotCount > 0);

    struct Slot {
        uint64_t generation = 0;
        uint64_t lastUsedFrame = 0;
        std::size_t capacity = 0;
        StagingSlotPhase phase = StagingSlotPhase::Empty;
    };

    bool BeginGeneration(uint64_t generation, std::size_t requiredBytes) noexcept {
        if (generation == 0 || requiredBytes == 0 || generation_ != 0) {
            return false;
        }
        generation_ = generation;
        requiredBytes_ = requiredBytes;
        nextSlot_ = 0;
        return true;
    }

    StagingAcquireResult
    Acquire(uint64_t generation, uint64_t safeFrame, std::size_t& slotIndex, bool& needsAllocation) noexcept {
        slotIndex = SlotCount;
        needsAllocation = false;
        if (generation == 0 || generation != generation_) {
            return StagingAcquireResult::StaleGeneration;
        }

        Collect(safeFrame);
        for (std::size_t offset = 0; offset < SlotCount; ++offset) {
            const std::size_t candidate = (nextSlot_ + offset) % SlotCount;
            Slot& slot = slots_[candidate];
            if (slot.phase != StagingSlotPhase::Empty && slot.phase != StagingSlotPhase::Ready) {
                continue;
            }

            needsAllocation = slot.phase == StagingSlotPhase::Empty || slot.generation != generation_ ||
                              slot.capacity < requiredBytes_;
            slot.generation = generation_;
            slot.phase = StagingSlotPhase::Reserved;
            slotIndex = candidate;
            nextSlot_ = (candidate + 1) % SlotCount;
            return StagingAcquireResult::Acquired;
        }
        return StagingAcquireResult::QueueFull;
    }

    bool MarkAllocated(std::size_t slotIndex, uint64_t generation, std::size_t capacity) noexcept {
        if (!IsReserved(slotIndex, generation) || capacity < requiredBytes_) {
            return false;
        }
        slots_[slotIndex].capacity = capacity;
        return true;
    }

    bool Commit(std::size_t slotIndex, uint64_t generation, uint64_t currentFrame) noexcept {
        if (!IsReserved(slotIndex, generation) || slots_[slotIndex].capacity < requiredBytes_) {
            return false;
        }
        Slot& slot = slots_[slotIndex];
        slot.lastUsedFrame = currentFrame;
        slot.phase = StagingSlotPhase::InFlight;
        return true;
    }

    bool Cancel(std::size_t slotIndex, uint64_t generation) noexcept {
        if (!IsReserved(slotIndex, generation)) {
            return false;
        }
        Slot& slot = slots_[slotIndex];
        slot.phase = slot.capacity == 0 ? StagingSlotPhase::Empty : StagingSlotPhase::Ready;
        nextSlot_ = slotIndex;
        return true;
    }

    std::size_t Collect(uint64_t safeFrame) noexcept {
        std::size_t retired = 0;
        for (Slot& slot: slots_) {
            if (slot.phase == StagingSlotPhase::InFlight && SafeFrameHasReached(slot.lastUsedFrame, safeFrame)) {
                slot.phase = StagingSlotPhase::Ready;
                ++retired;
            }
        }
        return retired;
    }

    std::size_t Shutdown() noexcept {
        std::size_t allocated = 0;
        for (Slot& slot: slots_) {
            if (slot.capacity != 0) {
                ++allocated;
            }
            slot = {};
        }
        generation_ = 0;
        requiredBytes_ = 0;
        nextSlot_ = 0;
        return allocated;
    }

    [[nodiscard]] uint64_t Generation() const noexcept {
        return generation_;
    }
    [[nodiscard]] std::size_t RequiredBytes() const noexcept {
        return requiredBytes_;
    }
    [[nodiscard]] const Slot& At(std::size_t slotIndex) const noexcept {
        return slots_[slotIndex];
    }

    [[nodiscard]] std::size_t AllocatedCount() const noexcept {
        return Count([](const Slot& slot) {
            return slot.capacity != 0;
        });
    }

    [[nodiscard]] std::size_t ReservedCount() const noexcept {
        return Count([](const Slot& slot) {
            return slot.phase == StagingSlotPhase::Reserved;
        });
    }

    [[nodiscard]] std::size_t InFlightCount() const noexcept {
        return Count([](const Slot& slot) {
            return slot.phase == StagingSlotPhase::InFlight;
        });
    }

    [[nodiscard]] bool HasInFlight() const noexcept {
        return InFlightCount() != 0;
    }

private:
    [[nodiscard]] bool IsReserved(std::size_t slotIndex, uint64_t generation) const noexcept {
        return slotIndex < SlotCount && generation != 0 && generation == generation_ &&
               slots_[slotIndex].generation == generation && slots_[slotIndex].phase == StagingSlotPhase::Reserved;
    }

    template <typename Predicate>
    [[nodiscard]] std::size_t Count(Predicate&& predicate) const noexcept {
        std::size_t count = 0;
        for (const Slot& slot: slots_) {
            if (predicate(slot)) {
                ++count;
            }
        }
        return count;
    }

    std::array<Slot, SlotCount> slots_{};
    uint64_t generation_ = 0;
    std::size_t requiredBytes_ = 0;
    std::size_t nextSlot_ = 0;
};

} // namespace milestro::unity_render::vulkan

#endif // MILESTRO_UNITY_RENDER_VULKAN_LIFECYCLE_H
