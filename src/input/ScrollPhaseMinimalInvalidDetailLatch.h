#ifndef MILESTRO_SCROLL_PHASE_MINIMAL_INVALID_DETAIL_LATCH_H
#define MILESTRO_SCROLL_PHASE_MINIMAL_INVALID_DETAIL_LATCH_H

#include <Milestro/input/ScrollPhaseMonitor.h>

namespace milestro::input {

class ScrollPhaseMinimalInvalidDetailLatch {
public:
    bool TryLatch(const ScrollPhaseMinimalInvalidDetail& detail) noexcept {
        if (hasDetail_) {
            return false;
        }
        detail_ = detail;
        hasDetail_ = true;
        return true;
    }

    ScrollPhaseMinimalInvalidDetailOutput Read() const noexcept {
        return {hasDetail_, detail_};
    }

    void Reset() noexcept {
        hasDetail_ = false;
        detail_ = {};
    }

    void FinishCleanup(bool cleanupSucceeded) noexcept {
        if (cleanupSucceeded) {
            Reset();
        }
    }

private:
    bool hasDetail_ = false;
    ScrollPhaseMinimalInvalidDetail detail_;
};

} // namespace milestro::input

#endif // MILESTRO_SCROLL_PHASE_MINIMAL_INVALID_DETAIL_LATCH_H
