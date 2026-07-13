#include "ScrollPhaseLease.h"

namespace milestro::input {

ScrollPhaseMonitorResult ScrollPhaseLease::Acquire(int64_t& leaseId) {
    leaseId = 0;
    if (HasActiveLease()) {
        return ScrollPhaseMonitorResult::AlreadyStarted;
    }

    int64_t candidate = nextLeaseId_.fetch_add(1, std::memory_order_relaxed);
    if (candidate == 0) {
        candidate = nextLeaseId_.fetch_add(1, std::memory_order_relaxed);
    }
    int64_t expected = 0;
    if (!activeLeaseId_.compare_exchange_strong(expected,
                                                candidate,
                                                std::memory_order_acq_rel,
                                                std::memory_order_acquire)) {
        return ScrollPhaseMonitorResult::AlreadyStarted;
    }

    leaseId = candidate;
    return ScrollPhaseMonitorResult::Succeeded;
}

ScrollPhaseMonitorResult ScrollPhaseLease::Validate(int64_t leaseId) const {
    return leaseId != 0 && activeLeaseId_.load(std::memory_order_acquire) == leaseId
                   ? ScrollPhaseMonitorResult::Succeeded
                   : ScrollPhaseMonitorResult::InvalidLease;
}

ScrollPhaseMonitorResult ScrollPhaseLease::Release(int64_t leaseId) {
    int64_t expected = leaseId;
    if (leaseId == 0 ||
        !activeLeaseId_.compare_exchange_strong(expected, 0, std::memory_order_acq_rel, std::memory_order_acquire)) {
        return ScrollPhaseMonitorResult::InvalidLease;
    }
    return ScrollPhaseMonitorResult::Succeeded;
}

void ScrollPhaseLease::ForceRelease() {
    activeLeaseId_.store(0, std::memory_order_release);
}

bool ScrollPhaseLease::HasActiveLease() const {
    return activeLeaseId_.load(std::memory_order_acquire) != 0;
}

} // namespace milestro::input
