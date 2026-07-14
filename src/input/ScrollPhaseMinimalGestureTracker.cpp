#include "ScrollPhaseMinimalGestureTracker.h"

#include <limits>

namespace milestro::input {

ScrollPhaseMinimalDecodedPhase DecodeMinimalScrollPhase(uint64_t phaseBits) noexcept {
    switch (phaseBits) {
        case kNativeScrollPhaseNone:
            return {true, ScrollPhase::None};
        case kNativeScrollPhaseBegan:
            return {true, ScrollPhase::Began};
        case kNativeScrollPhaseStationary:
            return {true, ScrollPhase::Stationary};
        case kNativeScrollPhaseChanged:
            return {true, ScrollPhase::Changed};
        case kNativeScrollPhaseEnded:
            return {true, ScrollPhase::Ended};
        case kNativeScrollPhaseCanceled:
            return {true, ScrollPhase::Canceled};
        case kNativeScrollPhaseMayBegin:
            return {true, ScrollPhase::MayBegin};
        default:
            return {false, ScrollPhase::Unknown};
    }
}

ScrollPhaseMinimalGestureTracker::ScrollPhaseMinimalGestureTracker(int64_t firstGestureId) noexcept
    : nextGestureId_(firstGestureId) {
}

ScrollPhaseMinimalGestureResult ScrollPhaseMinimalGestureTracker::Resolve(uint64_t gesturePhaseBits,
                                                                          uint64_t momentumPhaseBits) noexcept {
    return Resolve(DecodeMinimalScrollPhase(gesturePhaseBits), DecodeMinimalScrollPhase(momentumPhaseBits));
}

ScrollPhaseMinimalGestureResult
ScrollPhaseMinimalGestureTracker::Resolve(ScrollPhaseMinimalDecodedPhase gesture,
                                          ScrollPhaseMinimalDecodedPhase momentum) noexcept {
    if (failure_ != ScrollPhaseMinimalGestureFailure::None) {
        return Fail(failure_, ScrollPhase::Unknown, ScrollPhase::Unknown);
    }

    if (!gesture.valid) {
        return Fail(ScrollPhaseMinimalGestureFailure::InvalidGesturePhaseBits,
                    ScrollPhase::Unknown,
                    ScrollPhase::Unknown);
    }
    if (!momentum.valid) {
        return Fail(ScrollPhaseMinimalGestureFailure::InvalidMomentumPhaseBits, gesture.phase, ScrollPhase::Unknown);
    }

    if (gesture.phase == ScrollPhase::None && momentum.phase == ScrollPhase::None) {
        return NoGesture(gesture.phase, momentum.phase);
    }

    switch (state_) {
        case ScrollPhaseMinimalGestureTrackerState::Idle:
            if ((gesture.phase == ScrollPhase::MayBegin && momentum.phase == ScrollPhase::None) ||
                (gesture.phase == ScrollPhase::None && momentum.phase == ScrollPhase::MayBegin)) {
                return NoGesture(gesture.phase, momentum.phase);
            }
            if (gesture.phase == ScrollPhase::Began && momentum.phase == ScrollPhase::None) {
                return BeginGesture(gesture.phase, momentum.phase);
            }
            break;
        case ScrollPhaseMinimalGestureTrackerState::GestureActive:
            if (momentum.phase == ScrollPhase::None &&
                (gesture.phase == ScrollPhase::Changed || gesture.phase == ScrollPhase::Stationary)) {
                return Resolved(gesture.phase, momentum.phase);
            }
            if (gesture.phase == ScrollPhase::Ended && momentum.phase == ScrollPhase::None) {
                const ScrollPhaseMinimalGestureResult result = Resolved(gesture.phase, momentum.phase);
                state_ = ScrollPhaseMinimalGestureTrackerState::PendingMomentum;
                return result;
            }
            if (gesture.phase == ScrollPhase::Canceled && momentum.phase == ScrollPhase::None) {
                const ScrollPhaseMinimalGestureResult result = Resolved(gesture.phase, momentum.phase);
                state_ = ScrollPhaseMinimalGestureTrackerState::Idle;
                currentGestureId_ = 0;
                return result;
            }
            if (gesture.phase == ScrollPhase::Ended && momentum.phase == ScrollPhase::Began) {
                const ScrollPhaseMinimalGestureResult result = Resolved(gesture.phase, momentum.phase);
                state_ = ScrollPhaseMinimalGestureTrackerState::MomentumActive;
                return result;
            }
            break;
        case ScrollPhaseMinimalGestureTrackerState::PendingMomentum:
            if (gesture.phase == ScrollPhase::None && momentum.phase == ScrollPhase::Began) {
                const ScrollPhaseMinimalGestureResult result = Resolved(gesture.phase, momentum.phase);
                state_ = ScrollPhaseMinimalGestureTrackerState::MomentumActive;
                return result;
            }
            if (gesture.phase == ScrollPhase::Began && momentum.phase == ScrollPhase::None) {
                return BeginGesture(gesture.phase, momentum.phase);
            }
            break;
        case ScrollPhaseMinimalGestureTrackerState::MomentumActive:
            if (gesture.phase == ScrollPhase::None &&
                (momentum.phase == ScrollPhase::Changed || momentum.phase == ScrollPhase::Stationary)) {
                return Resolved(gesture.phase, momentum.phase);
            }
            if (gesture.phase == ScrollPhase::None &&
                (momentum.phase == ScrollPhase::Ended || momentum.phase == ScrollPhase::Canceled)) {
                const ScrollPhaseMinimalGestureResult result = Resolved(gesture.phase, momentum.phase);
                state_ = ScrollPhaseMinimalGestureTrackerState::Idle;
                currentGestureId_ = 0;
                return result;
            }
            break;
    }

    return Fail(ScrollPhaseMinimalGestureFailure::InvalidTransition, gesture.phase, momentum.phase);
}

void ScrollPhaseMinimalGestureTracker::Reset() noexcept {
    state_ = ScrollPhaseMinimalGestureTrackerState::Idle;
    currentGestureId_ = 0;
    nextGestureId_ = 1;
    failure_ = ScrollPhaseMinimalGestureFailure::None;
}

void ScrollPhaseMinimalGestureTracker::FinishCleanup(bool cleanupSucceeded) noexcept {
    if (cleanupSucceeded) {
        Reset();
    }
}

ScrollPhaseMinimalGestureTrackerSnapshot ScrollPhaseMinimalGestureTracker::Snapshot() const noexcept {
    return {state_, currentGestureId_, nextGestureId_, failure_};
}

ScrollPhaseMinimalGestureResult ScrollPhaseMinimalGestureTracker::BeginGesture(ScrollPhase gesturePhase,
                                                                               ScrollPhase momentumPhase) noexcept {
    if (nextGestureId_ <= 0 || nextGestureId_ == std::numeric_limits<int64_t>::max()) {
        return Fail(ScrollPhaseMinimalGestureFailure::GestureIdExhausted, gesturePhase, momentumPhase);
    }
    currentGestureId_ = nextGestureId_++;
    state_ = ScrollPhaseMinimalGestureTrackerState::GestureActive;
    return Resolved(gesturePhase, momentumPhase);
}

ScrollPhaseMinimalGestureResult ScrollPhaseMinimalGestureTracker::NoGesture(ScrollPhase gesturePhase,
                                                                            ScrollPhase momentumPhase) const noexcept {
    return {ScrollPhaseMinimalGestureResultKind::NoGesture,
            0,
            gesturePhase,
            momentumPhase,
            ScrollPhaseMinimalGestureFailure::None};
}

ScrollPhaseMinimalGestureResult ScrollPhaseMinimalGestureTracker::Resolved(ScrollPhase gesturePhase,
                                                                           ScrollPhase momentumPhase) const noexcept {
    return {ScrollPhaseMinimalGestureResultKind::Resolved,
            currentGestureId_,
            gesturePhase,
            momentumPhase,
            ScrollPhaseMinimalGestureFailure::None};
}

ScrollPhaseMinimalGestureResult ScrollPhaseMinimalGestureTracker::Fail(ScrollPhaseMinimalGestureFailure failure,
                                                                       ScrollPhase gesturePhase,
                                                                       ScrollPhase momentumPhase) noexcept {
    failure_ = failure;
    return {ScrollPhaseMinimalGestureResultKind::InvalidTransition, 0, gesturePhase, momentumPhase, failure_};
}

} // namespace milestro::input
