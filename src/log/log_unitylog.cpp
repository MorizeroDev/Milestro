#include <Milestro/log/log.h>

#include <IUnityLog.h>

namespace milestro::log {

template<typename Mutex>
void UnityLogSink<Mutex>::init(IUnityLog *log) {
    unityLog = log;
}

template<typename Mutex>
void UnityLogSink<Mutex>::sink_it_(const spdlog::details::log_msg &msg) {
    if (unityLog == nullptr) {
        return;
    }
    spdlog::memory_buf_t formatted;
    spdlog::sinks::base_sink<Mutex>::formatter_->format(msg, formatted);
    std::string message = fmt::to_string(formatted);

    switch (msg.level) {
    case spdlog::level::trace:
    case spdlog::level::debug:
    case spdlog::level::info:UNITY_LOG(unityLog, message.c_str());
        break;
    case spdlog::level::warn:UNITY_LOG_WARNING(unityLog, message.c_str());
        break;
    case spdlog::level::err:
    case spdlog::level::critical:UNITY_LOG_ERROR(unityLog, message.c_str());
        break;
    default:break;
    }
}

template<typename Mutex>
void UnityLogSink<Mutex>::flush_() {
}

} // namespace milestro::log

template class milestro::log::UnityLogSink<std::mutex>;
