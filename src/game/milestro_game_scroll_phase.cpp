#include <Milestro/game/milestro_game_interface.h>
#include <Milestro/game/milestro_game_retcode.h>
#include <Milestro/input/ScrollPhaseMonitor.h>

extern "C" {

MILESTRO_API int64_t MilestroScrollPhaseMonitorStart(int32_t& result, int32_t mode, int64_t& leaseId) {
    result = static_cast<int32_t>(
            milestro::input::StartScrollPhaseMonitor(static_cast<milestro::input::ScrollPhaseMonitorMode>(mode),
                                                     leaseId));
    return MILESTRO_API_RET_OK;
}

MILESTRO_API int64_t MilestroScrollPhaseMonitorStop(int32_t& result, int64_t leaseId) {
    result = static_cast<int32_t>(milestro::input::StopScrollPhaseMonitor(leaseId));
    return MILESTRO_API_RET_OK;
}

MILESTRO_API int64_t MilestroScrollPhaseMonitorPoll(int32_t& result,
                                                    int64_t leaseId,
                                                    int32_t& hasSample,
                                                    int64_t& sequence,
                                                    int64_t& gestureId,
                                                    double& timestamp,
                                                    int64_t& windowNumber,
                                                    int64_t& keyWindowNumber,
                                                    int64_t& eventNumber,
                                                    double& deltaX,
                                                    double& deltaY,
                                                    double& scrollingDeltaX,
                                                    double& scrollingDeltaY,
                                                    int32_t& gesturePhase,
                                                    int32_t& momentumPhase,
                                                    int32_t& precise,
                                                    int32_t& directionInvertedFromDevice,
                                                    int32_t& queueOverflowed) {
    milestro::input::ScrollPhaseSample sample;
    bool receivedSample = false;
    result = static_cast<int32_t>(milestro::input::PollScrollPhaseMonitor(leaseId, sample, receivedSample));
    hasSample = receivedSample ? 1 : 0;
    sequence = sample.sequence;
    gestureId = sample.gestureId;
    timestamp = sample.timestamp;
    windowNumber = sample.windowNumber;
    keyWindowNumber = sample.keyWindowNumber;
    eventNumber = sample.eventNumber;
    deltaX = sample.deltaX;
    deltaY = sample.deltaY;
    scrollingDeltaX = sample.scrollingDeltaX;
    scrollingDeltaY = sample.scrollingDeltaY;
    gesturePhase = static_cast<int32_t>(sample.gesturePhase);
    momentumPhase = static_cast<int32_t>(sample.momentumPhase);
    precise = sample.precise;
    directionInvertedFromDevice = sample.directionInvertedFromDevice;
    queueOverflowed = sample.queueOverflowed;
    return MILESTRO_API_RET_OK;
}

MILESTRO_API int64_t MilestroScrollPhaseMonitorPollMinimal(int32_t& result,
                                                           int64_t leaseId,
                                                           int32_t& captureInvalidReason,
                                                           int32_t& hasSample,
                                                           int32_t& hasMore,
                                                           int32_t& remaining,
                                                           uint32_t& validFields,
                                                           int64_t& sequence,
                                                           int64_t& gestureId,
                                                           double& timestamp,
                                                           int64_t& windowNumber,
                                                           double& scrollingDeltaX,
                                                           double& scrollingDeltaY,
                                                           int32_t& gesturePhase,
                                                           int32_t& momentumPhase) {
    milestro::input::ScrollPhaseMinimalPollOutput output;
    result = static_cast<int32_t>(milestro::input::PollMinimalScrollPhaseMonitor(leaseId, output));
    captureInvalidReason = static_cast<int32_t>(output.captureInvalidReason);
    hasSample = output.hasSample ? 1 : 0;
    hasMore = output.hasMore ? 1 : 0;
    remaining = output.remaining;
    validFields = output.sample.validFields;
    sequence = output.sample.sequence;
    gestureId = output.sample.gestureId;
    timestamp = output.sample.timestamp;
    windowNumber = output.sample.windowNumber;
    scrollingDeltaX = output.sample.scrollingDeltaX;
    scrollingDeltaY = output.sample.scrollingDeltaY;
    gesturePhase = static_cast<int32_t>(output.sample.gesturePhase);
    momentumPhase = static_cast<int32_t>(output.sample.momentumPhase);
    return MILESTRO_API_RET_OK;
}

MILESTRO_API int64_t MilestroScrollPhaseMonitorGetMinimalInvalidDetail(int32_t& result,
                                                                       int64_t leaseId,
                                                                       int32_t& hasDetail,
                                                                       int32_t& failure,
                                                                       int32_t& priorTrackerState,
                                                                       int64_t& priorGestureId,
                                                                       int64_t& sequence,
                                                                       uint64_t& gesturePhaseBits,
                                                                       uint64_t& momentumPhaseBits,
                                                                       int64_t& windowNumber) {
    milestro::input::ScrollPhaseMinimalInvalidDetailOutput output;
    result = static_cast<int32_t>(milestro::input::GetMinimalScrollPhaseInvalidDetail(leaseId, output));
    hasDetail = output.hasDetail ? 1 : 0;
    failure = output.detail.failure;
    priorTrackerState = output.detail.priorTrackerState;
    priorGestureId = output.detail.priorGestureId;
    sequence = output.detail.sequence;
    gesturePhaseBits = output.detail.gesturePhaseBits;
    momentumPhaseBits = output.detail.momentumPhaseBits;
    windowNumber = output.detail.windowNumber;
    return MILESTRO_API_RET_OK;
}
}
