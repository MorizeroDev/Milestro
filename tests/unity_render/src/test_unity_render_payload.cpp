#include "unity_render/MilestroUnityRenderSubmission.h"

#include <gtest/gtest.h>

#include <cstddef>
#include <limits>

namespace {

MilestroUnityRenderSubmission CurrentSubmission(uint64_t deviceEpoch = 7, float effectiveScale = 2.0f) {
    MilestroUnityRenderSubmission submission;
    submission.abiVersion = kMilestroUnityRenderPayloadAbiVersion;
    submission.structSize = kMilestroUnityRenderSubmissionSize;
    submission.target.abiVersion = kMilestroUnityRenderPayloadAbiVersion;
    submission.target.structSize = kMilestroUnityRenderTargetPayloadSize;
    submission.target.effectiveScale = effectiveScale;
    submission.target.deviceEpoch = deviceEpoch;
    return submission;
}

TEST(UnityRenderPayload, CurrentAbiScaleAndEpochAreAccepted) {
    const MilestroUnityRenderSubmission submission = CurrentSubmission();
    EXPECT_TRUE(MilestroUnityRenderSubmissionHasCurrentAbi(&submission, 7));
}

TEST(UnityRenderPayload, OldOrMismatchedLayoutsFailClosedBeforeUse) {
    MilestroUnityRenderSubmission submission = CurrentSubmission();

    submission.abiVersion = 0;
    EXPECT_FALSE(MilestroUnityRenderSubmissionHasCurrentAbi(&submission, 7));
    submission.abiVersion = kMilestroUnityRenderPayloadAbiVersion;

    submission.structSize = kMilestroUnityRenderSubmissionSize - 1;
    EXPECT_FALSE(MilestroUnityRenderSubmissionHasCurrentAbi(&submission, 7));
    submission.structSize = kMilestroUnityRenderSubmissionSize;

    submission.target.abiVersion = 0;
    EXPECT_FALSE(MilestroUnityRenderSubmissionHasCurrentAbi(&submission, 7));
    submission.target.abiVersion = kMilestroUnityRenderPayloadAbiVersion;

    submission.target.structSize = kMilestroUnityRenderTargetPayloadSize - 1;
    EXPECT_FALSE(MilestroUnityRenderSubmissionHasCurrentAbi(&submission, 7));
}

TEST(UnityRenderPayload, InvalidScaleAndStaleOrZeroEpochFailClosed) {
    MilestroUnityRenderSubmission submission = CurrentSubmission();

    submission.target.effectiveScale = 0.0f;
    EXPECT_FALSE(MilestroUnityRenderSubmissionHasCurrentAbi(&submission, 7));
    submission.target.effectiveScale = std::numeric_limits<float>::quiet_NaN();
    EXPECT_FALSE(MilestroUnityRenderSubmissionHasCurrentAbi(&submission, 7));
    submission.target.effectiveScale = std::numeric_limits<float>::infinity();
    EXPECT_FALSE(MilestroUnityRenderSubmissionHasCurrentAbi(&submission, 7));
    submission.target.effectiveScale = -1.0f;
    EXPECT_FALSE(MilestroUnityRenderSubmissionHasCurrentAbi(&submission, 7));
    submission.target.effectiveScale = 2.0f;

    submission.target.deviceEpoch = 0;
    EXPECT_FALSE(MilestroUnityRenderSubmissionHasCurrentAbi(&submission, 7));
    submission.target.deviceEpoch = 6;
    EXPECT_FALSE(MilestroUnityRenderSubmissionHasCurrentAbi(&submission, 7));
    submission.target.deviceEpoch = 7;
    EXPECT_FALSE(MilestroUnityRenderSubmissionHasCurrentAbi(&submission, 0));
}

TEST(UnityRenderPayload, LayoutOffsetsAndEpochSaturationAreStable) {
    EXPECT_NE(kMilestroUnityRenderPayloadLayoutFingerprint, 0U);
    EXPECT_EQ(kMilestroUnityRenderPayloadLayoutFingerprint, MilestroUnityRenderPayloadLayoutFingerprint());
    EXPECT_EQ(offsetof(MilestroUnityRenderTargetPayload, abiVersion), 0U);
    EXPECT_EQ(offsetof(MilestroUnityRenderTargetPayload, structSize), sizeof(uint32_t));
    EXPECT_EQ(kMilestroUnityRenderTargetEffectiveScaleOffset,
              offsetof(MilestroUnityRenderTargetPayload, effectiveScale));
    EXPECT_EQ(kMilestroUnityRenderTargetDeviceEpochOffset, offsetof(MilestroUnityRenderTargetPayload, deviceEpoch));
    EXPECT_EQ(kMilestroUnityRenderSubmissionTargetOffset, offsetof(MilestroUnityRenderSubmission, target));
    EXPECT_EQ(kMilestroUnityRenderSubmissionCompletedOffset, offsetof(MilestroUnityRenderSubmission, completed));

    EXPECT_EQ(MilestroUnityRenderNextDeviceEpoch(0), 1U);
    EXPECT_EQ(MilestroUnityRenderNextDeviceEpoch(1), 2U);
    EXPECT_EQ(MilestroUnityRenderNextDeviceEpoch(std::numeric_limits<uint64_t>::max()),
              std::numeric_limits<uint64_t>::max());
}

} // namespace
