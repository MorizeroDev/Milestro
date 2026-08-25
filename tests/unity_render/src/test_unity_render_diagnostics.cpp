#include "game/milestro_game_retcode.h"
#include "unity_render/MilestroUnityGraphicsBackend.h"
#include "unity_render/MilestroUnityRenderDiagnostics.h"
#include "unity_render/MilestroUnityRenderSubmission.h"

#include <gtest/gtest.h>

#include <cstddef>
#include <cstdint>
#include <limits>
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

using milestro::unity_render::kMilestroUnityRenderDiagnosticsAbiVersion;
using milestro::unity_render::kMilestroUnityRenderDiagnosticsSnapshotSize;
using milestro::unity_render::MilestroUnityRenderDiagnosticsSnapshot;

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

MilestroUnityRenderSubmission& KeepAliveSubmission(int32_t graphicsBackend,
                                                   int32_t width,
                                                   int32_t height,
                                                   float effectiveScale,
                                                   uint64_t deviceEpoch) {
    static std::vector<std::unique_ptr<MilestroUnityRenderSubmission>> submissions;
    auto submission = std::make_unique<MilestroUnityRenderSubmission>();
    submission->abiVersion = kMilestroUnityRenderPayloadAbiVersion;
    submission->structSize = kMilestroUnityRenderSubmissionSize;
    submission->target.abiVersion = kMilestroUnityRenderPayloadAbiVersion;
    submission->target.structSize = kMilestroUnityRenderTargetPayloadSize;
    submission->target.graphicsBackend = graphicsBackend;
    submission->target.width = width;
    submission->target.height = height;
    submission->target.effectiveScale = effectiveScale;
    submission->target.deviceEpoch = deviceEpoch;
    submissions.push_back(std::move(submission));
    return *submissions.back();
}

int64_t Enqueue(int32_t graphicsBackend, MilestroUnityRenderSubmission* submission) {
    return MilestroUnityRenderEnqueueSubmission(graphicsBackend, submission);
}

TEST(UnityRenderDiagnostics, SnapshotAbiAndLayoutAreStable) {
    EXPECT_EQ(kMilestroUnityRenderDiagnosticsAbiVersion, 1U);
    EXPECT_EQ(kMilestroUnityRenderDiagnosticsSnapshotSize, sizeof(MilestroUnityRenderDiagnosticsSnapshot));
    EXPECT_EQ(offsetof(MilestroUnityRenderDiagnosticsSnapshot, abiVersion), 0U);
    EXPECT_EQ(offsetof(MilestroUnityRenderDiagnosticsSnapshot, structSize), sizeof(uint32_t));
    EXPECT_EQ(offsetof(MilestroUnityRenderDiagnosticsSnapshot, acceptedSubmissionCount), 8U);
    EXPECT_EQ(offsetof(MilestroUnityRenderDiagnosticsSnapshot, rejectedSubmissionCount), 16U);
    EXPECT_EQ(offsetof(MilestroUnityRenderDiagnosticsSnapshot, hasLastAcceptedSubmission), 24U);
    EXPECT_EQ(offsetof(MilestroUnityRenderDiagnosticsSnapshot, lastAcceptedGraphicsBackend), 28U);
    EXPECT_EQ(offsetof(MilestroUnityRenderDiagnosticsSnapshot, lastAcceptedRasterWidth), 32U);
    EXPECT_EQ(offsetof(MilestroUnityRenderDiagnosticsSnapshot, lastAcceptedRasterHeight), 36U);
    EXPECT_EQ(offsetof(MilestroUnityRenderDiagnosticsSnapshot, lastAcceptedEffectiveScale), 40U);
    EXPECT_EQ(offsetof(MilestroUnityRenderDiagnosticsSnapshot, lastAcceptedDeviceEpoch), 48U);
    EXPECT_EQ(offsetof(MilestroUnityRenderDiagnosticsSnapshot, currentDeviceEpoch), 56U);

    const MilestroUnityRenderDiagnosticsSnapshot before = ReadProductionSnapshot();
    EXPECT_EQ(Enqueue(static_cast<int32_t>(MilestroUnityGraphicsBackend::Direct3D12), nullptr),
              MILESTRO_API_RET_FAILED);
    const MilestroUnityRenderDiagnosticsSnapshot after = ReadProductionSnapshot();

    EXPECT_EQ(after.abiVersion, kMilestroUnityRenderDiagnosticsAbiVersion);
    EXPECT_EQ(after.structSize, kMilestroUnityRenderDiagnosticsSnapshotSize);
    EXPECT_NE(after.currentDeviceEpoch, 0U);
    EXPECT_EQ(after.acceptedSubmissionCount, before.acceptedSubmissionCount);
    EXPECT_EQ(after.rejectedSubmissionCount, before.rejectedSubmissionCount + 1U);
}

TEST(UnityRenderDiagnostics, AcceptedAndRejectedCountsRemainIndependent) {
    const MilestroUnityRenderDiagnosticsSnapshot before = ReadProductionSnapshot();
    MilestroUnityRenderSubmission& invalidAbi = KeepAliveSubmission(
            static_cast<int32_t>(MilestroUnityGraphicsBackend::Direct3D12),
            3840,
            2160,
            2.0f,
            before.currentDeviceEpoch);
    invalidAbi.abiVersion = 0;
    EXPECT_EQ(Enqueue(invalidAbi.target.graphicsBackend, &invalidAbi), MILESTRO_API_RET_FAILED);

    MilestroUnityRenderSubmission& invalidScale = KeepAliveSubmission(
            static_cast<int32_t>(MilestroUnityGraphicsBackend::Direct3D12),
            3840,
            2160,
            0.0f,
            before.currentDeviceEpoch);
    EXPECT_EQ(Enqueue(invalidScale.target.graphicsBackend, &invalidScale), MILESTRO_API_RET_FAILED);

    MilestroUnityRenderSubmission& accepted = KeepAliveSubmission(
            static_cast<int32_t>(MilestroUnityGraphicsBackend::Direct3D12),
            3840,
            2160,
            2.0f,
            before.currentDeviceEpoch);
    EXPECT_EQ(Enqueue(accepted.target.graphicsBackend, &accepted), MILESTRO_API_RET_OK);

    const MilestroUnityRenderDiagnosticsSnapshot after = ReadProductionSnapshot();
    EXPECT_EQ(after.acceptedSubmissionCount, before.acceptedSubmissionCount + 1U);
    EXPECT_EQ(after.rejectedSubmissionCount, before.rejectedSubmissionCount + 2U);
    EXPECT_EQ(after.hasLastAcceptedSubmission, 1);
    EXPECT_EQ(after.lastAcceptedGraphicsBackend, accepted.target.graphicsBackend);
    EXPECT_EQ(after.lastAcceptedRasterWidth, 3840);
    EXPECT_EQ(after.lastAcceptedRasterHeight, 2160);
    EXPECT_FLOAT_EQ(after.lastAcceptedEffectiveScale, 2.0f);
    EXPECT_EQ(after.lastAcceptedDeviceEpoch, before.currentDeviceEpoch);
}

TEST(UnityRenderDiagnostics, RejectionNeverOverwritesLastAcceptedSubmission) {
    const MilestroUnityRenderDiagnosticsSnapshot before = ReadProductionSnapshot();
    MilestroUnityRenderSubmission& accepted = KeepAliveSubmission(
            static_cast<int32_t>(MilestroUnityGraphicsBackend::Vulkan), 2560, 1440, 1.5f, before.currentDeviceEpoch);
    EXPECT_EQ(Enqueue(accepted.target.graphicsBackend, &accepted), MILESTRO_API_RET_OK);
    const MilestroUnityRenderDiagnosticsSnapshot acceptedSnapshot = ReadProductionSnapshot();

    MilestroUnityRenderSubmission& staleEpoch = KeepAliveSubmission(
            static_cast<int32_t>(MilestroUnityGraphicsBackend::Vulkan),
            1,
            1,
            1.0f,
            before.currentDeviceEpoch + 1U);
    EXPECT_EQ(Enqueue(staleEpoch.target.graphicsBackend, &staleEpoch), MILESTRO_API_RET_FAILED);

    MilestroUnityRenderSubmission& backendMismatch = KeepAliveSubmission(
            static_cast<int32_t>(MilestroUnityGraphicsBackend::Vulkan), 1, 1, 1.0f, before.currentDeviceEpoch);
    EXPECT_EQ(Enqueue(static_cast<int32_t>(MilestroUnityGraphicsBackend::OpenGL), &backendMismatch),
              MILESTRO_API_RET_FAILED);

    MilestroUnityRenderSubmission& unknownBackend =
            KeepAliveSubmission(999, 1, 1, 1.0f, before.currentDeviceEpoch);
    EXPECT_EQ(Enqueue(unknownBackend.target.graphicsBackend, &unknownBackend), MILESTRO_API_RET_FAILED);

    const MilestroUnityRenderDiagnosticsSnapshot after = ReadProductionSnapshot();
    EXPECT_EQ(after.acceptedSubmissionCount, acceptedSnapshot.acceptedSubmissionCount);
    EXPECT_EQ(after.rejectedSubmissionCount, acceptedSnapshot.rejectedSubmissionCount + 3U);
    EXPECT_EQ(after.hasLastAcceptedSubmission, acceptedSnapshot.hasLastAcceptedSubmission);
    EXPECT_EQ(after.lastAcceptedGraphicsBackend, acceptedSnapshot.lastAcceptedGraphicsBackend);
    EXPECT_EQ(after.lastAcceptedRasterWidth, acceptedSnapshot.lastAcceptedRasterWidth);
    EXPECT_EQ(after.lastAcceptedRasterHeight, acceptedSnapshot.lastAcceptedRasterHeight);
    EXPECT_FLOAT_EQ(after.lastAcceptedEffectiveScale, acceptedSnapshot.lastAcceptedEffectiveScale);
    EXPECT_EQ(after.lastAcceptedDeviceEpoch, acceptedSnapshot.lastAcceptedDeviceEpoch);
}

TEST(UnityRenderDiagnostics, NewAcceptanceAtomicallyReplacesLastAcceptedSubmission) {
    const MilestroUnityRenderDiagnosticsSnapshot before = ReadProductionSnapshot();
    MilestroUnityRenderSubmission& first = KeepAliveSubmission(
            static_cast<int32_t>(MilestroUnityGraphicsBackend::OpenGL), 1920, 1080, 1.0f, before.currentDeviceEpoch);
    MilestroUnityRenderSubmission& second = KeepAliveSubmission(
            static_cast<int32_t>(MilestroUnityGraphicsBackend::OpenGLES), 3000, 2000, 1.25f, before.currentDeviceEpoch);
    EXPECT_EQ(Enqueue(first.target.graphicsBackend, &first), MILESTRO_API_RET_OK);

    MilestroUnityRenderSubmission& invalidTargetAbi = KeepAliveSubmission(
            static_cast<int32_t>(MilestroUnityGraphicsBackend::OpenGLES), 1, 1, 1.0f, before.currentDeviceEpoch);
    invalidTargetAbi.target.abiVersion = 0;
    EXPECT_EQ(Enqueue(invalidTargetAbi.target.graphicsBackend, &invalidTargetAbi), MILESTRO_API_RET_FAILED);

    MilestroUnityRenderSubmission& zeroEpoch = KeepAliveSubmission(
            static_cast<int32_t>(MilestroUnityGraphicsBackend::OpenGLES), 1, 1, 1.0f, 0);
    EXPECT_EQ(Enqueue(zeroEpoch.target.graphicsBackend, &zeroEpoch), MILESTRO_API_RET_FAILED);

    MilestroUnityRenderSubmission& nonFiniteScale = KeepAliveSubmission(
            static_cast<int32_t>(MilestroUnityGraphicsBackend::OpenGLES),
            1,
            1,
            std::numeric_limits<float>::quiet_NaN(),
            before.currentDeviceEpoch);
    EXPECT_EQ(Enqueue(nonFiniteScale.target.graphicsBackend, &nonFiniteScale), MILESTRO_API_RET_FAILED);

    EXPECT_EQ(Enqueue(second.target.graphicsBackend, &second), MILESTRO_API_RET_OK);

    const MilestroUnityRenderDiagnosticsSnapshot after = ReadProductionSnapshot();
    EXPECT_EQ(after.acceptedSubmissionCount, before.acceptedSubmissionCount + 2U);
    EXPECT_EQ(after.rejectedSubmissionCount, before.rejectedSubmissionCount + 3U);
    EXPECT_EQ(after.hasLastAcceptedSubmission, 1);
    EXPECT_EQ(after.lastAcceptedGraphicsBackend, second.target.graphicsBackend);
    EXPECT_EQ(after.lastAcceptedRasterWidth, 3000);
    EXPECT_EQ(after.lastAcceptedRasterHeight, 2000);
    EXPECT_FLOAT_EQ(after.lastAcceptedEffectiveScale, 1.25f);
    EXPECT_EQ(after.lastAcceptedDeviceEpoch, before.currentDeviceEpoch);
}

} // namespace
