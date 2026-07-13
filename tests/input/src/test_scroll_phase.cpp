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
    EXPECT_TRUE(milestro::input::IsValidScrollPhaseMonitorMode(ScrollPhaseMonitorMode::PassThrough));
    EXPECT_TRUE(milestro::input::IsValidScrollPhaseMonitorMode(ScrollPhaseMonitorMode::CaptureSamples));
    EXPECT_TRUE(milestro::input::IsValidScrollPhaseMonitorMode(ScrollPhaseMonitorMode::ReadProperties));
    EXPECT_TRUE(milestro::input::IsValidScrollPhaseMonitorMode(ScrollPhaseMonitorMode::ReadEventProperties));
    EXPECT_TRUE(milestro::input::IsValidScrollPhaseMonitorMode(ScrollPhaseMonitorMode::ReadEventScalars));
    EXPECT_FALSE(milestro::input::IsValidScrollPhaseMonitorMode(static_cast<ScrollPhaseMonitorMode>(-1)));
    EXPECT_FALSE(milestro::input::IsValidScrollPhaseMonitorMode(static_cast<ScrollPhaseMonitorMode>(5)));
}

TEST(ScrollPhaseMonitorModeTest, ModesHaveMutuallyExclusiveCallbackSideEffects) {
    int sampleCount = 0;
    int propertyReadCount = 0;
    int eventPropertyReadCount = 0;
    int eventScalarReadCount = 0;
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

    EXPECT_EQ(0, sampleCount);
    EXPECT_EQ(0, propertyReadCount);
    EXPECT_EQ(0, eventPropertyReadCount);
    EXPECT_EQ(0, eventScalarReadCount);
    EXPECT_TRUE(milestro::input::ShouldCaptureScrollPhaseSamples(ScrollPhaseMonitorMode::CaptureSamples));
    EXPECT_FALSE(milestro::input::ShouldReadScrollPhaseProperties(ScrollPhaseMonitorMode::CaptureSamples));
    EXPECT_FALSE(milestro::input::ShouldReadScrollPhaseEventProperties(ScrollPhaseMonitorMode::CaptureSamples));
    EXPECT_FALSE(milestro::input::ShouldReadScrollPhaseEventScalars(ScrollPhaseMonitorMode::CaptureSamples));
    EXPECT_FALSE(milestro::input::ShouldCaptureScrollPhaseSamples(ScrollPhaseMonitorMode::ReadProperties));
    EXPECT_TRUE(milestro::input::ShouldReadScrollPhaseProperties(ScrollPhaseMonitorMode::ReadProperties));
    EXPECT_FALSE(milestro::input::ShouldReadScrollPhaseEventProperties(ScrollPhaseMonitorMode::ReadProperties));
    EXPECT_FALSE(milestro::input::ShouldReadScrollPhaseEventScalars(ScrollPhaseMonitorMode::ReadProperties));
    EXPECT_FALSE(milestro::input::ShouldCaptureScrollPhaseSamples(ScrollPhaseMonitorMode::ReadEventProperties));
    EXPECT_FALSE(milestro::input::ShouldReadScrollPhaseProperties(ScrollPhaseMonitorMode::ReadEventProperties));
    EXPECT_TRUE(milestro::input::ShouldReadScrollPhaseEventProperties(ScrollPhaseMonitorMode::ReadEventProperties));
    EXPECT_FALSE(milestro::input::ShouldReadScrollPhaseEventScalars(ScrollPhaseMonitorMode::ReadEventProperties));
    EXPECT_FALSE(milestro::input::ShouldCaptureScrollPhaseSamples(ScrollPhaseMonitorMode::ReadEventScalars));
    EXPECT_FALSE(milestro::input::ShouldReadScrollPhaseProperties(ScrollPhaseMonitorMode::ReadEventScalars));
    EXPECT_FALSE(milestro::input::ShouldReadScrollPhaseEventProperties(ScrollPhaseMonitorMode::ReadEventScalars));
    EXPECT_TRUE(milestro::input::ShouldReadScrollPhaseEventScalars(ScrollPhaseMonitorMode::ReadEventScalars));
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
