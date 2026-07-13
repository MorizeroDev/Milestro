#ifndef MILESTRO_SCROLL_PHASE_MONITOR_MODE_PUBLICATION_H
#define MILESTRO_SCROLL_PHASE_MONITOR_MODE_PUBLICATION_H

#include <Milestro/input/ScrollPhaseMonitor.h>

#include <atomic>
#include <cstdint>

namespace milestro::input {

class ScrollPhaseMonitorModePublication {
public:
    void Publish(ScrollPhaseMonitorMode mode) noexcept {
        mode_.store(static_cast<int32_t>(mode), std::memory_order_release);
    }

    bool TryLoad(ScrollPhaseMonitorMode& mode) const noexcept {
        const int32_t publishedMode = mode_.load(std::memory_order_acquire);
        if (publishedMode == kInactiveMode) {
            return false;
        }
        mode = static_cast<ScrollPhaseMonitorMode>(publishedMode);
        return true;
    }

    bool IsActive() const noexcept {
        return mode_.load(std::memory_order_acquire) != kInactiveMode;
    }

    void FinishCleanup(bool succeeded) noexcept {
        if (succeeded) {
            mode_.store(kInactiveMode, std::memory_order_release);
        }
    }

private:
    static constexpr int32_t kInactiveMode = -1;
    std::atomic<int32_t> mode_{kInactiveMode};
};

} // namespace milestro::input

#endif // MILESTRO_SCROLL_PHASE_MONITOR_MODE_PUBLICATION_H
