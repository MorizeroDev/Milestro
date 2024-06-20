#include <Milestro/log/log.h>
#include <Milestro/log/log_c.h>
#include <spdlog/spdlog.h>

#include <IUnityLog.h>

// 在加载dll时会默认初始化
[[maybe_unused]] static int MilestroLoggerInitHelper = ([] {
    milestro::log::MilestroLogger::init();
}(),
    0);

namespace milestro::log {
void MilestroLogger::init() {
    MilestroLogger::getInstance().addConsoleSink();
    // 注意，不支持中文路径
    //    MilestroLogger::getInstance().addFileSink("./milestrolog.txt");
}

void MilestroLogger::initWithUnity(IUnityLog *log) {
    MilestroLogger::getInstance().addConsoleSink();
    //    MilestroLogger::getInstance().addFileSink("./milestrolog.txt");
    MilestroLogger::getInstance().addUnityLogSink(log);
}

MilestroLogger::MilestroLogger() {
    logger = std::make_shared<spdlog::logger>("Milestro");
    logger->set_level(spdlog::level::debug);
}

MilestroLogger &MilestroLogger::getInstance() {
    static MilestroLogger instance;
    return instance;
}

void MilestroLogger::addConsoleSink() {
    if (!consoleSink) {
        consoleSink = std::make_shared<spdlog::sinks::stdout_color_sink_mt>();
        consoleSink->set_level(spdlog::level::debug);
        logger->sinks().push_back(consoleSink);
    }
}

void MilestroLogger::addFileSink(const std::string &filename) {
    if (!fileSink) {
        fileSink = std::make_shared<spdlog::sinks::basic_file_sink_mt>(filename, true);
        fileSink->set_level(spdlog::level::trace);
        logger->sinks().push_back(fileSink);
    }
}

void MilestroLogger::addUnityLogSink(IUnityLog *log) {
    if (!unitySink) {
        unitySink = std::make_shared<UnityLogSink_mt>();
        unitySink->init(log);
        unitySink->set_level(spdlog::level::trace);
        logger->sinks().push_back(unitySink);
    }
}

std::shared_ptr<spdlog::sinks::stdout_color_sink_mt> MilestroLogger::getConsoleSink() const {
    return consoleSink;
}

std::shared_ptr<spdlog::sinks::basic_file_sink_mt> MilestroLogger::getFileSink() const {
    return fileSink;
}

std::shared_ptr<UnityLogSink_mt> MilestroLogger::getUnityLogSink() const {
    return unitySink;
}

void MilestroLogger::setLevel(spdlog::level::level_enum level) {
    logger->set_level(level);
}

void MilestroLogger::enableSink(const std::shared_ptr<spdlog::sinks::sink> &sink) {
    sink->set_level(spdlog::level::trace);
}

void MilestroLogger::disableSink(const std::shared_ptr<spdlog::sinks::sink> &sink) {
    sink->set_level(spdlog::level::off);
}

spdlog::logger *MilestroLogger::getLogger() const {
    return logger.get();
}
} // namespace milestro::log

extern "C" {

MILESTRO_API int32_t MilestroLog_EnableUnityLog() {
    auto sink = milestro::log::MilestroLogger::getInstance().getUnityLogSink();
    milestro::log::MilestroLogger::enableSink(sink);
    return 0;
}

MILESTRO_API int32_t MilestroLog_DisableUnityLog() {
    auto sink = milestro::log::MilestroLogger::getInstance().getUnityLogSink();
    milestro::log::MilestroLogger::disableSink(sink);
    return 0;
}

MILESTRO_API int32_t MilestroLog_EnableConsole() {
    auto sink = milestro::log::MilestroLogger::getInstance().getConsoleSink();
    milestro::log::MilestroLogger::enableSink(sink);
    return 0;
}

MILESTRO_API int32_t MilestroLog_DisableConsole() {
    auto sink = milestro::log::MilestroLogger::getInstance().getConsoleSink();
    milestro::log::MilestroLogger::disableSink(sink);
    return 0;
}

MILESTRO_API int32_t MilestroLog_EnableFile() {
    auto sink = milestro::log::MilestroLogger::getInstance().getFileSink();
    milestro::log::MilestroLogger::enableSink(sink);
    return 0;
}

MILESTRO_API int32_t MilestroLog_DisableFile() {
    auto sink = milestro::log::MilestroLogger::getInstance().getFileSink();
    milestro::log::MilestroLogger::disableSink(sink);
    return 0;
}
}
