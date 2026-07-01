cmake_minimum_required(VERSION 3.25)
project(icu)

list(APPEND CMAKE_MODULE_PATH "${CMAKE_CURRENT_SOURCE_DIR}/cmake")
find_package(Python COMPONENTS Interpreter)

################################################################
set(icudtl_platform "common")

set(icudtl_path ${CMAKE_CURRENT_LIST_DIR}/${icudtl_platform}/icudtl.dat)
set(icu_data_assembly_dir ${CMAKE_CURRENT_BINARY_DIR}/data)
set(icu_data_assembly_path ${icu_data_assembly_dir}/data_assembly.cpp)
execute_process(
        COMMAND ${CMAKE_COMMAND} -E make_directory ${icu_data_assembly_dir}
        COMMAND ${Python_EXECUTABLE} ${CMAKE_CURRENT_LIST_DIR}/make_data_cpp.py ${icudtl_path} ${icu_data_assembly_path}
        RESULT_VARIABLE icu_data_assembly_codegen_result
)
if(NOT icu_data_assembly_codegen_result EQUAL 0)
    message(FATAL_ERROR "failed to generate icu data assembly")
endif()

add_library(icu_data_assembly STATIC ${icu_data_assembly_path})
#add_dependencies(icu_data_assembly icu_data_assembly_generate)

################################################################
set(icu_sources_root ${CMAKE_CURRENT_LIST_DIR}/../icu/icu4c/source)

include(IcuSources)

set(icu_includes
        ${icu_sources_root}/common
        ${icu_sources_root}/i18n
)

add_library(icu STATIC ${icu_sources})
target_compile_definitions(icu PUBLIC
        $<$<COMPILE_LANGUAGE:C,CXX>:
            # http://userguide.icu-project.org/howtouseicu
            "U_COMMON_IMPLEMENTATION"
            "U_STATIC_IMPLEMENTATION"
            "U_ENABLE_DYLOAD=0"
            "U_I18N_IMPLEMENTATION"
            "U_DISABLE_RENAMING=1"
            "U_USING_ICU_NAMESPACE=0"
            "U_SHOW_CPLUSPLUS_API=1"

    #        "UDATA_DEBUG=1"

            # If we don't set this to zero, ICU will set it to 600,
            # which makes Macs set _POSIX_C_SOURCE=200112L,
            # which makes Macs set __DARWIN_C_LEVEL=_POSIX_C_SOURCE instead of =__DARWIN_C_FULL,
            # which makes <time.h> not expose timespec_get,
            # which makes recent libc++ <ctime> not #include-able with -std=c++17.
            "_XOPEN_SOURCE=0"
        >
)

target_compile_options(icu PRIVATE -Wno-deprecated-declarations)

target_include_directories(icu PUBLIC ${icu_includes})
target_link_libraries(icu PUBLIC icu_data_assembly)
set_target_properties(icu PROPERTIES
        C_VISIBILITY_PRESET hidden
        CXX_VISIBILITY_PRESET hidden
)
