set(MILESTRO_UNITY_RENDER_PAYLOAD_TEST_NAME "MilestroTest_UnityRenderPayload")

add_executable(${MILESTRO_UNITY_RENDER_PAYLOAD_TEST_NAME}
        src/test_unity_render_diagnostics.cpp
        src/test_unity_render_payload.cpp
        src/test_unity_render_transform.cpp
        src/test_unity_render_vulkan_lifecycle.cpp)
target_include_directories(${MILESTRO_UNITY_RENDER_PAYLOAD_TEST_NAME}
        PRIVATE
        ${PROJECT_SOURCE_DIR}/include
        ${PROJECT_SOURCE_DIR}/src)
target_compile_definitions(${MILESTRO_UNITY_RENDER_PAYLOAD_TEST_NAME} PRIVATE MILESTRO_BUILDING_ENV)
target_link_libraries(${MILESTRO_UNITY_RENDER_PAYLOAD_TEST_NAME}
        PRIVATE
        Milestro
        GTest::gtest
        GTest::gtest_main
        ${PROJECT_PLATFORM_LIBRARIES})
set_target_properties(${MILESTRO_UNITY_RENDER_PAYLOAD_TEST_NAME} PROPERTIES
        BUILD_WITH_INSTALL_RPATH OFF
        BUILD_RPATH "$<TARGET_FILE_DIR:Milestro>")

if (WIN32 AND MILESTRO_BUILD_SHARED_LIBS)
    add_custom_command(TARGET ${MILESTRO_UNITY_RENDER_PAYLOAD_TEST_NAME} POST_BUILD
            COMMAND ${CMAKE_COMMAND} -E copy_if_different
            $<TARGET_FILE:Milestro>
            $<TARGET_FILE_DIR:${MILESTRO_UNITY_RENDER_PAYLOAD_TEST_NAME}>)
endif ()

if (NOT ANDROID)
    add_test(NAME ${MILESTRO_UNITY_RENDER_PAYLOAD_TEST_NAME} COMMAND ${MILESTRO_UNITY_RENDER_PAYLOAD_TEST_NAME})
endif ()
