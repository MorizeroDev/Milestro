# Included after project() with the Android NDK toolchain, before third-party setup.
# The legacy NDK toolchain deliberately sets CMAKE_SYSTEM_VERSION=1. Prefer
# its resolved platform level; native CMake Android toolchains use the system version.
if (NOT ANDROID)
    message(FATAL_ERROR "MilestroAndroidGraphics requires an Android toolchain")
endif ()

set(MILESTRO_ANDROID_API_LEVEL "${CMAKE_SYSTEM_VERSION}")
if (DEFINED ANDROID_PLATFORM_LEVEL)
    set(MILESTRO_ANDROID_API_LEVEL "${ANDROID_PLATFORM_LEVEL}")
elseif (DEFINED ANDROID_NATIVE_API_LEVEL)
    string(REGEX REPLACE "^android-" "" MILESTRO_ANDROID_API_LEVEL "${ANDROID_NATIVE_API_LEVEL}")
endif ()

if (MILESTRO_ENABLE_ANDROID_VULKAN_RENDER)
    if (NOT MILESTRO_ANDROID_API_LEVEL MATCHES "^[0-9]+$" OR MILESTRO_ANDROID_API_LEVEL LESS 24)
        message(FATAL_ERROR
                "Milestro Android Vulkan requires ANDROID_PLATFORM=android-24 or newer. "
                "For a GLES3-only build on an older API, explicitly set "
                "MILESTRO_ENABLE_ANDROID_VULKAN_RENDER=OFF. Use a fresh build directory when changing API/ABI.")
    endif ()
endif ()

find_library(MILESTRO_ANDROID_GLESV3_LIBRARY GLESv3 REQUIRED)
find_library(MILESTRO_ANDROID_EGL_LIBRARY EGL REQUIRED)
find_library(MILESTRO_ANDROID_LOG_LIBRARY log REQUIRED)
# Skia archives do not propagate GN's system libraries. Higher API builds
# additionally reference AHardwareBuffer (android) and NDK codecs (jnigraphics).
find_library(MILESTRO_ANDROID_NATIVE_LIBRARY android REQUIRED)
find_library(MILESTRO_ANDROID_JNIGRAPHICS_LIBRARY jnigraphics REQUIRED)
set(MILESTRO_ENABLE_UNITY_GL_RENDER ON)
set(MILESTRO_ENABLE_UNITY_VULKAN_RENDER OFF)
list(APPEND PROJECT_PLATFORM_LIBRARIES
        ${MILESTRO_ANDROID_GLESV3_LIBRARY}
        ${MILESTRO_ANDROID_EGL_LIBRARY}
        ${MILESTRO_ANDROID_LOG_LIBRARY}
        ${MILESTRO_ANDROID_NATIVE_LIBRARY}
        ${MILESTRO_ANDROID_JNIGRAPHICS_LIBRARY})

if (MILESTRO_ENABLE_ANDROID_VULKAN_RENDER)
    find_library(MILESTRO_ANDROID_VULKAN_LIBRARY vulkan REQUIRED)
    set(MILESTRO_ENABLE_UNITY_VULKAN_RENDER ON)
    list(APPEND PROJECT_PLATFORM_LIBRARIES ${MILESTRO_ANDROID_VULKAN_LIBRARY})
endif ()
message(STATUS "Milestro Android backends: GLES3=${MILESTRO_ENABLE_UNITY_GL_RENDER}, Vulkan=${MILESTRO_ENABLE_UNITY_VULKAN_RENDER}, API=${MILESTRO_ANDROID_API_LEVEL}")
