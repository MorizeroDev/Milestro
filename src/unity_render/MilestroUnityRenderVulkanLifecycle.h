#ifndef MILESTRO_UNITY_RENDER_VULKAN_LIFECYCLE_H
#define MILESTRO_UNITY_RENDER_VULKAN_LIFECYCLE_H

#include <array>
#include <atomic>
#include <cstddef>
#include <cstdint>
#include <limits>
#include <utility>

namespace milestro::unity_render::vulkan {

constexpr std::size_t kSubmissionCapacity = 32;
constexpr std::size_t kEpochCapacity = 32;
constexpr int32_t kMaxCommandsPerSubmission = 256;
constexpr std::size_t kOutstandingEventCapacity = kSubmissionCapacity * 2;
constexpr int kEventIdsPerEpoch = 2;
constexpr int kReservedEventIdCount = static_cast<int>(kEpochCapacity) * kEventIdsPerEpoch;

constexpr bool IsValidCommandCount(int32_t commandCount) {
    return commandCount >= 0 && commandCount <= kMaxCommandsPerSubmission;
}

enum class SubmissionPhase : uint8_t {
    Enqueued,
    Prepared,
};

enum class EnqueueResult : uint8_t {
    Accepted,
    InactiveEpoch,
    QueueFull,
    EventBudgetExhausted,
    SerialExhausted,
};

struct EventInfo {
    int prepareEventId = -1;
    int submitEventId = -1;
    uint64_t epoch = 0;
};

struct EventConsumption {
    bool recognized = false;
    bool hadOutstandingEvent = false;
    bool active = false;
    uint64_t epoch = 0;
};

template <std::size_t EpochCapacity = kEpochCapacity>
class EpochCancellationMailbox {
public:
    static_assert(EpochCapacity > 0 && EpochCapacity <= 64);

    bool Publish(uint64_t epoch) {
        if (epoch == 0 || epoch > EpochCapacity) {
            return false;
        }

        pendingEpochs_.fetch_or(uint64_t{1} << (epoch - 1), std::memory_order_release);
        return true;
    }

    uint64_t TakePendingMask() {
        return pendingEpochs_.exchange(0, std::memory_order_acq_rel);
    }

    std::size_t PendingCount() const {
        uint64_t mask = pendingEpochs_.load(std::memory_order_acquire);
        std::size_t count = 0;
        while (mask != 0) {
            count += static_cast<std::size_t>(mask & 1U);
            mask >>= 1U;
        }
        return count;
    }

private:
    std::atomic<uint64_t> pendingEpochs_{0};
};

enum class DeviceTransitionKind : uint8_t {
    None,
    Shutdown,
    Initialize,
};

struct DeviceTransitionSnapshot {
    uint64_t sequence = 0;
    uint64_t intent = 0;
    DeviceTransitionKind kind = DeviceTransitionKind::None;
    int32_t renderer = 0;
    int firstRenderEventId = -1;
};

// Single-writer mailbox used by Unity device events. The caller must serialize
// Begin/Publish pairs; the dispatcher does this with its render-system mutex.
// Readers may run on the managed or render thread and only accept a stable,
// even sequence snapshot.
class DeviceTransitionMailbox {
public:
    uint64_t PublishShutdown(uint64_t intent) {
        BeginWrite();
        intent_.store(intent, std::memory_order_relaxed);
        renderer_.store(0, std::memory_order_relaxed);
        firstRenderEventId_.store(-1, std::memory_order_relaxed);
        kind_.store(DeviceTransitionKind::Shutdown, std::memory_order_relaxed);
        return EndWrite();
    }

    uint64_t PublishInitialize(uint64_t intent,
                               int32_t renderer,
                               int firstRenderEventId) {
        BeginWrite();
        intent_.store(intent, std::memory_order_relaxed);
        renderer_.store(renderer, std::memory_order_relaxed);
        firstRenderEventId_.store(firstRenderEventId, std::memory_order_relaxed);
        kind_.store(DeviceTransitionKind::Initialize, std::memory_order_relaxed);
        return EndWrite();
    }

    bool TrySnapshot(uint64_t appliedSequence, DeviceTransitionSnapshot& snapshot) const {
        snapshot = {};
        const uint64_t before = sequence_.load(std::memory_order_acquire);
        if (before == appliedSequence || (before & 1U) != 0) {
            return false;
        }

        DeviceTransitionSnapshot candidate;
        candidate.sequence = before;
        candidate.intent = intent_.load(std::memory_order_relaxed);
        candidate.kind = kind_.load(std::memory_order_relaxed);
        candidate.renderer = renderer_.load(std::memory_order_relaxed);
        candidate.firstRenderEventId = firstRenderEventId_.load(std::memory_order_relaxed);

        const uint64_t after = sequence_.load(std::memory_order_acquire);
        if (before != after || (after & 1U) != 0) {
            return false;
        }

        snapshot = candidate;
        return true;
    }

    uint64_t PublishedSequence() const {
        return sequence_.load(std::memory_order_acquire);
    }

private:
    void BeginWrite() {
        sequence_.fetch_add(1, std::memory_order_acq_rel);
    }

    uint64_t EndWrite() {
        return sequence_.fetch_add(1, std::memory_order_release) + 1;
    }

    std::atomic<uint64_t> sequence_{0};
    std::atomic<uint64_t> intent_{0};
    std::atomic<DeviceTransitionKind> kind_{DeviceTransitionKind::None};
    std::atomic<int32_t> renderer_{0};
    std::atomic<int> firstRenderEventId_{-1};
};

class DeviceTransitionState {
public:
    uint64_t BeginTransition() {
        return latestIntent_.fetch_add(1, std::memory_order_acq_rel) + 1;
    }

    uint64_t PublishShutdown(uint64_t intent) {
        return mailbox_.PublishShutdown(intent);
    }

    uint64_t PublishInitialize(uint64_t intent,
                               int32_t renderer,
                               int firstRenderEventId) {
        return mailbox_.PublishInitialize(intent, renderer, firstRenderEventId);
    }

    template <typename Apply>
    void ApplyLocked(Apply&& apply) {
        for (;;) {
            DeviceTransitionSnapshot transition;
            const uint64_t applied = appliedSequence_.load(std::memory_order_relaxed);
            if (!mailbox_.TrySnapshot(applied, transition)) {
                return;
            }

            apply(transition);
            appliedSequence_.store(transition.sequence, std::memory_order_release);
        }
    }

    bool HasStablePendingTransition() const {
        const uint64_t published = mailbox_.PublishedSequence();
        const uint64_t applied = appliedSequence_.load(std::memory_order_acquire);
        return published != applied && (published & 1U) == 0;
    }

    uint64_t AppliedSequence() const {
        return appliedSequence_.load(std::memory_order_acquire);
    }

    bool IsCurrentIntent(uint64_t intent) const {
        return intent != 0 && latestIntent_.load(std::memory_order_acquire) == intent;
    }

private:
    DeviceTransitionMailbox mailbox_;
    std::atomic<uint64_t> appliedSequence_{0};
    std::atomic<uint64_t> latestIntent_{0};
};

template <typename Payload,
          std::size_t SubmissionCapacity = kSubmissionCapacity,
          std::size_t EpochCapacity = kEpochCapacity>
class FixedEventLifecycle {
public:
    static constexpr std::size_t kNotFound = std::numeric_limits<std::size_t>::max();

    struct Record {
        uint64_t epoch = 0;
        uint64_t serial = 0;
        SubmissionPhase phase = SubmissionPhase::Enqueued;
        Payload payload{};
    };

    bool StartEpoch(int prepareEventId, int submitEventId, EventInfo& eventInfo) {
        eventInfo = {};
        if (disabled_ || activeEpoch_ != 0 || epochCount_ == EpochCapacity ||
            nextEpoch_ == std::numeric_limits<uint64_t>::max() ||
            prepareEventId < 0 || submitEventId < 0 || prepareEventId == submitEventId ||
            EventIdWasUsed(prepareEventId) || EventIdWasUsed(submitEventId)) {
            if (epochCount_ == EpochCapacity || nextEpoch_ == std::numeric_limits<uint64_t>::max()) {
                Disable();
            }
            return false;
        }

        EpochSlot& slot = epochs_[epochCount_++];
        slot.epoch = ++nextEpoch_;
        slot.prepareEventId = prepareEventId;
        slot.submitEventId = submitEventId;
        slot.active = true;
        activeEpoch_ = slot.epoch;
        eventInfo = EventInfo{prepareEventId, submitEventId, slot.epoch};
        return true;
    }

    uint64_t DetachActiveEpoch() {
        const uint64_t detached = activeEpoch_;
        EpochSlot* slot = FindEpoch(detached);
        if (slot != nullptr) {
            slot->active = false;
        }
        activeEpoch_ = 0;
        return detached;
    }

    bool RollbackActiveEpoch(uint64_t epoch) {
        if (activeEpoch_ != epoch || epochCount_ == 0 || recordCount_ != 0) {
            return false;
        }

        EpochSlot& slot = epochs_[epochCount_ - 1];
        if (slot.epoch != epoch || slot.prepareOutstanding != 0 ||
            slot.submitOutstanding != 0 || nextEpoch_ != epoch) {
            return false;
        }

        slot = {};
        --epochCount_;
        --nextEpoch_;
        activeEpoch_ = 0;
        return true;
    }

    EnqueueResult TryEnqueue(uint64_t epoch, Payload payload, uint64_t& serial) {
        serial = 0;
        EpochSlot* slot = FindEpoch(epoch);
        if (disabled_ || slot == nullptr || !slot->active || activeEpoch_ != epoch) {
            return EnqueueResult::InactiveEpoch;
        }
        if (recordCount_ == SubmissionCapacity) {
            return EnqueueResult::QueueFull;
        }
        if (outstandingEvents_ + 2 > kOutstandingEventCapacity) {
            Disable();
            return EnqueueResult::EventBudgetExhausted;
        }
        if (nextSerial_ == std::numeric_limits<uint64_t>::max()) {
            Disable();
            return EnqueueResult::SerialExhausted;
        }

        serial = ++nextSerial_;
        records_[recordCount_++] = Record{epoch, serial, SubmissionPhase::Enqueued, std::move(payload)};
        ++slot->prepareOutstanding;
        ++slot->submitOutstanding;
        outstandingEvents_ += 2;
        return EnqueueResult::Accepted;
    }

    bool Retire(uint64_t epoch, uint64_t serial, Payload& payload) {
        const std::size_t index = Find(epoch, serial);
        if (index == kNotFound) {
            return false;
        }

        // Unity may have accepted either fixed event before the managed submit
        // path reported failure. Keep both remaining callback credits as
        // tombstones; the caller must detach this epoch before accepting more
        // work under a new, never-reused event-id pair.
        payload = TakeAt(index);
        return true;
    }

    EventConsumption ConsumePrepareEvent(int eventId) {
        return ConsumeEvent(eventId, true);
    }

    EventConsumption ConsumeSubmitEvent(int eventId) {
        return ConsumeEvent(eventId, false);
    }

    std::size_t FindFirst(uint64_t epoch, SubmissionPhase phase) const {
        for (std::size_t i = 0; i < recordCount_; ++i) {
            if (records_[i].epoch == epoch && records_[i].phase == phase) {
                return i;
            }
        }
        return kNotFound;
    }

    std::size_t Find(uint64_t epoch, uint64_t serial) const {
        for (std::size_t i = 0; i < recordCount_; ++i) {
            if (records_[i].epoch == epoch && records_[i].serial == serial) {
                return i;
            }
        }
        return kNotFound;
    }

    Record& At(std::size_t index) {
        return records_[index];
    }

    const Record& At(std::size_t index) const {
        return records_[index];
    }

    void MarkPrepared(std::size_t index) {
        if (index < recordCount_) {
            records_[index].phase = SubmissionPhase::Prepared;
        }
    }

    Payload TakeAt(std::size_t index) {
        if (index >= recordCount_) {
            return {};
        }

        Payload payload = std::move(records_[index].payload);
        for (std::size_t i = index + 1; i < recordCount_; ++i) {
            records_[i - 1] = std::move(records_[i]);
        }
        --recordCount_;
        records_[recordCount_] = Record{};
        return payload;
    }

    bool GetActiveEventInfo(EventInfo& eventInfo) const {
        const EpochSlot* slot = FindEpoch(activeEpoch_);
        if (disabled_ || slot == nullptr || !slot->active) {
            eventInfo = {};
            return false;
        }

        eventInfo = EventInfo{slot->prepareEventId, slot->submitEventId, slot->epoch};
        return true;
    }

    void Disable() {
        disabled_ = true;
        EpochSlot* slot = FindEpoch(activeEpoch_);
        if (slot != nullptr) {
            slot->active = false;
        }
        activeEpoch_ = 0;
    }

    bool Disabled() const {
        return disabled_;
    }

    uint64_t ActiveEpoch() const {
        return activeEpoch_;
    }

    std::size_t QueueSize() const {
        return recordCount_;
    }

    std::size_t EpochCount() const {
        return epochCount_;
    }

    std::size_t OutstandingEvents() const {
        return outstandingEvents_;
    }

private:
    struct EpochSlot {
        uint64_t epoch = 0;
        int prepareEventId = -1;
        int submitEventId = -1;
        std::size_t prepareOutstanding = 0;
        std::size_t submitOutstanding = 0;
        bool active = false;
    };

    EventConsumption ConsumeEvent(int eventId, bool prepare) {
        EpochSlot* slot = FindEpochByEvent(eventId, prepare);
        if (slot == nullptr) {
            return {};
        }

        EventConsumption result;
        result.recognized = true;
        result.epoch = slot->epoch;
        std::size_t& phaseOutstanding = prepare ? slot->prepareOutstanding : slot->submitOutstanding;
        if (phaseOutstanding == 0) {
            return result;
        }

        --phaseOutstanding;
        --outstandingEvents_;
        result.hadOutstandingEvent = true;
        result.active = !disabled_ && slot->active && activeEpoch_ == slot->epoch;
        return result;
    }

    bool EventIdWasUsed(int eventId) const {
        for (std::size_t i = 0; i < epochCount_; ++i) {
            if (epochs_[i].prepareEventId == eventId || epochs_[i].submitEventId == eventId) {
                return true;
            }
        }
        return false;
    }

    EpochSlot* FindEpoch(uint64_t epoch) {
        if (epoch == 0) {
            return nullptr;
        }
        for (std::size_t i = 0; i < epochCount_; ++i) {
            if (epochs_[i].epoch == epoch) {
                return &epochs_[i];
            }
        }
        return nullptr;
    }

    const EpochSlot* FindEpoch(uint64_t epoch) const {
        if (epoch == 0) {
            return nullptr;
        }
        for (std::size_t i = 0; i < epochCount_; ++i) {
            if (epochs_[i].epoch == epoch) {
                return &epochs_[i];
            }
        }
        return nullptr;
    }

    EpochSlot* FindEpochByEvent(int eventId, bool prepare) {
        for (std::size_t i = 0; i < epochCount_; ++i) {
            const int candidate = prepare ? epochs_[i].prepareEventId : epochs_[i].submitEventId;
            if (candidate == eventId) {
                return &epochs_[i];
            }
        }
        return nullptr;
    }

    std::array<Record, SubmissionCapacity> records_{};
    std::array<EpochSlot, EpochCapacity> epochs_{};
    std::size_t recordCount_ = 0;
    std::size_t epochCount_ = 0;
    std::size_t outstandingEvents_ = 0;
    uint64_t nextEpoch_ = 0;
    uint64_t nextSerial_ = 0;
    uint64_t activeEpoch_ = 0;
    bool disabled_ = false;
};

} // namespace milestro::unity_render::vulkan

#endif // MILESTRO_UNITY_RENDER_VULKAN_LIFECYCLE_H
