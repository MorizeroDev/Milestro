#ifndef MILESTRO_SCROLL_PHASE_GESTURE_TRACKER_H
#define MILESTRO_SCROLL_PHASE_GESTURE_TRACKER_H

#include <Milestro/input/ScrollPhaseMonitor.h>

namespace milestro::input {

class ScrollPhaseGestureTracker {
public:
    int64_t Resolve(ScrollPhase gesturePhase, ScrollPhase momentumPhase);
    void Reset();

private:
    int64_t nextGestureId_ = 1;
    int64_t activeGestureId_ = 0;
    int64_t pendingMomentumGestureId_ = 0;
    int64_t activeMomentumGestureId_ = 0;
};

} // namespace milestro::input

#endif // MILESTRO_SCROLL_PHASE_GESTURE_TRACKER_H
