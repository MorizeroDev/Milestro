#include <Milestro/common/milestro_platform.h>
#include <Milestro/input/ScrollPhaseMonitor.h>

#include "ScrollPhaseGestureTracker.h"
#include "ScrollPhaseLease.h"

#if MILESTRO_PLATFORM_MAC

#import <AppKit/AppKit.h>

#include <deque>

namespace milestro::input {
namespace {

constexpr size_t kMaximumQueuedSamples = 256;

id monitorToken = nil;
std::deque<ScrollPhaseSample> samples;
int64_t nextSequence = 1;
ScrollPhaseLease lease;
ScrollPhaseGestureTracker gestureTracker;
bool queueOverflowed = false;

ScrollPhase ConvertPhase(NSEventPhase phase) {
    if (phase == NSEventPhaseNone) {
        return ScrollPhase::None;
    }
    if ((phase & NSEventPhaseCancelled) != 0) {
        return ScrollPhase::Canceled;
    }
    if ((phase & NSEventPhaseEnded) != 0) {
        return ScrollPhase::Ended;
    }
    if ((phase & NSEventPhaseBegan) != 0) {
        return ScrollPhase::Began;
    }
    if ((phase & NSEventPhaseChanged) != 0) {
        return ScrollPhase::Changed;
    }
    if ((phase & NSEventPhaseStationary) != 0) {
        return ScrollPhase::Stationary;
    }
    return ScrollPhase::Unknown;
}

void Enqueue(NSEvent* event) {
    const ScrollPhase gesturePhase = ConvertPhase(event.phase);
    const ScrollPhase momentumPhase = ConvertPhase(event.momentumPhase);
    ScrollPhaseSample sample;
    sample.sequence = nextSequence++;
    sample.gestureId = gestureTracker.Resolve(gesturePhase, momentumPhase);
    sample.timestamp = event.timestamp;
    sample.windowNumber = event.windowNumber;
    sample.keyWindowNumber = NSApp.keyWindow.windowNumber;
    sample.eventNumber = event.eventNumber;
    sample.deltaX = event.deltaX;
    sample.deltaY = event.deltaY;
    sample.scrollingDeltaX = event.scrollingDeltaX;
    sample.scrollingDeltaY = event.scrollingDeltaY;
    sample.gesturePhase = gesturePhase;
    sample.momentumPhase = momentumPhase;
    sample.precise = event.hasPreciseScrollingDeltas ? 1 : 0;
    sample.directionInvertedFromDevice = event.isDirectionInvertedFromDevice ? 1 : 0;

    if (samples.size() >= kMaximumQueuedSamples) {
        samples.pop_front();
        queueOverflowed = true;
    }
    samples.push_back(sample);
}

void ResetState() {
    samples.clear();
    nextSequence = 1;
    gestureTracker.Reset();
    queueOverflowed = false;
}

ScrollPhaseMonitorResult RemoveMonitor() {
    @try {
        if (monitorToken != nil) {
            [NSEvent removeMonitor:monitorToken];
            monitorToken = nil;
        }
        lease.ForceRelease();
        ResetState();
        return ScrollPhaseMonitorResult::Succeeded;
    } @catch (NSException* exception) {
        (void) exception;
        monitorToken = nil;
        lease.ForceRelease();
        ResetState();
        return ScrollPhaseMonitorResult::Failed;
    }
}

} // namespace

ScrollPhaseMonitorResult StartScrollPhaseMonitor(int64_t& leaseId) noexcept {
    @autoreleasepool {
        leaseId = 0;
        if (![NSThread isMainThread]) {
            return ScrollPhaseMonitorResult::WrongThread;
        }
        if (monitorToken != nil) {
            return ScrollPhaseMonitorResult::AlreadyStarted;
        }

        @try {
            ResetState();
            monitorToken = [NSEvent addLocalMonitorForEventsMatchingMask:NSEventMaskScrollWheel
                                                                 handler:^NSEvent*(NSEvent* event) {
                                                                   Enqueue(event);
                                                                   return event;
                                                                 }];
            if (monitorToken == nil) {
                return ScrollPhaseMonitorResult::Failed;
            }
            const ScrollPhaseMonitorResult leaseResult = lease.Acquire(leaseId);
            if (leaseResult != ScrollPhaseMonitorResult::Succeeded) {
                (void) RemoveMonitor();
                return leaseResult;
            }
            return ScrollPhaseMonitorResult::Succeeded;
        } @catch (NSException* exception) {
            (void) exception;
            monitorToken = nil;
            ResetState();
            return ScrollPhaseMonitorResult::Failed;
        }
    }
}

ScrollPhaseMonitorResult StopScrollPhaseMonitor(int64_t leaseId) noexcept {
    @autoreleasepool {
        if (![NSThread isMainThread]) {
            return ScrollPhaseMonitorResult::WrongThread;
        }
        const ScrollPhaseMonitorResult leaseResult = lease.Validate(leaseId);
        if (leaseResult != ScrollPhaseMonitorResult::Succeeded) {
            return leaseResult;
        }
        return RemoveMonitor();
    }
}

ScrollPhaseMonitorResult PollScrollPhaseMonitor(int64_t leaseId, ScrollPhaseSample& sample, bool& hasSample) noexcept {
    @autoreleasepool {
        sample = {};
        hasSample = false;
        if (![NSThread isMainThread]) {
            return ScrollPhaseMonitorResult::WrongThread;
        }
        const ScrollPhaseMonitorResult leaseResult = lease.Validate(leaseId);
        if (leaseResult != ScrollPhaseMonitorResult::Succeeded) {
            return leaseResult;
        }
        if (samples.empty()) {
            return ScrollPhaseMonitorResult::Succeeded;
        }

        sample = samples.front();
        samples.pop_front();
        sample.queueOverflowed = queueOverflowed ? 1 : 0;
        queueOverflowed = false;
        hasSample = true;
        return ScrollPhaseMonitorResult::Succeeded;
    }
}

bool HasActiveScrollPhaseMonitorLease() noexcept {
    return lease.HasActiveLease();
}

ScrollPhaseMonitorResult ShutdownScrollPhaseMonitorForPluginUnload() noexcept {
    @autoreleasepool {
        if (!HasActiveScrollPhaseMonitorLease()) {
            return ScrollPhaseMonitorResult::Succeeded;
        }
        if (![NSThread isMainThread]) {
            return ScrollPhaseMonitorResult::WrongThread;
        }
        return RemoveMonitor();
    }
}

} // namespace milestro::input

#endif
