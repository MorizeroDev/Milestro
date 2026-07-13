#include <Milestro/common/milestro_platform.h>
#include <Milestro/input/ScrollPhaseMonitor.h>

#if !MILESTRO_PLATFORM_MAC

namespace milestro::input {

ScrollPhaseMonitorResult StartScrollPhaseMonitor() noexcept {
    return ScrollPhaseMonitorResult::Unsupported;
}

ScrollPhaseMonitorResult StopScrollPhaseMonitor() noexcept {
    return ScrollPhaseMonitorResult::Unsupported;
}

ScrollPhaseMonitorResult PollScrollPhaseMonitor(ScrollPhaseSample& sample, bool& hasSample) noexcept {
    sample = {};
    hasSample = false;
    return ScrollPhaseMonitorResult::Unsupported;
}

} // namespace milestro::input

#endif
