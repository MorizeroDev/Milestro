#ifndef MILESTRO_MILESTRO_TIME_H
#define MILESTRO_MILESTRO_TIME_H

#include <Milestro/log/log.h>
#include <chrono>

namespace milestro::util::time {

typedef std::chrono::time_point<std::chrono::high_resolution_clock, std::chrono::duration<double, std::nano>> TimePoint;

inline double GetDuration(TimePoint end, TimePoint start) {
    using namespace std::chrono;
    auto tDuration = end - start;
    auto durationNano = duration_cast<nanoseconds>(tDuration).count();
    return (double) durationNano / 1000000000.0;
}

inline void
StopWatch(std::function<void()> task, const std::string taskName, [[maybe_unused]] const std::string& flags...) {
    auto startTime = std::chrono::high_resolution_clock::now();
    task();
    auto endTime = std::chrono::high_resolution_clock::now();
    MILESTROLOG_DEBUG("{}: {}", taskName, GetDuration(endTime, startTime));
}

inline void StopWatch(std::function<void()> task, const std::string taskName) {
    auto startTime = std::chrono::high_resolution_clock::now();
    task();
    auto endTime = std::chrono::high_resolution_clock::now();
    MILESTROLOG_DEBUG("{}: {}", taskName, GetDuration(endTime, startTime));
}

} // namespace milize::util::time

#endif //MILESTRO_MILESTRO_TIME_H
