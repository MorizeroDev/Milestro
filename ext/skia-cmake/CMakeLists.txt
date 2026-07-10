cmake_minimum_required(VERSION 3.25)
project(skia-cmake LANGUAGES NONE)

find_package(Python3 REQUIRED COMPONENTS Interpreter)
find_program(MILESTRO_SKIA_NINJA_EXECUTABLE ninja REQUIRED)

set(MILESTRO_SKIA_REPOSITORY_URL
        "https://github.com/ExerciseBook/skia.git"
        CACHE STRING
        "Skia git repository URL used when the source checkout is missing."
)
set(MILESTRO_SKIA_COMMIT
        "8b136c7c44cf5f144c8edbb289890b9610cedfe2"
        CACHE STRING
        "Skia git commit checked out after cloning. Leave empty to use the repository default branch."
)

unset(MILESTRO_SKIA_SOURCE_DIR CACHE)
unset(MILESTRO_SKIA_OUT_DIR CACHE)
unset(MILESTRO_SKIA_CHECK_COMMIT CACHE)

get_filename_component(MILESTRO_SKIA_SOURCE_DIR
        "${CMAKE_CURRENT_LIST_DIR}/../skia"
        ABSOLUTE
)

set(MILESTRO_SKIA_OUT_DIR "${CMAKE_CURRENT_BINARY_DIR}")
set(MILESTRO_SKIA_EXTRA_GN_ARGS
        ""
        CACHE STRING
        "Additional raw GN args appended after Milestro defaults."
)
set(MILESTRO_SKIA_GN_IDE
        ""
        CACHE STRING
        "Optional GN IDE generator, for example xcode, vs2022, or json."
)
option(MILESTRO_SKIA_SYNC_DEPS
        "Run Skia tools/git-sync-deps before GN generation even when the checkout already exists. New clones always sync."
        OFF
)

set(MILESTRO_SKIA_DID_CLONE OFF)
if (NOT EXISTS "${MILESTRO_SKIA_SOURCE_DIR}/BUILD.gn")
    find_package(Git REQUIRED)
    get_filename_component(MILESTRO_SKIA_SOURCE_PARENT_DIR "${MILESTRO_SKIA_SOURCE_DIR}" DIRECTORY)
    file(MAKE_DIRECTORY "${MILESTRO_SKIA_SOURCE_PARENT_DIR}")

    if (EXISTS "${MILESTRO_SKIA_SOURCE_DIR}")
        file(GLOB MILESTRO_SKIA_SOURCE_DIR_CONTENTS
                LIST_DIRECTORIES TRUE
                "${MILESTRO_SKIA_SOURCE_DIR}/*"
                "${MILESTRO_SKIA_SOURCE_DIR}/.[!.]*"
        )
        if (MILESTRO_SKIA_SOURCE_DIR_CONTENTS)
            message(FATAL_ERROR
                    "Skia source path exists but is not a Skia checkout: "
                    "${MILESTRO_SKIA_SOURCE_DIR}"
            )
        endif ()
    endif ()

    message(STATUS "Cloning Skia into ${MILESTRO_SKIA_SOURCE_DIR}")
    execute_process(
            COMMAND "${GIT_EXECUTABLE}" clone "${MILESTRO_SKIA_REPOSITORY_URL}" "${MILESTRO_SKIA_SOURCE_DIR}"
            RESULT_VARIABLE MILESTRO_SKIA_CLONE_RESULT
    )
    if (NOT MILESTRO_SKIA_CLONE_RESULT EQUAL 0)
        message(FATAL_ERROR "Failed to clone Skia from ${MILESTRO_SKIA_REPOSITORY_URL}")
    endif ()
    set(MILESTRO_SKIA_DID_CLONE ON)

    if (MILESTRO_SKIA_COMMIT)
        message(STATUS "Checking out Skia ${MILESTRO_SKIA_COMMIT}")
        execute_process(
                COMMAND "${GIT_EXECUTABLE}" checkout "${MILESTRO_SKIA_COMMIT}"
                WORKING_DIRECTORY "${MILESTRO_SKIA_SOURCE_DIR}"
                RESULT_VARIABLE MILESTRO_SKIA_CHECKOUT_RESULT
        )
        if (NOT MILESTRO_SKIA_CHECKOUT_RESULT EQUAL 0)
            message(FATAL_ERROR "Failed to checkout Skia ${MILESTRO_SKIA_COMMIT}")
        endif ()
    endif ()
endif ()

if (MILESTRO_SKIA_DID_CLONE OR MILESTRO_SKIA_SYNC_DEPS)
    message(STATUS "Syncing Skia DEPS")
    execute_process(
            COMMAND "${Python3_EXECUTABLE}" tools/git-sync-deps
            WORKING_DIRECTORY "${MILESTRO_SKIA_SOURCE_DIR}"
            RESULT_VARIABLE MILESTRO_SKIA_SYNC_DEPS_RESULT
    )
    if (NOT MILESTRO_SKIA_SYNC_DEPS_RESULT EQUAL 0)
        message(FATAL_ERROR "Failed to run Skia tools/git-sync-deps")
    endif ()
endif ()

set(MILESTRO_SKIA_GN_EXECUTABLE "${MILESTRO_SKIA_SOURCE_DIR}/bin/gn")
if (CMAKE_HOST_WIN32)
    set(MILESTRO_SKIA_GN_EXECUTABLE "${MILESTRO_SKIA_GN_EXECUTABLE}.exe")
endif ()
if (NOT EXISTS "${MILESTRO_SKIA_GN_EXECUTABLE}")
    message(FATAL_ERROR "Skia GN executable not found: ${MILESTRO_SKIA_GN_EXECUTABLE}")
endif ()

if (ANDROID)
    if (DEFINED ANDROID_ABI AND ANDROID_ABI)
        set(MILESTRO_SKIA_ANDROID_ABI "${ANDROID_ABI}")
    elseif (DEFINED CMAKE_ANDROID_ARCH_ABI AND CMAKE_ANDROID_ARCH_ABI)
        set(MILESTRO_SKIA_ANDROID_ABI "${CMAKE_ANDROID_ARCH_ABI}")
    else ()
        message(FATAL_ERROR "Android build requires ANDROID_ABI or CMAKE_ANDROID_ARCH_ABI")
    endif ()

    if (MILESTRO_SKIA_ANDROID_ABI STREQUAL "arm64-v8a")
        set(MILESTRO_SKIA_DEFAULT_TARGET_CPU "arm64")
    elseif (MILESTRO_SKIA_ANDROID_ABI STREQUAL "armeabi-v7a")
        set(MILESTRO_SKIA_DEFAULT_TARGET_CPU "arm")
    elseif (MILESTRO_SKIA_ANDROID_ABI STREQUAL "x86_64")
        set(MILESTRO_SKIA_DEFAULT_TARGET_CPU "x64")
    elseif (MILESTRO_SKIA_ANDROID_ABI STREQUAL "x86")
        set(MILESTRO_SKIA_DEFAULT_TARGET_CPU "x86")
    else ()
        message(FATAL_ERROR "Unsupported Android ABI for Skia: ${MILESTRO_SKIA_ANDROID_ABI}")
    endif ()
elseif (CMAKE_OSX_ARCHITECTURES)
    list(GET CMAKE_OSX_ARCHITECTURES 0 MILESTRO_SKIA_DEFAULT_TARGET_CPU)
else ()
    set(MILESTRO_SKIA_DEFAULT_TARGET_CPU "${CMAKE_SYSTEM_PROCESSOR}")
endif ()

if (NOT ANDROID)
    string(TOLOWER "${MILESTRO_SKIA_DEFAULT_TARGET_CPU}" MILESTRO_SKIA_DEFAULT_TARGET_CPU_LOWER)
    if (MILESTRO_SKIA_DEFAULT_TARGET_CPU_LOWER MATCHES "^(aarch64|arm64)$")
        set(MILESTRO_SKIA_DEFAULT_TARGET_CPU "arm64")
    elseif (MILESTRO_SKIA_DEFAULT_TARGET_CPU_LOWER MATCHES "^(x86_64|amd64)$")
        set(MILESTRO_SKIA_DEFAULT_TARGET_CPU "x64")
    elseif (MILESTRO_SKIA_DEFAULT_TARGET_CPU_LOWER MATCHES "^(i[3-6]86|x86)$")
        set(MILESTRO_SKIA_DEFAULT_TARGET_CPU "x86")
    elseif (MILESTRO_SKIA_DEFAULT_TARGET_CPU_LOWER MATCHES "^(armv[6-8].*|arm)$")
        set(MILESTRO_SKIA_DEFAULT_TARGET_CPU "arm")
    endif ()
endif ()

if (ANDROID)
    set(MILESTRO_SKIA_TARGET_CPU
            "${MILESTRO_SKIA_DEFAULT_TARGET_CPU}"
            CACHE STRING
            "GN target_cpu for Skia."
            FORCE
    )
else ()
    set(MILESTRO_SKIA_TARGET_CPU
            "${MILESTRO_SKIA_DEFAULT_TARGET_CPU}"
            CACHE STRING
            "GN target_cpu for Skia."
    )
endif ()

if (APPLE)
    if (IOS)
        set(MILESTRO_SKIA_DEFAULT_TARGET_OS "ios")
    else ()
        set(MILESTRO_SKIA_DEFAULT_TARGET_OS "mac")
    endif ()
elseif (ANDROID)
    set(MILESTRO_SKIA_DEFAULT_TARGET_OS "android")
elseif (WIN32)
    set(MILESTRO_SKIA_DEFAULT_TARGET_OS "win")
elseif (CMAKE_SYSTEM_NAME STREQUAL "Linux")
    set(MILESTRO_SKIA_DEFAULT_TARGET_OS "linux")
else ()
    string(TOLOWER "${CMAKE_SYSTEM_NAME}" MILESTRO_SKIA_DEFAULT_TARGET_OS)
endif ()

set(MILESTRO_SKIA_TARGET_OS
        "${MILESTRO_SKIA_DEFAULT_TARGET_OS}"
        CACHE STRING
        "GN target_os for Skia."
)

if (ANDROID)
    set(MILESTRO_SKIA_DEFAULT_ANDROID_NDK "")
    if (DEFINED CMAKE_ANDROID_NDK AND NOT CMAKE_ANDROID_NDK STREQUAL "")
        set(MILESTRO_SKIA_DEFAULT_ANDROID_NDK "${CMAKE_ANDROID_NDK}")
    elseif (DEFINED ANDROID_NDK AND NOT ANDROID_NDK STREQUAL "")
        set(MILESTRO_SKIA_DEFAULT_ANDROID_NDK "${ANDROID_NDK}")
    endif ()

    if (MILESTRO_SKIA_DEFAULT_ANDROID_NDK STREQUAL "" AND DEFINED CMAKE_TOOLCHAIN_FILE AND EXISTS "${CMAKE_TOOLCHAIN_FILE}")
        get_filename_component(MILESTRO_SKIA_ANDROID_CMAKE_DIR "${CMAKE_TOOLCHAIN_FILE}" DIRECTORY)
        get_filename_component(MILESTRO_SKIA_ANDROID_BUILD_DIR "${MILESTRO_SKIA_ANDROID_CMAKE_DIR}" DIRECTORY)
        get_filename_component(MILESTRO_SKIA_ANDROID_NDK_CANDIDATE "${MILESTRO_SKIA_ANDROID_BUILD_DIR}" DIRECTORY)
        if (EXISTS "${MILESTRO_SKIA_ANDROID_NDK_CANDIDATE}/toolchains/llvm")
            set(MILESTRO_SKIA_DEFAULT_ANDROID_NDK "${MILESTRO_SKIA_ANDROID_NDK_CANDIDATE}")
        endif ()
    endif ()

    if (MILESTRO_SKIA_DEFAULT_ANDROID_NDK STREQUAL "" AND CMAKE_C_COMPILER MATCHES "^(.*)[/\\\\]toolchains[/\\\\]llvm[/\\\\]prebuilt[/\\\\][^/\\\\]+[/\\\\]bin[/\\\\]clang(\\.exe)?$")
        set(MILESTRO_SKIA_DEFAULT_ANDROID_NDK "${CMAKE_MATCH_1}")
    endif ()

    file(TO_CMAKE_PATH "${MILESTRO_SKIA_DEFAULT_ANDROID_NDK}" MILESTRO_SKIA_DEFAULT_ANDROID_NDK)
    set(MILESTRO_SKIA_ANDROID_NDK
            "${MILESTRO_SKIA_DEFAULT_ANDROID_NDK}"
            CACHE PATH
            "Android NDK path passed to Skia GN."
    )
    if (NOT EXISTS "${MILESTRO_SKIA_ANDROID_NDK}/toolchains/llvm")
        message(FATAL_ERROR "Android NDK path for Skia is invalid: ${MILESTRO_SKIA_ANDROID_NDK}")
    endif ()

    set(MILESTRO_SKIA_DEFAULT_ANDROID_NDK_API "")
    if (DEFINED ANDROID_PLATFORM AND NOT ANDROID_PLATFORM STREQUAL "")
        set(MILESTRO_SKIA_ANDROID_PLATFORM "${ANDROID_PLATFORM}")
    elseif (DEFINED CMAKE_SYSTEM_VERSION AND NOT CMAKE_SYSTEM_VERSION STREQUAL "")
        set(MILESTRO_SKIA_ANDROID_PLATFORM "${CMAKE_SYSTEM_VERSION}")
    elseif (DEFINED ANDROID_NATIVE_API_LEVEL AND NOT ANDROID_NATIVE_API_LEVEL STREQUAL "")
        set(MILESTRO_SKIA_ANDROID_PLATFORM "${ANDROID_NATIVE_API_LEVEL}")
    else ()
        set(MILESTRO_SKIA_ANDROID_PLATFORM "21")
    endif ()

    if (MILESTRO_SKIA_ANDROID_PLATFORM MATCHES "^android-([0-9]+)$")
        set(MILESTRO_SKIA_DEFAULT_ANDROID_NDK_API "${CMAKE_MATCH_1}")
    elseif (MILESTRO_SKIA_ANDROID_PLATFORM MATCHES "^[0-9]+$")
        set(MILESTRO_SKIA_DEFAULT_ANDROID_NDK_API "${MILESTRO_SKIA_ANDROID_PLATFORM}")
    else ()
        message(FATAL_ERROR "Unsupported Android platform for Skia: ${MILESTRO_SKIA_ANDROID_PLATFORM}")
    endif ()

    set(MILESTRO_SKIA_ANDROID_NDK_API
            "${MILESTRO_SKIA_DEFAULT_ANDROID_NDK_API}"
            CACHE STRING
            "Android NDK API level passed to Skia GN."
    )
endif ()

if (DEFINED MILESTRO_SKIA_IS_DEBUG)
    set(MILESTRO_SKIA_DEFAULT_IS_DEBUG "${MILESTRO_SKIA_IS_DEBUG}")
elseif (CMAKE_BUILD_TYPE STREQUAL "Debug")
    set(MILESTRO_SKIA_DEFAULT_IS_DEBUG ON)
else ()
    set(MILESTRO_SKIA_DEFAULT_IS_DEBUG OFF)
endif ()

set(MILESTRO_SKIA_IS_DEBUG
        "${MILESTRO_SKIA_DEFAULT_IS_DEBUG}"
        CACHE BOOL
        "Build Skia with is_debug=true."
)

if (APPLE)
    execute_process(
            COMMAND xcrun --find clang
            OUTPUT_VARIABLE MILESTRO_SKIA_DEFAULT_CC
            OUTPUT_STRIP_TRAILING_WHITESPACE
            ERROR_QUIET
    )
    execute_process(
            COMMAND xcrun --find clang++
            OUTPUT_VARIABLE MILESTRO_SKIA_DEFAULT_CXX
            OUTPUT_STRIP_TRAILING_WHITESPACE
            ERROR_QUIET
    )
endif ()

if (NOT MILESTRO_SKIA_DEFAULT_CC)
    set(MILESTRO_SKIA_DEFAULT_CC "${CMAKE_C_COMPILER}")
endif ()
if (NOT MILESTRO_SKIA_DEFAULT_CXX)
    set(MILESTRO_SKIA_DEFAULT_CXX "${CMAKE_CXX_COMPILER}")
endif ()

set(MILESTRO_SKIA_DEFAULT_CLANG_WIN "")
if (WIN32 AND CMAKE_CXX_COMPILER_ID STREQUAL "Clang" AND CMAKE_CXX_SIMULATE_ID STREQUAL "MSVC")
    get_filename_component(MILESTRO_SKIA_CXX_COMPILER_NAME "${CMAKE_CXX_COMPILER}" NAME_WE)
    if (MILESTRO_SKIA_CXX_COMPILER_NAME STREQUAL "clang-cl")
        get_filename_component(MILESTRO_SKIA_CLANG_BIN_DIR "${CMAKE_CXX_COMPILER}" DIRECTORY)
        get_filename_component(MILESTRO_SKIA_DEFAULT_CLANG_WIN "${MILESTRO_SKIA_CLANG_BIN_DIR}" DIRECTORY)
        file(TO_CMAKE_PATH "${MILESTRO_SKIA_DEFAULT_CLANG_WIN}" MILESTRO_SKIA_DEFAULT_CLANG_WIN)
    endif ()
endif ()

set(MILESTRO_SKIA_CC
        "${MILESTRO_SKIA_DEFAULT_CC}"
        CACHE FILEPATH
        "C compiler passed to Skia GN."
)
set(MILESTRO_SKIA_CXX
        "${MILESTRO_SKIA_DEFAULT_CXX}"
        CACHE FILEPATH
        "C++ compiler passed to Skia GN."
)
set(MILESTRO_SKIA_CLANG_WIN
        "${MILESTRO_SKIA_DEFAULT_CLANG_WIN}"
        CACHE PATH
        "LLVM root passed to Skia GN as clang_win for Windows clang-cl builds."
)

set(MILESTRO_SKIA_USE_METAL OFF)
set(MILESTRO_SKIA_USE_GL OFF)
set(MILESTRO_SKIA_GL_STANDARD "")
set(MILESTRO_SKIA_USE_EGL OFF)
set(MILESTRO_SKIA_USE_DIRECT3D OFF)
set(MILESTRO_SKIA_USE_VULKAN OFF)
option(MILESTRO_SKIA_USE_PARTITION_ALLOC
        "Build Skia with PartitionAlloc/raw_ptr backing. Disabled for standalone plugin builds to avoid allocator interposition."
        OFF)

if (APPLE)
    set(MILESTRO_SKIA_USE_METAL ON)
elseif (ANDROID)
    set(MILESTRO_SKIA_USE_GL ON)
    set(MILESTRO_SKIA_GL_STANDARD "gles")
    set(MILESTRO_SKIA_USE_VULKAN ON)
elseif (WIN32)
    set(MILESTRO_SKIA_USE_DIRECT3D ON)
    if (MILESTRO_ENABLE_DESKTOP_VULKAN_RENDER)
        set(MILESTRO_SKIA_USE_VULKAN ON)
    endif ()
elseif (UNIX)
    if (MILESTRO_ENABLE_DESKTOP_OPENGL_RENDER)
        set(MILESTRO_SKIA_USE_GL ON)
        set(MILESTRO_SKIA_GL_STANDARD "gl")
    endif ()
    if (MILESTRO_ENABLE_DESKTOP_VULKAN_RENDER)
        set(MILESTRO_SKIA_USE_VULKAN ON)
    endif ()
endif ()

function(milestro_skia_append_gn_bool name value)
    if (${value})
        list(APPEND MILESTRO_SKIA_GN_ARGS "${name}=true")
    else ()
        list(APPEND MILESTRO_SKIA_GN_ARGS "${name}=false")
    endif ()
    set(MILESTRO_SKIA_GN_ARGS "${MILESTRO_SKIA_GN_ARGS}" PARENT_SCOPE)
endfunction()

function(milestro_skia_append_gn_string name value)
    string(REPLACE "\\" "\\\\" value "${value}")
    string(REPLACE "\"" "\\\"" value "${value}")
    list(APPEND MILESTRO_SKIA_GN_ARGS "${name}=\"${value}\"")
    set(MILESTRO_SKIA_GN_ARGS "${MILESTRO_SKIA_GN_ARGS}" PARENT_SCOPE)
endfunction()

function(milestro_skia_append_gn_string_list name)
    set(MILESTRO_SKIA_GN_STRING_LIST)
    foreach (value IN LISTS ARGN)
        string(REPLACE "\\" "\\\\" value "${value}")
        string(REPLACE "\"" "\\\"" value "${value}")
        list(APPEND MILESTRO_SKIA_GN_STRING_LIST "\"${value}\"")
    endforeach ()
    list(JOIN MILESTRO_SKIA_GN_STRING_LIST "," MILESTRO_SKIA_GN_STRING_LIST_JOINED)
    list(APPEND MILESTRO_SKIA_GN_ARGS "${name}=[${MILESTRO_SKIA_GN_STRING_LIST_JOINED}]")
    set(MILESTRO_SKIA_GN_ARGS "${MILESTRO_SKIA_GN_ARGS}" PARENT_SCOPE)
endfunction()

set(MILESTRO_SKIA_GN_ARGS)
milestro_skia_append_gn_string(target_os "${MILESTRO_SKIA_TARGET_OS}")
milestro_skia_append_gn_string(target_cpu "${MILESTRO_SKIA_TARGET_CPU}")
if (ANDROID)
    milestro_skia_append_gn_string(ndk "${MILESTRO_SKIA_ANDROID_NDK}")
    list(APPEND MILESTRO_SKIA_GN_ARGS "ndk_api=${MILESTRO_SKIA_ANDROID_NDK_API}")
endif ()

milestro_skia_append_gn_bool(is_debug MILESTRO_SKIA_IS_DEBUG)
milestro_skia_append_gn_bool(is_official_build OFF)

milestro_skia_append_gn_bool(skia_enable_fontmgr_custom_empty ON)
milestro_skia_append_gn_bool(skia_use_freetype ON)
milestro_skia_append_gn_bool(skia_use_system_freetype2 OFF)
milestro_skia_append_gn_bool(skia_use_freetype_woff2 ON)

milestro_skia_append_gn_bool(skia_use_libavif ON)

milestro_skia_append_gn_bool(skia_use_metal MILESTRO_SKIA_USE_METAL)
milestro_skia_append_gn_bool(skia_use_gl MILESTRO_SKIA_USE_GL)
milestro_skia_append_gn_string(skia_gl_standard "${MILESTRO_SKIA_GL_STANDARD}")
milestro_skia_append_gn_bool(skia_use_egl MILESTRO_SKIA_USE_EGL)
milestro_skia_append_gn_bool(skia_use_direct3d MILESTRO_SKIA_USE_DIRECT3D)
milestro_skia_append_gn_bool(skia_use_vulkan MILESTRO_SKIA_USE_VULKAN)
milestro_skia_append_gn_bool(skia_use_partition_alloc MILESTRO_SKIA_USE_PARTITION_ALLOC)

milestro_skia_append_gn_bool(skia_use_icu OFF)
milestro_skia_append_gn_bool(skia_use_client_icu ON)
milestro_skia_append_gn_bool(skia_enable_tools OFF)

if (WIN32 AND (MSVC OR CMAKE_CXX_SIMULATE_ID STREQUAL "MSVC"))
    if (MILESTRO_SKIA_IS_DEBUG)
        set(MILESTRO_SKIA_MSVC_ITERATOR_DEBUG_LEVEL "2")
        set(MILESTRO_SKIA_MSVC_RUNTIME_FLAG "/MDd")
    else ()
        set(MILESTRO_SKIA_MSVC_ITERATOR_DEBUG_LEVEL "0")
        set(MILESTRO_SKIA_MSVC_RUNTIME_FLAG "/MD")
    endif ()

    if (NOT MILESTRO_SKIA_EXTRA_GN_ARGS MATCHES "(^|[ \t])extra_cflags[ \t]*=")
        milestro_skia_append_gn_string_list(extra_cflags
                "/D_ITERATOR_DEBUG_LEVEL=${MILESTRO_SKIA_MSVC_ITERATOR_DEBUG_LEVEL}"
                "${MILESTRO_SKIA_MSVC_RUNTIME_FLAG}"
        )
    endif ()
endif ()

if (MILESTRO_SKIA_CLANG_WIN)
    milestro_skia_append_gn_string(clang_win "${MILESTRO_SKIA_CLANG_WIN}")
endif ()

milestro_skia_append_gn_string(cc "${MILESTRO_SKIA_CC}")
milestro_skia_append_gn_string(cxx "${MILESTRO_SKIA_CXX}")

list(JOIN MILESTRO_SKIA_GN_ARGS " " MILESTRO_SKIA_GN_ARGS_STRING)
if (MILESTRO_SKIA_EXTRA_GN_ARGS)
    string(APPEND MILESTRO_SKIA_GN_ARGS_STRING " ${MILESTRO_SKIA_EXTRA_GN_ARGS}")
endif ()

set(MILESTRO_SKIA_GN_COMMAND
        "${MILESTRO_SKIA_GN_EXECUTABLE}"
        gen
        "${MILESTRO_SKIA_OUT_DIR}"
        "--root=${MILESTRO_SKIA_SOURCE_DIR}"
        "--args=${MILESTRO_SKIA_GN_ARGS_STRING}"
        "--ninja-executable=${MILESTRO_SKIA_NINJA_EXECUTABLE}"
)
if (MILESTRO_SKIA_GN_IDE)
    list(APPEND MILESTRO_SKIA_GN_COMMAND "--ide=${MILESTRO_SKIA_GN_IDE}")
endif ()

set(MILESTRO_SKIA_BUILD_NINJA "${MILESTRO_SKIA_OUT_DIR}/build.ninja")
execute_process(
        COMMAND ${MILESTRO_SKIA_GN_COMMAND}
        WORKING_DIRECTORY "${MILESTRO_SKIA_SOURCE_DIR}"
        RESULT_VARIABLE MILESTRO_SKIA_GN_RESULT
)
if (NOT MILESTRO_SKIA_GN_RESULT EQUAL 0)
    message(FATAL_ERROR "Failed to generate Skia GN build in ${MILESTRO_SKIA_OUT_DIR}")
endif ()

function(milestro_skia_lib_path out_var library_name)
    if (WIN32)
        set(${out_var} "${MILESTRO_SKIA_OUT_DIR}/${library_name}.lib" PARENT_SCOPE)
    else ()
        set(${out_var} "${MILESTRO_SKIA_OUT_DIR}/lib${library_name}.a" PARENT_SCOPE)
    endif ()
endfunction()

function(milestro_skia_collect_static_libraries out_var target_label target_library_name)
    milestro_skia_lib_path(MILESTRO_SKIA_TARGET_LIBRARY_FILE "${target_library_name}")
    set(MILESTRO_SKIA_COLLECTED_LIBRARIES "${MILESTRO_SKIA_TARGET_LIBRARY_FILE}")

    execute_process(
            COMMAND "${MILESTRO_SKIA_GN_EXECUTABLE}"
                    "--root=${MILESTRO_SKIA_SOURCE_DIR}"
                    desc
                    "${MILESTRO_SKIA_OUT_DIR}"
                    "${target_label}"
                    deps
                    --all
                    --as=output
                    --type=static_library
            OUTPUT_VARIABLE MILESTRO_SKIA_GN_DEPS_OUTPUT
            ERROR_VARIABLE MILESTRO_SKIA_GN_DEPS_ERROR
            RESULT_VARIABLE MILESTRO_SKIA_GN_DEPS_RESULT
            OUTPUT_STRIP_TRAILING_WHITESPACE
    )
    if (NOT MILESTRO_SKIA_GN_DEPS_RESULT EQUAL 0)
        message(FATAL_ERROR
                "Failed to query Skia GN static-library deps for ${target_label}: "
                "${MILESTRO_SKIA_GN_DEPS_ERROR}"
        )
    endif ()

    string(REPLACE "\r\n" "\n" MILESTRO_SKIA_GN_DEPS_OUTPUT "${MILESTRO_SKIA_GN_DEPS_OUTPUT}")
    string(REPLACE "\r" "\n" MILESTRO_SKIA_GN_DEPS_OUTPUT "${MILESTRO_SKIA_GN_DEPS_OUTPUT}")
    string(REPLACE "\n" ";" MILESTRO_SKIA_GN_DEPS "${MILESTRO_SKIA_GN_DEPS_OUTPUT}")
    foreach (MILESTRO_SKIA_GN_DEP IN LISTS MILESTRO_SKIA_GN_DEPS)
        string(STRIP "${MILESTRO_SKIA_GN_DEP}" MILESTRO_SKIA_GN_DEP)
        if (MILESTRO_SKIA_GN_DEP STREQUAL "")
            continue()
        endif ()

        if (IS_ABSOLUTE "${MILESTRO_SKIA_GN_DEP}")
            set(MILESTRO_SKIA_GN_DEP_FILE "${MILESTRO_SKIA_GN_DEP}")
        else ()
            set(MILESTRO_SKIA_GN_DEP_FILE "${MILESTRO_SKIA_OUT_DIR}/${MILESTRO_SKIA_GN_DEP}")
        endif ()
        list(APPEND MILESTRO_SKIA_COLLECTED_LIBRARIES "${MILESTRO_SKIA_GN_DEP_FILE}")
    endforeach ()

    list(REMOVE_DUPLICATES MILESTRO_SKIA_COLLECTED_LIBRARIES)
    set(${out_var} "${MILESTRO_SKIA_COLLECTED_LIBRARIES}" PARENT_SCOPE)
endfunction()

milestro_skia_collect_static_libraries(MILESTRO_SKIA_LIBRARIES_SKIA "//:skia" skia)
milestro_skia_collect_static_libraries(MILESTRO_SKIA_LIBRARIES_SKPARAGRAPH "//modules/skparagraph:skparagraph" skparagraph)
milestro_skia_collect_static_libraries(MILESTRO_SKIA_LIBRARIES_SVG "//modules/svg:svg" svg)

milestro_skia_lib_path(MILESTRO_SKIA_LIB_SKIA skia)
milestro_skia_lib_path(MILESTRO_SKIA_LIB_SKPARAGRAPH skparagraph)
milestro_skia_lib_path(MILESTRO_SKIA_LIB_SVG svg)

set(MILESTRO_SKIA_LIBRARY_FILES
        ${MILESTRO_SKIA_LIBRARIES_SKIA}
        ${MILESTRO_SKIA_LIBRARIES_SKPARAGRAPH}
        ${MILESTRO_SKIA_LIBRARIES_SVG}
)
list(REMOVE_DUPLICATES MILESTRO_SKIA_LIBRARY_FILES)

set(MILESTRO_SKIA_LINK_LIBRARIES
        ${MILESTRO_SKIA_LIB_SKPARAGRAPH}
        ${MILESTRO_SKIA_LIB_SVG}
        ${MILESTRO_SKIA_LIB_SKIA}
        ${MILESTRO_SKIA_LIBRARIES_SKPARAGRAPH}
        ${MILESTRO_SKIA_LIBRARIES_SVG}
        ${MILESTRO_SKIA_LIBRARIES_SKIA}
)
if (APPLE)
    list(REMOVE_DUPLICATES MILESTRO_SKIA_LINK_LIBRARIES)
endif ()

set(MILESTRO_SKIA_NINJA_TARGETS skia skparagraph svg)
add_custom_target(skia_gn_build
        COMMAND "${MILESTRO_SKIA_NINJA_EXECUTABLE}" -C "${MILESTRO_SKIA_OUT_DIR}" ${MILESTRO_SKIA_NINJA_TARGETS}
        DEPENDS "${MILESTRO_SKIA_BUILD_NINJA}"
        BYPRODUCTS ${MILESTRO_SKIA_LIBRARY_FILES}
        COMMENT "Building Skia static libraries"
        VERBATIM
        USES_TERMINAL
)

set(MILESTRO_SKIA_PUBLIC_INCLUDES
        "${MILESTRO_SKIA_SOURCE_DIR}"
        "${MILESTRO_SKIA_OUT_DIR}/gen"
        "${MILESTRO_SKIA_SOURCE_DIR}/third_party/externals/icu/source/common"
)

add_library(skia INTERFACE)
add_dependencies(skia skia_gn_build)
target_include_directories(skia INTERFACE ${MILESTRO_SKIA_PUBLIC_INCLUDES})
target_compile_definitions(skia INTERFACE
        SK_UNICODE_AVAILABLE
        SK_UNICODE_CLIENT_IMPLEMENTATION
        SK_ENABLE_PARAGRAPH
        SK_ENABLE_SVG
)
# Skia's static archives do not carry GN transitive dependency metadata.
target_link_libraries(skia INTERFACE
        ${MILESTRO_SKIA_LINK_LIBRARIES}
)

message(STATUS "Skia source: ${MILESTRO_SKIA_SOURCE_DIR}")
message(STATUS "Skia GN out: ${MILESTRO_SKIA_OUT_DIR}")
message(STATUS "Skia GN args: ${MILESTRO_SKIA_GN_ARGS_STRING}")
