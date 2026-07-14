#include <Milestro/common/milestro_platform.h>
#include <Milestro/input/ScrollPhaseMonitor.h>

#if !MILESTRO_PLATFORM_MAC

namespace milestro::input {

ScrollPhaseMonitorResult StartScrollPhaseMonitor(ScrollPhaseMonitorMode mode, int64_t& leaseId) noexcept {
    (void) mode;
    leaseId = 0;
    return ScrollPhaseMonitorResult::Unsupported;
}

ScrollPhaseMonitorResult StopScrollPhaseMonitor(int64_t leaseId) noexcept {
    (void) leaseId;
    return ScrollPhaseMonitorResult::Unsupported;
}

ScrollPhaseMonitorResult PollScrollPhaseMonitor(int64_t leaseId, ScrollPhaseSample& sample, bool& hasSample) noexcept {
    (void) leaseId;
    sample = {};
    hasSample = false;
    return ScrollPhaseMonitorResult::Unsupported;
}

ScrollPhaseMonitorResult PollMinimalScrollPhaseMonitor(int64_t leaseId, ScrollPhaseMinimalPollOutput& output) noexcept {
    (void) leaseId;
    output = {};
    return ScrollPhaseMonitorResult::Unsupported;
}

ScrollPhaseMonitorResult GetMinimalScrollPhaseInvalidDetail(int64_t leaseId,
                                                            ScrollPhaseMinimalInvalidDetailOutput& output) noexcept {
    (void) leaseId;
    output = {};
    return ScrollPhaseMonitorResult::Unsupported;
}

bool HasActiveScrollPhaseMonitorLease() noexcept {
    return false;
}

bool HasActiveScrollPhaseMonitorState() noexcept {
    return false;
}

bool IsScrollPhaseMonitorMainThread() noexcept {
    return true;
}

ScrollPhaseMonitorResult ShutdownScrollPhaseMonitorForPluginUnload() noexcept {
    return ScrollPhaseMonitorResult::Succeeded;
}

} // namespace milestro::input

#endif
