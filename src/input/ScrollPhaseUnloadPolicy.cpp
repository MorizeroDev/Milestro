#include <Milestro/input/ScrollPhaseMonitor.h>

namespace milestro::input {

ScrollPhasePluginUnloadDecision DecideScrollPhasePluginUnload(bool activeState,
                                                              bool mainThread,
                                                              bool cleanupAttempted,
                                                              bool cleanupSucceeded) noexcept {
    if (cleanupAttempted) {
        return cleanupSucceeded && !activeState ? ScrollPhasePluginUnloadDecision::Return
                                                : ScrollPhasePluginUnloadDecision::Abort;
    }
    if (!activeState) {
        return ScrollPhasePluginUnloadDecision::Return;
    }
    return mainThread ? ScrollPhasePluginUnloadDecision::Cleanup : ScrollPhasePluginUnloadDecision::Abort;
}

} // namespace milestro::input
