#ifndef MILESTRO_SCROLL_PHASE_MONITOR_H
#define MILESTRO_SCROLL_PHASE_MONITOR_H

#include <cstdint>

namespace milestro::input {

enum class ScrollPhaseMonitorResult : int32_t {
    Succeeded = 0,
    Unsupported = 1,
    WrongThread = 2,
    Failed = 3,
};

enum class ScrollPhase : int32_t {
    Unknown = 0,
    None = 1,
    Began = 2,
    Changed = 3,
    Stationary = 4,
    Ended = 5,
    Canceled = 6,
};

struct ScrollPhaseSample {
    int64_t sequence = 0;
    int64_t gestureId = 0;
    double timestamp = 0.0;
    int64_t windowNumber = 0;
    int64_t eventNumber = 0;
    double deltaX = 0.0;
    double deltaY = 0.0;
    double scrollingDeltaX = 0.0;
    double scrollingDeltaY = 0.0;
    ScrollPhase gesturePhase = ScrollPhase::Unknown;
    ScrollPhase momentumPhase = ScrollPhase::Unknown;
    int32_t precise = 0;
    int32_t directionInvertedFromDevice = 0;
    int32_t queueOverflowed = 0;
};

ScrollPhaseMonitorResult StartScrollPhaseMonitor() noexcept;
ScrollPhaseMonitorResult StopScrollPhaseMonitor() noexcept;
ScrollPhaseMonitorResult PollScrollPhaseMonitor(ScrollPhaseSample& sample, bool& hasSample) noexcept;

} // namespace milestro::input

#endif // MILESTRO_SCROLL_PHASE_MONITOR_H
