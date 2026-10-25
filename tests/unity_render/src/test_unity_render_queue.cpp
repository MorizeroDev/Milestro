#include "game/milestro_game_retcode.h"
#include "unity_render/MilestroUnityGraphicsBackend.h"
#include "unity_render/MilestroUnityRenderDiagnostics.h"
#include "unity_render/MilestroUnityRenderSubmission.h"

#include <gtest/gtest.h>

#include <cstdint>
#include <memory>
#include <vector>

#if defined(_WIN32) && defined(MILESTRO_DLL)
#define MILESTRO_TEST_IMPORT __declspec(dllimport)
#else
#define MILESTRO_TEST_IMPORT
#endif

extern "C" {

MILESTRO_TEST_IMPORT int64_t MilestroUnityRenderEnqueueSubmission(int32_t graphicsBackend, void* submission);
MILESTRO_TEST_IMPORT int64_t MilestroUnityRenderGetDiagnosticsSnapshot(uint32_t& abiVersion,
                                                                       uint32_t& structSize,
                                                                       uint64_t& acceptedSubmissionCount,
                                                                       uint64_t& rejectedSubmissionCount,
                                                                       int32_t& hasLastAcceptedSubmission,
                                                                       int32_t& lastAcceptedGraphicsBackend,
                                                                       int32_t& lastAcceptedRasterWidth,
                                                                       int32_t& lastAcceptedRasterHeight,
                                                                       float& lastAcceptedEffectiveScale,
                                                                       uint64_t& lastAcceptedDeviceEpoch,
                                                                       uint64_t& currentDeviceEpoch);

}

#undef MILESTRO_TEST_IMPORT

namespace {

using milestro::unity_render::MilestroUnityRenderDiagnosticsSnapshot;

constexpr std::size_t kQueueCapacity = 256;

MilestroUnityRenderDiagnosticsSnapshot ReadProductionSnapshot() {
    MilestroUnityRenderDiagnosticsSnapshot snapshot;
    EXPECT_EQ(MilestroUnityRenderGetDiagnosticsSnapshot(snapshot.abiVersion,
                                                         snapshot.structSize,
                                                         snapshot.acceptedSubmissionCount,
                                                         snapshot.rejectedSubmissionCount,
                                                         snapshot.hasLastAcceptedSubmission,
                                                         snapshot.lastAcceptedGraphicsBackend,
                                                         snapshot.lastAcceptedRasterWidth,
                                                         snapshot.lastAcceptedRasterHeight,
                                                         snapshot.lastAcceptedEffectiveScale,
                                                         snapshot.lastAcceptedDeviceEpoch,
                                                         snapshot.currentDeviceEpoch),
              MILESTRO_API_RET_OK);
    return snapshot;
}

MilestroUnityRenderSubmission& KeepAliveSubmission(int32_t graphicsBackend, uint64_t deviceEpoch) {
    static std::vector<std::unique_ptr<MilestroUnityRenderSubmission>> submissions;
    auto submission = std::make_unique<MilestroUnityRenderSubmission>();
    submission->abiVersion = kMilestroUnityRenderPayloadAbiVersion;
    submission->structSize = kMilestroUnityRenderSubmissionSize;
    submission->target.abiVersion = kMilestroUnityRenderPayloadAbiVersion;
    submission->target.structSize = kMilestroUnityRenderTargetPayloadSize;
    submission->target.graphicsBackend = graphicsBackend;
    submission->target.width = 16;
    submission->target.height = 16;
    submission->target.effectiveScale = 1.0f;
    submission->target.deviceEpoch = deviceEpoch;
    submissions.push_back(std::move(submission));
    return *submissions.back();
}

TEST(UnityRenderQueue, FullRouteDefersOnlyTheRejectedSubmission) {
    const auto before = ReadProductionSnapshot();
    const int32_t fullRoute = static_cast<int32_t>(MilestroUnityGraphicsBackend::OpenGL);
    for (std::size_t index = 0; index < kQueueCapacity; ++index) {
        auto& submission = KeepAliveSubmission(fullRoute, before.currentDeviceEpoch);
        ASSERT_EQ(MilestroUnityRenderEnqueueSubmission(fullRoute, &submission), MILESTRO_API_RET_OK);
    }

    auto& rejected = KeepAliveSubmission(fullRoute, before.currentDeviceEpoch);
    const int64_t status = MilestroUnityRenderEnqueueSubmission(fullRoute, &rejected);
    const auto raw = static_cast<uint64_t>(status);

    ASSERT_LT(status, 0);
    EXPECT_EQ((raw >> MILESTRO_API_STATUS_MAGIC_SHIFT) & MILESTRO_API_STATUS_MAGIC_MASK,
              MILESTRO_API_STATUS_MAGIC);
    EXPECT_EQ((raw >> MILESTRO_API_STATUS_SEVERITY_SHIFT) & MILESTRO_API_STATUS_SEVERITY_MASK,
              MILESTRO_API_STATUS_SEVERITY_FAILURE);
    EXPECT_EQ((raw >> MILESTRO_API_STATUS_FLAGS_SHIFT) & MILESTRO_API_STATUS_FLAGS_MASK,
              MILESTRO_API_STATUS_FLAG_DEFERRED | MILESTRO_API_STATUS_FLAG_RETRYABLE);
    EXPECT_EQ((raw >> MILESTRO_API_STATUS_OPERATION_SHIFT) & MILESTRO_API_STATUS_OPERATION_MASK,
              MILESTRO_API_STATUS_OPERATION_ENQUEUE);
    EXPECT_EQ(raw & MILESTRO_API_STATUS_DETAIL_MASK, MILESTRO_API_STATUS_DETAIL_QUEUE_FULL);
    EXPECT_EQ(rejected.completed, static_cast<int32_t>(MilestroUnityRenderSubmissionStatus::Pending));

    const int32_t independentRoute = static_cast<int32_t>(MilestroUnityGraphicsBackend::Direct3D11);
    auto& independent = KeepAliveSubmission(independentRoute, before.currentDeviceEpoch);
    EXPECT_EQ(MilestroUnityRenderEnqueueSubmission(independentRoute, &independent), MILESTRO_API_RET_OK);

    const auto after = ReadProductionSnapshot();
    EXPECT_EQ(after.acceptedSubmissionCount, before.acceptedSubmissionCount + kQueueCapacity + 1U);
    EXPECT_EQ(after.rejectedSubmissionCount, before.rejectedSubmissionCount + 1U);
}

} // namespace
