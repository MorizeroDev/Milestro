#include <Milestro/common/milestro_platform.h>
#include <Milestro/input/ScrollPhaseMonitor.h>

#include "ScrollPhaseGestureTracker.h"
#include "ScrollPhaseLease.h"
#include "ScrollPhaseMinimalGestureTracker.h"
#include "ScrollPhaseMinimalPollQueue.h"
#include "ScrollPhaseMinimalQueueAdmission.h"
#include "ScrollPhaseMonitorModePublication.h"

#if MILESTRO_PLATFORM_MAC

#import <AppKit/AppKit.h>

#include <atomic>
#include <deque>

static_assert(milestro::input::kNativeScrollPhaseNone == static_cast<uint64_t>(NSEventPhaseNone));
static_assert(milestro::input::kNativeScrollPhaseBegan == static_cast<uint64_t>(NSEventPhaseBegan));
static_assert(milestro::input::kNativeScrollPhaseStationary == static_cast<uint64_t>(NSEventPhaseStationary));
static_assert(milestro::input::kNativeScrollPhaseChanged == static_cast<uint64_t>(NSEventPhaseChanged));
static_assert(milestro::input::kNativeScrollPhaseEnded == static_cast<uint64_t>(NSEventPhaseEnded));
static_assert(milestro::input::kNativeScrollPhaseCanceled == static_cast<uint64_t>(NSEventPhaseCancelled));
static_assert(milestro::input::kNativeScrollPhaseMayBegin == static_cast<uint64_t>(NSEventPhaseMayBegin));

namespace milestro::input {
namespace {

constexpr size_t kMaximumQueuedSamples = 256;
static_assert(kMaximumQueuedSamples == ScrollPhaseMinimalQueueAdmission::MaximumQueuedSamples);

id monitorToken = nil;
std::atomic<bool> monitorPublished{false};
std::deque<ScrollPhaseSample> samples;
int64_t nextSequence = 1;
ScrollPhaseLease lease;
ScrollPhaseGestureTracker gestureTracker;
ScrollPhaseMinimalQueueAdmission minimalQueueAdmission;
ScrollPhaseMinimalGestureTracker minimalGestureTracker;
bool queueOverflowed = false;
ScrollPhaseMonitorModePublication activeMonitorMode;

ScrollPhase ConvertPhase(NSEventPhase phase) {
    if (phase == NSEventPhaseNone) {
        return ScrollPhase::None;
    }
    if ((phase & NSEventPhaseCancelled) != 0) {
        return ScrollPhase::Canceled;
    }
    if ((phase & NSEventPhaseEnded) != 0) {
        return ScrollPhase::Ended;
    }
    if ((phase & NSEventPhaseBegan) != 0) {
        return ScrollPhase::Began;
    }
    if ((phase & NSEventPhaseChanged) != 0) {
        return ScrollPhase::Changed;
    }
    if ((phase & NSEventPhaseStationary) != 0) {
        return ScrollPhase::Stationary;
    }
    return ScrollPhase::Unknown;
}

struct ScrollPhaseEventProperties {
    ScrollPhase gesturePhase;
    ScrollPhase momentumPhase;
    double timestamp;
    int64_t windowNumber;
    int64_t keyWindowNumber;
    int64_t eventNumber;
    double deltaX;
    double deltaY;
    double scrollingDeltaX;
    double scrollingDeltaY;
    int32_t precise;
    int32_t directionInvertedFromDevice;
};

void ReadProperties(NSEvent* event) {
    volatile ScrollPhaseEventProperties properties;
    properties.gesturePhase = ConvertPhase(event.phase);
    properties.momentumPhase = ConvertPhase(event.momentumPhase);
    properties.timestamp = event.timestamp;
    properties.windowNumber = event.windowNumber;
    properties.keyWindowNumber = NSApp.keyWindow.windowNumber;
    properties.eventNumber = event.eventNumber;
    properties.deltaX = event.deltaX;
    properties.deltaY = event.deltaY;
    properties.scrollingDeltaX = event.scrollingDeltaX;
    properties.scrollingDeltaY = event.scrollingDeltaY;
    properties.precise = event.hasPreciseScrollingDeltas ? 1 : 0;
    properties.directionInvertedFromDevice = event.isDirectionInvertedFromDevice ? 1 : 0;
}

void ReadEventProperties(NSEvent* event) {
    volatile ScrollPhaseEventProperties properties;
    properties.gesturePhase = ConvertPhase(event.phase);
    properties.momentumPhase = ConvertPhase(event.momentumPhase);
    properties.timestamp = event.timestamp;
    properties.windowNumber = event.windowNumber;
    properties.eventNumber = event.eventNumber;
    properties.deltaX = event.deltaX;
    properties.deltaY = event.deltaY;
    properties.scrollingDeltaX = event.scrollingDeltaX;
    properties.scrollingDeltaY = event.scrollingDeltaY;
    properties.precise = event.hasPreciseScrollingDeltas ? 1 : 0;
    properties.directionInvertedFromDevice = event.isDirectionInvertedFromDevice ? 1 : 0;
}

void ReadEventScalars(NSEvent* event) {
    volatile ScrollPhaseEventProperties properties;
    properties.gesturePhase = ConvertPhase(event.phase);
    properties.momentumPhase = ConvertPhase(event.momentumPhase);
    properties.timestamp = event.timestamp;
    properties.eventNumber = event.eventNumber;
    properties.deltaX = event.deltaX;
    properties.deltaY = event.deltaY;
    properties.scrollingDeltaX = event.scrollingDeltaX;
    properties.scrollingDeltaY = event.scrollingDeltaY;
    properties.precise = event.hasPreciseScrollingDeltas ? 1 : 0;
    properties.directionInvertedFromDevice = event.isDirectionInvertedFromDevice ? 1 : 0;
}

void WriteLocalPod() {
    volatile ScrollPhaseEventProperties properties;
    properties.gesturePhase = ScrollPhase::Began;
    properties.momentumPhase = ScrollPhase::None;
    properties.timestamp = 1.0;
    properties.eventNumber = 1;
    properties.deltaX = 1.0;
    properties.deltaY = 1.0;
    properties.scrollingDeltaX = 1.0;
    properties.scrollingDeltaY = 1.0;
    properties.precise = 1;
    properties.directionInvertedFromDevice = 0;
}

void ReadPhasesOnly(NSEvent* event) {
    volatile ScrollPhaseEventProperties properties;
    properties.gesturePhase = ConvertPhase(event.phase);
    properties.momentumPhase = ConvertPhase(event.momentumPhase);
    properties.timestamp = 1.0;
    properties.eventNumber = 1;
    properties.deltaX = 1.0;
    properties.deltaY = 1.0;
    properties.scrollingDeltaX = 1.0;
    properties.scrollingDeltaY = 1.0;
    properties.precise = 1;
    properties.directionInvertedFromDevice = 0;
}

void ReadPhasesTimestamp(NSEvent* event) {
    volatile ScrollPhaseEventProperties properties;
    properties.gesturePhase = ConvertPhase(event.phase);
    properties.momentumPhase = ConvertPhase(event.momentumPhase);
    properties.timestamp = event.timestamp;
    properties.eventNumber = 1;
    properties.deltaX = 1.0;
    properties.deltaY = 1.0;
    properties.scrollingDeltaX = 1.0;
    properties.scrollingDeltaY = 1.0;
    properties.precise = 1;
    properties.directionInvertedFromDevice = 0;
}

void WritePhasesTimestampWindowPod(NSEvent* event) {
    volatile ScrollPhaseEventProperties properties;
    properties.gesturePhase = ConvertPhase(event.phase);
    properties.momentumPhase = ConvertPhase(event.momentumPhase);
    properties.timestamp = event.timestamp;
    properties.windowNumber = 1;
    properties.eventNumber = 1;
    properties.deltaX = 1.0;
    properties.deltaY = 1.0;
    properties.scrollingDeltaX = 1.0;
    properties.scrollingDeltaY = 1.0;
    properties.precise = 1;
    properties.directionInvertedFromDevice = 0;
}

void ReadPhasesTimestampWindow(NSEvent* event) {
    volatile ScrollPhaseEventProperties properties;
    properties.gesturePhase = ConvertPhase(event.phase);
    properties.momentumPhase = ConvertPhase(event.momentumPhase);
    properties.timestamp = event.timestamp;
    properties.windowNumber = event.windowNumber;
    properties.eventNumber = 1;
    properties.deltaX = 1.0;
    properties.deltaY = 1.0;
    properties.scrollingDeltaX = 1.0;
    properties.scrollingDeltaY = 1.0;
    properties.precise = 1;
    properties.directionInvertedFromDevice = 0;
}

void ReadPhasesTimestampWindowScrollingDelta(NSEvent* event) {
    volatile ScrollPhaseEventProperties properties;
    properties.gesturePhase = ConvertPhase(event.phase);
    properties.momentumPhase = ConvertPhase(event.momentumPhase);
    properties.timestamp = event.timestamp;
    properties.windowNumber = event.windowNumber;
    properties.eventNumber = 1;
    properties.deltaX = 1.0;
    properties.deltaY = 1.0;
    properties.scrollingDeltaX = event.scrollingDeltaX;
    properties.scrollingDeltaY = event.scrollingDeltaY;
    properties.precise = 1;
    properties.directionInvertedFromDevice = 0;
}

void Enqueue(NSEvent* event) {
    const ScrollPhase gesturePhase = ConvertPhase(event.phase);
    const ScrollPhase momentumPhase = ConvertPhase(event.momentumPhase);
    ScrollPhaseSample sample;
    sample.validFields = kLegacyCaptureScrollPhaseSampleFields;
    sample.sequence = nextSequence++;
    sample.gestureId = gestureTracker.Resolve(gesturePhase, momentumPhase);
    sample.timestamp = event.timestamp;
    sample.windowNumber = event.windowNumber;
    sample.keyWindowNumber = NSApp.keyWindow.windowNumber;
    sample.eventNumber = event.eventNumber;
    sample.deltaX = event.deltaX;
    sample.deltaY = event.deltaY;
    sample.scrollingDeltaX = event.scrollingDeltaX;
    sample.scrollingDeltaY = event.scrollingDeltaY;
    sample.gesturePhase = gesturePhase;
    sample.momentumPhase = momentumPhase;
    sample.precise = event.hasPreciseScrollingDeltas ? 1 : 0;
    sample.directionInvertedFromDevice = event.isDirectionInvertedFromDevice ? 1 : 0;

    if (samples.size() >= kMaximumQueuedSamples) {
        samples.pop_front();
        queueOverflowed = true;
    }
    samples.push_back(sample);
}

void EnqueueMinimal(NSEvent* event) {
    ScrollPhaseSample sample;
    if (!minimalQueueAdmission.TryAccept(samples.size(), sample.sequence)) {
        return;
    }

    sample.validFields = kMinimalQueueScrollPhaseSampleFields;
    sample.gesturePhase = ConvertPhase(event.phase);
    sample.momentumPhase = ConvertPhase(event.momentumPhase);
    sample.timestamp = event.timestamp;
    sample.windowNumber = event.windowNumber;
    sample.scrollingDeltaX = event.scrollingDeltaX;
    sample.scrollingDeltaY = event.scrollingDeltaY;
    samples.push_back(sample);
}

void EnqueueMinimalTracked(NSEvent* event) {
    ScrollPhaseSample sample;
    if (!minimalQueueAdmission.TryAccept(samples.size(), sample.sequence)) {
        return;
    }

    sample.validFields = kMinimalQueueScrollPhaseSampleFields;
    const ScrollPhaseMinimalDecodedPhase gesturePhase = DecodeMinimalScrollPhase(static_cast<uint64_t>(event.phase));
    sample.gesturePhase = gesturePhase.phase;
    const ScrollPhaseMinimalDecodedPhase momentumPhase =
            DecodeMinimalScrollPhase(static_cast<uint64_t>(event.momentumPhase));
    sample.momentumPhase = momentumPhase.phase;
    sample.timestamp = event.timestamp;
    sample.windowNumber = event.windowNumber;
    sample.scrollingDeltaX = event.scrollingDeltaX;
    sample.scrollingDeltaY = event.scrollingDeltaY;

    const ScrollPhaseMinimalGestureResult gesture = minimalGestureTracker.Resolve(gesturePhase, momentumPhase);
    sample.validFields =
            MinimalTrackedScrollPhaseSampleFields(gesture.kind == ScrollPhaseMinimalGestureResultKind::Resolved);
    sample.gestureId = gesture.gestureId;
    samples.push_back(sample);

    if (gesture.kind == ScrollPhaseMinimalGestureResultKind::InvalidTransition) {
        (void) minimalQueueAdmission.Fail(ScrollPhaseMinimalQueueFailure::InvalidGestureTransition);
    }
}

void ResetState() {
    samples.clear();
    nextSequence = 1;
    gestureTracker.Reset();
    minimalQueueAdmission.FinishCleanup(true);
    minimalGestureTracker.FinishCleanup(true);
    queueOverflowed = false;
}

ScrollPhaseMonitorResult RemoveMonitor(int64_t leaseId, bool forceRelease) {
    @try {
        if (monitorToken != nil) {
            [NSEvent removeMonitor:monitorToken];
        }
    } @catch (NSException* exception) {
        (void) exception;
        minimalQueueAdmission.FinishCleanup(false);
        minimalGestureTracker.FinishCleanup(false);
        activeMonitorMode.FinishCleanup(false);
        return ScrollPhaseMonitorResult::Failed;
    }

    monitorToken = nil;
    monitorPublished.store(false, std::memory_order_release);
    if (forceRelease) {
        lease.ForceRelease();
    } else {
        const ScrollPhaseMonitorResult releaseResult = lease.Release(leaseId);
        if (releaseResult != ScrollPhaseMonitorResult::Succeeded) {
            minimalQueueAdmission.FinishCleanup(false);
            minimalGestureTracker.FinishCleanup(false);
            activeMonitorMode.FinishCleanup(false);
            return releaseResult;
        }
    }
    activeMonitorMode.FinishCleanup(true);
    ResetState();
    return ScrollPhaseMonitorResult::Succeeded;
}

bool RemoveUnpublishedMonitor(id token) {
    @try {
        [NSEvent removeMonitor:token];
        return true;
    } @catch (NSException* exception) {
        (void) exception;
        return false;
    }
}

bool ReleaseStartLease(int64_t& leaseId) {
    if (lease.Release(leaseId) != ScrollPhaseMonitorResult::Succeeded) {
        return false;
    }
    leaseId = 0;
    ResetState();
    return true;
}

} // namespace

ScrollPhaseMonitorResult StartScrollPhaseMonitor(ScrollPhaseMonitorMode mode, int64_t& leaseId) noexcept {
    @autoreleasepool {
        leaseId = 0;
        if (!IsValidScrollPhaseMonitorMode(mode)) {
            return ScrollPhaseMonitorResult::Failed;
        }
        const bool captureSamples = ShouldCaptureScrollPhaseSamples(mode);
        const bool readProperties = ShouldReadScrollPhaseProperties(mode);
        const bool readEventProperties = ShouldReadScrollPhaseEventProperties(mode);
        const bool readEventScalars = ShouldReadScrollPhaseEventScalars(mode);
        const bool writeLocalPod = ShouldWriteScrollPhaseLocalPod(mode);
        const bool readPhasesOnly = ShouldReadScrollPhasesOnly(mode);
        const bool readPhasesTimestamp = ShouldReadScrollPhasesTimestamp(mode);
        const bool writePhasesTimestampWindowPod = ShouldWriteScrollPhasesTimestampWindowPod(mode);
        const bool readPhasesTimestampWindow = ShouldReadScrollPhasesTimestampWindow(mode);
        const bool readPhasesTimestampWindowScrollingDelta = ShouldReadScrollPhasesTimestampWindowScrollingDelta(mode);
        const bool queueMinimalSamples = ShouldQueueMinimalScrollPhaseSamples(mode);
        const bool queueMinimalTrackedSamples = ShouldQueueMinimalTrackedScrollPhaseSamples(mode);
        if (![NSThread isMainThread]) {
            return ScrollPhaseMonitorResult::WrongThread;
        }
        if (monitorPublished.load(std::memory_order_acquire)) {
            return ScrollPhaseMonitorResult::AlreadyStarted;
        }

        const ScrollPhaseMonitorResult leaseResult = lease.Acquire(leaseId);
        if (leaseResult != ScrollPhaseMonitorResult::Succeeded) {
            return leaseResult;
        }

        id newToken = nil;
        @try {
            ResetState();
            newToken = [NSEvent addLocalMonitorForEventsMatchingMask:NSEventMaskScrollWheel
                                                             handler:^NSEvent*(NSEvent* event) {
                                                               if (captureSamples) {
                                                                   Enqueue(event);
                                                               } else if (readProperties) {
                                                                   ReadProperties(event);
                                                               } else if (readEventProperties) {
                                                                   ReadEventProperties(event);
                                                               } else if (readEventScalars) {
                                                                   ReadEventScalars(event);
                                                               } else if (writeLocalPod) {
                                                                   WriteLocalPod();
                                                               } else if (readPhasesOnly) {
                                                                   ReadPhasesOnly(event);
                                                               } else if (readPhasesTimestamp) {
                                                                   ReadPhasesTimestamp(event);
                                                               } else if (writePhasesTimestampWindowPod) {
                                                                   WritePhasesTimestampWindowPod(event);
                                                               } else if (readPhasesTimestampWindow) {
                                                                   ReadPhasesTimestampWindow(event);
                                                               } else if (readPhasesTimestampWindowScrollingDelta) {
                                                                   ReadPhasesTimestampWindowScrollingDelta(event);
                                                               } else if (queueMinimalSamples) {
                                                                   EnqueueMinimal(event);
                                                               } else if (queueMinimalTrackedSamples) {
                                                                   EnqueueMinimalTracked(event);
                                                               }
                                                               return event;
                                                             }];
            if (newToken == nil) {
                (void) ReleaseStartLease(leaseId);
                return ScrollPhaseMonitorResult::Failed;
            }
            activeMonitorMode.Publish(mode);
            monitorToken = newToken;
            monitorPublished.store(true, std::memory_order_release);
            return ScrollPhaseMonitorResult::Succeeded;
        } @catch (NSException* exception) {
            (void) exception;
            if (newToken != nil && !RemoveUnpublishedMonitor(newToken)) {
                activeMonitorMode.Publish(mode);
                monitorToken = newToken;
                monitorPublished.store(true, std::memory_order_release);
                return ScrollPhaseMonitorResult::Failed;
            }
            (void) ReleaseStartLease(leaseId);
            return ScrollPhaseMonitorResult::Failed;
        }
    }
}

ScrollPhaseMonitorResult StopScrollPhaseMonitor(int64_t leaseId) noexcept {
    @autoreleasepool {
        if (![NSThread isMainThread]) {
            return ScrollPhaseMonitorResult::WrongThread;
        }
        const ScrollPhaseMonitorResult leaseResult = lease.Validate(leaseId);
        if (leaseResult != ScrollPhaseMonitorResult::Succeeded) {
            return leaseResult;
        }
        return RemoveMonitor(leaseId, false);
    }
}

ScrollPhaseMonitorResult PollScrollPhaseMonitor(int64_t leaseId, ScrollPhaseSample& sample, bool& hasSample) noexcept {
    @autoreleasepool {
        sample = {};
        hasSample = false;
        if (![NSThread isMainThread]) {
            return ScrollPhaseMonitorResult::WrongThread;
        }
        const ScrollPhaseMonitorResult leaseResult = lease.Validate(leaseId);
        if (leaseResult != ScrollPhaseMonitorResult::Succeeded) {
            return leaseResult;
        }
        ScrollPhaseMonitorMode publishedMode = ScrollPhaseMonitorMode::PassThrough;
        if (!activeMonitorMode.TryLoad(publishedMode)) {
            return ScrollPhaseMonitorResult::ModeContractMismatch;
        }
        const ScrollPhaseMonitorResult modeResult = ValidateLegacyScrollPhasePollMode(publishedMode);
        if (modeResult != ScrollPhaseMonitorResult::Succeeded) {
            return modeResult;
        }
        if (samples.empty()) {
            return ScrollPhaseMonitorResult::Succeeded;
        }

        sample = samples.front();
        samples.pop_front();
        sample.queueOverflowed = queueOverflowed ? 1 : 0;
        queueOverflowed = false;
        hasSample = true;
        return ScrollPhaseMonitorResult::Succeeded;
    }
}

ScrollPhaseMonitorResult PollMinimalScrollPhaseMonitor(int64_t leaseId, ScrollPhaseMinimalPollOutput& output) noexcept {
    @autoreleasepool {
        output = {};
        if (![NSThread isMainThread]) {
            return ScrollPhaseMonitorResult::WrongThread;
        }
        const ScrollPhaseMonitorResult leaseResult = lease.Validate(leaseId);
        if (leaseResult != ScrollPhaseMonitorResult::Succeeded) {
            return leaseResult;
        }
        ScrollPhaseMonitorMode publishedMode = ScrollPhaseMonitorMode::PassThrough;
        if (!activeMonitorMode.TryLoad(publishedMode)) {
            return ScrollPhaseMonitorResult::ModeContractMismatch;
        }
        const ScrollPhaseMonitorResult modeResult = ValidateMinimalScrollPhasePollMode(publishedMode);
        if (modeResult != ScrollPhaseMonitorResult::Succeeded) {
            return modeResult;
        }
        return PollMinimalScrollPhaseQueue(samples, minimalQueueAdmission.Failure(), output);
    }
}

bool HasActiveScrollPhaseMonitorLease() noexcept {
    return lease.HasActiveLease();
}

bool HasActiveScrollPhaseMonitorState() noexcept {
    return monitorPublished.load(std::memory_order_acquire) || lease.HasActiveLease() || activeMonitorMode.IsActive();
}

bool IsScrollPhaseMonitorMainThread() noexcept {
    return [NSThread isMainThread];
}

ScrollPhaseMonitorResult ShutdownScrollPhaseMonitorForPluginUnload() noexcept {
    @autoreleasepool {
        if (!HasActiveScrollPhaseMonitorState()) {
            return ScrollPhaseMonitorResult::Succeeded;
        }
        if (!IsScrollPhaseMonitorMainThread()) {
            return ScrollPhaseMonitorResult::WrongThread;
        }
        return RemoveMonitor(0, true);
    }
}

} // namespace milestro::input

#endif
