#include "unity_render/MilestroUnityRenderAtomic.h"

#include <array>
#include <gtest/gtest.h>
#include <thread>

namespace milestro::unity_render {

TEST(UnityRenderAtomic, ReleaseStorePreservesPlainWordAbi) {
    int32_t word = 0;
    AtomicStoreRelease(word, 42);
    EXPECT_EQ(word, 42);
}

TEST(UnityRenderAtomic, StrongCompareExchangeUpdatesExpectedOnFailure) {
    int32_t word = 3;
    int32_t expected = 3;
    EXPECT_TRUE(AtomicCompareExchangeAcquireRelease(word, expected, 7));
    EXPECT_EQ(word, 7);
    EXPECT_EQ(expected, 3);
    EXPECT_FALSE(AtomicCompareExchangeAcquireRelease(word, expected, 9));
    EXPECT_EQ(word, 7);
    EXPECT_EQ(expected, 7);
}

TEST(UnityRenderAtomic, ContendedPlainWordIncrementDoesNotLoseUpdates) {
    int32_t word = 0;
    std::array<std::thread, 4> workers;
    for (auto& worker: workers) {
        worker = std::thread([&word] {
            for (int i = 0; i < 10000; ++i) {
                int32_t expected = 0;
                while (!AtomicCompareExchangeAcquireRelease(word, expected, expected + 1)) {}
            }
        });
    }
    for (auto& worker: workers) {
        worker.join();
    }
    EXPECT_EQ(word, 40000);
}

} // namespace milestro::unity_render
