#include "ScrollPhaseGestureTracker.h"
#include "ScrollPhaseLease.h"

#include <gtest/gtest.h>

namespace {

using milestro::input::ScrollPhase;
using milestro::input::ScrollPhaseGestureTracker;
using milestro::input::ScrollPhaseLease;
using milestro::input::ScrollPhaseMonitorMode;
using milestro::input::ScrollPhaseMonitorResult;
using milestro::input::ScrollPhasePluginUnloadDecision;

TEST(ScrollPhaseMonitorModeTest, AcceptsOnlyDefinedModes) {
    EXPECT_EQ(0, static_cast<int32_t>(ScrollPhaseMonitorMode::PassThrough));
    EXPECT_EQ(1, static_cast<int32_t>(ScrollPhaseMonitorMode::CaptureSamples));
    EXPECT_EQ(2, static_cast<int32_t>(ScrollPhaseMonitorMode::ReadProperties));
    EXPECT_EQ(3, static_cast<int32_t>(ScrollPhaseMonitorMode::ReadEventProperties));
    EXPECT_EQ(4, static_cast<int32_t>(ScrollPhaseMonitorMode::ReadEventScalars));
    EXPECT_EQ(5, static_cast<int32_t>(ScrollPhaseMonitorMode::WriteLocalPod));
    EXPECT_EQ(6, static_cast<int32_t>(ScrollPhaseMonitorMode::ReadPhasesOnly));
    EXPECT_EQ(7, static_cast<int32_t>(ScrollPhaseMonitorMode::ReadPhasesTimestamp));
    EXPECT_TRUE(milestro::input::IsValidScrollPhaseMonitorMode(ScrollPhaseMonitorMode::PassThrough));
    EXPECT_TRUE(milestro::input::IsValidScrollPhaseMonitorMode(ScrollPhaseMonitorMode::CaptureSamples));
    EXPECT_TRUE(milestro::input::IsValidScrollPhaseMonitorMode(ScrollPhaseMonitorMode::ReadProperties));
    EXPECT_TRUE(milestro::input::IsValidScrollPhaseMonitorMode(ScrollPhaseMonitorMode::ReadEventProperties));
    EXPECT_TRUE(milestro::input::IsValidScrollPhaseMonitorMode(ScrollPhaseMonitorMode::ReadEventScalars));
    EXPECT_TRUE(milestro::input::IsValidScrollPhaseMonitorMode(ScrollPhaseMonitorMode::WriteLocalPod));
    EXPECT_TRUE(milestro::input::IsValidScrollPhaseMonitorMode(ScrollPhaseMonitorMode::ReadPhasesOnly));
    EXPECT_TRUE(milestro::input::IsValidScrollPhaseMonitorMode(ScrollPhaseMonitorMode::ReadPhasesTimestamp));
    EXPECT_FALSE(milestro::input::IsValidScrollPhaseMonitorMode(static_cast<ScrollPhaseMonitorMode>(-1)));
    EXPECT_FALSE(milestro::input::IsValidScrollPhaseMonitorMode(static_cast<ScrollPhaseMonitorMode>(8)));
}

TEST(ScrollPhaseMonitorModeTest, ModesHaveMutuallyExclusiveCallbackSideEffects) {
    int sampleCount = 0;
    int propertyReadCount = 0;
    int eventPropertyReadCount = 0;
    int eventScalarReadCount = 0;
    int localPodWriteCount = 0;
    int phaseReadCount = 0;
    int phaseTimestampReadCount = 0;
    if (milestro::input::ShouldCaptureScrollPhaseSamples(ScrollPhaseMonitorMode::PassThrough)) {
        ++sampleCount;
    }
    if (milestro::input::ShouldReadScrollPhaseProperties(ScrollPhaseMonitorMode::PassThrough)) {
        ++propertyReadCount;
    }
    if (milestro::input::ShouldReadScrollPhaseEventProperties(ScrollPhaseMonitorMode::PassThrough)) {
        ++eventPropertyReadCount;
    }
    if (milestro::input::ShouldReadScrollPhaseEventScalars(ScrollPhaseMonitorMode::PassThrough)) {
        ++eventScalarReadCount;
    }
    if (milestro::input::ShouldWriteScrollPhaseLocalPod(ScrollPhaseMonitorMode::PassThrough)) {
        ++localPodWriteCount;
    }
    if (milestro::input::ShouldReadScrollPhasesOnly(ScrollPhaseMonitorMode::PassThrough)) {
        ++phaseReadCount;
    }
    if (milestro::input::ShouldReadScrollPhasesTimestamp(ScrollPhaseMonitorMode::PassThrough)) {
        ++phaseTimestampReadCount;
    }

    EXPECT_EQ(0, sampleCount);
    EXPECT_EQ(0, propertyReadCount);
    EXPECT_EQ(0, eventPropertyReadCount);
    EXPECT_EQ(0, eventScalarReadCount);
    EXPECT_EQ(0, localPodWriteCount);
    EXPECT_EQ(0, phaseReadCount);
    EXPECT_EQ(0, phaseTimestampReadCount);
    EXPECT_TRUE(milestro::input::ShouldCaptureScrollPhaseSamples(ScrollPhaseMonitorMode::CaptureSamples));
    EXPECT_FALSE(milestro::input::ShouldReadScrollPhaseProperties(ScrollPhaseMonitorMode::CaptureSamples));
    EXPECT_FALSE(milestro::input::ShouldReadScrollPhaseEventProperties(ScrollPhaseMonitorMode::CaptureSamples));
    EXPECT_FALSE(milestro::input::ShouldReadScrollPhaseEventScalars(ScrollPhaseMonitorMode::CaptureSamples));
    EXPECT_FALSE(milestro::input::ShouldWriteScrollPhaseLocalPod(ScrollPhaseMonitorMode::CaptureSamples));
    EXPECT_FALSE(milestro::input::ShouldReadScrollPhasesOnly(ScrollPhaseMonitorMode::CaptureSamples));
    EXPECT_FALSE(milestro::input::ShouldReadScrollPhasesTimestamp(ScrollPhaseMonitorMode::CaptureSamples));
    EXPECT_FALSE(milestro::input::ShouldCaptureScrollPhaseSamples(ScrollPhaseMonitorMode::ReadProperties));
    EXPECT_TRUE(milestro::input::ShouldReadScrollPhaseProperties(ScrollPhaseMonitorMode::ReadProperties));
    EXPECT_FALSE(milestro::input::ShouldReadScrollPhaseEventProperties(ScrollPhaseMonitorMode::ReadProperties));
    EXPECT_FALSE(milestro::input::ShouldReadScrollPhaseEventScalars(ScrollPhaseMonitorMode::ReadProperties));
    EXPECT_FALSE(milestro::input::ShouldWriteScrollPhaseLocalPod(ScrollPhaseMonitorMode::ReadProperties));
    EXPECT_FALSE(milestro::input::ShouldReadScrollPhasesOnly(ScrollPhaseMonitorMode::ReadProperties));
    EXPECT_FALSE(milestro::input::ShouldReadScrollPhasesTimestamp(ScrollPhaseMonitorMode::ReadProperties));
    EXPECT_FALSE(milestro::input::ShouldCaptureScrollPhaseSamples(ScrollPhaseMonitorMode::ReadEventProperties));
    EXPECT_FALSE(milestro::input::ShouldReadScrollPhaseProperties(ScrollPhaseMonitorMode::ReadEventProperties));
    EXPECT_TRUE(milestro::input::ShouldReadScrollPhaseEventProperties(ScrollPhaseMonitorMode::ReadEventProperties));
    EXPECT_FALSE(milestro::input::ShouldReadScrollPhaseEventScalars(ScrollPhaseMonitorMode::ReadEventProperties));
    EXPECT_FALSE(milestro::input::ShouldWriteScrollPhaseLocalPod(ScrollPhaseMonitorMode::ReadEventProperties));
    EXPECT_FALSE(milestro::input::ShouldReadScrollPhasesOnly(ScrollPhaseMonitorMode::ReadEventProperties));
    EXPECT_FALSE(milestro::input::ShouldReadScrollPhasesTimestamp(ScrollPhaseMonitorMode::ReadEventProperties));
    EXPECT_FALSE(milestro::input::ShouldCaptureScrollPhaseSamples(ScrollPhaseMonitorMode::ReadEventScalars));
    EXPECT_FALSE(milestro::input::ShouldReadScrollPhaseProperties(ScrollPhaseMonitorMode::ReadEventScalars));
    EXPECT_FALSE(milestro::input::ShouldReadScrollPhaseEventProperties(ScrollPhaseMonitorMode::ReadEventScalars));
    EXPECT_TRUE(milestro::input::ShouldReadScrollPhaseEventScalars(ScrollPhaseMonitorMode::ReadEventScalars));
    EXPECT_FALSE(milestro::input::ShouldWriteScrollPhaseLocalPod(ScrollPhaseMonitorMode::ReadEventScalars));
    EXPECT_FALSE(milestro::input::ShouldReadScrollPhasesOnly(ScrollPhaseMonitorMode::ReadEventScalars));
    EXPECT_FALSE(milestro::input::ShouldReadScrollPhasesTimestamp(ScrollPhaseMonitorMode::ReadEventScalars));
    EXPECT_FALSE(milestro::input::ShouldCaptureScrollPhaseSamples(ScrollPhaseMonitorMode::WriteLocalPod));
    EXPECT_FALSE(milestro::input::ShouldReadScrollPhaseProperties(ScrollPhaseMonitorMode::WriteLocalPod));
    EXPECT_FALSE(milestro::input::ShouldReadScrollPhaseEventProperties(ScrollPhaseMonitorMode::WriteLocalPod));
    EXPECT_FALSE(milestro::input::ShouldReadScrollPhaseEventScalars(ScrollPhaseMonitorMode::WriteLocalPod));
    EXPECT_TRUE(milestro::input::ShouldWriteScrollPhaseLocalPod(ScrollPhaseMonitorMode::WriteLocalPod));
    EXPECT_FALSE(milestro::input::ShouldReadScrollPhasesOnly(ScrollPhaseMonitorMode::WriteLocalPod));
    EXPECT_FALSE(milestro::input::ShouldReadScrollPhasesTimestamp(ScrollPhaseMonitorMode::WriteLocalPod));
    EXPECT_FALSE(milestro::input::ShouldCaptureScrollPhaseSamples(ScrollPhaseMonitorMode::ReadPhasesOnly));
    EXPECT_FALSE(milestro::input::ShouldReadScrollPhaseProperties(ScrollPhaseMonitorMode::ReadPhasesOnly));
    EXPECT_FALSE(milestro::input::ShouldReadScrollPhaseEventProperties(ScrollPhaseMonitorMode::ReadPhasesOnly));
    EXPECT_FALSE(milestro::input::ShouldReadScrollPhaseEventScalars(ScrollPhaseMonitorMode::ReadPhasesOnly));
    EXPECT_FALSE(milestro::input::ShouldWriteScrollPhaseLocalPod(ScrollPhaseMonitorMode::ReadPhasesOnly));
    EXPECT_TRUE(milestro::input::ShouldReadScrollPhasesOnly(ScrollPhaseMonitorMode::ReadPhasesOnly));
    EXPECT_FALSE(milestro::input::ShouldReadScrollPhasesTimestamp(ScrollPhaseMonitorMode::ReadPhasesOnly));
    EXPECT_FALSE(milestro::input::ShouldCaptureScrollPhaseSamples(ScrollPhaseMonitorMode::ReadPhasesTimestamp));
    EXPECT_FALSE(milestro::input::ShouldReadScrollPhaseProperties(ScrollPhaseMonitorMode::ReadPhasesTimestamp));
    EXPECT_FALSE(milestro::input::ShouldReadScrollPhaseEventProperties(ScrollPhaseMonitorMode::ReadPhasesTimestamp));
    EXPECT_FALSE(milestro::input::ShouldReadScrollPhaseEventScalars(ScrollPhaseMonitorMode::ReadPhasesTimestamp));
    EXPECT_FALSE(milestro::input::ShouldWriteScrollPhaseLocalPod(ScrollPhaseMonitorMode::ReadPhasesTimestamp));
    EXPECT_FALSE(milestro::input::ShouldReadScrollPhasesOnly(ScrollPhaseMonitorMode::ReadPhasesTimestamp));
    EXPECT_TRUE(milestro::input::ShouldReadScrollPhasesTimestamp(ScrollPhaseMonitorMode::ReadPhasesTimestamp));
}

TEST(ScrollPhaseGestureTrackerTest, KeepsOneIdThroughDelayedMomentum) {
    ScrollPhaseGestureTracker tracker;

    EXPECT_EQ(tracker.Resolve(ScrollPhase::Began, ScrollPhase::None), 1);
    EXPECT_EQ(tracker.Resolve(ScrollPhase::Changed, ScrollPhase::None), 1);
    EXPECT_EQ(tracker.Resolve(ScrollPhase::Ended, ScrollPhase::None), 1);
    EXPECT_EQ(tracker.Resolve(ScrollPhase::None, ScrollPhase::Began), 1);
    EXPECT_EQ(tracker.Resolve(ScrollPhase::None, ScrollPhase::Changed), 1);
    EXPECT_EQ(tracker.Resolve(ScrollPhase::None, ScrollPhase::Ended), 1);
}

TEST(ScrollPhaseGestureTrackerTest, KeepsOneIdWhenMomentumBeginsOnGestureEnd) {
    ScrollPhaseGestureTracker tracker;

    EXPECT_EQ(tracker.Resolve(ScrollPhase::Began, ScrollPhase::None), 1);
    EXPECT_EQ(tracker.Resolve(ScrollPhase::Ended, ScrollPhase::Began), 1);
    EXPECT_EQ(tracker.Resolve(ScrollPhase::None, ScrollPhase::Changed), 1);
    EXPECT_EQ(tracker.Resolve(ScrollPhase::None, ScrollPhase::Ended), 1);
}

TEST(ScrollPhaseGestureTrackerTest, ResetRestartsSequenceForANewMonitorLease) {
    ScrollPhaseGestureTracker tracker;
    EXPECT_EQ(tracker.Resolve(ScrollPhase::Began, ScrollPhase::None), 1);
    tracker.Reset();
    EXPECT_EQ(tracker.Resolve(ScrollPhase::Began, ScrollPhase::None), 1);
}

TEST(ScrollPhaseLeaseTest, RejectsSecondOwnerAndNonOwnerRelease) {
    ScrollPhaseLease lease;
    int64_t owner = 0;
    int64_t second = 0;

    EXPECT_EQ(lease.Acquire(owner), ScrollPhaseMonitorResult::Succeeded);
    EXPECT_NE(owner, 0);
    EXPECT_EQ(lease.Acquire(second), ScrollPhaseMonitorResult::AlreadyStarted);
    EXPECT_EQ(second, 0);
    EXPECT_EQ(lease.Validate(owner + 1), ScrollPhaseMonitorResult::InvalidLease);
    EXPECT_EQ(lease.Release(owner + 1), ScrollPhaseMonitorResult::InvalidLease);
    EXPECT_TRUE(lease.HasActiveLease());
    EXPECT_EQ(lease.Release(owner), ScrollPhaseMonitorResult::Succeeded);
    EXPECT_FALSE(lease.HasActiveLease());
}

TEST(ScrollPhaseLeaseTest, ForceReleaseAllowsRecoveryWithANewId) {
    ScrollPhaseLease lease;
    int64_t first = 0;
    int64_t second = 0;

    ASSERT_EQ(lease.Acquire(first), ScrollPhaseMonitorResult::Succeeded);
    lease.ForceRelease();
    ASSERT_EQ(lease.Acquire(second), ScrollPhaseMonitorResult::Succeeded);
    EXPECT_NE(second, first);
}

TEST(ScrollPhasePluginUnloadPolicyTest, ReturnsWhenNoStateIsActive) {
    EXPECT_EQ(milestro::input::DecideScrollPhasePluginUnload(false, false, false, false),
              ScrollPhasePluginUnloadDecision::Return);
}

TEST(ScrollPhasePluginUnloadPolicyTest, RequestsCleanupForMainThreadResidualState) {
    EXPECT_EQ(milestro::input::DecideScrollPhasePluginUnload(true, true, false, false),
              ScrollPhasePluginUnloadDecision::Cleanup);
}

TEST(ScrollPhasePluginUnloadPolicyTest, AbortsForWrongThreadResidualState) {
    EXPECT_EQ(milestro::input::DecideScrollPhasePluginUnload(true, false, false, false),
              ScrollPhasePluginUnloadDecision::Abort);
}

TEST(ScrollPhasePluginUnloadPolicyTest, ReturnsOnlyAfterSuccessfulCleanupLeavesNoState) {
    EXPECT_EQ(milestro::input::DecideScrollPhasePluginUnload(false, true, true, true),
              ScrollPhasePluginUnloadDecision::Return);
    EXPECT_EQ(milestro::input::DecideScrollPhasePluginUnload(true, true, true, true),
              ScrollPhasePluginUnloadDecision::Abort);
    EXPECT_EQ(milestro::input::DecideScrollPhasePluginUnload(false, true, true, false),
              ScrollPhasePluginUnloadDecision::Abort);
}

} // namespace
