#include "unity_render/MilestroUnityRenderAsyncCallbackTracker.h"
#include "unity_render/MilestroUnityRenderVulkanMemorySupport.h"

#include <gtest/gtest.h>

#include <array>
#include <chrono>
#include <condition_variable>
#include <cstdint>
#include <mutex>
#include <thread>

namespace {

using milestro::unity_render::AsyncCallbackTracker;
using milestro::unity_render::vulkan::ClassifyVulkanHostMemorySupport;
using milestro::unity_render::vulkan::VulkanHostMemorySupport;
using namespace std::chrono_literals;

constexpr uint32_t kHostVisible = 1U << 0U;
constexpr uint32_t kHostCoherent = 1U << 1U;
constexpr uint32_t kDeviceLocal = 1U << 2U;

struct FakeMemoryType {
    uint32_t propertyFlags = 0;
};

struct FakeMemoryProperties {
    uint32_t memoryTypeCount = 0;
    std::array<FakeMemoryType, 4> memoryTypes{};
};

TEST(UnityRenderVulkanMemorySupportTest, ReportsNonCoherentWhenCoherentTypeIsNotACandidate) {
    FakeMemoryProperties properties;
    properties.memoryTypeCount = 3;
    properties.memoryTypes[0].propertyFlags = kDeviceLocal;
    properties.memoryTypes[1].propertyFlags = kHostVisible;
    properties.memoryTypes[2].propertyFlags = kHostVisible | kHostCoherent;

    constexpr uint32_t candidateBits = (1U << 0U) | (1U << 1U);
    EXPECT_EQ(ClassifyVulkanHostMemorySupport(
                  properties, candidateBits, kHostVisible, kHostCoherent),
              VulkanHostMemorySupport::NonCoherent);
}

TEST(UnityRenderVulkanMemorySupportTest, ReportsUnavailableWhenCandidatesAreNotHostVisible) {
    FakeMemoryProperties properties;
    properties.memoryTypeCount = 2;
    properties.memoryTypes[0].propertyFlags = kDeviceLocal;
    properties.memoryTypes[1].propertyFlags = 0;

    constexpr uint32_t candidateBits = 1U << 0U;
    EXPECT_EQ(ClassifyVulkanHostMemorySupport(
                  properties, candidateBits, kHostVisible, kHostCoherent),
              VulkanHostMemorySupport::Unavailable);
}

TEST(UnityRenderAsyncCallbackTrackerTest, WaitsForCallbackAfterRenderEventReturns) {
    AsyncCallbackTracker tracker;
    ASSERT_TRUE(tracker.StartAccepting());
    ASSERT_TRUE(tracker.TryAcquire());

    std::mutex stateMutex;
    std::condition_variable stateChanged;
    bool waitStarted = false;
    bool waitFinished = false;
    std::thread waiter([&] {
        {
            std::lock_guard lock(stateMutex);
            waitStarted = true;
        }
        stateChanged.notify_all();
        tracker.WaitForIdle();
        {
            std::lock_guard lock(stateMutex);
            waitFinished = true;
        }
        stateChanged.notify_all();
    });

    {
        std::unique_lock lock(stateMutex);
        EXPECT_TRUE(stateChanged.wait_for(lock, 1s, [&] { return waitStarted; }));
        EXPECT_FALSE(waitFinished);
    }
    EXPECT_TRUE(tracker.Release());
    {
        std::unique_lock lock(stateMutex);
        EXPECT_TRUE(stateChanged.wait_for(lock, 1s, [&] { return waitFinished; }));
    }
    waiter.join();
}

TEST(UnityRenderAsyncCallbackTrackerTest, ShutdownRejectsNewWorkAndWaitsForAcceptedCallback) {
    AsyncCallbackTracker tracker;
    ASSERT_TRUE(tracker.StartAccepting());
    ASSERT_TRUE(tracker.TryAcquire());

    tracker.StopAccepting();
    EXPECT_FALSE(tracker.TryAcquire());
    EXPECT_FALSE(tracker.StartAccepting());

    std::mutex stateMutex;
    std::condition_variable stateChanged;
    bool shutdownStarted = false;
    bool shutdownFinished = false;
    std::thread shutdown([&] {
        {
            std::lock_guard lock(stateMutex);
            shutdownStarted = true;
        }
        stateChanged.notify_all();
        tracker.WaitForIdle();
        {
            std::lock_guard lock(stateMutex);
            shutdownFinished = true;
        }
        stateChanged.notify_all();
    });

    {
        std::unique_lock lock(stateMutex);
        EXPECT_TRUE(stateChanged.wait_for(lock, 1s, [&] { return shutdownStarted; }));
        EXPECT_FALSE(shutdownFinished);
    }
    EXPECT_TRUE(tracker.Release());
    {
        std::unique_lock lock(stateMutex);
        EXPECT_TRUE(stateChanged.wait_for(lock, 1s, [&] { return shutdownFinished; }));
    }
    shutdown.join();
    EXPECT_EQ(tracker.PendingCount(), 0U);
    EXPECT_TRUE(tracker.StartAccepting());
    tracker.StopAccepting();
}

} // namespace
