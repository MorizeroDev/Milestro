cmake_minimum_required(VERSION 3.28)
project(Milestro)

option(MILESTRO_BUILD_SHARED_LIBS "Build shared library" ON)
option(MILESTRO_ENABLE_CLI "Enable cli" ON)
option(MILESTRO_ENABLE_TESTS "Enable tests" ON)
option(MILESTRO_WITH_ADDRESS_SANITIZER "Build with clang address sanitizer" OFF)
option(MILESTRO_SKIA_LIB_PATH "Skia library path")
option(MILESTRO_SKIA_INCLUDE_PATH "Skia include path")

set(CMAKE_CXX_STANDARD 20)

# Need to explicitly disable scanning
# Reference: https://www.kitware.com/import-cmake-the-experiment-is-over/
set(CMAKE_CXX_SCAN_FOR_MODULES OFF)

#cmake_policy(SET CMP0063 NEW)
set(CMAKE_POLICY_DEFAULT_CMP0063 NEW)
set(CMAKE_POLICY_DEFAULT_CMP0077 NEW)
set(CMAKE_C_VISIBILITY_PRESET hidden)
set(CMAKE_CXX_VISIBILITY_PRESET hidden)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

if (MSVC)
    add_compile_options("$<$<C_COMPILER_ID:MSVC>:/utf-8>")
    add_compile_options("$<$<CXX_COMPILER_ID:MSVC>:/utf-8>")
else ()
    set(CMAKE_CXX_FLAGS_DEBUG "")
    set(CMAKE_CXX_FLAGS_RELEASE "-O3")
endif ()

# Keep this option OFF
# The third-party libraries might decide to build dynamic libraries based on this option,
# but we want to only generate static libraries in all cases.
set(BUILD_SHARED_LIBS OFF)

# Because the current Windows build uniformly uses MSYS2 Clang, it is necessary to use statically link for all targets.
# e.g. googletest
if (WIN32)
    if (MINGW)
        set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -static")
    endif ()
endif ()

if (MILESTRO_WITH_ADDRESS_SANITIZER)
    set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -fsanitize=address -fno-omit-frame-pointer -fno-optimize-sibling-calls -fsanitize-recover=address")
    set(CMAKE_SHARED_LINKER_FLAGS "${CMAKE_SHARED_LINKER_FLAGS} -fno-omit-frame-pointer -fsanitize=address -fsanitize-recover=address")
endif ()

if (BUILD_SHARED_LIBS)
    message(FATAL_ERROR "Please do not set BUILD_SHARED_LIBS")
endif ()

if (MILESTRO_BUILD_SHARED_LIBS)
    add_compile_definitions(MILESTRO_DLL MILESTRO_BUILDING_SHARED_LIBS)
    set(PROJECT_LIBRARY_PREFIX "${CMAKE_SHARED_LIBRARY_PREFIX}")
    set(PROJECT_LIBRARY_SUFFIX "${CMAKE_SHARED_LIBRARY_SUFFIX}")
else ()
    set(PROJECT_LIBRARY_PREFIX "${CMAKE_STATIC_LIBRARY_PREFIX}")
    set(PROJECT_LIBRARY_SUFFIX "${CMAKE_STATIC_LIBRARY_SUFFIX}")
endif ()


#############################################################
# Setup project
set(CMAKE_INSTALL_RPATH "$ORIGIN/../lib:$ORIGIN/../lib/release:$ORIGIN/../lib/debug")
set(CMAKE_SKIP_BUILD_RPATH FALSE)
set(CMAKE_BUILD_WITH_INSTALL_RPATH TRUE)

# Read the version information from the VERSION file
file(STRINGS "${PROJECT_SOURCE_DIR}/VERSION" PACKAGE_VERSION)
string(REGEX REPLACE "([0-9]+)\\.[0-9]+\\.[0-9]+.*" "\\1" CPACK_PACKAGE_VERSION_MAJOR ${PACKAGE_VERSION})
string(REGEX REPLACE "[0-9]+\\.([0-9]+)\\.[0-9]+.*" "\\1" CPACK_PACKAGE_VERSION_MINOR ${PACKAGE_VERSION})
string(REGEX REPLACE "[0-9]+\\.[0-9]+\\.([0-9]+).*" "\\1" CPACK_PACKAGE_VERSION_PATCH ${PACKAGE_VERSION})

set(PROJECT_VERSION ${CPACK_PACKAGE_VERSION_MAJOR}.${CPACK_PACKAGE_VERSION_MINOR}.${CPACK_PACKAGE_VERSION_PATCH})
set(PROJECT_MODULE_EXPORT_NAMESPACE "${PROJECT_NAME}::")
add_compile_definitions(MILESTRO_VERSION="${PROJECT_VERSION}")
add_compile_definitions(MILESTRO_VERSION_MAJOR=${CPACK_PACKAGE_VERSION_MAJOR})
add_compile_definitions(MILESTRO_VERSION_MINOR=${CPACK_PACKAGE_VERSION_MINOR})
add_compile_definitions(MILESTRO_VERSION_PATCH=${CPACK_PACKAGE_VERSION_PATCH})

# Put the libaries and binaries that get built into directories at the
# top of the build tree rather than in hard-to-find leaf
# directories. This simplifies manual testing and the use of the build
# tree rather than installed Boost libraries.
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib)
set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib)
# Windows DLLs are "runtime" for CMake. Output them to "bin" like the Visual Studio projects do.
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin)

# Reset output dirs for multi-config builds
foreach (OUTPUTCONFIG ${CMAKE_CONFIGURATION_TYPES})
    string(TOUPPER ${OUTPUTCONFIG} OUTPUTCONFIG)
    set(CMAKE_RUNTIME_OUTPUT_DIRECTORY_${OUTPUTCONFIG} ${CMAKE_BINARY_DIR}/bin)
    set(CMAKE_LIBRARY_OUTPUT_DIRECTORY_${OUTPUTCONFIG} ${CMAKE_BINARY_DIR}/lib)
    set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY_${OUTPUTCONFIG} ${CMAKE_BINARY_DIR}/lib)
endforeach (OUTPUTCONFIG)

# Append our module directory to CMake
list(APPEND CMAKE_MODULE_PATH "${CMAKE_CURRENT_SOURCE_DIR}/cmake")
list(APPEND CMAKE_MODULE_PATH "${CMAKE_CURRENT_SOURCE_DIR}/cmake/Modules")

include(MilestroMacros)

if (ANDROID)
    # 检查 ANDROID_ABI 变量的值，并根据其值设置架构变量
    if (ANDROID_ABI STREQUAL "arm64-v8a")
        set(MILESTRO_TARGET_ARCH "aarch64")
    else ()
        message(FATAL_ERROR "Unsupported ANDROID_ABI: ${ANDROID_ABI}")
    endif ()

    # 输出设置的架构
    message(STATUS "MILESTRO_TARGET_ARCH: ${MILESTRO_TARGET_ARCH}")

    # 提取版本号
    if (NOT DEFINED ANDROID_PLATFORM)
        set(ANDROID_PLATFORM "android-23")
    endif ()
    message(STATUS "ANDROID_PLATFORM: ${ANDROID_PLATFORM}")
    string(REGEX REPLACE ".*android-([0-9]+).*" "\\1" ANDROID_SDK_VERSION ${ANDROID_PLATFORM})
    message(STATUS "ANDROID_SDK_VERSION: ${ANDROID_SDK_VERSION}")

    if (CMAKE_HOST_WIN32)
        set(NDK_SCRIPT_SUFFIX ".cmd")
        set(NDK_SCRIPT_SUFFIX_EXE ".exe")
    else ()
        set(NDK_SCRIPT_SUFFIX "")
        set(NDK_SCRIPT_SUFFIX_EXE "")
    endif ()
    message(STATUS "NDK_SCRIPT_SUFFIX: ${NDK_SCRIPT_SUFFIX}")
endif ()

if (NOT APPLE)
    set(MILESTRO_WITH_JAVASCRIPTCORE OFF)
endif ()

#############################################################
# Setup external libraries
include(ExternalProject)

set(SPDLOG_BUILD_SHARED OFF)
set(SPDLOG_SHARED_LIB OFF)
set(SPDLOG_BUILD_PIC ON)
set(SPDLOG_BUILD_TESTS OFF)
add_subdirectory("ext/spdlog" EXCLUDE_FROM_ALL)

add_subdirectory("ext/PluginAPI" EXCLUDE_FROM_ALL)

add_library(skia INTERFACE IMPORTED)
if (DEFINED MILESTRO_SKIA_LIB_PATH AND DEFINED MILESTRO_SKIA_INCLUDE_PATH)
    if (MSVC)
        file(GLOB SKIA_LIB_FILES "${MILESTRO_SKIA_LIB_PATH}/*.lib")
    else ()
        file(GLOB SKIA_LIB_FILES "${MILESTRO_SKIA_LIB_PATH}/*.a")
    endif ()

    if (SKIA_LIB_FILES)
        set(SKIA_LIB_LIST ${SKIA_LIB_FILES})
        set(SKIA_INCLUDE_PATH "${MILESTRO_SKIA_INCLUDE_PATH}")
        target_link_libraries(skia INTERFACE ${SKIA_LIB_LIST})
        target_include_directories(skia INTERFACE ${SKIA_INCLUDE_PATH})
    else ()
        message(FATAL_ERROR "No Skia libraries found in ${MILESTRO_SKIA_LIB_PATH}")
    endif ()
else ()
    message(FATAL_ERROR "No Skia set")
endif ()

#############################################################
# Setup Milestro
set(PROJECT_PLATFORM_DEFINITIONS "MILESTRO_BUILDING_ENV")

set(
        MILESTRO_CORE_FILE

        src/skia/textlayout/TextStyle.cpp
)

set(
        MILESTRO_APPLICATION_FILE

        src/game/milestro_game_plugin_main.cpp
        src/log/log.cpp
        src/log/log_unitylog.cpp
)

set(PROJECT_PLATFORM_INCLUDES ${PROJECT_PLATFORM_INCLUDES} ${CMAKE_CURRENT_BINARY_DIR}/include)

set(
        PUBLIC_GAME_INTERFACE
        include/Milestro/common/milestro_export_macros.h
        include/Milestro/game/milestro_game_types.h
        include/Milestro/game/milestro_game_interface.h
)

# We will not use BUILD_SHARED_LIBS, and you can see the above comment for reasons.
if (MILESTRO_BUILD_SHARED_LIBS)
    add_library(Milestro SHARED ${MILESTRO_CORE_FILE} ${MILESTRO_APPLICATION_FILE})
else ()
    add_library(Milestro STATIC ${MILESTRO_CORE_FILE} ${MILESTRO_APPLICATION_FILE})
endif ()

target_compile_definitions(Milestro PUBLIC ${PROJECT_PLATFORM_DEFINITIONS})

target_include_directories(Milestro
        PUBLIC
        include
        ${PROJECT_PLATFORM_INCLUDES}
        PRIVATE
        src
)

target_link_libraries(Milestro
        PRIVATE
        UnityPluginAPI
        PUBLIC
        spdlog::spdlog
        skia
)