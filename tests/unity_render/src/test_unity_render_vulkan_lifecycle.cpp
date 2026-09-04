#include "unity_render/MilestroUnityRenderVulkanLifecycle.h"

#include <gtest/gtest.h>

#include <cstddef>
#include <cstdint>

namespace {

using milestro::unity_render::vulkan::BackendBinding;
using milestro::unity_render::vulkan::StagingAcquireResult;
using milestro::unity_render::vulkan::StagingRingLifecycle;
using milestro::unity_render::vulkan::VulkanBackendKind;
using milestro::unity_render::vulkan::VulkanBackendKindFromRaw;

TEST(UnityRenderVulkanLifecycle, RejectsInvalidBackendChoice) {
    VulkanBackendKind kind = VulkanBackendKind::Direct;
    EXPECT_FALSE(VulkanBackendKindFromRaw(0, kind));
    EXPECT_FALSE(VulkanBackendKindFromRaw(-1, kind));
    EXPECT_FALSE(VulkanBackendKindFromRaw(3, kind));
    EXPECT_TRUE(VulkanBackendKindFromRaw(1, kind));
    EXPECT_EQ(kind, VulkanBackendKind::Direct);
    EXPECT_TRUE(VulkanBackendKindFromRaw(2, kind));
    EXPECT_EQ(kind, VulkanBackendKind::StagingCopy);
}

TEST(UnityRenderVulkanLifecycle, BackendChoiceIsFixedForTargetLifetime) {
    BackendBinding binding;
    EXPECT_TRUE(binding.Bind(VulkanBackendKind::Direct));
    EXPECT_TRUE(binding.Matches(VulkanBackendKind::Direct));
    EXPECT_FALSE(binding.Bind(VulkanBackendKind::StagingCopy));
    EXPECT_FALSE(binding.Matches(VulkanBackendKind::StagingCopy));

    binding.Reset();
    EXPECT_TRUE(binding.Bind(VulkanBackendKind::StagingCopy));
    EXPECT_TRUE(binding.Matches(VulkanBackendKind::StagingCopy));
}

TEST(UnityRenderVulkanLifecycle, StagingRingIsBoundedAndRetiresOnlyAtSafeFrame) {
    StagingRingLifecycle<3> ring;
    ASSERT_TRUE(ring.BeginGeneration(7, 4096));

    for (std::size_t expected = 0; expected < 3; ++expected) {
        std::size_t slot = 99;
        bool needsAllocation = false;
        ASSERT_EQ(ring.Acquire(7, 0, slot, needsAllocation), StagingAcquireResult::Acquired);
        EXPECT_EQ(slot, expected);
        EXPECT_TRUE(needsAllocation);
        ASSERT_TRUE(ring.MarkAllocated(slot, 7, 4096));
        ASSERT_TRUE(ring.Commit(slot, 7, 10 + expected));
    }

    std::size_t unavailable = 99;
    bool needsAllocation = false;
    EXPECT_EQ(ring.Acquire(7, 9, unavailable, needsAllocation), StagingAcquireResult::QueueFull);
    EXPECT_EQ(ring.InFlightCount(), 3U);

    EXPECT_EQ(ring.Collect(10), 1U);
    EXPECT_EQ(ring.InFlightCount(), 2U);
    ASSERT_EQ(ring.Acquire(7, 10, unavailable, needsAllocation), StagingAcquireResult::Acquired);
    EXPECT_EQ(unavailable, 0U);
    EXPECT_FALSE(needsAllocation);
}

TEST(UnityRenderVulkanLifecycle, SafeFrameBoundaryRetiresExactlyOnce) {
    StagingRingLifecycle<1> ring;
    ASSERT_TRUE(ring.BeginGeneration(23, 2048));

    std::size_t slot = 99;
    bool needsAllocation = false;
    ASSERT_EQ(ring.Acquire(23, 0, slot, needsAllocation), StagingAcquireResult::Acquired);
    ASSERT_TRUE(ring.MarkAllocated(slot, 23, 2048));
    ASSERT_TRUE(ring.Commit(slot, 23, 42));

    EXPECT_EQ(ring.Collect(41), 0U);
    EXPECT_EQ(ring.InFlightCount(), 1U);
    EXPECT_EQ(ring.Collect(42), 1U);
    EXPECT_EQ(ring.Collect(42), 0U);
    EXPECT_EQ(ring.InFlightCount(), 0U);

    ASSERT_EQ(ring.Acquire(23, 42, slot, needsAllocation), StagingAcquireResult::Acquired);
    EXPECT_FALSE(needsAllocation);
}

TEST(UnityRenderVulkanLifecycle, ReservedSlotCannotBeCollectedOrCommittedTwice) {
    StagingRingLifecycle<1> ring;
    ASSERT_TRUE(ring.BeginGeneration(29, 1024));

    std::size_t slot = 99;
    bool needsAllocation = false;
    ASSERT_EQ(ring.Acquire(29, 0, slot, needsAllocation), StagingAcquireResult::Acquired);
    ASSERT_TRUE(ring.MarkAllocated(slot, 29, 1024));
    EXPECT_EQ(ring.Collect(UINT64_MAX), 0U);
    EXPECT_EQ(ring.ReservedCount(), 1U);
    ASSERT_TRUE(ring.Commit(slot, 29, 4));
    EXPECT_FALSE(ring.Commit(slot, 29, 4));
    EXPECT_FALSE(ring.Cancel(slot, 29));
}

TEST(UnityRenderVulkanLifecycle, CancellationDoesNotConsumeCapacity) {
    StagingRingLifecycle<2> ring;
    ASSERT_TRUE(ring.BeginGeneration(3, 1024));

    std::size_t slot = 99;
    bool needsAllocation = false;
    ASSERT_EQ(ring.Acquire(3, 0, slot, needsAllocation), StagingAcquireResult::Acquired);
    ASSERT_TRUE(ring.MarkAllocated(slot, 3, 1024));
    EXPECT_TRUE(ring.Cancel(slot, 3));
    EXPECT_EQ(ring.ReservedCount(), 0U);
    EXPECT_EQ(ring.InFlightCount(), 0U);

    std::size_t reacquired = 99;
    ASSERT_EQ(ring.Acquire(3, 0, reacquired, needsAllocation), StagingAcquireResult::Acquired);
    EXPECT_EQ(reacquired, slot);
    EXPECT_FALSE(needsAllocation);
}

TEST(UnityRenderVulkanLifecycle, OldGenerationCannotCommitAfterRecreate) {
    StagingRingLifecycle<3> ring;
    ASSERT_TRUE(ring.BeginGeneration(11, 2048));
    std::size_t slot = 99;
    bool needsAllocation = false;
    ASSERT_EQ(ring.Acquire(11, 0, slot, needsAllocation), StagingAcquireResult::Acquired);
    ASSERT_TRUE(ring.MarkAllocated(slot, 11, 2048));

    EXPECT_EQ(ring.Shutdown(), 1U);
    ASSERT_TRUE(ring.BeginGeneration(12, 4096));
    EXPECT_FALSE(ring.Commit(slot, 11, 1));
    EXPECT_FALSE(ring.Cancel(slot, 11));
    EXPECT_EQ(ring.InFlightCount(), 0U);
}

TEST(UnityRenderVulkanLifecycle, ShutdownReleasesAllocatedSlotsExactlyOnce) {
    StagingRingLifecycle<3> ring;
    ASSERT_TRUE(ring.BeginGeneration(19, 512));
    for (std::size_t expected = 0; expected < 2; ++expected) {
        std::size_t slot = 99;
        bool needsAllocation = false;
        ASSERT_EQ(ring.Acquire(19, 0, slot, needsAllocation), StagingAcquireResult::Acquired);
        ASSERT_TRUE(ring.MarkAllocated(slot, 19, 512));
        if (expected == 0) {
            ASSERT_TRUE(ring.Commit(slot, 19, 5));
        } else {
            ASSERT_TRUE(ring.Cancel(slot, 19));
        }
    }

    EXPECT_EQ(ring.Shutdown(), 2U);
    EXPECT_EQ(ring.Shutdown(), 0U);
    EXPECT_EQ(ring.AllocatedCount(), 0U);
    EXPECT_EQ(ring.InFlightCount(), 0U);
    EXPECT_EQ(ring.Generation(), 0U);
}

TEST(UnityRenderVulkanLifecycle, ShutdownPermitsFreshGenerationWithoutRevivingOldSlots) {
    StagingRingLifecycle<1> ring;
    ASSERT_TRUE(ring.BeginGeneration(31, 512));
    std::size_t oldSlot = 99;
    bool needsAllocation = false;
    ASSERT_EQ(ring.Acquire(31, 0, oldSlot, needsAllocation), StagingAcquireResult::Acquired);
    ASSERT_TRUE(ring.MarkAllocated(oldSlot, 31, 512));
    ASSERT_TRUE(ring.Commit(oldSlot, 31, 3));

    EXPECT_EQ(ring.Shutdown(), 1U);
    ASSERT_TRUE(ring.BeginGeneration(32, 1024));
    EXPECT_FALSE(ring.Commit(oldSlot, 31, 4));

    std::size_t newSlot = 99;
    ASSERT_EQ(ring.Acquire(32, 0, newSlot, needsAllocation), StagingAcquireResult::Acquired);
    EXPECT_EQ(newSlot, 0U);
    EXPECT_TRUE(needsAllocation);
    EXPECT_EQ(ring.RequiredBytes(), 1024U);
}

TEST(UnityRenderVulkanLifecycle, RejectsZeroGenerationAndSizeOverflowInputs) {
    StagingRingLifecycle<3> ring;
    EXPECT_FALSE(ring.BeginGeneration(0, 4096));
    EXPECT_FALSE(ring.BeginGeneration(1, 0));

    ASSERT_TRUE(ring.BeginGeneration(1, 4096));
    std::size_t slot = 99;
    bool needsAllocation = false;
    EXPECT_EQ(ring.Acquire(2, 0, slot, needsAllocation), StagingAcquireResult::StaleGeneration);
}

} // namespace
