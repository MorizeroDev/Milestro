cmake_minimum_required(VERSION 3.25)
project(RubyThrowawayPrototype LANGUAGES C CXX)
set(CMAKE_CXX_STANDARD 20)
add_compile_options($<$<COMPILE_LANGUAGE:CXX>:-fno-rtti>)
get_filename_component(ROOT "../.." ABSOLUTE)
set(SKIA "${ROOT}/ext/skia")
set(SKIA_OUT "${SKIA}/out/ruby-prototype" CACHE PATH "Pinned CPU Skia build")
set(ICU_SOURCE "" CACHE PATH "Exact Milestro ICU source checkout")
set(ICU_ARCHIVES "" CACHE PATH "Directory of existing exact ICU archives")
add_library(skia STATIC IMPORTED)
set_target_properties(skia PROPERTIES IMPORTED_LOCATION "${SKIA_OUT}/libskia.a"
    INTERFACE_INCLUDE_DIRECTORIES "${SKIA}")
add_library(skia_shaper STATIC IMPORTED)
set_target_properties(skia_shaper PROPERTIES IMPORTED_LOCATION "${SKIA_OUT}/libskshaper.a")
add_library(icu INTERFACE)
target_include_directories(icu INTERFACE "${ICU_SOURCE}/icu4c/source/common" "${ICU_SOURCE}/icu4c/source/i18n")
target_compile_definitions(icu INTERFACE U_DISABLE_RENAMING=1 U_STATIC_IMPLEMENTATION)
target_link_libraries(icu INTERFACE "${ICU_ARCHIVES}/libicu.a" "${ICU_ARCHIVES}/libicu_data_assembly.a")
add_subdirectory("${ROOT}/ext/skmodules" skmodules)
target_compile_definitions(milestro_skparagraph PUBLIC MILESTRO_RUBY_PROTOTYPE)
add_executable(ruby-prototype main.cpp
    "${ROOT}/ext/skmodules/modules/skparagraph/src/experimental/RubyPrototype.cpp")
target_link_libraries(ruby-prototype PRIVATE Milestro::SkParagraph
    "-Wl,--start-group" skia_shaper skia
    "${SKIA_OUT}/libharfbuzz.a" "${SKIA_OUT}/libfreetype2.a"
    "${SKIA_OUT}/libbrotli.a" "${SKIA_OUT}/libpng.a" "${SKIA_OUT}/libzlib.a"
    "-Wl,--end-group" pthread dl)
target_compile_definitions(ruby-prototype PRIVATE PROTOTYPE_ROOT="${ROOT}")
