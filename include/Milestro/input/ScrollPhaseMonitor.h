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
    ModeContractMismatch = 6,
    CaptureInvalid = 7,
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
    ReadPhasesTimestampWindowScrollingDelta = 10,
    QueueMinimalSamples = 11,
    QueueMinimalTrackedSamples = 12,
};

constexpr bool IsValidScrollPhaseMonitorMode(ScrollPhaseMonitorMode mode) noexcept {
    return mode == ScrollPhaseMonitorMode::PassThrough || mode == ScrollPhaseMonitorMode::CaptureSamples ||
           mode == ScrollPhaseMonitorMode::ReadProperties || mode == ScrollPhaseMonitorMode::ReadEventProperties ||
           mode == ScrollPhaseMonitorMode::ReadEventScalars || mode == ScrollPhaseMonitorMode::WriteLocalPod ||
           mode == ScrollPhaseMonitorMode::ReadPhasesOnly || mode == ScrollPhaseMonitorMode::ReadPhasesTimestamp ||
           mode == ScrollPhaseMonitorMode::WritePhasesTimestampWindowPod ||
           mode == ScrollPhaseMonitorMode::ReadPhasesTimestampWindow ||
           mode == ScrollPhaseMonitorMode::ReadPhasesTimestampWindowScrollingDelta ||
           mode == ScrollPhaseMonitorMode::QueueMinimalSamples ||
           mode == ScrollPhaseMonitorMode::QueueMinimalTrackedSamples;
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

constexpr bool ShouldReadScrollPhasesTimestampWindowScrollingDelta(ScrollPhaseMonitorMode mode) noexcept {
    return mode == ScrollPhaseMonitorMode::ReadPhasesTimestampWindowScrollingDelta;
}

constexpr bool ShouldQueueMinimalScrollPhaseSamples(ScrollPhaseMonitorMode mode) noexcept {
    return mode == ScrollPhaseMonitorMode::QueueMinimalSamples;
}

constexpr bool ShouldQueueMinimalTrackedScrollPhaseSamples(ScrollPhaseMonitorMode mode) noexcept {
    return mode == ScrollPhaseMonitorMode::QueueMinimalTrackedSamples;
}

constexpr bool CanUseLegacyScrollPhasePoll(ScrollPhaseMonitorMode mode) noexcept {
    return mode == ScrollPhaseMonitorMode::CaptureSamples;
}

constexpr ScrollPhaseMonitorResult ValidateLegacyScrollPhasePollMode(ScrollPhaseMonitorMode mode) noexcept {
    return CanUseLegacyScrollPhasePoll(mode) ? ScrollPhaseMonitorResult::Succeeded
                                             : ScrollPhaseMonitorResult::ModeContractMismatch;
}

constexpr bool CanUseMinimalScrollPhasePoll(ScrollPhaseMonitorMode mode) noexcept {
    return mode == ScrollPhaseMonitorMode::QueueMinimalTrackedSamples;
}

constexpr ScrollPhaseMonitorResult ValidateMinimalScrollPhasePollMode(ScrollPhaseMonitorMode mode) noexcept {
    return CanUseMinimalScrollPhasePoll(mode) ? ScrollPhaseMonitorResult::Succeeded
                                              : ScrollPhaseMonitorResult::ModeContractMismatch;
}

enum class ScrollPhaseMinimalQueueFailure : int32_t {
    None = 0,
    CapacityExceeded = 1,
    SequenceExhausted = 2,
    InvalidGestureTransition = 3,
};

enum class ScrollPhase : int32_t {
    Unknown = 0,
    None = 1,
    Began = 2,
    Changed = 3,
    Stationary = 4,
    Ended = 5,
    Canceled = 6,
    MayBegin = 7,
};

enum class ScrollPhasePluginUnloadDecision : int32_t {
    Return = 0,
    Cleanup = 1,
    Abort = 2,
};

enum class ScrollPhaseSampleField : uint32_t {
    Sequence = 1U << 0U,
    GestureId = 1U << 1U,
    Timestamp = 1U << 2U,
    WindowNumber = 1U << 3U,
    KeyWindowNumber = 1U << 4U,
    EventNumber = 1U << 5U,
    RawDelta = 1U << 6U,
    ScrollingDelta = 1U << 7U,
    GesturePhase = 1U << 8U,
    MomentumPhase = 1U << 9U,
    Precise = 1U << 10U,
    NaturalDirection = 1U << 11U,
};

constexpr uint32_t ScrollPhaseSampleFieldBit(ScrollPhaseSampleField field) noexcept {
    return static_cast<uint32_t>(field);
}

constexpr uint32_t kMinimalQueueScrollPhaseSampleFields =
        ScrollPhaseSampleFieldBit(ScrollPhaseSampleField::Sequence) |
        ScrollPhaseSampleFieldBit(ScrollPhaseSampleField::Timestamp) |
        ScrollPhaseSampleFieldBit(ScrollPhaseSampleField::WindowNumber) |
        ScrollPhaseSampleFieldBit(ScrollPhaseSampleField::ScrollingDelta) |
        ScrollPhaseSampleFieldBit(ScrollPhaseSampleField::GesturePhase) |
        ScrollPhaseSampleFieldBit(ScrollPhaseSampleField::MomentumPhase);

constexpr uint32_t kLegacyCaptureScrollPhaseSampleFields =
        kMinimalQueueScrollPhaseSampleFields | ScrollPhaseSampleFieldBit(ScrollPhaseSampleField::GestureId) |
        ScrollPhaseSampleFieldBit(ScrollPhaseSampleField::KeyWindowNumber) |
        ScrollPhaseSampleFieldBit(ScrollPhaseSampleField::EventNumber) |
        ScrollPhaseSampleFieldBit(ScrollPhaseSampleField::RawDelta) |
        ScrollPhaseSampleFieldBit(ScrollPhaseSampleField::Precise) |
        ScrollPhaseSampleFieldBit(ScrollPhaseSampleField::NaturalDirection);

constexpr uint32_t MinimalTrackedScrollPhaseSampleFields(bool gestureIdValid) noexcept {
    return gestureIdValid
                   ? kMinimalQueueScrollPhaseSampleFields | ScrollPhaseSampleFieldBit(ScrollPhaseSampleField::GestureId)
                   : kMinimalQueueScrollPhaseSampleFields;
}

constexpr bool HasScrollPhaseSampleFields(uint32_t validFields, uint32_t requiredFields) noexcept {
    return (validFields & requiredFields) == requiredFields;
}

constexpr bool HasScrollPhaseSampleField(uint32_t validFields, ScrollPhaseSampleField field) noexcept {
    return HasScrollPhaseSampleFields(validFields, ScrollPhaseSampleFieldBit(field));
}

struct ScrollPhaseSample {
    uint32_t validFields = 0;
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

struct ScrollPhaseMinimalSample {
    uint32_t validFields = 0;
    int64_t sequence = 0;
    int64_t gestureId = 0;
    double timestamp = 0.0;
    int64_t windowNumber = 0;
    double scrollingDeltaX = 0.0;
    double scrollingDeltaY = 0.0;
    ScrollPhase gesturePhase = ScrollPhase::Unknown;
    ScrollPhase momentumPhase = ScrollPhase::Unknown;
};

struct ScrollPhaseMinimalPollOutput {
    ScrollPhaseMinimalQueueFailure captureInvalidReason = ScrollPhaseMinimalQueueFailure::None;
    bool hasSample = false;
    bool hasMore = false;
    int32_t remaining = 0;
    ScrollPhaseMinimalSample sample;
};

struct ScrollPhaseMinimalInvalidDetail {
    int32_t failure = 0;
    int32_t priorTrackerState = 0;
    int64_t priorGestureId = 0;
    int64_t sequence = 0;
    uint64_t gesturePhaseBits = 0;
    uint64_t momentumPhaseBits = 0;
    int64_t windowNumber = 0;
};

struct ScrollPhaseMinimalInvalidDetailOutput {
    bool hasDetail = false;
    ScrollPhaseMinimalInvalidDetail detail;
};

ScrollPhaseMonitorResult StartScrollPhaseMonitor(ScrollPhaseMonitorMode mode, int64_t& leaseId) noexcept;
ScrollPhaseMonitorResult StopScrollPhaseMonitor(int64_t leaseId) noexcept;
ScrollPhaseMonitorResult PollScrollPhaseMonitor(int64_t leaseId, ScrollPhaseSample& sample, bool& hasSample) noexcept;
ScrollPhaseMonitorResult PollMinimalScrollPhaseMonitor(int64_t leaseId, ScrollPhaseMinimalPollOutput& output) noexcept;
ScrollPhaseMonitorResult GetMinimalScrollPhaseInvalidDetail(int64_t leaseId,
                                                            ScrollPhaseMinimalInvalidDetailOutput& output) noexcept;
bool HasActiveScrollPhaseMonitorLease() noexcept;
bool HasActiveScrollPhaseMonitorState() noexcept;
bool IsScrollPhaseMonitorMainThread() noexcept;
ScrollPhaseMonitorResult ShutdownScrollPhaseMonitorForPluginUnload() noexcept;
ScrollPhasePluginUnloadDecision
DecideScrollPhasePluginUnload(bool activeState, bool mainThread, bool cleanupAttempted, bool cleanupSucceeded) noexcept;

} // namespace milestro::input

#endif // MILESTRO_SCROLL_PHASE_MONITOR_H
