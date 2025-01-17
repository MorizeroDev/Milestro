macro(MILESTRO_SHOW_CONFIG)
    message(STATUS "CMake ${CMAKE_VERSION} successfully configured ${PROJECT_NAME} using ${CMAKE_GENERATOR} generator")
    message(STATUS "${PROJECT_NAME} package version: ${PROJECT_VERSION}")
    message(STATUS "[cmake] CXX COMPILER: ${CMAKE_CXX_COMPILER_ID}")
    message(STATUS "[cmake] C COMPILER: ${CMAKE_C_COMPILER_ID}")
    message(STATUS "[cmake] C COMPILER VERSION: ${CMAKE_C_COMPILER_VERSION}")
    message(STATUS "[cmake] CXX COMPILER VERSION: ${CMAKE_CXX_COMPILER_VERSION}")
    if(MILESTRO_BUILD_SHARED_LIBS)
        message(STATUS "[cmake] Build dynamic libraries")
    else()
        message(STATUS "[cmake] Build static libraries")
    endif()
    message(STATUS "[cmake] Installation target path: ${CMAKE_INSTALL_PREFIX}")
    if(CMAKE_TOOLCHAIN_FILE)
        message(STATUS "[cmake] Use toolchain file:		${CMAKE_TOOLCHAIN_FILE}")
    endif()
    message(STATUS "[cmake] Build for OS type:      ${CMAKE_SYSTEM_NAME}")
    message(STATUS "[cmake] Build for OS version:   ${CMAKE_SYSTEM_VERSION}")
    message(STATUS "[cmake] Build for CPU type:     ${CMAKE_SYSTEM_PROCESSOR}")
    message(STATUS "[cmake] Build type:             ${CMAKE_BUILD_TYPE}")
    string(TOUPPER "${CMAKE_BUILD_TYPE}" BUILD_TYPE)
    message(STATUS "[cmake] Build with cxx flags:   ${CMAKE_CXX_FLAGS_${BUILD_TYPE}} ${CMAKE_CXX_FLAGS}")
    message(STATUS "[cmake] Build with c flags:     ${CMAKE_C_FLAGS_${BUILD_TYPE}} ${CMAKE_C_FLAGS}")

    message("Milestro, launch!")
endmacro()
