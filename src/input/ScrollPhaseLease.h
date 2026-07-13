#ifndef MILESTRO_SCROLL_PHASE_LEASE_H
#define MILESTRO_SCROLL_PHASE_LEASE_H

#include <Milestro/input/ScrollPhaseMonitor.h>

#include <atomic>

namespace milestro::input {

class ScrollPhaseLease {
public:
    ScrollPhaseMonitorResult Acquire(int64_t& leaseId);
    ScrollPhaseMonitorResult Validate(int64_t leaseId) const;
    ScrollPhaseMonitorResult Release(int64_t leaseId);
    void ForceRelease();
    bool HasActiveLease() const;

private:
    std::atomic<int64_t> nextLeaseId_{1};
    std::atomic<int64_t> activeLeaseId_{0};
};

} // namespace milestro::input

#endif // MILESTRO_SCROLL_PHASE_LEASE_H
