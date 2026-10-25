cmake_minimum_required(VERSION 3.25)

if (NOT TARGET skia)
    message(FATAL_ERROR "skttbshaper requires the skia target; add ext/skia-cmake first.")
endif ()

add_library(milestro_skttbshaper STATIC src/TtbShaper.cpp)
add_library(Milestro::SkTtbShaper ALIAS milestro_skttbshaper)

target_compile_features(milestro_skttbshaper PUBLIC cxx_std_20)
target_include_directories(milestro_skttbshaper
        PUBLIC
        "${CMAKE_CURRENT_SOURCE_DIR}/include"
        PRIVATE
        "${CMAKE_CURRENT_SOURCE_DIR}/../skia/third_party/externals/harfbuzz/src"
)
target_link_libraries(milestro_skttbshaper
        PUBLIC
        skia
)
