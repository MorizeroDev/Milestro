#include "ScrollPhaseGestureTracker.h"

namespace milestro::input {

int64_t ScrollPhaseGestureTracker::Resolve(ScrollPhase gesturePhase, ScrollPhase momentumPhase) {
    if (gesturePhase == ScrollPhase::Began) {
        activeGestureId_ = nextGestureId_++;
        pendingMomentumGestureId_ = 0;
    }

    int64_t gestureId = activeGestureId_;
    if (momentumPhase == ScrollPhase::Began) {
        activeMomentumGestureId_ = pendingMomentumGestureId_ != 0 ? pendingMomentumGestureId_ : activeGestureId_;
        pendingMomentumGestureId_ = 0;
    }
    if (gestureId == 0) {
        gestureId = activeMomentumGestureId_;
    } else if (activeMomentumGestureId_ != 0 && activeMomentumGestureId_ != gestureId) {
        gestureId = 0;
    }

    if (gesturePhase == ScrollPhase::Ended) {
        pendingMomentumGestureId_ = activeMomentumGestureId_ == activeGestureId_ ? 0 : activeGestureId_;
        activeGestureId_ = 0;
    } else if (gesturePhase == ScrollPhase::Canceled) {
        activeGestureId_ = 0;
        pendingMomentumGestureId_ = 0;
    }

    if (momentumPhase == ScrollPhase::Ended || momentumPhase == ScrollPhase::Canceled) {
        activeMomentumGestureId_ = 0;
    }

    return gestureId;
}

void ScrollPhaseGestureTracker::Reset() {
    nextGestureId_ = 1;
    activeGestureId_ = 0;
    pendingMomentumGestureId_ = 0;
    activeMomentumGestureId_ = 0;
}

} // namespace milestro::input
