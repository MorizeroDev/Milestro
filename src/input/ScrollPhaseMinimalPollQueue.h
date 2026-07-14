#ifndef MILESTRO_SCROLL_PHASE_MINIMAL_POLL_QUEUE_H
#define MILESTRO_SCROLL_PHASE_MINIMAL_POLL_QUEUE_H

#include <Milestro/input/ScrollPhaseMonitor.h>

#include <deque>

namespace milestro::input {

inline ScrollPhaseMonitorResult PollMinimalScrollPhaseQueue(std::deque<ScrollPhaseSample>& samples,
                                                            ScrollPhaseMinimalQueueFailure failure,
                                                            ScrollPhaseMinimalPollOutput& output) noexcept {
    output = {};
    if (failure != ScrollPhaseMinimalQueueFailure::None) {
        output.captureInvalidReason = failure;
        return ScrollPhaseMonitorResult::CaptureInvalid;
    }
    if (samples.empty()) {
        return ScrollPhaseMonitorResult::Succeeded;
    }

    const ScrollPhaseSample& source = samples.front();
    output.hasSample = true;
    output.sample.validFields = source.validFields;
    output.sample.sequence = source.sequence;
    output.sample.gestureId = source.gestureId;
    output.sample.timestamp = source.timestamp;
    output.sample.windowNumber = source.windowNumber;
    output.sample.scrollingDeltaX = source.scrollingDeltaX;
    output.sample.scrollingDeltaY = source.scrollingDeltaY;
    output.sample.gesturePhase = source.gesturePhase;
    output.sample.momentumPhase = source.momentumPhase;
    samples.pop_front();
    output.remaining = static_cast<int32_t>(samples.size());
    output.hasMore = output.remaining > 0;
    return ScrollPhaseMonitorResult::Succeeded;
}

} // namespace milestro::input

#endif // MILESTRO_SCROLL_PHASE_MINIMAL_POLL_QUEUE_H
