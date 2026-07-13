#include <Milestro/game/milestro_game_interface.h>
#include <Milestro/game/milestro_game_retcode.h>
#include <Milestro/input/ScrollPhaseMonitor.h>

extern "C" {

MILESTRO_API int64_t MilestroScrollPhaseMonitorStart(int32_t& result) {
    result = static_cast<int32_t>(milestro::input::StartScrollPhaseMonitor());
    return MILESTRO_API_RET_OK;
}

MILESTRO_API int64_t MilestroScrollPhaseMonitorStop(int32_t& result) {
    result = static_cast<int32_t>(milestro::input::StopScrollPhaseMonitor());
    return MILESTRO_API_RET_OK;
}

MILESTRO_API int64_t MilestroScrollPhaseMonitorPoll(int32_t& result,
                                                    int32_t& hasSample,
                                                    int64_t& sequence,
                                                    int64_t& gestureId,
                                                    double& timestamp,
                                                    int64_t& windowNumber,
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
    result = static_cast<int32_t>(milestro::input::PollScrollPhaseMonitor(sample, receivedSample));
    hasSample = receivedSample ? 1 : 0;
    sequence = sample.sequence;
    gestureId = sample.gestureId;
    timestamp = sample.timestamp;
    windowNumber = sample.windowNumber;
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
}
