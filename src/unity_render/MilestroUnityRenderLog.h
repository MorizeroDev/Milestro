#ifndef MILESTRO_UNITY_RENDER_LOG_H
#define MILESTRO_UNITY_RENDER_LOG_H

#include <Milestro/log/log.h>

#if defined(MILESTRO_RENDER_DEBUG_LOG)
#define MILESTRO_RENDER_LOG_TRACE(...) MILESTROLOG_TRACE(__VA_ARGS__)
#define MILESTRO_RENDER_LOG_DEBUG(...) MILESTROLOG_DEBUG(__VA_ARGS__)
#define MILESTRO_RENDER_LOG_INFO(...) MILESTROLOG_INFO(__VA_ARGS__)
#define MILESTRO_RENDER_LOG_WARN(...) MILESTROLOG_WARN(__VA_ARGS__)
#else
#define MILESTRO_RENDER_LOG_TRACE(...) do { if (false) { MILESTROLOG_TRACE(__VA_ARGS__); } } while (false)
#define MILESTRO_RENDER_LOG_DEBUG(...) do { if (false) { MILESTROLOG_DEBUG(__VA_ARGS__); } } while (false)
#define MILESTRO_RENDER_LOG_INFO(...) do { if (false) { MILESTROLOG_INFO(__VA_ARGS__); } } while (false)
#define MILESTRO_RENDER_LOG_WARN(...) do { if (false) { MILESTROLOG_WARN(__VA_ARGS__); } } while (false)
#endif

#endif // MILESTRO_UNITY_RENDER_LOG_H
