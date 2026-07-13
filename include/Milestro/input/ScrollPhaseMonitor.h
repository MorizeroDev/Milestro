#ifndef MILESTRO_SCROLL_PHASE_MONITOR_H
#define MILESTRO_SCROLL_PHASE_MONITOR_H

#include <cstdint>

namespace milestro::input {

enum class ScrollPhaseMonitorResult : int32_t {
    Succeeded = 0,
    Unsupported = 1,
    WrongThread = 2,
    Failed = 3,
    AlreadyStarted = 4,
    InvalidLease = 5,
};

enum class ScrollPhaseMonitorMode : int32_t {
    PassThrough = 0,
    CaptureSamples = 1,
    ReadProperties = 2,
    ReadEventProperties = 3,
    ReadEventScalars = 4,
    WriteLocalPod = 5,
    ReadPhasesOnly = 6,
    ReadPhasesTimestamp = 7,
    WritePhasesTimestampWindowPod = 8,
    ReadPhasesTimestampWindow = 9,
};

constexpr bool IsValidScrollPhaseMonitorMode(ScrollPhaseMonitorMode mode) noexcept {
    return mode == ScrollPhaseMonitorMode::PassThrough || mode == ScrollPhaseMonitorMode::CaptureSamples ||
           mode == ScrollPhaseMonitorMode::ReadProperties || mode == ScrollPhaseMonitorMode::ReadEventProperties ||
           mode == ScrollPhaseMonitorMode::ReadEventScalars || mode == ScrollPhaseMonitorMode::WriteLocalPod ||
           mode == ScrollPhaseMonitorMode::ReadPhasesOnly || mode == ScrollPhaseMonitorMode::ReadPhasesTimestamp ||
           mode == ScrollPhaseMonitorMode::WritePhasesTimestampWindowPod ||
           mode == ScrollPhaseMonitorMode::ReadPhasesTimestampWindow;
}

constexpr bool ShouldCaptureScrollPhaseSamples(ScrollPhaseMonitorMode mode) noexcept {
    return mode == ScrollPhaseMonitorMode::CaptureSamples;
}

constexpr bool ShouldReadScrollPhaseProperties(ScrollPhaseMonitorMode mode) noexcept {
    return mode == ScrollPhaseMonitorMode::ReadProperties;
}

constexpr bool ShouldReadScrollPhaseEventProperties(ScrollPhaseMonitorMode mode) noexcept {
    return mode == ScrollPhaseMonitorMode::ReadEventProperties;
}

constexpr bool ShouldReadScrollPhaseEventScalars(ScrollPhaseMonitorMode mode) noexcept {
    return mode == ScrollPhaseMonitorMode::ReadEventScalars;
}

constexpr bool ShouldWriteScrollPhaseLocalPod(ScrollPhaseMonitorMode mode) noexcept {
    return mode == ScrollPhaseMonitorMode::WriteLocalPod;
}

constexpr bool ShouldReadScrollPhasesOnly(ScrollPhaseMonitorMode mode) noexcept {
    return mode == ScrollPhaseMonitorMode::ReadPhasesOnly;
}

constexpr bool ShouldReadScrollPhasesTimestamp(ScrollPhaseMonitorMode mode) noexcept {
    return mode == ScrollPhaseMonitorMode::ReadPhasesTimestamp;
}

constexpr bool ShouldWriteScrollPhasesTimestampWindowPod(ScrollPhaseMonitorMode mode) noexcept {
    return mode == ScrollPhaseMonitorMode::WritePhasesTimestampWindowPod;
}

constexpr bool ShouldReadScrollPhasesTimestampWindow(ScrollPhaseMonitorMode mode) noexcept {
    return mode == ScrollPhaseMonitorMode::ReadPhasesTimestampWindow;
}

enum class ScrollPhase : int32_t {
    Unknown = 0,
    None = 1,
    Began = 2,
    Changed = 3,
    Stationary = 4,
    Ended = 5,
    Canceled = 6,
};

enum class ScrollPhasePluginUnloadDecision : int32_t {
    Return = 0,
    Cleanup = 1,
    Abort = 2,
};

struct ScrollPhaseSample {
    int64_t sequence = 0;
    int64_t gestureId = 0;
    double timestamp = 0.0;
    int64_t windowNumber = 0;
    int64_t keyWindowNumber = 0;
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

ScrollPhaseMonitorResult StartScrollPhaseMonitor(ScrollPhaseMonitorMode mode, int64_t& leaseId) noexcept;
ScrollPhaseMonitorResult StopScrollPhaseMonitor(int64_t leaseId) noexcept;
ScrollPhaseMonitorResult PollScrollPhaseMonitor(int64_t leaseId, ScrollPhaseSample& sample, bool& hasSample) noexcept;
bool HasActiveScrollPhaseMonitorLease() noexcept;
bool HasActiveScrollPhaseMonitorState() noexcept;
bool IsScrollPhaseMonitorMainThread() noexcept;
ScrollPhaseMonitorResult ShutdownScrollPhaseMonitorForPluginUnload() noexcept;
ScrollPhasePluginUnloadDecision
DecideScrollPhasePluginUnload(bool activeState, bool mainThread, bool cleanupAttempted, bool cleanupSucceeded) noexcept;

} // namespace milestro::input

#endif // MILESTRO_SCROLL_PHASE_MONITOR_H
