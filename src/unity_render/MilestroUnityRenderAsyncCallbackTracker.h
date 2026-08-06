#pragma once

#include <condition_variable>
#include <cstddef>
#include <mutex>

namespace milestro::unity_render {

class AsyncCallbackTracker {
public:
    bool StartAccepting() {
        std::lock_guard lock(mutex_);
        accepting_ = pending_ == 0;
        return accepting_;
    }

    void StopAccepting() {
        std::lock_guard lock(mutex_);
        accepting_ = false;
    }

    bool TryAcquire() {
        std::lock_guard lock(mutex_);
        if (!accepting_) {
            return false;
        }

        ++pending_;
        return true;
    }

    bool Release() {
        std::lock_guard lock(mutex_);
        if (pending_ == 0) {
            return false;
        }

        --pending_;
        if (pending_ == 0) {
            idle_.notify_all();
        }
        return true;
    }

    void WaitForIdle() {
        std::unique_lock lock(mutex_);
        idle_.wait(lock, [this] { return pending_ == 0; });
    }

    std::size_t PendingCount() const {
        std::lock_guard lock(mutex_);
        return pending_;
    }

private:
    mutable std::mutex mutex_;
    std::condition_variable idle_;
    std::size_t pending_ = 0;
    bool accepting_ = false;
};

} // namespace milestro::unity_render
