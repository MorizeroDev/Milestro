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

TEST(ScrollPhaseMonitorModeTest, AcceptsOnlyPassThroughAndCaptureSamples) {
    EXPECT_EQ(0, static_cast<int32_t>(ScrollPhaseMonitorMode::PassThrough));
    EXPECT_EQ(1, static_cast<int32_t>(ScrollPhaseMonitorMode::CaptureSamples));
    EXPECT_TRUE(milestro::input::IsValidScrollPhaseMonitorMode(ScrollPhaseMonitorMode::PassThrough));
    EXPECT_TRUE(milestro::input::IsValidScrollPhaseMonitorMode(ScrollPhaseMonitorMode::CaptureSamples));
    EXPECT_FALSE(milestro::input::IsValidScrollPhaseMonitorMode(static_cast<ScrollPhaseMonitorMode>(-1)));
    EXPECT_FALSE(milestro::input::IsValidScrollPhaseMonitorMode(static_cast<ScrollPhaseMonitorMode>(2)));
}

TEST(ScrollPhaseMonitorModeTest, PassThroughHasNoSamplingSideEffect) {
    int sampleCount = 0;
    if (milestro::input::ShouldCaptureScrollPhaseSamples(ScrollPhaseMonitorMode::PassThrough)) {
        ++sampleCount;
    }

    EXPECT_EQ(0, sampleCount);
    EXPECT_TRUE(milestro::input::ShouldCaptureScrollPhaseSamples(ScrollPhaseMonitorMode::CaptureSamples));
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
