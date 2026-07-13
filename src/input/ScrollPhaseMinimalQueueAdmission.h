#ifndef MILESTRO_SCROLL_PHASE_MINIMAL_QUEUE_ADMISSION_H
#define MILESTRO_SCROLL_PHASE_MINIMAL_QUEUE_ADMISSION_H

#include <cstddef>
#include <cstdint>
#include <limits>

namespace milestro::input {

enum class ScrollPhaseMinimalQueueFailure : int32_t {
    None = 0,
    CapacityExceeded = 1,
    SequenceExhausted = 2,
};

class ScrollPhaseMinimalQueueAdmission {
public:
    static constexpr size_t MaximumQueuedSamples = 256;

    explicit ScrollPhaseMinimalQueueAdmission(int64_t firstSequence = 1) noexcept : nextSequence_(firstSequence) {
        if (firstSequence <= 0) {
            failure_ = ScrollPhaseMinimalQueueFailure::SequenceExhausted;
        }
    }

    bool TryAccept(size_t queuedSamples, int64_t& sequence) noexcept {
        sequence = 0;
        if (failure_ != ScrollPhaseMinimalQueueFailure::None) {
            return false;
        }
        if (queuedSamples >= MaximumQueuedSamples) {
            failure_ = ScrollPhaseMinimalQueueFailure::CapacityExceeded;
            return false;
        }
        if (nextSequence_ == std::numeric_limits<int64_t>::max()) {
            failure_ = ScrollPhaseMinimalQueueFailure::SequenceExhausted;
            return false;
        }

        sequence = nextSequence_++;
        return true;
    }

    void Reset() noexcept {
        nextSequence_ = 1;
        failure_ = ScrollPhaseMinimalQueueFailure::None;
    }

    ScrollPhaseMinimalQueueFailure Failure() const noexcept {
        return failure_;
    }

    int64_t NextSequence() const noexcept {
        return nextSequence_;
    }

private:
    int64_t nextSequence_;
    ScrollPhaseMinimalQueueFailure failure_ = ScrollPhaseMinimalQueueFailure::None;
};

} // namespace milestro::input

#endif // MILESTRO_SCROLL_PHASE_MINIMAL_QUEUE_ADMISSION_H
