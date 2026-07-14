#ifndef MILESTRO_SCROLL_PHASE_MINIMAL_GESTURE_TRACKER_H
#define MILESTRO_SCROLL_PHASE_MINIMAL_GESTURE_TRACKER_H

#include <Milestro/input/ScrollPhaseMonitor.h>

#include <cstdint>

namespace milestro::input {

constexpr uint64_t kNativeScrollPhaseNone = 0;
constexpr uint64_t kNativeScrollPhaseBegan = UINT64_C(1) << 0U;
constexpr uint64_t kNativeScrollPhaseStationary = UINT64_C(1) << 1U;
constexpr uint64_t kNativeScrollPhaseChanged = UINT64_C(1) << 2U;
constexpr uint64_t kNativeScrollPhaseEnded = UINT64_C(1) << 3U;
constexpr uint64_t kNativeScrollPhaseCanceled = UINT64_C(1) << 4U;
constexpr uint64_t kNativeScrollPhaseMayBegin = UINT64_C(1) << 5U;

enum class ScrollPhaseMinimalGestureResultKind : int32_t {
    NoGesture = 0,
    Resolved = 1,
    InvalidTransition = 2,
};

enum class ScrollPhaseMinimalGestureTrackerState : int32_t {
    Idle = 0,
    GestureActive = 1,
    PendingMomentum = 2,
    MomentumActive = 3,
};

enum class ScrollPhaseMinimalGestureFailure : int32_t {
    None = 0,
    InvalidGesturePhaseBits = 1,
    InvalidMomentumPhaseBits = 2,
    InvalidTransition = 3,
    GestureIdExhausted = 4,
};

struct ScrollPhaseMinimalGestureResult {
    ScrollPhaseMinimalGestureResultKind kind = ScrollPhaseMinimalGestureResultKind::NoGesture;
    int64_t gestureId = 0;
    ScrollPhase gesturePhase = ScrollPhase::Unknown;
    ScrollPhase momentumPhase = ScrollPhase::Unknown;
    ScrollPhaseMinimalGestureFailure failure = ScrollPhaseMinimalGestureFailure::None;
};

struct ScrollPhaseMinimalDecodedPhase {
    bool valid = false;
    ScrollPhase phase = ScrollPhase::Unknown;
};

ScrollPhaseMinimalDecodedPhase DecodeMinimalScrollPhase(uint64_t phaseBits) noexcept;

struct ScrollPhaseMinimalGestureTrackerSnapshot {
    ScrollPhaseMinimalGestureTrackerState state = ScrollPhaseMinimalGestureTrackerState::Idle;
    int64_t currentGestureId = 0;
    int64_t nextGestureId = 1;
    ScrollPhaseMinimalGestureFailure failure = ScrollPhaseMinimalGestureFailure::None;
};

class ScrollPhaseMinimalGestureTracker {
public:
    explicit ScrollPhaseMinimalGestureTracker(int64_t firstGestureId = 1) noexcept;

    ScrollPhaseMinimalGestureResult Resolve(uint64_t gesturePhaseBits, uint64_t momentumPhaseBits) noexcept;
    ScrollPhaseMinimalGestureResult Resolve(ScrollPhaseMinimalDecodedPhase gesturePhase,
                                            ScrollPhaseMinimalDecodedPhase momentumPhase) noexcept;
    void Reset() noexcept;
    void FinishCleanup(bool cleanupSucceeded) noexcept;
    ScrollPhaseMinimalGestureTrackerSnapshot Snapshot() const noexcept;

private:
    ScrollPhaseMinimalGestureResult BeginGesture(ScrollPhase gesturePhase, ScrollPhase momentumPhase) noexcept;
    ScrollPhaseMinimalGestureResult NoGesture(ScrollPhase gesturePhase, ScrollPhase momentumPhase) const noexcept;
    ScrollPhaseMinimalGestureResult Resolved(ScrollPhase gesturePhase, ScrollPhase momentumPhase) const noexcept;
    ScrollPhaseMinimalGestureResult
    Fail(ScrollPhaseMinimalGestureFailure failure, ScrollPhase gesturePhase, ScrollPhase momentumPhase) noexcept;

    ScrollPhaseMinimalGestureTrackerState state_ = ScrollPhaseMinimalGestureTrackerState::Idle;
    int64_t currentGestureId_ = 0;
    int64_t nextGestureId_ = 1;
    ScrollPhaseMinimalGestureFailure failure_ = ScrollPhaseMinimalGestureFailure::None;
};

} // namespace milestro::input

#endif // MILESTRO_SCROLL_PHASE_MINIMAL_GESTURE_TRACKER_H
