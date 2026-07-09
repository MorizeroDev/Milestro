cmake_minimum_required(VERSION 3.26)
project(Milestro)

option(MILESTRO_BUILD_SHARED_LIBS "Build shared library" ON)
option(MILESTRO_BUILD_FRAMEWORK_LIBS "Build framework library" OFF)
option(MILESTRO_ENABLE_CLI "Enable cli" ON)
option(MILESTRO_ENABLE_TESTS "Enable tests" ON)
option(MILESTRO_WITH_ADDRESS_SANITIZER "Build with clang address sanitizer" OFF)
option(MILESTRO_ENABLE_RELEASE_SYMBOLS "Build release binaries with debug info for split symbol packages" ON)
option(MILESTRO_REMAP_SOURCE_PATHS "Remap absolute source and build paths in release debug info" ON)
option(MILESTRO_ENABLE_DESKTOP_OPENGL_RENDER "Enable experimental Unity OpenGLCore RenderTexture backend" OFF)
option(MILESTRO_ENABLE_DESKTOP_VULKAN_RENDER "Enable experimental Unity Vulkan RenderTexture backend" OFF)
option(MILESTRO_RENDER_DEBUG_LOG "Enable Unity render-flow debug logs" OFF)
option(MILESTRO_SKIA_LIB_PATH "Skia library path")
option(MILESTRO_SKIA_INCLUDE_PATH "Skia include path")
option(MILESTRO_SKIA_CMAKE "Skia cmake path")

set(MILESTRO_REMAP_SOURCE_PREFIX "Milestro" CACHE STRING "Logical source root used in release debug info and __FILE__ remapping")
set(MILESTRO_REMAP_BUILD_PREFIX "MilestroBuild" CACHE STRING "Logical build root used in release debug info and __FILE__ remapping")
set(MILESTRO_UNITY_PLUGIN_OUTPUT_DIR "" CACHE PATH "Optional Unity plugin package output directory for the milestro_unity_plugin target")

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
    set(CMAKE_CXX_FLAGS_DEBUG "-g")
    set(CMAKE_CXX_FLAGS_RELEASE "-O3")
endif ()

if (MILESTRO_ENABLE_RELEASE_SYMBOLS AND NOT MSVC)
    foreach (MILESTRO_DEBUG_LANG C CXX OBJC OBJCXX)
        add_compile_options("$<$<AND:$<CONFIG:Release>,$<COMPILE_LANG_AND_ID:${MILESTRO_DEBUG_LANG},GNU,Clang,AppleClang>>:-g>")
    endforeach ()
endif ()

if (MILESTRO_REMAP_SOURCE_PATHS)
    if (MSVC)
        foreach (MILESTRO_REMAP_LANG C CXX)
            if (CMAKE_${MILESTRO_REMAP_LANG}_COMPILER_ID STREQUAL "Clang")
                foreach (MILESTRO_REMAP_FLAG -ffile-prefix-map -fdebug-prefix-map -fmacro-prefix-map)
                    add_compile_options("$<$<COMPILE_LANGUAGE:${MILESTRO_REMAP_LANG}>:/clang:${MILESTRO_REMAP_FLAG}=${PROJECT_SOURCE_DIR}=${MILESTRO_REMAP_SOURCE_PREFIX}>")
                    add_compile_options("$<$<COMPILE_LANGUAGE:${MILESTRO_REMAP_LANG}>:/clang:${MILESTRO_REMAP_FLAG}=${PROJECT_BINARY_DIR}=${MILESTRO_REMAP_BUILD_PREFIX}>")
                endforeach ()
            elseif (CMAKE_${MILESTRO_REMAP_LANG}_COMPILER_ID STREQUAL "MSVC")
                add_compile_options("$<$<COMPILE_LANGUAGE:${MILESTRO_REMAP_LANG}>:/pathmap:${PROJECT_SOURCE_DIR}=${MILESTRO_REMAP_SOURCE_PREFIX}>")
                add_compile_options("$<$<COMPILE_LANGUAGE:${MILESTRO_REMAP_LANG}>:/pathmap:${PROJECT_BINARY_DIR}=${MILESTRO_REMAP_BUILD_PREFIX}>")
            endif ()
        endforeach ()
    else ()
        foreach (MILESTRO_REMAP_LANG C CXX OBJC OBJCXX)
            foreach (MILESTRO_REMAP_FLAG -ffile-prefix-map -fdebug-prefix-map -fmacro-prefix-map)
                add_compile_options("$<$<COMPILE_LANG_AND_ID:${MILESTRO_REMAP_LANG},GNU,Clang,AppleClang>:${MILESTRO_REMAP_FLAG}=${PROJECT_SOURCE_DIR}=${MILESTRO_REMAP_SOURCE_PREFIX}>")
                add_compile_options("$<$<COMPILE_LANG_AND_ID:${MILESTRO_REMAP_LANG},GNU,Clang,AppleClang>:${MILESTRO_REMAP_FLAG}=${PROJECT_BINARY_DIR}=${MILESTRO_REMAP_BUILD_PREFIX}>")
            endforeach ()
        endforeach ()
    endif ()
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
    add_link_options(-fno-omit-frame-pointer -fsanitize=address -fsanitize-recover=address)
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

if (CMAKE_HOST_WIN32)
    set(MILESTRO_GRADLE_WRAPPER "${PROJECT_SOURCE_DIR}/gradlew.bat")
else ()
    set(MILESTRO_GRADLE_WRAPPER "${PROJECT_SOURCE_DIR}/gradlew")
endif ()

set(MILESTRO_UNITY_PLUGIN_GRADLE_ARGS "packageUnityPlugin")
if (MILESTRO_UNITY_PLUGIN_OUTPUT_DIR)
    get_filename_component(MILESTRO_UNITY_PLUGIN_OUTPUT_DIR_ABSOLUTE
            "${MILESTRO_UNITY_PLUGIN_OUTPUT_DIR}"
            ABSOLUTE
            BASE_DIR "${PROJECT_BINARY_DIR}"
    )
else ()
    set(MILESTRO_UNITY_PLUGIN_OUTPUT_DIR_ABSOLUTE "${PROJECT_BINARY_DIR}/unity-plugin")
endif ()

list(APPEND MILESTRO_UNITY_PLUGIN_GRADLE_ARGS
        "-PmilestroUnityPluginOutputDir=${MILESTRO_UNITY_PLUGIN_OUTPUT_DIR_ABSOLUTE}"
)

add_custom_target(milestro_unity_plugin
        COMMAND "${MILESTRO_GRADLE_WRAPPER}" ${MILESTRO_UNITY_PLUGIN_GRADLE_ARGS}
        WORKING_DIRECTORY "${PROJECT_SOURCE_DIR}"
        COMMENT "Packaging Milestro Unity plugin"
        USES_TERMINAL
)

# Append our module directory to CMake
list(APPEND CMAKE_MODULE_PATH "${CMAKE_CURRENT_SOURCE_DIR}/cmake")
list(APPEND CMAKE_MODULE_PATH "${CMAKE_CURRENT_SOURCE_DIR}/cmake/Modules")

include(MilestroMacros)

if (ANDROID)
    # 检查 ANDROID_ABI 变量的值，并根据其值设置架构变量
    if (ANDROID_ABI STREQUAL "arm64-v8a")
        set(MILESTRO_TARGET_ARCH "aarch64")
    elseif (ANDROID_ABI STREQUAL "armeabi-v7a")
        set(MILESTRO_TARGET_ARCH "arm")
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

#############################################################
set(PROJECT_PLATFORM_LIBRARIES)
set(PROJECT_PLATFORM_INCLUDES)

# Setup external libraries
include(ExternalProject)

set(SPDLOG_BUILD_SHARED OFF)
set(SPDLOG_SHARED_LIB OFF)
set(SPDLOG_BUILD_PIC ON)
set(SPDLOG_BUILD_TESTS OFF)
add_subdirectory("ext/spdlog" EXCLUDE_FROM_ALL)

add_subdirectory("ext/json" EXCLUDE_FROM_ALL)

add_subdirectory("ext/PluginAPI" EXCLUDE_FROM_ALL)

set(CLI11_PRECOMPILED ON)
add_subdirectory("ext/cli11" EXCLUDE_FROM_ALL)

add_subdirectory("ext/icu-cmake")
set(PROJECT_PLATFORM_LIBRARIES ${PROJECT_PLATFORM_LIBRARIES} "icu")
set(PROJECT_PLATFORM_INCLUDES ${PROJECT_PLATFORM_INCLUDES} ${icu_includes})

if (MILESTRO_SKIA_CMAKE AND MILESTRO_SKIA_INCLUDE_PATH)
    add_subdirectory("${MILESTRO_SKIA_CMAKE}" "ext/skia" EXCLUDE_FROM_ALL)
    set(PROJECT_PLATFORM_LIBRARIES ${PROJECT_PLATFORM_LIBRARIES}
            "modules__skparagraph"
            "modules__svg"
            "third_party__icu"
    )
    set(SKIA_INCLUDE_PATH "${MILESTRO_SKIA_INCLUDE_PATH}")
    target_compile_definitions(third_party__icu INTERFACE
            U_USING_ICU_NAMESPACE=0
            U_DISABLE_RENAMING=1
            U_NOEXCEPT=
            U_STATIC_IMPLEMENTATION
    )
elseif (MILESTRO_SKIA_LIB_PATH AND MILESTRO_SKIA_INCLUDE_PATH)
    add_library(skia INTERFACE IMPORTED)
    if (MSVC)
        file(GLOB SKIA_LIB_FILES "${MILESTRO_SKIA_LIB_PATH}/*.lib")
    else ()
        file(GLOB SKIA_LIB_FILES "${MILESTRO_SKIA_LIB_PATH}/*.a")
    endif ()

    if (NOT SKIA_LIB_FILES)
        message(FATAL_ERROR "Unknown Skia : ${MILESTRO_SKIA_LIB_PATH}")
    endif ()

    set(MILESTRO_SKIA_EXCLUDED_LIB_NAMES pathkit)
    set(SKIA_LIB_LIST)
    foreach (SKIA_LIB_FILE ${SKIA_LIB_FILES})
        get_filename_component(SKIA_LIB_NAME "${SKIA_LIB_FILE}" NAME_WE)
        if (SKIA_LIB_NAME MATCHES "^lib(.+)$")
            set(SKIA_LIB_NAME "${CMAKE_MATCH_1}")
        endif ()

        if (SKIA_LIB_NAME IN_LIST MILESTRO_SKIA_EXCLUDED_LIB_NAMES)
            message(STATUS "Skipping Skia library: ${SKIA_LIB_FILE}")
        else ()
            list(APPEND SKIA_LIB_LIST "${SKIA_LIB_FILE}")
        endif ()
    endforeach ()
    set(SKIA_INCLUDE_PATH "${MILESTRO_SKIA_INCLUDE_PATH}")
    target_link_libraries(skia INTERFACE ${SKIA_LIB_LIST})
    target_include_directories(skia INTERFACE ${SKIA_INCLUDE_PATH})
    target_compile_definitions(skia INTERFACE
            U_USING_ICU_NAMESPACE=0
            U_DISABLE_RENAMING=1
            U_NOEXCEPT=
            U_STATIC_IMPLEMENTATION
    )

    if (NOT CMAKE_CXX_COMPILER_ID STREQUAL "Clang" AND NOT CMAKE_CXX_COMPILER_ID STREQUAL "AppleClang")
        message(STATUS ${CMAKE_CXX_COMPILER_ID})
        message(FATAL_ERROR "Must use Clang compiler")
    endif ()
else ()
    message(FATAL_ERROR "No Skia set")
endif ()

if (APPLE AND CMAKE_GENERATOR STREQUAL "Ninja")
    if(IOS)
        set(APPLE_SDK_PLATFORM "iphoneos")
    else()
        set(APPLE_SDK_PLATFORM "macosx")
    endif()
    execute_process(
            COMMAND xcrun --sdk ${APPLE_SDK_PLATFORM} --show-sdk-path
            OUTPUT_VARIABLE APPLE_SDK_PATH
            OUTPUT_STRIP_TRAILING_WHITESPACE
    )
    execute_process(
            COMMAND xcrun --sdk ${APPLE_SDK_PLATFORM} --show-sdk-version
            OUTPUT_VARIABLE APPLE_SDK_VERSION
            OUTPUT_STRIP_TRAILING_WHITESPACE
    )

    message(STATUS "APPLE_SDK_PATH: ${APPLE_SDK_PATH}")
    message(STATUS "APPLE_SDK_VERSION: ${APPLE_SDK_VERSION}")

    set(CMAKE_OSX_SYSROOT "${APPLE_SDK_PATH}")

    # 从 cmake 参数那边读，并且兼容 ios-cmake 的 DEPLOYMENT_TARGET 选项
    set(SWIFT_DEPLOYMENT_TARGET "${CMAKE_OSX_DEPLOYMENT_TARGET}")
    if (NOT SWIFT_DEPLOYMENT_TARGET)
        set(SWIFT_DEPLOYMENT_TARGET "${DEPLOYMENT_TARGET}")
    endif ()
    if (NOT SWIFT_DEPLOYMENT_TARGET)
        set(SWIFT_DEPLOYMENT_TARGET "${APPLE_SDK_VERSION}")
    endif ()

    if (IOS)
        set(SWIFT_TARGET "arm64-apple-ios${SWIFT_DEPLOYMENT_TARGET}")
    else ()
        set(SWIFT_TARGET "arm64-apple-macosx${SWIFT_DEPLOYMENT_TARGET}")
    endif()

    set(CMAKE_Swift_COMPILER_TARGET "${SWIFT_TARGET}")
    set(CMAKE_Swift_FLAGS "-sdk ${CMAKE_OSX_SYSROOT}")
    if (MILIZE_ENABLE_RELEASE_SYMBOLS)
        string(APPEND CMAKE_Swift_FLAGS " -g")
    endif ()
    if (MILIZE_REMAP_SOURCE_PATHS)
        string(APPEND CMAKE_Swift_FLAGS " -debug-prefix-map ${PROJECT_SOURCE_DIR}=${MILIZE_REMAP_SOURCE_PREFIX}")
        string(APPEND CMAKE_Swift_FLAGS " -debug-prefix-map ${PROJECT_BINARY_DIR}=${MILIZE_REMAP_BUILD_PREFIX}")
    endif ()

    execute_process(
            COMMAND xcrun --find swiftc
            OUTPUT_VARIABLE CMAKE_Swift_COMPILER
            OUTPUT_STRIP_TRAILING_WHITESPACE
    )
    if (NOT CMAKE_Swift_COMPILER)
        message(FATAL_ERROR "Could not locate swiftc via xcrun")
    endif()
    message(STATUS "Using Swift compiler: ${CMAKE_Swift_COMPILER}")
    message(STATUS "SWIFT_TARGET: ${SWIFT_TARGET}")
endif()

if (APPLE)
    # Enable language support (CMake will probe using the above target/SDK)
    enable_language(Swift)
    enable_language(OBJC)
    enable_language(OBJCXX)

    # Check that Swift is new enough and C++ is Clang-based
    if ("${CMAKE_Swift_COMPILER_VERSION}" VERSION_LESS 5.9)
        message(FATAL_ERROR "Bidirectional C++ Interop requires Swift 5.9 or greater. Found ${CMAKE_Swift_COMPILER_VERSION}")
    endif ()

    if (NOT "${CMAKE_CXX_COMPILER_ID}" STREQUAL "Clang" AND NOT "${CMAKE_CXX_COMPILER_ID}" STREQUAL "AppleClang")
        message(FATAL_ERROR "Project requires building with Clang. Found ${CMAKE_CXX_COMPILER_ID}")
    endif ()

    # Include Swift support modules
    include(InitializeSwift)
    include(AddSwift)

    # Link against Foundation framework
    find_library(FOUNDATION Foundation)
    find_library(COREFOUNDATION CoreFoundation)
    find_library(CORETEXT CoreText)
    find_library(CORESERVICES CoreServices)
    find_library(COREGRAPHICS CoreGraphics)
    find_library(METAL Metal)
    set(PROJECT_PLATFORM_LIBRARIES ${PROJECT_PLATFORM_LIBRARIES}
            "${FOUNDATION}" "${COREFOUNDATION}" "${CORETEXT}" "${CORESERVICES}" "${COREGRAPHICS}" "${METAL}")
endif ()

if (WIN32)
    set(PROJECT_PLATFORM_LIBRARIES ${PROJECT_PLATFORM_LIBRARIES} d3d12 dxgi d3dcompiler dwrite gdi32 ole32 uuid)
elseif (CMAKE_DL_LIBS)
    set(PROJECT_PLATFORM_LIBRARIES ${PROJECT_PLATFORM_LIBRARIES} ${CMAKE_DL_LIBS})
endif ()

set(MILESTRO_ENABLE_UNITY_GL_RENDER OFF)
set(MILESTRO_ENABLE_UNITY_VULKAN_RENDER OFF)
if (ANDROID)
    find_library(MILESTRO_ANDROID_GLESV3_LIBRARY GLESv3)
    find_library(MILESTRO_ANDROID_EGL_LIBRARY EGL)
    find_library(MILESTRO_ANDROID_VULKAN_LIBRARY vulkan)
    if (MILESTRO_ANDROID_GLESV3_LIBRARY AND MILESTRO_ANDROID_EGL_LIBRARY)
        set(MILESTRO_ENABLE_UNITY_GL_RENDER ON)
        set(PROJECT_PLATFORM_LIBRARIES ${PROJECT_PLATFORM_LIBRARIES}
                ${MILESTRO_ANDROID_GLESV3_LIBRARY}
                ${MILESTRO_ANDROID_EGL_LIBRARY})
    else ()
        message(WARNING "Milestro Unity GL render backend disabled: Android GLESv3/EGL libraries not found")
    endif ()
    if (MILESTRO_ANDROID_VULKAN_LIBRARY)
        set(MILESTRO_ENABLE_UNITY_VULKAN_RENDER ON)
        set(PROJECT_PLATFORM_LIBRARIES ${PROJECT_PLATFORM_LIBRARIES}
                ${MILESTRO_ANDROID_VULKAN_LIBRARY})
    else ()
        message(WARNING "Milestro Unity Vulkan render backend disabled: Android vulkan library not found")
    endif ()
elseif (MILESTRO_ENABLE_DESKTOP_OPENGL_RENDER AND UNIX AND NOT APPLE)
    find_package(OpenGL QUIET)
    if (OpenGL_FOUND)
        set(MILESTRO_ENABLE_UNITY_GL_RENDER ON)
        set(PROJECT_PLATFORM_LIBRARIES ${PROJECT_PLATFORM_LIBRARIES} OpenGL::GL)
    else ()
        message(STATUS "Milestro Unity GL render backend disabled: OpenGL package not found")
    endif ()
endif ()

if (MILESTRO_ENABLE_DESKTOP_VULKAN_RENDER AND NOT ANDROID)
    find_package(Vulkan QUIET)
    if (Vulkan_FOUND)
        set(MILESTRO_ENABLE_UNITY_VULKAN_RENDER ON)
        set(PROJECT_PLATFORM_LIBRARIES ${PROJECT_PLATFORM_LIBRARIES} Vulkan::Vulkan)
    else ()
        message(STATUS "Milestro Unity Vulkan render backend disabled: Vulkan package not found")
    endif ()
endif ()

#############################################################
# Setup Milestro
set(MILESTRO_DEFINITIONS "MILESTRO_BUILDING_ENV")
set(MILESTRO_CLI_DEFINITIONS "MILESTRO_BUILDING_ENV")

set(MILESTRO_INCLUDES ${PROJECT_PLATFORM_INCLUDES} ${MILESTRO_SKIA_INCLUDE_PATH})
set(MILESTRO_CLI_INCLUDES ${PROJECT_PLATFORM_INCLUDES} ${MILESTRO_SKIA_INCLUDE_PATH} "${PROJECT_SOURCE_DIR}/ext/json/include")

set(MILESTRO_LIBRARIES ${PROJECT_PLATFORM_LIBRARIES})
set(MILESTRO_CLI_LIBRARIES ${PROJECT_PLATFORM_LIBRARIES})

set(
        MILESTRO_SKUNICODE_ICU_FILE
        ext/skia-unicode/src/SkUnicode.cpp
        ext/skia-unicode/src/SkUnicode_icu.cpp
        ext/skia-unicode/src/SkUnicode_icu_bidi.cpp
        ext/skia-unicode/src/SkBidiFactory_icu_full.cpp
        ext/skia-unicode/src/SkUnicode_icu_builtin.cpp
)

if (MILESTRO_ENABLE_CLI)
    set(MILESTRO_DEFINITIONS ${MILESTRO_DEFINITIONS} "MILESTRO_USE_CLI")
    set(MILESTRO_CLI_DEFINITIONS ${MILESTRO_CLI_DEFINITIONS} "MILESTRO_USE_CLI")
endif ()

set(
        MILESTRO_CORE_FILE

        src/log/log.cpp
        src/log/log_unitylog.cpp
        src/io/milestro_io.cpp

        src/skia/FontRegistry.cpp
        src/skia/MilestroRegisteredFontMgr.cpp
        src/skia/unicode/SkUnicodeNop.cpp
        src/skia/unicode/Unicode.cpp
        src/skia/Canvas.cpp
        src/skia/Typeface.cpp
        src/skia/Font.cpp
        src/skia/Image.cpp
        src/skia/Path.cpp
        src/skia/Svg.cpp
        src/skia/VertexData.cpp
        src/skia/textlayout/TextStyle.cpp
        src/skia/textlayout/FontCollection.cpp
        src/skia/textlayout/ParagraphStyle.cpp
        src/skia/textlayout/ParagraphBuilder.cpp
        src/skia/textlayout/Paragraph.cpp
        src/skia/textlayout/NoWrapLayout.cpp
        src/skia/textlayout/InputBox.cpp

        src/unicode/icu.cpp
        src/unicode/comparison.cpp
        src/unicode/normalize.cpp
        src/unicode/segments.cpp
        src/unicode/transliterator.cpp
        src/unicode/util.cpp
)

set(
        MILESTRO_APPLICATION_FILE

        src/game/milestro_game_plugin_main.cpp
        src/game/milestro_game_unity_render.cpp
        src/unity_render/MilestroUnityRenderDispatcher.cpp
        src/unity_render/MilestroUnityRenderSubmissionDraw.cpp
        src/game/milestro_game_skia_canvas.cpp
        src/game/milestro_game_skia_image.cpp
        src/game/milestro_game_skia_fontregistry.cpp
        src/game/milestro_game_skia_fontcollection.cpp
        src/game/milestro_game_skia_typeface.cpp
        src/game/milestro_game_skia_font.cpp
        src/game/milestro_game_skia_slim_text.cpp
        src/game/milestro_game_skia_path.cpp
        src/game/milestro_game_skia_svg.cpp
        src/game/milestro_game_skia_vertexdata.cpp
        src/game/milestro_game_skia_textlayout_paragraph.cpp
        src/game/milestro_game_skia_textlayout_inputbox.cpp
        src/game/milestro_game_skia_textlayout_paragraphbuilder.cpp
        src/game/milestro_game_skia_textlayout_paragraphstyle.cpp
        src/game/milestro_game_skia_textlayout_strutstyle.cpp
        src/game/milestro_game_skia_textlayout_textstyle.cpp
        src/game/milestro_game_unicode_comparison.cpp
        src/game/milestro_game_unicode_segments.cpp
        src/game/milestro_game_unicode_normalize.cpp
        src/game/milestro_game_unicode_transliterator.cpp
        src/game/milestro_game_unicode_util.cpp
        src/game/milize_game_model.cpp
)

if (APPLE)
    list(APPEND MILESTRO_APPLICATION_FILE
            src/unity_render/MilestroUnityRenderMetalBackend.mm)
endif ()

if (WIN32)
    list(APPEND MILESTRO_APPLICATION_FILE
            src/unity_render/MilestroUnityRenderD3D12Backend.cpp)
endif ()

if (MILESTRO_ENABLE_UNITY_GL_RENDER)
    set(MILESTRO_DEFINITIONS ${MILESTRO_DEFINITIONS} "MILESTRO_ENABLE_UNITY_GL_RENDER")
    list(APPEND MILESTRO_APPLICATION_FILE
            src/unity_render/MilestroUnityRenderGLBackend.cpp)
endif ()

if (MILESTRO_ENABLE_UNITY_VULKAN_RENDER)
    set(MILESTRO_DEFINITIONS ${MILESTRO_DEFINITIONS} "MILESTRO_ENABLE_UNITY_VULKAN_RENDER")
    list(APPEND MILESTRO_APPLICATION_FILE
            src/unity_render/MilestroUnityRenderVulkanBackend.cpp)
endif ()

if (MILESTRO_RENDER_DEBUG_LOG)
    set(MILESTRO_DEFINITIONS ${MILESTRO_DEFINITIONS} "MILESTRO_RENDER_DEBUG_LOG")
endif ()

set(
        MILESTRO_CMD_FILE
        apps/cmd/cli.cpp
)

set(MILESTRO_INCLUDES ${MILESTRO_INCLUDES} ${CMAKE_CURRENT_BINARY_DIR}/include)

set(
        PUBLIC_GAME_INTERFACE
        include/Milestro/common/milestro_export_macros.h
        include/Milestro/game/milestro_game_types.h
        include/Milestro/game/milestro_game_interface.h
)

# We will not use BUILD_SHARED_LIBS, and you can see the above comment for reasons.
if (MILESTRO_BUILD_SHARED_LIBS)
    add_library(Milestro SHARED ${MILESTRO_CORE_FILE} ${MILESTRO_APPLICATION_FILE} ${MILESTRO_SKUNICODE_ICU_FILE} ${PUBLIC_GAME_INTERFACE})
else ()
    add_library(Milestro STATIC ${MILESTRO_CORE_FILE} ${MILESTRO_APPLICATION_FILE} ${MILESTRO_SKUNICODE_ICU_FILE} ${PUBLIC_GAME_INTERFACE})
endif ()

# Only Apple Can Do
if (DEFINED APPLE AND DEFINED IOS AND MILESTRO_BUILD_FRAMEWORK_LIBS)
    set_target_properties(
            Milestro PROPERTIES
            FRAMEWORK TRUE
            FRAMEWORK_VERSION C
            MACOSX_FRAMEWORK_INFO_PLIST "${PROJECT_SOURCE_DIR}/Info.plist"
            PUBLIC_HEADER "${PUBLIC_GAME_INTERFACE}"
    )
endif ()

target_compile_definitions(Milestro PUBLIC ${MILESTRO_DEFINITIONS})
target_compile_definitions(Milestro PRIVATE
        SKUNICODE_IMPLEMENTATION=1
        SK_UNICODE_AVAILABLE
        SK_UNICODE_ICU_IMPLEMENTATION
        U_USING_ICU_NAMESPACE=0
        U_DISABLE_RENAMING=1
)

if (MSVC)
    target_compile_options(Milestro PRIVATE
            "$<$<COMPILE_LANGUAGE:CXX>:/GR->"
    )
else ()
    target_compile_options(Milestro PRIVATE
            "$<$<COMPILE_LANG_AND_ID:CXX,Clang,AppleClang,GNU>:-fno-rtti>"
            "$<$<COMPILE_LANG_AND_ID:OBJCXX,Clang,AppleClang,GNU>:-fno-rtti>"
    )
endif ()

target_include_directories(Milestro
        PUBLIC
        include
        ${MILESTRO_INCLUDES}
        PRIVATE
        src
)

target_link_libraries(Milestro
        PRIVATE
        UnityPluginAPI
        ${MILESTRO_LIBRARIES}
        PUBLIC
        spdlog::spdlog
        skia
        nlohmann_json::nlohmann_json
)

install(TARGETS Milestro
        RUNTIME DESTINATION bin
        LIBRARY DESTINATION lib
        ARCHIVE DESTINATION lib
        FRAMEWORK DESTINATION lib
        PUBLIC_HEADER DESTINATION include/Milestro/game)

if (MSVC)
    #    set_target_properties(Milestro PROPERTIES LIBRARY_OUTPUT_NAME  "libMilestro")
    set_target_properties(Milestro PROPERTIES RUNTIME_OUTPUT_NAME "libMilestro")
    #    set_target_properties(Milestro PROPERTIES ARCHIVE_OUTPUT_NAME  "libMilestro")
endif ()

if (MILESTRO_ENABLE_TESTS)
    include(CTest)
    enable_testing()

    add_subdirectory(ext/googletest EXCLUDE_FROM_ALL)
    add_subdirectory(tests)
    message(STATUS "Building with tests")
else ()
    message(STATUS "Building without tests")
endif ()

if (MILESTRO_ENABLE_CLI)
    add_executable(MilestroCli ${MILESTRO_CMD_FILE})
    target_compile_definitions(MilestroCli PRIVATE Milestro ${MILESTRO_CLI_DEFINITIONS})
    target_link_libraries(MilestroCli
            PRIVATE
            Milestro
            ${MILESTRO_CLI_LIBRARIES}
            nlohmann_json::nlohmann_json
            CLI11::CLI11
            skia
    )
    target_include_directories(MilestroCli PRIVATE ${MILESTRO_CLI_INCLUDES})

    if (WIN32)
        if (MINGW)
            target_link_options(MilestroCli BEFORE PRIVATE "-static")
        endif ()
    elseif (LINUX)
        if (NOT ANDROID)
            # Excluding Android
            # Android defaults to static linking with libc++, no tinkering needed.
            target_link_options(MilestroCli BEFORE PRIVATE "-static-libstdc++")
        endif ()
        #        elseif (APPLE)
        #            target_link_options(MilestroCli BEFORE PRIVATE "-static-libgcc -static-libstdc++")
    endif ()

    install(TARGETS MilestroCli DESTINATION bin)
endif ()


if (CMAKE_HOST_WIN32)
    add_custom_target(H2CS
            COMMAND cd ${PROJECT_SOURCE_DIR} & cmd /c gradlew.bat h2cs
    )
else ()
    add_custom_target(H2CS
            COMMAND cd ${PROJECT_SOURCE_DIR} && ./gradlew h2cs
    )
endif ()
