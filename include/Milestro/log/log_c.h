#ifndef MILESTRO_LOG_C_H
#define MILESTRO_LOG_C_H

#include <Milestro/common/milestro_export_macros.h>
#include <cstdint>
#include <cstdlib>

extern "C" {
/**
 * @brief 启用Unity控制台日志记录。
 *
 * 此函数激活日志输出到Unity的控制台。启用日志记录后，程序可以将日志消息发送到Unity控制台，以便调试和监控。
 *
 * @return int32_t 若设置成功启用则返回0，否则返回<0表示失败。
 */
MILESTRO_API int32_t MilestroLog_EnableUnityLog();

/**
 * @brief 禁用Unity控制台日志记录。
 *
 * 此函数停用日志输出到Unity的控制台。禁用日志记录后，日志消息将不再发送到Unity控制台。
 *
 * @return int32_t 若设置成功禁用则返回0，否则返回<0表示失败。
 */
MILESTRO_API int32_t MilestroLog_DisableUnityLog();

/**
 * @brief 启用控制台日志记录。
 *
 * 此函数激活日志输出到系统控制台。启用日志记录后，程序可以将日志消息发送到系统控制台，以便调试和监控。
 *
 * @return int32_t 若设置成功启用则返回0，否则返回<0表示失败。
 */
MILESTRO_API int32_t MilestroLog_EnableConsole();

/**
 * @brief 禁用控制台日志记录。
 *
 * 此函数停用日志输出到系统控制台。禁用日志记录后，日志消息将不再发送到系统控制台。
 *
 * @return int32_t 若设置成功禁用则返回0，否则返回<0表示失败。
 */
MILESTRO_API int32_t MilestroLog_DisableConsole();

/**
 * @brief 启用文件日志记录。
 *
 * 此函数激活日志输出到指定文件。启用日志记录后，程序可以将日志消息发送到文件中，用于记录和后期回顾。
 *
 * @return int32_t 若设置成功启用则返回0，否则返回<0表示失败。
 */
MILESTRO_API int32_t MilestroLog_EnableFile();

/**
 * @brief 禁用文件日志记录。
 *
 * 此函数停用日志输出到文件。禁用日志记录后，日志消息将不再发送到指定文件。
 *
 * @return int32_t 若设置成功禁用则返回0，否则返回<0表示失败。
 */
MILESTRO_API int32_t MilestroLog_DisableFile();
}

#endif //MILESTRO_LOG_C_H
