set(MILESTRO_TEST_NAME "MilestroTest_UnityRenderVulkanSupport")

add_executable(${MILESTRO_TEST_NAME}
        src/test_unity_render_vulkan_support.cpp
)
target_include_directories(${MILESTRO_TEST_NAME} PRIVATE
        ${PROJECT_SOURCE_DIR}/src
)
target_link_libraries(${MILESTRO_TEST_NAME} PRIVATE
        GTest::gtest
        GTest::gtest_main
)

if (NOT ANDROID)
    add_test(NAME ${MILESTRO_TEST_NAME} COMMAND ${MILESTRO_TEST_NAME})
endif ()
