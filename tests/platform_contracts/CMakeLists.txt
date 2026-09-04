cmake_minimum_required(VERSION 3.26)
project(MilestroPlatformContracts LANGUAGES CXX)
enable_testing()
get_filename_component(MILESTRO_ROOT "${CMAKE_CURRENT_LIST_DIR}/../.." ABSOLUTE)
if (NOT EXISTS "${MILESTRO_ROOT}/ext/googletest/CMakeLists.txt")
    message(FATAL_ERROR "Run git submodule update --init ext/googletest first")
endif ()
set(gtest_force_shared_crt ON CACHE BOOL "" FORCE)
add_subdirectory("${MILESTRO_ROOT}/ext/googletest" googletest EXCLUDE_FROM_ALL)
add_executable(MilestroPlatformContracts
        ../unity_render/src/test_unity_render_atomic.cpp
        ../unity_render/src/test_unity_render_payload.cpp
        ../unity_render/src/test_unity_render_transform.cpp
        ../unity_render/src/test_unity_render_vulkan_lifecycle.cpp
        ../unity_render/src/test_unity_render_gl_state.cpp)
target_compile_features(MilestroPlatformContracts PRIVATE cxx_std_20)
if (MSVC)
    target_compile_options(MilestroPlatformContracts PRIVATE /utf-8)
else ()
    target_compile_options(MilestroPlatformContracts PRIVATE -Wall -Wextra -Werror)
endif ()
target_compile_definitions(MilestroPlatformContracts PRIVATE MILESTRO_BUILDING_ENV)
target_include_directories(MilestroPlatformContracts PRIVATE "${MILESTRO_ROOT}/src" "${MILESTRO_ROOT}/include")
target_link_libraries(MilestroPlatformContracts PRIVATE GTest::gtest_main)
include(GoogleTest)
if (NOT ANDROID)
    gtest_discover_tests(MilestroPlatformContracts)
endif ()
