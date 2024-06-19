#ifndef MILESTRO_LOG_H
#define MILESTRO_LOG_H

// 使用该头文件则必须依赖于spdlog

#include <algorithm>
#include <memory>
#include <spdlog/sinks/base_sink.h>
#include <spdlog/sinks/basic_file_sink.h>
#include <spdlog/sinks/stdout_color_sinks.h>
#include <spdlog/spdlog.h>

#include <Milestro/common/milestro_export_macros.h>

// 直接用以下宏

#define MILESTROLOG_TRACE(...) milestro::log::MilestroLogger::getInstance().getLogger()->trace(__VA_ARGS__)

#define MILESTROLOG_DEBUG(...) milestro::log::MilestroLogger::getInstance().getLogger()->debug(__VA_ARGS__)

#define MILESTROLOG_INFO(...) milestro::log::MilestroLogger::getInstance().getLogger()->info(__VA_ARGS__)

#define MILESTROLOG_WARN(...) milestro::log::MilestroLogger::getInstance().getLogger()->warn(__VA_ARGS__)

#define MILESTROLOG_ERROR(...) milestro::log::MilestroLogger::getInstance().getLogger()->error(__VA_ARGS__)

#define MILESTROLOG_CRITICAL(...) milestro::log::MilestroLogger::getInstance().getLogger()->critical(__VA_ARGS__)

// 避免直接包含unity头文件
typedef struct IUnityInterface IUnityInterface;
typedef struct IUnityLog IUnityLog;

namespace milestro::log {

// 自定义UnityLogSink
template <typename Mutex>
class UnityLogSink : public spdlog::sinks::base_sink<Mutex> {
public:
    void init(IUnityLog* log);

protected:
    void sink_it_(const spdlog::details::log_msg& msg) override;
    void flush_() override;

private:
    IUnityLog* unityLog{};
};

using UnityLogSink_mt = UnityLogSink<std::mutex>;

// 管理sink和logger，支持控制台，文件，unity
class MILESTRO_API MilestroLogger {
public:
    static MilestroLogger& getInstance();

    static void init();
    static void initWithUnity(IUnityLog* log);

    void addConsoleSink();
    void addFileSink(const std::string& filename);
    void addUnityLogSink(IUnityLog* log);

    std::shared_ptr<spdlog::sinks::stdout_color_sink_mt> getConsoleSink() const;
    std::shared_ptr<spdlog::sinks::basic_file_sink_mt> getFileSink() const;
    std::shared_ptr<UnityLogSink_mt> getUnityLogSink() const;

    static void enableSink(const std::shared_ptr<spdlog::sinks::sink>& sink);
    static void disableSink(const std::shared_ptr<spdlog::sinks::sink>& sink);

    spdlog::logger* getLogger() const;
    void setLevel(spdlog::level::level_enum level);

    MilestroLogger(const MilestroLogger&) = delete;
    MilestroLogger& operator=(const MilestroLogger&) = delete;

private:
    MilestroLogger();
    ~MilestroLogger() = default;

    std::shared_ptr<spdlog::logger> logger;
    std::shared_ptr<spdlog::sinks::stdout_color_sink_mt> consoleSink;
    std::shared_ptr<spdlog::sinks::basic_file_sink_mt> fileSink;
    std::shared_ptr<UnityLogSink_mt> unitySink;
};

} // namespace milestro::log

#endif //MILESTRO_LOG_H
