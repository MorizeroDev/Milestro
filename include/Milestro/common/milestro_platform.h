#ifndef MILESTRO_PLATFORM_H
#define MILESTRO_PLATFORM_H


#if __APPLE__

#include <TargetConditionals.h>

#if TARGET_OS_IPHONE

#define MILESTRO_PLATFORM_IOS 1

#elif TARGET_OS_MAC

#define MILESTRO_PLATFORM_MAC 1

#else

#error Not Supported Platform
#endif

#elif defined(WIN32) || defined(_WIN32) || defined(__WIN32__) || defined(__NT__)

#define MILESTRO_PLATFORM_WINDOWS 1

#elif __ANDROID__

#define MILESTRO_PLATFORM_ANDROID 1

#else

#error Not Supported Platform

#endif


#endif //MILESTRO_MILESTRO_PLATFORM_H